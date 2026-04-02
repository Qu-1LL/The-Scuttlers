using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

public sealed class BfsField
{
    private int[] _values = [];
    private bool[] _covered = [];
    private bool[] _nextCovered = [];
    private bool[] _blocked = [];
    private bool[] _seeded = [];
    private bool[] _queued = [];
    private readonly Queue<int> _queue = [];
    private readonly List<int> _seedIds = [];
    private bool _fieldCacheDirty = true;
    private int _coverageCount;

    public BfsField(string name = "", string type = "shared", Cave? cave = null, Building? ownerBuilding = null)
    {
        Name = name;
        Type = type;
        Cave = cave;
        OwnerBuilding = ownerBuilding;
        Field = new Dictionary<string, int>(StringComparer.Ordinal);
        UpdatedTiles = new HashSet<string>(StringComparer.Ordinal);
        UpdatedBuildings = [];
        UpdatedCreatures = [];
        TrackedBuildings = [];
        TrackedCreatures = [];
        EnsureCapacity(cave?.TileCapacity ?? 0);
    }

    public string Name { get; }

    public string Type { get; }

    public Cave? Cave { get; private set; }

    public Building? OwnerBuilding { get; private set; }

    public Dictionary<string, int> Field { get; private set; }

    public bool Updated { get; private set; }

    public HashSet<string> UpdatedTiles { get; }

    public HashSet<Building> UpdatedBuildings { get; }

    public HashSet<Creature> UpdatedCreatures { get; }

    public HashSet<Building> TrackedBuildings { get; }

    public HashSet<Creature> TrackedCreatures { get; }

    public void SetCave(Cave? cave)
    {
        Cave = cave;
        EnsureCapacity(cave?.TileCapacity ?? 0);
        _fieldCacheDirty = true;
    }

    public void SetOwnerBuilding(Building? building)
    {
        OwnerBuilding = building;
    }

    public void SetField(Dictionary<string, int>? field)
    {
        Field = field is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(field, StringComparer.Ordinal);
        _fieldCacheDirty = false;
        ImportField(Field);
    }

    public Dictionary<string, int> CommitField(Dictionary<string, int> field)
    {
        SetField(field);
        ClearUpdates();
        return Field;
    }

    public bool IsUpdated() => Updated;

    public void SetTrackedTargets(IEnumerable<Building>? buildings = null, IEnumerable<Creature>? creatures = null)
    {
        TrackedBuildings.Clear();
        TrackedCreatures.Clear();

        foreach (var building in buildings ?? [])
        {
            TrackedBuildings.Add(building);
        }

        foreach (var creature in creatures ?? [])
        {
            TrackedCreatures.Add(creature);
        }
    }

    public bool ClearUpdates()
    {
        Updated = true;
        UpdatedTiles.Clear();
        UpdatedBuildings.Clear();
        UpdatedCreatures.Clear();
        return Updated;
    }

    public bool MarkTilesDirty(IEnumerable<string>? tileKeys)
    {
        Updated = false;
        foreach (var tileKey in tileKeys ?? [])
        {
            if (!string.IsNullOrWhiteSpace(tileKey))
            {
                UpdatedTiles.Add(tileKey);
            }
        }

        return Updated;
    }

    public bool MarkBuildingsDirty(IEnumerable<Building>? buildings)
    {
        Updated = false;
        foreach (var building in buildings ?? [])
        {
            UpdatedBuildings.Add(building);
        }

        return Updated;
    }

    public bool MarkCreaturesDirty(IEnumerable<Creature>? creatures)
    {
        Updated = false;
        foreach (var creature in creatures ?? [])
        {
            UpdatedCreatures.Add(creature);
        }

        return Updated;
    }

    public bool MarkDirty(IEnumerable<string>? tileKeys, IEnumerable<Building>? buildings, IEnumerable<Creature>? creatures)
    {
        Updated = false;
        MarkTilesDirty(tileKeys);
        MarkBuildingsDirty(buildings);
        MarkCreaturesDirty(creatures);
        return Updated;
    }

    public Dictionary<string, int> GetField(bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        if (!_fieldCacheDirty)
        {
            return Field;
        }

        var field = new Dictionary<string, int>(Math.Max(0, _coverageCount), StringComparer.Ordinal);
        if (Cave is not null)
        {
            foreach (var tile in Cave.GetTiles())
            {
                if (_covered[tile.Id])
                {
                    field[tile.Key] = _values[tile.Id];
                }
            }
        }

        Field = field;
        _fieldCacheDirty = false;
        return Field;
    }

