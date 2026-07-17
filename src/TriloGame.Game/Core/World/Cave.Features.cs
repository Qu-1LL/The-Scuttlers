using TriloGame.Game.Audio;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private readonly Dictionary<string, AntHole> _antHolesByTileKey = new(StringComparer.Ordinal);
    private readonly Dictionary<Enemy, AntHole> _antHoleByEnemy = [];

    private bool IsEnemySpawnBlockedTile(Tile tile)
    {
        var queen = GetQueenBuilding();
        return queen is not null && ContainsProjectedBuilding(tile.Projections, queen);
    }

    public bool HasAntHole(Tile tile) => _antHolesByTileKey.ContainsKey(tile.Key);

    public bool HasBlockingSurfaceFeature(Tile tile) => HasAntHole(tile);

    public IReadOnlyCollection<AntHole> GetAntHoles() => _antHolesByTileKey.Values;

    public bool CanPlaceAntHole(Tile holeTile)
    {
        return string.Equals(holeTile.Base, "empty", StringComparison.Ordinal) &&
               holeTile.Built is null &&
               holeTile.CreatureFits() &&
               !HasCreatureInCell(holeTile.Coordinates) &&
               !HasBlockingSurfaceFeature(holeTile);
    }

    public bool TrySpawnAntHole()
    {
        if (Enemies.Count >= GameConstants.MaxAmbientAntCount)
        {
            return false;
        }

        var queen = GetQueenBuilding();
        if (queen is null)
        {
            return false;
        }

        var queenCenter = queen.GetCenter();
        var candidates = GetTiles()
            .Where(tile =>
                IsTileRevealed(tile) &&
                CanPlaceAntHole(tile) &&
                !IsEnemySpawnBlockedTile(tile) &&
                GridPoint.ManhattanDistance(tile.Coordinates, queenCenter) >= GameConstants.AntHoleMinSpawnDistanceFromQueen)
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        var holeTile = candidates[RandomUtil.NextInt(candidates.Length)];
        var spawnCount = Math.Min(
            RandomUtil.NextInt(GameConstants.MinAmbientAntSpawnCount, GameConstants.MaxAmbientAntSpawnCount + 1),
            GameConstants.MaxAmbientAntCount - Enemies.Count);
        if (spawnCount <= 0)
        {
            return false;
        }

        return SpawnAntHole(holeTile, spawnCount);
    }

    public bool SpawnAntHole(Tile holeTile, int requestedCount)
    {
        var clampedCount = Math.Min(requestedCount, GameConstants.MaxAmbientAntSpawnCount);
        if (clampedCount <= 0 || !CanPlaceAntHole(holeTile))
        {
            return false;
        }

        var antHole = new AntHole(holeTile.Key);
        var spawnTiles = PreviewAntHoleSpawnTiles(holeTile, clampedCount);
        foreach (var tile in spawnTiles)
        {
            var ant = new Enemy($"Ant {Session.Runtime.AllocateDebugEnemyId()}", tile.Coordinates, Session);
            if (!Spawn(ant, tile))
            {
                continue;
            }

            antHole.RegisterAnt(ant);
            _antHoleByEnemy[ant] = antHole;
        }

        if (antHole.IsCleared)
        {
            return false;
        }

        _antHolesByTileKey[holeTile.Key] = antHole;
        Session.RequestAudioCue(GameAudioCue.AntHoleSpawn, WorldPoint.FromGridPoint(holeTile.Coordinates), 1f);
        return true;
    }

    public void TickSurfaceFeatures()
    {
    }

    public int GetAntHoleSpawnChanceDenominator()
    {
        return GameConstants.AntHoleBaseSpawnChanceDenominator;
    }

    public bool AllowsNaturalEnemySpawns()
    {
        if (Session.Runtime.DisableEnemySpawns)
        {
            return false;
        }

        return true;
    }

    public void HandleRemovedEnemySurfaceFeature(Enemy enemy)
    {
        if (!_antHoleByEnemy.Remove(enemy, out var antHole))
        {
            return;
        }

        antHole.UnregisterAnt(enemy);
        if (antHole.IsCleared)
        {
            _antHolesByTileKey.Remove(antHole.TileKey);
        }
    }

    public int DespawnAntHoles()
    {
        if (_antHolesByTileKey.Count == 0 && _antHoleByEnemy.Count == 0)
        {
            return 0;
        }

        var removedCount = _antHolesByTileKey.Count;
        _antHolesByTileKey.Clear();
        _antHoleByEnemy.Clear();
        return removedCount;
    }

    public IReadOnlyList<Tile> PreviewAntHoleSpawnTiles(Tile holeTile, int requestedCount)
    {
        var selectedTiles = new List<Tile>(requestedCount);
        var queue = new Queue<(Tile Tile, int Distance)>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { holeTile.Key };

        foreach (var neighbor in holeTile.Neighbors.OrderBy(neighbor => neighbor.Key, StringComparer.Ordinal))
        {
            queue.Enqueue((neighbor, 1));
            visited.Add(neighbor.Key);
        }

        while (queue.Count > 0 && selectedTiles.Count < requestedCount)
        {
            var (tile, distance) = queue.Dequeue();
            if (distance > GameConstants.AntHoleSpawnRadius)
            {
                continue;
            }

            if (tile.CreatureFits() &&
                string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
                tile.Built is null &&
                !HasCreatureInCell(tile.Coordinates) &&
                !HasBlockingSurfaceFeature(tile) &&
                !IsEnemySpawnBlockedTile(tile))
            {
                selectedTiles.Add(tile);
            }

            if (distance >= GameConstants.AntHoleSpawnRadius)
            {
                continue;
            }

            foreach (var neighbor in tile.Neighbors.OrderBy(neighbor => neighbor.Key, StringComparer.Ordinal))
            {
                if (visited.Add(neighbor.Key))
                {
                    queue.Enqueue((neighbor, distance + 1));
                }
            }
        }

        return selectedTiles;
    }
}
