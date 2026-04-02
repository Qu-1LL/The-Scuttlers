using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class MiningPost : Building
{
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private readonly Dictionary<Creature, string?> _assignments = [];
    private readonly Dictionary<Creature, ResourceReservation> _materialReservations = [];
    private readonly Dictionary<string, List<string>> _mineableQueues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _mineableQueueHeads = new(StringComparer.Ordinal);
    private readonly List<string> _mineableTypes = [];
    private readonly HashSet<string> _dirtyMineableTileKeys = new(StringComparer.Ordinal);

    public MiningPost(GameSession session)
        : base("Mining Post", new GridPoint(3, 3), [[1, 1, 1], [1, 0, 1], [1, 1, 1]], session, true)
    {
        TextureKey = "MiningPost";
        Capacity = 1000;
        Radius = 10;
        Description = $"Units assigned to this post will mine ore and stone in a {Radius}-block radius and store it here. Has a capacity of {Capacity}.";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };

        foreach (var ore in Economy.OreType.GetOres())
        {
            _inventory[ore.Name] = 0;
        }
    }

    public int Capacity { get; }

    public int Radius { get; }

    public bool MineableQueuesReady { get; private set; }

    public bool MineableQueuesDirty { get; private set; } = true;

    public IReadOnlyDictionary<string, int> GetInventory() => _inventory;

    public int GetInventoryTotal() => _inventory.Values.Sum();

    public int GetInventorySpace() => System.Math.Max(0, Capacity - GetInventoryTotal());

    public ResourceReservation? GetMaterialReservation(Creature creature)
    {
        return _materialReservations.GetValueOrDefault(creature);
    }

    public int GetReservedAmount(string resourceType, Creature? excludeCreature = null)
    {
        return _materialReservations
            .Where(pair => pair.Key != excludeCreature && string.Equals(pair.Value.ResourceType, resourceType, StringComparison.Ordinal))
            .Sum(pair => pair.Value.Amount);
    }

    public int GetAvailableInventory(string resourceType, Creature? excludeCreature = null)
    {
        return System.Math.Max(0, _inventory.GetValueOrDefault(resourceType, 0) - GetReservedAmount(resourceType, excludeCreature));
    }

    public int Deposit(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        var accepted = System.Math.Min(GetInventorySpace(), amount);
        _inventory[resourceType] += accepted;
        return accepted;
    }

    public int Withdraw(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        var taken = System.Math.Min(_inventory[resourceType], amount);
        _inventory[resourceType] -= taken;
        return taken;
    }

    public int ReserveMaterial(Creature creature, string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
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
        if (tileKey is not null && IsTileAssignedToOther(creature, tileKey))
        {
            return;
        }

        _assignments[creature] = tileKey;
    }

    public void RemoveAssignment(Creature creature)
    {
        _assignments.Remove(creature);
    }

    public string? GetAssignment(Creature creature)
    {
        return _assignments.GetValueOrDefault(creature);
    }

    public bool IsTileAssignedToOther(Creature creature, string tileKey)
    {
        return _assignments.Any(pair => pair.Key != creature && string.Equals(pair.Value, tileKey, StringComparison.Ordinal));
    }

    public HashSet<string> GetAssignedTileKeys(Creature? excludeCreature = null)
    {
        return _assignments
            .Where(pair => pair.Key != excludeCreature && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    public int GetVolume() => _assignments.Count;

    public override void OnBuilt(World.Cave cave)
    {
        RebuildMineableQueues(cave);
    }

    public void InvalidateMineableQueues()
    {
        MineableQueuesDirty = true;
        _dirtyMineableTileKeys.Clear();
    }

    public void InvalidateMineableQueuesForKeys(IEnumerable<string> tileKeys)
    {
        foreach (var tileKey in tileKeys)
        {
            if (string.IsNullOrWhiteSpace(tileKey))
            {
                continue;
            }

            var tile = Cave?.GetTile(tileKey);
            var location = tile?.Coordinates ?? GridPoint.Parse(tileKey);
            if (IsLocationInArea(location))
            {
                MineableQueuesDirty = true;
                _dirtyMineableTileKeys.Add(tileKey);
            }
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
        var center = GetCenter();
        var radiusSq = Radius * Radius;
        var grouped = new Dictionary<string, List<(string Key, int Dist)>>(StringComparer.Ordinal);

        foreach (var tile in cave.GetTiles())
        {
            var distance = GridPoint.SquaredDistance(tile.Coordinates, center);
            if (distance > radiusSq || !Building.IsMineableType(tile.Base))
            {
                continue;
            }

            if (!grouped.TryGetValue(tile.Base, out var queue))
            {
                queue = [];
                grouped[tile.Base] = queue;
            }

            queue.Add((tile.Key, distance));
        }

        _mineableQueues.Clear();
        _mineableQueueHeads.Clear();
        _mineableTypes.Clear();

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
    }

    private void ApplyDirtyMineableQueueUpdates(World.Cave cave)
    {
        if (_dirtyMineableTileKeys.Count == 0)
        {
            RebuildMineableQueues(cave);
            return;
        }

        if (_dirtyMineableTileKeys.Count > 24)
        {
            RebuildMineableQueues(cave);
            return;
        }

        foreach (var tileKey in _dirtyMineableTileKeys)
        {
            RemoveTileFromQueues(tileKey);

            var tile = cave.GetTile(tileKey);
            if (tile is null || !Building.IsMineableType(tile.Base) || !IsLocationInArea(tile.Coordinates))
            {
                continue;
            }

            InsertTileIntoQueue(tile.Base, tile.Key, GridPoint.SquaredDistance(tile.Coordinates, GetCenter()));
        }

        MineableQueuesDirty = false;
        _dirtyMineableTileKeys.Clear();
    }

    private void RemoveTileFromQueues(string tileKey)
    {
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
        EnsureMineableQueues(cave);
        return _mineableTypes.Any(type => GetTypeQueueLength(type) > 0);
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

        _mineableQueues[type] = queue.Skip(head).ToList();
        _mineableQueueHeads[type] = 0;
    }

    private World.Tile? PullQueuedMineableTile(World.Cave cave, string type, HashSet<string> reservedTiles)
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

            if (reservedTiles.Contains(tileKey))
            {
                PushTypeQueueKey(type, tileKey);
                continue;
            }

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
        return TileArray.Any(tile => tile.Key == location.ToString());
    }

    public GridPoint? GetNavigationTarget(World.Cave cave, World.Tile tile)
    {
        var center = GetCenter();

        if (!string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            return tile.CreatureFits() ? tile.Coordinates : null;
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

    public World.Tile? GrabMineableTile(World.Cave cave, Creature? creature = null)
    {
        EnsureMineableQueues(cave);
        var mineableTypes = _mineableTypes.Where(type => GetTypeQueueLength(type) > 0).ToArray();
        if (mineableTypes.Length == 0)
        {
            return null;
        }

        var shuffledTypes = RandomUtil.Shuffle(mineableTypes);
        var reservedTiles = GetAssignedTileKeys(creature);
        foreach (var type in shuffledTypes)
        {
            var queuedTile = PullQueuedMineableTile(cave, type, reservedTiles);
            if (queuedTile is null)
            {
                continue;
            }

            if (creature is not null)
            {
                Assign(creature, queuedTile.Key);
            }

            return queuedTile;
        }

        return null;
    }
}
