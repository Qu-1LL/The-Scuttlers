using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class MiningPost : Building, IResourceStorage, IStorage
{
    private static readonly IReadOnlyList<InteractionZoneDefinition> ZoneDefinitions =
    [
        new("Resource transfer", InteractionZonePurpose.ResourceTransfer, new GridPoint(0, 0), new GridPoint(3, 3),
            [
                new GridPoint(0, 0), new GridPoint(1, 0), new GridPoint(2, 0),
                new GridPoint(0, 1), new GridPoint(1, 1), new GridPoint(2, 1),
                new GridPoint(0, 2), new GridPoint(1, 2), new GridPoint(2, 2)
            ])
    ];
    private readonly Dictionary<ResourceName, int> _inventory = [];
    private readonly Dictionary<Creature, string?> _assignments = [];
    private readonly Dictionary<Creature, ResourceReservation> _materialReservations = [];
    private readonly Dictionary<string, List<string>> _mineableQueues = new(StringComparer.Ordinal);
    private readonly HashSet<string> _trackedMineableTileKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _mineableQueueHeads = new(StringComparer.Ordinal);
    private readonly List<string> _mineableTypes = [];
    private readonly HashSet<string> _dirtyMineableTileKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revealedWallTileKeys = new(StringComparer.Ordinal);
    private int _inventoryTotal;
    private int _mineableTypeCursor;

    public MiningPost(GameSession session)
        : base("Mining Post", new GridPoint(3, 3), [[1, 1, 1], [1, 1, 1], [1, 1, 1]], session, true)
    {
        TextureKey = "MiningPost";
        Capacity = 1000;
        Radius = 10;
        Description = $"Units assigned to this post will mine ore and stone in a {Radius}-block radius and store it here. Has a capacity of {Capacity}.";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];

        foreach (var ore in Economy.OreType.GetOres())
        {
            _inventory[ore.Resource] = 0;
        }
    }

    public int Capacity { get; }

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Asynchronous;

    public int Radius { get; }

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => ZoneDefinitions;

    public bool MineableQueuesReady { get; private set; }

    public bool MineableQueuesDirty { get; private set; } = true;

    public bool AssignmentsAvailable { get; private set; } = true;

    public IReadOnlyDictionary<ResourceName, int> GetInventory() => _inventory;

    public IReadOnlyDictionary<ResourceName, int> GetStoredResources() => _inventory;

    public int GetStoredAmount(ResourceName resourceType) => _inventory.GetValueOrDefault(resourceType, 0);

    public int GetStoredAmount(ResourceCategory resourceCategory)
    {
        return ResourceInventoryHelper.GetStoredAmount(resourceCategory, GetStoredAmount);
    }

    public ResourceStorageMatch? FindStoredResource(ResourceRequirement requirement, int maxAmount)
    {
        return ResourceInventoryHelper.FindStoredResource(requirement, maxAmount, GetStoredAmount);
    }

    public int GetInventoryTotal() => _inventoryTotal;

    public int GetInventorySpace() => System.Math.Max(0, Capacity - GetInventoryTotal());

    public ResourceReservation? GetMaterialReservation(Creature creature)
    {
        return _materialReservations.GetValueOrDefault(creature);
    }

    public int GetReservedAmount(ResourceName resourceType, Creature? excludeCreature = null)
    {
        return _materialReservations
            .Where(pair => pair.Key != excludeCreature && pair.Value.ResourceType == resourceType)
            .Sum(pair => pair.Value.Amount);
    }

    public int GetAvailableInventory(ResourceName resourceType, Creature? excludeCreature = null)
    {
        return System.Math.Max(0, _inventory.GetValueOrDefault(resourceType, 0) - GetReservedAmount(resourceType, excludeCreature));
    }

    public int GetAvailableInventory(ResourceCategory resourceCategory, Creature? excludeCreature = null)
    {
        return ResourceInventoryHelper.GetStoredAmount(resourceCategory, resourceType => GetAvailableInventory(resourceType, excludeCreature));
    }

    public ResourceStorageMatch? FindAvailableResource(ResourceRequirement requirement, int maxAmount, Creature? excludeCreature = null)
    {
        return ResourceInventoryHelper.FindStoredResource(
            requirement,
            maxAmount,
            resourceType => GetAvailableInventory(resourceType, excludeCreature));
    }

    public int Deposit(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        var accepted = System.Math.Min(GetInventorySpace(), amount);
        _inventory[resourceType] += accepted;
        _inventoryTotal += accepted;
        if (accepted > 0)
        {
            EmitStorageInventoryChanged(resourceType, accepted);
            Cave?.SyncMiningPostAssignmentAvailability();
        }

        return accepted;
    }

    public int Withdraw(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        var taken = System.Math.Min(_inventory[resourceType], amount);
        _inventory[resourceType] -= taken;
        _inventoryTotal -= taken;
        if (taken > 0)
        {
            EmitStorageInventoryChanged(resourceType, -taken);
            Cave?.SyncMiningPostAssignmentAvailability();
        }

        return taken;
    }

    public int ReserveMaterial(Creature creature, ResourceName resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        ReleaseMaterialReservation(creature);
        var reserved = System.Math.Min(amount, GetAvailableInventory(resourceType, creature));
        if (reserved <= 0)
        {
            return 0;
        }

        _materialReservations[creature] = new ResourceReservation(resourceType, reserved);
        return reserved;
    }

    public ResourceReservation? ReleaseMaterialReservation(Creature creature)
    {
        if (!_materialReservations.TryGetValue(creature, out var reservation))
        {
            return null;
        }

        _materialReservations.Remove(creature);
        return reservation;
    }

    public ResourceReservation? WithdrawReservedMaterial(Creature creature, int? amount = null)
    {
        if (!_materialReservations.TryGetValue(creature, out var reservation))
        {
            return null;
        }

        var requested = amount.HasValue && amount.Value > 0
            ? System.Math.Min(amount.Value, reservation.Amount)
            : reservation.Amount;
        var taken = System.Math.Min(requested, _inventory.GetValueOrDefault(reservation.ResourceType, 0));
        if (taken <= 0)
        {
            if (_inventory.GetValueOrDefault(reservation.ResourceType, 0) <= 0 || reservation.Amount <= 0)
            {
                _materialReservations.Remove(creature);
            }

            return null;
        }

        _inventory[reservation.ResourceType] -= taken;
        _inventoryTotal -= taken;
        EmitStorageInventoryChanged(reservation.ResourceType, -taken);
        Cave?.SyncMiningPostAssignmentAvailability();
        var remaining = reservation.Amount - taken;
        if (remaining <= 0)
        {
            _materialReservations.Remove(creature);
        }
        else
        {
            _materialReservations[creature] = reservation with { Amount = remaining };
        }

        return new ResourceReservation(reservation.ResourceType, taken);
    }

    public void Assign(Creature creature, string? tileKey)
    {
        var hadAssignment = _assignments.ContainsKey(creature);
        var previousTileKey = _assignments.GetValueOrDefault(creature);
        _assignments[creature] = tileKey;
        TrackCreature(creature);
        if (!hadAssignment)
        {
            Cave?.SyncMiningPostAssignmentCount(this, _assignments.Count);
        }

        if (Cave is null)
        {
            return;
        }

        if (!string.Equals(previousTileKey, tileKey, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(previousTileKey))
        {
            InvalidateMineableQueuesForKeys([previousTileKey]);
            return;
        }

        RefreshAssignmentsAvailable(Cave);
    }

    public void RemoveAssignment(Creature creature)
    {
        if (!_assignments.Remove(creature, out var previousTileKey))
        {
            return;
        }

        UntrackCreature(creature);
        Cave?.SyncMiningPostAssignmentCount(this, _assignments.Count);

        if (Cave is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousTileKey))
        {
            InvalidateMineableQueuesForKeys([previousTileKey]);
            return;
        }

        RefreshAssignmentsAvailable(Cave);
    }

    public string? GetAssignment(Creature creature)
    {
        return _assignments.GetValueOrDefault(creature);
    }

    public HashSet<string> GetAssignedTileKeys(Creature? excludeCreature = null)
    {
        return _assignments
            .Where(pair => pair.Key != excludeCreature && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    // The mineable queues are also the post's authoritative target index for route searches.
    internal bool IsTrackedMineableTarget(World.Tile tile, ResourceName? requiredResource)
    {
        return _trackedMineableTileKeys.Contains(tile.Key) &&
               Building.IsMineableType(tile.Base) &&
               IsMineableTypeCompatibleWithResource(tile.Base, requiredResource);
    }

    internal bool HasUnassignedTrackedMineableTarget(World.Cave cave, Creature creature, ResourceName? requiredResource)
    {
        foreach (var tileKey in _trackedMineableTileKeys)
        {
            var tile = cave.GetTile(tileKey);
            if (tile is not null &&
                IsTrackedMineableTarget(tile, requiredResource) &&
                !IsTargetAssignedToOther(creature, tileKey))
            {
                return true;
            }
        }

        return false;
    }

    internal bool IsTargetAssignedToOther(Creature creature, string tileKey)
    {
        foreach (var assignment in _assignments)
        {
            if (!ReferenceEquals(assignment.Key, creature) &&
                string.Equals(assignment.Value, tileKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public int GetVolume() => _assignments.Count;

    public override void OnBuilt(World.Cave cave)
    {
        base.OnBuilt(cave);
        RefreshPossibleAssignmentsOnBuilt(cave);
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        if (_inventoryTotal > 0)
        {
            foreach (var pair in _inventory)
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                EmitStorageInventoryChanged(pair.Key, -pair.Value);
            }
        }

        _inventory.Clear();
        _inventoryTotal = 0;
        base.CleanupBeforeRemoval(source);
    }

    public override void TrackedCreatureDied(Creature creature)
    {
        RemoveAssignment(creature);
    }

    public void InvalidateMineableQueues()
    {
        _dirtyMineableTileKeys.Clear();
        var cave = Cave;
        if (cave is null)
        {
            MineableQueuesDirty = true;
            return;
        }

        EnsureMineableQueues(cave);
        SetAssignmentsAvailable(HasAvailableMineableTile(cave));
    }

    public void InvalidateMineableQueuesForKeys(IEnumerable<string> tileKeys)
    {
        var cave = Cave;
        if (cave is null)
        {
            return;
        }

        if (!MineableQueuesReady)
        {
            RebuildMineableQueues(cave);
        }

        foreach (var tileKey in tileKeys)
        {
            if (string.IsNullOrWhiteSpace(tileKey))
            {
                continue;
            }

            var tile = cave.GetTile(tileKey);
            var location = tile?.Coordinates ?? GridPoint.Parse(tileKey);
            if (IsLocationInArea(location))
            {
                MineableQueuesDirty = true;
                _dirtyMineableTileKeys.Add(tileKey);
            }
        }

        if (MineableQueuesDirty)
        {
            ApplyDirtyMineableQueueUpdates(cave);
        }
    }

    public void EnsureMineableQueues(World.Cave cave)
    {
        if (!MineableQueuesReady)
        {
            RebuildMineableQueues(cave);
            return;
        }

        if (MineableQueuesDirty)
        {
            ApplyDirtyMineableQueueUpdates(cave);
        }
    }

    public void RebuildMineableQueues(World.Cave cave)
    {
        PopulateMineableQueues(cave, cave.GetRevealedTiles(), requireReveal: true);
    }

    private void RefreshPossibleAssignmentsOnBuilt(World.Cave cave)
    {
        // The starter mining post is built before the initial cave reveal runs, so it
        // needs one construction-time seed pass over in-radius mineables. Runtime upkeep
        // stays incremental after this initial population.
        PopulateMineableQueues(cave, cave.GetTiles(), requireReveal: false);
    }

    private void PopulateMineableQueues(World.Cave cave, IEnumerable<World.Tile> tiles, bool requireReveal)
    {
        var grouped = new Dictionary<string, List<(string Key, int Dist)>>(StringComparer.Ordinal);

        _mineableQueues.Clear();
        _trackedMineableTileKeys.Clear();
        _mineableQueueHeads.Clear();
        _mineableTypes.Clear();
        _mineableTypeCursor = 0;
        _revealedWallTileKeys.Clear();

        foreach (var tile in tiles)
        {
            if (!ShouldTrackMineableTile(cave, tile, requireReveal))
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(tile.Coordinates, GetCenter());
            if (!grouped.TryGetValue(tile.Base, out var queue))
            {
                queue = [];
                grouped[tile.Base] = queue;
            }

            queue.Add((tile.Key, distance));
            _trackedMineableTileKeys.Add(tile.Key);
            if (string.Equals(tile.Base, "wall", StringComparison.Ordinal))
            {
                _revealedWallTileKeys.Add(tile.Key);
            }
        }

        foreach (var pair in grouped)
        {
            var ordered = pair.Value
                .OrderBy(entry => entry.Dist)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Key)
                .ToList();
            _mineableQueues[pair.Key] = ordered;
            _mineableQueueHeads[pair.Key] = 0;
            _mineableTypes.Add(pair.Key);
        }

        MineableQueuesReady = true;
        MineableQueuesDirty = false;
        _dirtyMineableTileKeys.Clear();
        SetAssignmentsAvailable(HasAvailableMineableTile(cave));
    }

    private void ApplyDirtyMineableQueueUpdates(World.Cave cave)
    {
        if (!MineableQueuesReady)
        {
            RebuildMineableQueues(cave);
            return;
        }

        if (_dirtyMineableTileKeys.Count == 0)
        {
            MineableQueuesDirty = false;
            SetAssignmentsAvailable(HasAvailableMineableTile(cave));
            return;
        }

        foreach (var tileKey in _dirtyMineableTileKeys)
        {
            ApplyMineableTileChange(cave, tileKey);
        }

        MineableQueuesDirty = false;
        _dirtyMineableTileKeys.Clear();
        SetAssignmentsAvailable(HasAvailableMineableTile(cave));
    }

    private void ApplyMineableTileChange(World.Cave cave, string tileKey)
    {
        RemoveTileFromQueues(tileKey);
        _revealedWallTileKeys.Remove(tileKey);

        var tile = cave.GetTile(tileKey);
        if (!ShouldTrackMineableTile(cave, tile))
        {
            return;
        }

        var trackedTile = tile!;

        if (string.Equals(trackedTile.Base, "wall", StringComparison.Ordinal))
        {
            _revealedWallTileKeys.Add(trackedTile.Key);
        }

        InsertTileIntoQueue(trackedTile.Base, trackedTile.Key, GridPoint.SquaredDistance(trackedTile.Coordinates, GetCenter()));
    }

    private bool ShouldTrackMineableTile(World.Cave cave, World.Tile? tile)
    {
        return ShouldTrackMineableTile(cave, tile, requireReveal: true);
    }

    private bool ShouldTrackMineableTile(World.Cave cave, World.Tile? tile, bool requireReveal)
    {
        return tile is not null &&
               (!requireReveal || cave.IsTileRevealed(tile)) &&
               Building.IsMineableType(tile.Base) &&
               IsLocationInArea(tile.Coordinates);
    }

    private void SetAssignmentsAvailable(bool assignmentsAvailable)
    {
        var previousValue = AssignmentsAvailable;
        if (previousValue == assignmentsAvailable)
        {
            return;
        }

        AssignmentsAvailable = assignmentsAvailable;
        Cave?.OnMiningPostAssignmentsAvailableChanged(this, previousValue, assignmentsAvailable);
    }

    private bool HasAvailableMineableTile(World.Cave cave)
    {
        return HasClaimableMineableFor(cave, carriedResource: null);
    }

    public bool RefreshAssignmentsAvailable(World.Cave cave)
    {
        EnsureMineableQueues(cave);
        SetAssignmentsAvailable(HasAvailableMineableTile(cave));
        return AssignmentsAvailable;
    }

    private void RemoveTileFromQueues(string tileKey)
    {
        _trackedMineableTileKeys.Remove(tileKey);
        for (var index = _mineableTypes.Count - 1; index >= 0; index--)
        {
            var type = _mineableTypes[index];
            if (!_mineableQueues.TryGetValue(type, out var queue))
            {
                continue;
            }

            var queueIndex = queue.IndexOf(tileKey);
            if (queueIndex < 0)
            {
                continue;
            }

            var head = _mineableQueueHeads.GetValueOrDefault(type, 0);
            queue.RemoveAt(queueIndex);
            if (queueIndex < head)
            {
                _mineableQueueHeads[type] = Math.Max(0, head - 1);
            }

            if (queue.Count == 0)
            {
                _mineableQueues.Remove(type);
                _mineableQueueHeads.Remove(type);
                _mineableTypes.RemoveAt(index);
            }
        }
    }

    private void InsertTileIntoQueue(string type, string tileKey, int distance)
    {
        _trackedMineableTileKeys.Add(tileKey);
        if (!_mineableQueues.TryGetValue(type, out var queue))
        {
            queue = [];
            _mineableQueues[type] = queue;
            _mineableQueueHeads[type] = 0;
            _mineableTypes.Add(type);
        }

        var insertIndex = queue.Count;
        while (insertIndex > 0)
        {
            var previousKey = queue[insertIndex - 1];
            var previousTile = Cave?.GetTile(previousKey);
            var previousDistance = previousTile is null
                ? int.MaxValue
                : GridPoint.SquaredDistance(previousTile.Coordinates, GetCenter());
            if (previousDistance < distance ||
                (previousDistance == distance && string.CompareOrdinal(previousKey, tileKey) <= 0))
            {
                break;
            }

            insertIndex--;
        }

        queue.Insert(insertIndex, tileKey);
    }

    public bool HasQueuedMineableTiles(World.Cave cave)
    {
        return HasAnyClaimableMineable(cave);
    }

    public bool HasQueuedMineableTiles(World.Cave cave, ResourceName? requiredResource, Creature? creature)
    {
        return HasClaimableMineableFor(cave, creature, requiredResource);
    }

    public bool HasAnyClaimableMineable(World.Cave cave)
    {
        return HasClaimableMineableFor(cave, carriedResource: null);
    }

    public bool HasClaimableMineableFor(World.Cave cave, Creature? creature, ResourceName? carriedResource)
    {
        return HasClaimableMineableFor(cave, carriedResource);
    }

    private bool HasClaimableMineableFor(World.Cave cave, ResourceName? carriedResource)
    {
        EnsureMineableQueues(cave);
        for (var index = 0; index < _mineableTypes.Count; index++)
        {
            var type = _mineableTypes[index];
            if (IsMineableTypeCompatibleWithResource(type, carriedResource) &&
                HasValidQueuedMineableTile(cave, type))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMineableTypeCompatibleWithResource(string tileType, ResourceName? requiredResource)
    {
        if (!requiredResource.HasValue)
        {
            return true;
        }

        if (string.Equals(tileType, World.Tile.CaveCrystalBase, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(tileType, "wall", StringComparison.Ordinal))
        {
            return requiredResource.Value == GameConstants.WallMineResourceType;
        }

        return OreType.TryGet(tileType, out var oreType) && oreType.Resource == requiredResource.Value;
    }

    public int GetTypeQueueLength(string type)
    {
        return _mineableQueues.TryGetValue(type, out var queue)
            ? System.Math.Max(0, queue.Count - _mineableQueueHeads.GetValueOrDefault(type, 0))
            : 0;
    }

    private string? PopTypeQueueKey(string type)
    {
        if (!_mineableQueues.TryGetValue(type, out var queue))
        {
            return null;
        }

        var head = _mineableQueueHeads.GetValueOrDefault(type, 0);
        if (head >= queue.Count)
        {
            return null;
        }

        var tileKey = queue[head];
        _mineableQueueHeads[type] = head + 1;
        CompactTypeQueue(type);
        return tileKey;
    }

    private void PushTypeQueueKey(string type, string tileKey)
    {
        if (!_mineableQueues.ContainsKey(type))
        {
            _mineableQueues[type] = [];
            _mineableQueueHeads[type] = 0;
            _mineableTypes.Add(type);
        }

        _mineableQueues[type].Add(tileKey);
    }

    private void CompactTypeQueue(string type)
    {
        if (!_mineableQueues.TryGetValue(type, out var queue))
        {
            return;
        }

        var head = _mineableQueueHeads.GetValueOrDefault(type, 0);
        if (head < 64 || (head * 2) < queue.Count)
        {
            return;
        }

        var compacted = new List<string>(queue.Count - head);
        for (var index = head; index < queue.Count; index++)
        {
            compacted.Add(queue[index]);
        }

        _mineableQueues[type] = compacted;
        _mineableQueueHeads[type] = 0;
    }

    private bool HasValidQueuedMineableTile(World.Cave cave, string type)
    {
        if (!_mineableQueues.TryGetValue(type, out var queue))
        {
            return false;
        }

        var center = GetCenter();
        var radiusSq = Radius * Radius;
        var head = _mineableQueueHeads.GetValueOrDefault(type, 0);
        for (var index = head; index < queue.Count; index++)
        {
            var tileKey = queue[index];
            var tile = cave.GetTile(tileKey);
            if (tile is null || tile.Base != type ||
                GridPoint.SquaredDistance(tile.Coordinates, center) > radiusSq ||
                GetNavigationTarget(cave, tile) is null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private World.Tile? PullQueuedMineableTile(World.Cave cave, string type, Creature? creature)
    {
        var center = GetCenter();
        var radiusSq = Radius * Radius;
        var queueLength = GetTypeQueueLength(type);

        for (var index = 0; index < queueLength; index++)
        {
            var tileKey = PopTypeQueueKey(type);
            if (tileKey is null)
            {
                return null;
            }

            var tile = cave.GetTile(tileKey);
            if (tile is null || tile.Base != type)
            {
                continue;
            }

            if (GridPoint.SquaredDistance(tile.Coordinates, center) > radiusSq)
            {
                continue;
            }

            var navTarget = GetNavigationTarget(cave, tile);
            if (navTarget is null)
            {
                continue;
            }

            PushTypeQueueKey(type, tileKey);
            return tile;
        }

        return null;
    }

    public bool IsLocationInArea(GridPoint location)
    {
        return Location is not null && GridPoint.SquaredDistance(location, GetCenter()) <= (Radius * Radius);
    }

    public bool IsLocationOnPost(GridPoint location)
    {
        foreach (var tile in TileArray)
        {
            if (tile.Coordinates == location)
            {
                return true;
            }
        }

        return false;
    }

    public GridPoint? GetNavigationTarget(World.Cave cave, World.Tile tile)
    {
        var center = GetCenter();

        if (!Building.IsMineableType(tile.Base))
        {
            return null;
        }

        GridPoint? bestTarget = null;
        var bestDistance = int.MaxValue;
        foreach (var neighbor in tile.Neighbors)
        {
            if (!neighbor.CreatureFits())
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(neighbor.Coordinates, center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = neighbor.Coordinates;
            }
        }

        return bestTarget;
    }

    public GridPoint? GetApproachTile(World.Cave cave, GridPoint startLocation)
    {
        GridPoint? bestTile = null;
        var bestDistance = int.MaxValue;

        foreach (var tile in TileArray)
        {
            if (!tile.CreatureFits())
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(tile.Coordinates, startLocation);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile.Coordinates;
            }
        }

        return bestTile;
    }

    public World.Tile? GrabMineableTile(World.Cave cave, Creature? creature = null, ResourceName? requiredResource = null)
    {
        EnsureMineableQueues(cave);
        if (_mineableTypes.Count == 0)
        {
            SetAssignmentsAvailable(false);
            return null;
        }

        var startIndex = _mineableTypeCursor % _mineableTypes.Count;
        for (var offset = 0; offset < _mineableTypes.Count; offset++)
        {
            var typeIndex = (startIndex + offset) % _mineableTypes.Count;
            var type = _mineableTypes[typeIndex];
            if (GetTypeQueueLength(type) <= 0 ||
                !IsMineableTypeCompatibleWithResource(type, requiredResource))
            {
                continue;
            }

            var queuedTile = PullQueuedMineableTile(cave, type, creature);
            if (queuedTile is null)
            {
                continue;
            }

            if (creature is not null)
            {
                Assign(creature, queuedTile.Key);
            }

            _mineableTypeCursor = (typeIndex + 1) % _mineableTypes.Count;

            return queuedTile;
        }

        if (!requiredResource.HasValue)
        {
            SetAssignmentsAvailable(HasAvailableMineableTile(cave));
        }

        return null;
    }

    private void EmitStorageInventoryChanged(ResourceName resourceType, int resourceDelta)
    {
        if (resourceDelta == 0)
        {
            return;
        }

        Session.Emit(
            GameEvents.StorageInventoryChanged,
            new GameEventPayload(
                Cave,
                null,
                Location,
                null,
                resourceType,
                this,
                resourceDelta));
    }
}
