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
    private readonly List<AntHole> _antHoleTickBuffer = [];

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
               holeTile.Trilobites.Count == 0 &&
               holeTile.EnemyOccupant is null &&
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

    public bool SpawnAntHole(Tile holeTile, int requestedCount, int? spawnSourceId = null)
    {
        var clampedCount = Math.Min(requestedCount, GameConstants.MaxAmbientAntSpawnCount);
        if (clampedCount <= 0 || !CanPlaceAntHole(holeTile))
        {
            return false;
        }

        var spawnTiles = PreviewAntHoleSpawnTiles(holeTile, clampedCount);
        if (spawnTiles.Count == 0)
        {
            return false;
        }

        var antHole = new AntHole(holeTile.Key, clampedCount, GameConstants.AntHoleSpawnDelayTicks, spawnSourceId);
        _antHolesByTileKey[holeTile.Key] = antHole;
        Session.RequestAudioCue(GameAudioCue.AntHoleSpawn);
        return true;
    }

    public void TickSurfaceFeatures()
    {
        if (_antHolesByTileKey.Count == 0)
        {
            return;
        }

        _antHoleTickBuffer.Clear();
        if (_antHoleTickBuffer.Capacity < _antHolesByTileKey.Count)
        {
            _antHoleTickBuffer.Capacity = _antHolesByTileKey.Count;
        }

        foreach (var antHole in _antHolesByTileKey.Values)
        {
            _antHoleTickBuffer.Add(antHole);
        }

        for (var index = 0; index < _antHoleTickBuffer.Count; index++)
        {
            var antHole = _antHoleTickBuffer[index];
            if (!_antHolesByTileKey.ContainsKey(antHole.TileKey))
            {
                continue;
            }

            antHole.Tick();
            if (!antHole.IsReadyToSpawn)
            {
                continue;
            }

            ReleaseAntHole(antHole);
            _antHolesByTileKey.Remove(antHole.TileKey);
        }

        _antHoleTickBuffer.Clear();
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
        foreach (var antHole in _antHolesByTileKey.Values)
        {
            ReportFailedAntHoleSpawns(antHole);
        }

        _antHolesByTileKey.Clear();
        _antHoleByEnemy.Clear();
        return removedCount;
    }

    private void ReleaseAntHole(AntHole antHole)
    {
        var holeTile = GetTile(antHole.TileKey);
        if (holeTile is null)
        {
            ReportFailedAntHoleSpawns(antHole);
            return;
        }

        var spawnTiles = PreviewAntHoleSpawnTiles(holeTile, antHole.PendingAntCount);
        var spawnedCount = 0;
        var resolvedCount = 0;
        for (var index = 0; index < spawnTiles.Count && resolvedCount < antHole.PendingAntCount; index++)
        {
            var tile = spawnTiles[index];
            var ant = new Enemy($"Ant {Session.Runtime.AllocateDebugEnemyId()}", tile.Coordinates, Session);
            if (!Spawn(ant, tile))
            {
                resolvedCount++;
                Session.ReportAntHoleSpawnResolved(null, antHole.SpawnSourceId);
                continue;
            }

            spawnedCount++;
            resolvedCount++;
            Session.ReportAntHoleSpawnResolved(ant, antHole.SpawnSourceId);
        }

        for (var index = resolvedCount; index < antHole.PendingAntCount; index++)
        {
            Session.ReportAntHoleSpawnResolved(null, antHole.SpawnSourceId);
        }
    }

    private void ReportFailedAntHoleSpawns(AntHole antHole)
    {
        for (var index = 0; index < antHole.PendingAntCount; index++)
        {
            Session.ReportAntHoleSpawnResolved(null, antHole.SpawnSourceId);
        }
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
                tile.Trilobites.Count == 0 &&
                tile.EnemyOccupant is null &&
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