    public bool HasActiveBuildingTarget()
    {
        return string.Equals(Type, "building", StringComparison.Ordinal) &&
               Cave is not null &&
               OwnerBuilding is not null &&
               OwnerBuilding.TileArray.Count > 0;
    }

    public Tile? GetTile(string? tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey) || Cave is null)
        {
            return null;
        }

        return Cave.GetTile(tileKey);
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _values.Length)
        {
            return;
        }

        var oldLength = _values.Length;
        var newLength = Math.Max(requiredCapacity, Math.Max(8, oldLength * 2));

        Array.Resize(ref _values, newLength);
        Array.Fill(_values, int.MaxValue, oldLength, newLength - oldLength);
        Array.Resize(ref _covered, newLength);
        Array.Resize(ref _nextCovered, newLength);
        Array.Resize(ref _blocked, newLength);
        Array.Resize(ref _seeded, newLength);
        Array.Resize(ref _queued, newLength);
    }

    private void ImportField(Dictionary<string, int> field)
    {
        if (Cave is null)
        {
            _coverageCount = 0;
            return;
        }

        EnsureCapacity(Cave.TileCapacity);
        Array.Clear(_covered, 0, _covered.Length);
        _coverageCount = 0;

        foreach (var tile in Cave.GetTiles())
        {
            _values[tile.Id] = int.MaxValue;
        }

        foreach (var pair in field)
        {
            var tile = Cave.GetTile(pair.Key);
            if (tile is null)
            {
                continue;
            }

            if (!_covered[tile.Id])
            {
                _coverageCount++;
            }

            _covered[tile.Id] = true;
            _values[tile.Id] = pair.Value;
        }
    }

    private bool IsTileInCoverage(Tile? tile)
    {
        if (tile is null || Cave is null)
        {
            return false;
        }

        if (string.Equals(Type, "building", StringComparison.Ordinal))
        {
            return HasActiveBuildingTarget() && Cave.IsTileReachable(tile);
        }

        return Cave.IsTileRevealed(tile);
    }

    private void RefreshCoverageState(bool resetValues)
    {
        if (Cave is null)
        {
            _coverageCount = 0;
            Array.Clear(_covered, 0, _covered.Length);
            return;
        }

        EnsureCapacity(Cave.TileCapacity);
        Array.Clear(_nextCovered, 0, _nextCovered.Length);

        if (string.Equals(Type, "building", StringComparison.Ordinal))
        {
            if (HasActiveBuildingTarget())
            {
                foreach (var tile in Cave.GetReachableTiles())
                {
                    _nextCovered[tile.Id] = true;
                }
            }
        }
        else
        {
            foreach (var tile in Cave.GetTiles())
            {
                if (IsTileInCoverage(tile))
                {
                    _nextCovered[tile.Id] = true;
                }
            }
        }

        _coverageCount = 0;
        foreach (var tile in Cave.GetTiles())
        {
            var tileId = tile.Id;
            var shouldCover = _nextCovered[tileId];
            _covered[tileId] = shouldCover;
            if (shouldCover)
            {
                _coverageCount++;
                if (resetValues)
                {
                    _values[tileId] = int.MaxValue;
                }
            }
            else
            {
                _values[tileId] = int.MaxValue;
            }
        }

        _fieldCacheDirty = true;
    }

    private void AddSeed(Tile tile)
    {
        if (_seeded[tile.Id])
        {
            return;
        }

        _seeded[tile.Id] = true;
        _seedIds.Add(tile.Id);
    }

    private void AddAdjacentPassableSeeds(Tile? tile)
    {
        if (tile is null)
        {
            return;
        }

        foreach (var neighbor in tile.Neighbors)
        {
            if (!_covered[neighbor.Id] || !neighbor.CreatureFits() || _blocked[neighbor.Id])
            {
                continue;
            }

            AddSeed(neighbor);
        }
    }

    private void AddBuildingTargets(Building? building, bool blockPassableTiles = false)
    {
        if (building is null || building.TileArray.Count == 0)
        {
            return;
        }

        foreach (var tile in building.TileArray)
        {
            var shouldBlockTile = blockPassableTiles || !tile.CreatureFits();
            if (!shouldBlockTile)
            {
                continue;
            }

            _blocked[tile.Id] = true;
            AddAdjacentPassableSeeds(tile);
        }
    }

    private void AddBuildingSeedIds(Building? building)
    {
        if (building is null || building.TileArray.Count == 0)
        {
            return;
        }

        foreach (var tile in building.TileArray)
        {
            if (tile.CreatureFits() && _covered[tile.Id])
            {
                AddSeed(tile);
            }
        }

        if (_seedIds.Count > 0)
        {
            return;
        }

        foreach (var tile in building.TileArray)
        {
            foreach (var neighbor in tile.Neighbors)
            {
                if (neighbor.CreatureFits() && _covered[neighbor.Id])
                {
                    AddSeed(neighbor);
                }
            }
        }
    }

    private void BuildSnapshot()
    {
        var trackedBuildings = new List<Building>();
        var trackedCreatures = new List<Creature>();

        Array.Clear(_blocked, 0, _blocked.Length);
        Array.Clear(_seeded, 0, _seeded.Length);
        _seedIds.Clear();

        if (Cave is null)
        {
            SetTrackedTargets();
            return;
        }

        foreach (var tile in Cave.GetTiles())
        {
            if (!_covered[tile.Id] || !tile.CreatureFits())
            {
                _blocked[tile.Id] = true;
            }
        }

        if (string.Equals(Type, "building", StringComparison.Ordinal))
        {
            if (HasActiveBuildingTarget())
            {
                trackedBuildings.Add(OwnerBuilding!);
                AddBuildingSeedIds(OwnerBuilding);
            }
        }
        else if (string.Equals(Type, "enemy", StringComparison.Ordinal))
        {
            foreach (var creature in Cave.GetEnemyList())
            {
                trackedCreatures.Add(creature);
                var tile = GetTile(creature.Location.ToString());
                if (tile is null)
                {
                    continue;
                }

                _blocked[tile.Id] = true;
                AddAdjacentPassableSeeds(tile);
            }
        }
        else if (string.Equals(Type, "colony", StringComparison.Ordinal))
        {
            foreach (var creature in Cave.GetTrilobiteList())
            {
                trackedCreatures.Add(creature);
                var tile = GetTile(creature.Location.ToString());
                if (tile is null)
                {
                    continue;
                }

                _blocked[tile.Id] = true;
                AddAdjacentPassableSeeds(tile);
            }

            foreach (var building in Cave.GetBuildingList())
            {
                trackedBuildings.Add(building);
                var isAlgaeFarm = string.Equals(building.Name, "Algae Farm", StringComparison.Ordinal) ||
                                  string.Equals(building.GetType().Name, "AlgaeFarm", StringComparison.Ordinal);
                AddBuildingTargets(building, isAlgaeFarm);
            }
        }

        SetTrackedTargets(trackedBuildings, trackedCreatures);
    }

    private int ComputeValue(Tile tile)
    {
        if (!_covered[tile.Id] || _blocked[tile.Id])
        {
            return int.MaxValue;
        }

        if (_seeded[tile.Id])
        {
            return 0;
        }

        var bestNeighbor = int.MaxValue;
        foreach (var neighbor in tile.Neighbors)
        {
            if (!_covered[neighbor.Id] || _blocked[neighbor.Id])
            {
                continue;
            }

            var neighborValue = _values[neighbor.Id];
            if (neighborValue < bestNeighbor)
            {
                bestNeighbor = neighborValue;
            }
        }

        return bestNeighbor == int.MaxValue ? int.MaxValue : bestNeighbor + 1;
    }

    private void EnqueueTile(Tile tile)
    {
        if (!_covered[tile.Id] || _queued[tile.Id])
        {
            return;
        }

        _queued[tile.Id] = true;
        _queue.Enqueue(tile.Id);
    }

    private Dictionary<string, int> CommitCurrentField()
    {
        _fieldCacheDirty = true;
        ClearUpdates();
        return GetField(false);
    }

    public Dictionary<string, int> Rebuild()
    {
        if (Cave is null)
        {
            Field = new Dictionary<string, int>(StringComparer.Ordinal);
            _fieldCacheDirty = false;
            ClearUpdates();
            return Field;
        }

        RefreshCoverageState(resetValues: true);
        BuildSnapshot();
        Array.Clear(_queued, 0, _queued.Length);
        _queue.Clear();

        foreach (var seedId in _seedIds)
        {
            if (!_covered[seedId] || _blocked[seedId])
            {
                continue;
            }

            _values[seedId] = 0;
            if (!_queued[seedId])
            {
                _queued[seedId] = true;
                _queue.Enqueue(seedId);
            }
        }

        while (_queue.Count > 0)
        {
            var currentId = _queue.Dequeue();
            _queued[currentId] = false;

            var currentTile = Cave.GetTileById(currentId);
            if (currentTile is null)
            {
                continue;
            }

            var currentValue = _values[currentId];
            if (currentValue == int.MaxValue)
            {
                continue;
            }

            foreach (var neighbor in currentTile.Neighbors)
            {
                if (!_covered[neighbor.Id] || _blocked[neighbor.Id])
                {
                    continue;
                }

                var nextValue = currentValue + 1;
                if (nextValue >= _values[neighbor.Id])
                {
                    continue;
                }

                _values[neighbor.Id] = nextValue;
                EnqueueTile(neighbor);
            }
        }

        return CommitCurrentField();
    }

    public Dictionary<string, int> Rebalance(IEnumerable<string>? dirtyKeys = null)
    {
        if (Cave is null || _coverageCount == 0)
        {
            return Rebuild();
        }

        RefreshCoverageState(resetValues: false);
        BuildSnapshot();
        Array.Clear(_queued, 0, _queued.Length);
        _queue.Clear();

        var hasDirty = false;
        foreach (var dirtyKey in dirtyKeys ?? UpdatedTiles)
        {
            var tile = GetTile(dirtyKey);
            if (tile is null)
            {
                continue;
            }

            hasDirty = true;
            EnqueueTile(tile);
            foreach (var neighbor in tile.Neighbors)
            {
                EnqueueTile(neighbor);
            }
        }

        if (!hasDirty)
        {
            return Rebuild();
        }

        while (_queue.Count > 0)
        {
            var currentId = _queue.Dequeue();
            _queued[currentId] = false;

            var currentTile = Cave.GetTileById(currentId);
            if (currentTile is null)
            {
                continue;
            }

            var nextValue = ComputeValue(currentTile);
            if (_values[currentId] == nextValue)
            {
                continue;
            }

            _values[currentId] = nextValue;
            foreach (var neighbor in currentTile.Neighbors)
            {
                EnqueueTile(neighbor);
            }
        }

        return CommitCurrentField();
    }

    public Dictionary<string, int> Refresh()
    {
        if (_coverageCount == 0)
        {
            return Rebuild();
        }

        if (IsUpdated())
        {
            return GetField(false);
        }

        return UpdatedTiles.Count == 0 ? Rebuild() : Rebalance();
    }

    public int GetFieldValue(GridPoint location, bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        var tile = GetTile(location.ToString());
        return tile is null || !_covered[tile.Id]
            ? int.MaxValue
            : _values[tile.Id];
    }

    public GridPoint? GetNextStep(GridPoint location, bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        var currentTile = GetTile(location.ToString());
        if (currentTile is null || !_covered[currentTile.Id])
        {
            return null;
        }

        var currentValue = _values[currentTile.Id];
        Tile? bestNeighbor = null;
        var bestValue = currentValue;

        foreach (var neighbor in currentTile.Neighbors)
        {
            if (!neighbor.CreatureFits() || !_covered[neighbor.Id])
            {
                continue;
            }

            var neighborValue = _values[neighbor.Id];
            if (neighborValue == int.MaxValue || neighborValue >= bestValue)
            {
                continue;
            }

            if (bestNeighbor is null || neighborValue < bestValue || string.CompareOrdinal(neighbor.Key, bestNeighbor.Key) < 0)
            {
                bestNeighbor = neighbor;
                bestValue = neighborValue;
            }
        }

        return bestNeighbor?.Coordinates;
    }

    public List<GridPoint>? BuildPathFrom(GridPoint startLocation, bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        var startTile = GetTile(startLocation.ToString());
        if (startTile is null || !_covered[startTile.Id])
        {
            return null;
        }

        var startValue = _values[startTile.Id];
        if (startValue == int.MaxValue)
        {
            return null;
        }

        var path = new List<GridPoint> { startLocation };
        var current = startLocation;
        var currentValue = startValue;
        var timeCount = 0;

        while (currentValue > 0 && timeCount < 7850)
        {
            var next = GetNextStep(current, false);
            if (next is null)
            {
                return null;
            }

            path.Add(next.Value);
            current = next.Value;
            var currentTile = GetTile(current.ToString());
            if (currentTile is null)
            {
                return null;
            }

            currentValue = _values[currentTile.Id];
            timeCount++;
        }

        return currentValue == 0 ? path : null;
    }
}
