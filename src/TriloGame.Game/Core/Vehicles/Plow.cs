using System.Numerics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Vehicles;

public sealed class Plow : Vehicle, IDriveable, IStorage
{
    private const int DefaultCapacity = 400;
    public static readonly GridPoint DefaultSize = new(2, 2);
    private readonly Dictionary<ResourceName, int> _inventory = [];
    private int _inventoryTotal;

    public Plow(GameSession session)
        : base(
            "Plow",
            "A ranch plow that carries one farmer, harvests trailing soil tiles, and brings the yield back to the garage.",
            "Plow",
            "farmer",
            DefaultSize,
            40,
            1,
            [new VehicleStationSlot(new Vector2(40f, 0f), MathF.PI * 0.5f)],
            session)
    {
        Capacity = DefaultCapacity;
    }

    public int Capacity { get; }

    public IReadOnlyDictionary<ResourceName, int> GetInventory() => _inventory;

    public int GetInventoryTotal() => _inventoryTotal;

    public int GetInventorySpace() => System.Math.Max(0, Capacity - _inventoryTotal);

    public int Deposit(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        var accepted = System.Math.Min(GetInventorySpace(), amount);
        if (accepted <= 0)
        {
            return 0;
        }

        _inventory[resourceType] += accepted;
        _inventoryTotal += accepted;
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
        if (taken <= 0)
        {
            return 0;
        }

        _inventory[resourceType] -= taken;
        _inventoryTotal -= taken;
        return taken;
    }

    internal int TransferInventoryTo(IStorage storage)
    {
        if (storage is null || ReferenceEquals(storage, this))
        {
            return 0;
        }

        var transferred = 0;
        foreach (var resourceType in _inventory.Keys.ToArray())
        {
            var available = _inventory[resourceType];
            if (available <= 0)
            {
                continue;
            }

            var accepted = storage.Deposit(resourceType, available);
            if (accepted <= 0)
            {
                continue;
            }

            _inventory[resourceType] -= accepted;
            _inventoryTotal -= accepted;
            transferred += accepted;
        }

        return transferred;
    }

    protected override void OnStationCreature(Creature creature)
    {
    }

    protected override void OnDestationCreature(Creature creature)
    {
    }

    // Process the trailing edge after every successful move, including in-place turns, so ripe crops are harvested and dormant soil gets planted once the plow has fully passed over a tile.
    protected override void OnMoveSucceeded(GridPoint previousLocation, GridPoint currentLocation)
    {
        if (Cave is not { } cave || Location is not { } location)
        {
            return;
        }

        foreach (var harvestLocation in EnumerateBackEdgeLocations(location, GetDisplayRotationTurns(), GetRotatedSize()))
        {
            TryProcessTrailingTile(cave, harvestLocation);
        }

        cave.TryTransferPlowAlgaeToAdjacentSilo(this);
    }

    protected override void OnVehicleDestroyed(object? source)
    {
        if (source is string reason && string.Equals(reason, "ranchCycleComplete", StringComparison.Ordinal))
        {
            return;
        }

        ClearInventory();
    }

    private bool TryProcessTrailingTile(Cave cave, GridPoint location)
    {
        var soilTile = cave.GetSoilTile(location);
        if (soilTile is null || soilTile.ParentPatch.Cave != cave)
        {
            return false;
        }

        if (soilTile.TryGetHarvest(out var harvestedResource, out var harvestAmount))
        {
            if (harvestedResource is null || harvestAmount <= 0 || GetInventorySpace() < harvestAmount)
            {
                return false;
            }

            var harvested = soilTile.Harvest();
            if (harvested <= 0 || !TryStoreExact(harvestedResource.Resource, harvested))
            {
                return false;
            }
        }

        return soilTile.Ranch?.Garage is { } garage && soilTile.Plant(garage.ChosenResource);
    }

    private bool TryStoreExact(ResourceName resourceType, int amount)
    {
        return amount > 0 &&
               Deposit(resourceType, amount) == amount;
    }

    private void ClearInventory()
    {
        if (_inventoryTotal <= 0)
        {
            return;
        }

        var resourceTypes = _inventory.Keys.ToArray();
        for (var index = 0; index < resourceTypes.Length; index++)
        {
            _inventory[resourceTypes[index]] = 0;
        }

        _inventoryTotal = 0;
    }

    private static IEnumerable<GridPoint> EnumerateBackEdgeLocations(GridPoint location, int rotationTurns, GridPoint rotatedSize)
    {
        switch (((rotationTurns % 4) + 4) % 4)
        {
            case 0:
                for (var y = 0; y < rotatedSize.Y; y++)
                {
                    yield return new GridPoint(location.X, location.Y + y);
                }

                yield break;
            case 1:
                for (var x = 0; x < rotatedSize.X; x++)
                {
                    yield return new GridPoint(location.X + x, location.Y);
                }

                yield break;
            case 2:
                for (var y = 0; y < rotatedSize.Y; y++)
                {
                    yield return new GridPoint(location.X + rotatedSize.X - 1, location.Y + y);
                }

                yield break;
            default:
                for (var x = 0; x < rotatedSize.X; x++)
                {
                    yield return new GridPoint(location.X + x, location.Y + rotatedSize.Y - 1);
                }

                yield break;
        }
    }
}
