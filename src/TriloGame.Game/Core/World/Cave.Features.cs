using TriloGame.Game.Audio;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private OpalNode? _opalNode;
    private readonly Dictionary<string, AntHole> _antHolesByTileKey = new(StringComparer.Ordinal);
    private readonly Dictionary<Enemy, AntHole> _antHoleByEnemy = [];

    private bool IsEnemySpawnBlockedTile(Tile tile)
    {
        var queen = GetQueenBuilding();
        return queen is not null && ContainsProjectedBuilding(tile.Projections, queen);
    }

    public OpalNode? GetOpalNode() => GameConstants.EnableOpal ? _opalNode : null;

    public OpalNode? GetOpalNode(Tile tile)
    {
        if (!GameConstants.EnableOpal)
        {
            return null;
        }

        return _opalNode is not null && string.Equals(_opalNode.TileKey, tile.Key, StringComparison.Ordinal)
            ? _opalNode
            : null;
    }

    public bool HasOpal(Tile tile) => GetOpalNode(tile) is not null;

    public bool HasAntHole(Tile tile) => _antHolesByTileKey.ContainsKey(tile.Key);

    public bool HasBlockingSurfaceFeature(Tile tile) => HasOpal(tile) || HasAntHole(tile);

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

    public bool TrySpawnQueenOpal()
    {
        if (!GameConstants.EnableOpal)
        {
            _opalNode = null;
            return false;
        }

        if (_opalNode is not null)
        {
            return true;
        }

        var queen = GetQueenBuilding();
        if (queen is null)
        {
            return false;
        }

        var candidate = queen.TileArray
            .SelectMany(tile => tile.Neighbors)
            .Where(tile =>
                IsTileRevealed(tile) &&
                CanPlaceAntHole(tile) &&
                !queen.TileArray.Contains(tile))
            .Distinct()
            .OrderBy(tile => GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter()))
            .ThenBy(tile => tile.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        if (candidate is null)
        {
            candidate = GetTiles()
                .Where(tile =>
                    IsTileRevealed(tile) &&
                    CanPlaceAntHole(tile))
                .OrderBy(tile => GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter()))
                .ThenBy(tile => tile.Key, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        if (candidate is null)
        {
            return false;
        }

        _opalNode = new OpalNode(candidate.Key);
        return true;
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
        var spawnTiles = GetAntHoleSpawnTiles(holeTile, clampedCount);
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
        Session.RequestAudioCue(GameAudioCue.AntHoleSpawn);
        return true;
    }

    public void TickSurfaceFeatures()
    {
        if (!GameConstants.EnableOpal)
        {
            _opalNode = null;
            return;
        }

        _opalNode?.Tick();
    }

    public int GetAntHoleSpawnChanceDenominator()
    {
        if (!GameConstants.EnableOpal)
        {
            return GameConstants.AntHoleBaseSpawnChanceDenominator;
        }

        return _opalNode?.GetAntHoleSpawnChanceDenominator() ?? GameConstants.AntHoleBaseSpawnChanceDenominator;
    }

    public bool AllowsNaturalEnemySpawns()
    {
        if (Session.Runtime.DisableEnemySpawns)
        {
            return false;
        }

        return !GameConstants.EnableOpal || _opalNode?.BlocksNaturalAntHoleSpawns() != true;
    }

    public MineTileResult MineOpal(Tile tile)
    {
        if (!GameConstants.EnableOpal)
        {
            return MineTileResult.NotApplied;
        }

        var opal = GetOpalNode(tile);
        if (opal is null || !opal.ApplyMineHit())
        {
            return MineTileResult.NotApplied;
        }

        var remainingYield = opal.RemainingYield;
        var depleted = opal.IsDepleted;
        if (depleted)
        {
            _opalNode = null;
        }

        return new MineTileResult(
            true,
            false,
            null,
            0,
            depleted,
            null,
            0,
            remainingYield,
            remainingYield);
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

    private IReadOnlyList<Tile> GetAntHoleSpawnTiles(Tile holeTile, int requestedCount)
    {
        var selectedTiles = new List<Tile>(requestedCount);
        var queue = new Queue<(Tile Tile, int Distance)>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { holeTile.Key };

        foreach (var neighbor in RandomUtil.Shuffle(holeTile.Neighbors))
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

            foreach (var neighbor in RandomUtil.Shuffle(tile.Neighbors))
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
