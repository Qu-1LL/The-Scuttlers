using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Vehicles;
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
    private int[] _nextStepIds = [];
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
        if (ReferenceEquals(Cave, cave))
        {
            return;
        }

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

    public bool HasCoverage() => _coverageCount > 0;

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

    public bool MarkTileDirty(string? tileKey)
    {
        Updated = false;
        if (!string.IsNullOrWhiteSpace(tileKey))
        {
            UpdatedTiles.Add(tileKey);
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

    public bool MarkCreatureDirty(Creature? creature)
    {
        Updated = false;
        if (creature is not null)
        {
            UpdatedCreatures.Add(creature);
        }

        return Updated;
    }

    public void ClearField()
    {
        Array.Fill(_values, int.MaxValue);
        Array.Clear(_covered, 0, _covered.Length);
        Array.Clear(_nextCovered, 0, _nextCovered.Length);
        Array.Clear(_blocked, 0, _blocked.Length);
        Array.Clear(_seeded, 0, _seeded.Length);
        Array.Clear(_queued, 0, _queued.Length);
        Array.Fill(_nextStepIds, -1);
        _queue.Clear();
        _seedIds.Clear();
        _coverageCount = 0;
        Field = new Dictionary<string, int>(StringComparer.Ordinal);
        _fieldCacheDirty = false;
        SetTrackedTargets();
        ClearUpdates();
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

    private bool ShouldRemainCleared()
    {
        return string.Equals(Type, "enemy", StringComparison.Ordinal) &&
               Cave is not null &&
               !Cave.Session.Danger;
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
        Array.Resize(ref _nextStepIds, newLength);
        Array.Fill(_nextStepIds, -1, oldLength, newLength - oldLength);
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

        RebuildNextStepCache();
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

        if (string.Equals(Type, "wall", StringComparison.Ordinal))
        {
            return true;
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
                _nextStepIds[tileId] = -1;
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

    private bool IsTilePassableToField(Tile tile)
    {
        if (string.Equals(Type, "colony", StringComparison.Ordinal) ||
            string.Equals(Type, "wall", StringComparison.Ordinal))
        {
            return tile.EnemyFits();
        }

        return tile.CreatureFits();
    }

    private void AddAdjacentPassableSeeds(Tile? tile)
    {
        if (tile is null)
        {
            return;
        }

        foreach (var neighbor in tile.Neighbors)
        {
            if (!_covered[neighbor.Id] || !IsTilePassableToField(neighbor) || _blocked[neighbor.Id])
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
            var shouldBlockTile = blockPassableTiles || !IsTilePassableToField(tile);
            if (!shouldBlockTile)
            {
                continue;
            }

            _blocked[tile.Id] = true;
            AddAdjacentPassableSeeds(tile);
        }
    }

    private void AddVehicleTargets(Vehicle? vehicle)
    {
        if (vehicle is null || vehicle.TileArray.Count == 0)
        {
            return;
        }

        foreach (var tile in vehicle.TileArray)
        {
            _blocked[tile.Id] = true;
            AddAdjacentPassableSeeds(tile);
        }
    }

    private void AddBuildingSeedIds(Building? building)
    {
        var cave = Cave;
        if (building is null || cave is null || building.TileArray.Count == 0)
        {
            return;
        }

        var hasNavigationTargets = false;
        for (var zoneIndex = 0; zoneIndex < building.InteractionZones.Count; zoneIndex++)
        {
            var zone = building.InteractionZones[zoneIndex];
            if (!zone.IsNavigationTarget)
            {
                continue;
            }

            hasNavigationTargets = true;
            for (var slotIndex = 0; slotIndex < zone.SlotPositions.Count; slotIndex++)
            {
                var tile = cave.GetTile(zone.SlotPositions[slotIndex].ToGridPoint());
                if (tile is not null && IsTilePassableToField(tile) && _covered[tile.Id])
                {
                    AddSeed(tile);
                }
            }
        }

        if (hasNavigationTargets)
        {
            return;
        }

        if (building.NavigationSeedMode == BuildingNavigationSeedMode.AdjacentExteriorPassableTiles)
        {
            foreach (var tile in building.TileArray)
            {
                foreach (var neighbor in tile.Neighbors)
                {
                    if (ReferenceEquals(neighbor.Built, building) ||
                        !IsTilePassableToField(neighbor) ||
                        !_covered[neighbor.Id])
                    {
                        continue;
                    }

                    AddSeed(neighbor);
                }
            }

            return;
        }

        foreach (var tile in building.TileArray)
        {
            if (ReferenceEquals(tile.Built, building) && IsTilePassableToField(tile) && _covered[tile.Id])
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
                if (IsTilePassableToField(neighbor) && _covered[neighbor.Id])
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
            if (!_covered[tile.Id] || !IsTilePassableToField(tile))
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
        else if (string.Equals(Type, "wall", StringComparison.Ordinal))
        {
            foreach (var wall in Cave.GetWalls())
            {
                trackedBuildings.Add(wall);
                AddBuildingTargets(wall);
            }
        }
        else if (string.Equals(Type, "enemy", StringComparison.Ordinal))
        {
            foreach (var creature in Cave.GetEnemyList())
            {
                if (!creature.IsLocomotionEnabled)
                {
                    continue;
                }

                trackedCreatures.Add(creature);
                var tile = Cave.GetTile(creature.Location);
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
                if (!creature.IsLocomotionEnabled)
                {
                    continue;
                }

                trackedCreatures.Add(creature);
                var tile = Cave.GetTile(creature.Location);
                if (tile is null)
                {
                    continue;
                }

                _blocked[tile.Id] = true;
                AddAdjacentPassableSeeds(tile);
            }

            foreach (var building in Cave.GetBuildingList())
            {
                if (building is Wall || building.IgnoredByAnts)
                {
                    continue;
                }

                trackedBuildings.Add(building);
                // Every colony building is a valid ant destination. Passable tiles inside
                // an open-map building still need to be blocked as target seeds; otherwise
                // the colony field has no route endpoint for that building.
                AddBuildingTargets(building, blockPassableTiles: true);
            }

            foreach (var vehicle in Cave.GetVehicles())
            {
                AddVehicleTargets(vehicle);
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

    private void SetTileCoverage(Tile tile, bool shouldCover)
    {
        if (_covered[tile.Id] == shouldCover)
        {
            return;
        }

        _covered[tile.Id] = shouldCover;
        _coverageCount += shouldCover ? 1 : -1;
        if (!shouldCover)
        {
            _values[tile.Id] = int.MaxValue;
        }
    }

    public bool ApplyMinedTileUpdate(string tileKey)
    {
        if (Cave is null)
        {
            return false;
        }

        var tile = GetTile(tileKey);
        if (tile is null)
        {
            return false;
        }

        EnsureCapacity(Cave.TileCapacity);

        var shouldCover = IsTileInCoverage(tile);
        SetTileCoverage(tile, shouldCover);
        if (!shouldCover)
        {
            _fieldCacheDirty = true;
            return false;
        }

        // Mining only opens the tile that was just cleared; we keep this update local
        // by deriving its value from already-known neighbor distances.
        _blocked[tile.Id] = !IsTilePassableToField(tile);
        _seeded[tile.Id] = false;
        _values[tile.Id] = ComputeValue(tile);
        _fieldCacheDirty = true;
        return _values[tile.Id] != int.MaxValue;
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

    // Distance increases need a temporary invalidation step so disconnected pockets
    // collapse to infinity instead of ratcheting upward forever through cycles.
    private void InvalidateTileValue(Tile tile)
    {
        _values[tile.Id] = int.MaxValue;
        foreach (var neighbor in tile.Neighbors)
        {
            EnqueueTile(neighbor);
        }

        EnqueueTile(tile);
    }

    private Dictionary<string, int> CommitCurrentField()
    {
        RebuildNextStepCache();
        _fieldCacheDirty = true;
        ClearUpdates();
        return GetField(false);
    }

    private void RebuildNextStepCache()
    {
        if (Cave is null)
        {
            Array.Fill(_nextStepIds, -1);
            return;
        }

        EnsureCapacity(Cave.TileCapacity);
        Array.Fill(_nextStepIds, -1);

        foreach (var tile in Cave.GetTiles())
        {
            RefreshNextStepForTile(tile);
        }
    }

    private void RefreshNextStepForTile(Tile tile)
    {
        _nextStepIds[tile.Id] = -1;
        if (!_covered[tile.Id] || _blocked[tile.Id])
        {
            return;
        }

        var currentValue = _values[tile.Id];
        if (currentValue == int.MaxValue || currentValue == 0)
        {
            return;
        }

        Tile? bestNeighbor = null;
        var bestValue = currentValue;
        foreach (var neighbor in tile.Neighbors)
        {
            if (!IsTilePassableToField(neighbor) || !_covered[neighbor.Id] || _blocked[neighbor.Id])
            {
                continue;
            }

            var neighborValue = _values[neighbor.Id];
            if (neighborValue == int.MaxValue || neighborValue >= bestValue)
            {
                continue;
            }

            if (bestNeighbor is null ||
                neighborValue < bestValue ||
                string.CompareOrdinal(neighbor.Key, bestNeighbor.Key) < 0)
            {
                bestNeighbor = neighbor;
                bestValue = neighborValue;
            }
        }

        _nextStepIds[tile.Id] = bestNeighbor?.Id ?? -1;
    }

    // Repair a building field without rebuilding coverage, seeds, or next steps for the whole map.
    public Dictionary<string, int> RefreshBuildingIncrementally(IEnumerable<string>? dirtyKeys = null)
    {
        if (!string.Equals(Type, "building", StringComparison.Ordinal))
        {
            return Refresh();
        }

        if (Cave is null || _coverageCount == 0)
        {
            return Rebuild();
        }

        EnsureCapacity(Cave.TileCapacity);
        Array.Clear(_queued, 0, _queued.Length);
        _queue.Clear();
        var touchedIds = new HashSet<int>();
        var hasDirty = false;
        foreach (var dirtyKey in dirtyKeys ?? UpdatedTiles)
        {
            var tile = GetTile(dirtyKey);
            if (tile is null)
            {
                continue;
            }

            hasDirty = true;
            SetTileCoverage(tile, IsTileInCoverage(tile));
            if (_covered[tile.Id])
            {
                _blocked[tile.Id] = !IsTilePassableToField(tile);
            }

            touchedIds.Add(tile.Id);
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
            touchedIds.Add(currentId);

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

            if (_values[currentId] != int.MaxValue && nextValue > _values[currentId])
            {
                InvalidateTileValue(currentTile);
                continue;
            }

            _values[currentId] = nextValue;
            foreach (var neighbor in currentTile.Neighbors)
            {
                EnqueueTile(neighbor);
            }
        }

        if (_fieldCacheDirty)
        {
            RebuildNextStepCache();
            ClearUpdates();
            return GetField(false);
        }

        foreach (var tileId in touchedIds)
        {
            var tile = Cave.GetTileById(tileId);
            if (tile is null)
            {
                continue;
            }

            RefreshNextStepForTile(tile);
            if (_covered[tileId])
            {
                Field[tile.Key] = _values[tileId];
            }
            else
            {
                Field.Remove(tile.Key);
            }
        }

        ClearUpdates();
        return Field;
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

        if (ShouldRemainCleared())
        {
            ClearField();
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

            if (_values[currentId] != int.MaxValue && nextValue > _values[currentId])
            {
                InvalidateTileValue(currentTile);
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
        if (ShouldRemainCleared())
        {
            ClearField();
            return Field;
        }

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

        EnsureCapacity(Cave?.TileCapacity ?? 0);

        var tile = Cave?.GetTile(location);
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

        EnsureCapacity(Cave?.TileCapacity ?? 0);

        var currentTile = Cave?.GetTile(location);
        if (currentTile is null || !_covered[currentTile.Id])
        {
            return null;
        }

        var nextStepId = _nextStepIds[currentTile.Id];
        return nextStepId < 0 ? null : Cave?.GetTileById(nextStepId)?.Coordinates;
    }

    public List<GridPoint>? BuildPathFrom(GridPoint startLocation, bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        EnsureCapacity(Cave?.TileCapacity ?? 0);

        var startTile = Cave?.GetTile(startLocation);
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
