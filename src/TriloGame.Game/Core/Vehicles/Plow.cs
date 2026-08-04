using System.Numerics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Vehicles;

public sealed class Plow : Vehicle, IDriveable, IStorage
{
    private const int DefaultCapacity = 400;
    private const int MovementSpeedMultiplierNumerator = 3;
    private const int MovementSpeedMultiplierDenominator = 2;
    private const double QuarterTurnDurationMilliseconds = 500d;
    public const int QuarterTurnDurationTicks = (int)(QuarterTurnDurationMilliseconds / GameConstants.GameTimePerSimulationTickMs);
    private const float FarmerStationDefaultOffsetPixels = 256f;
    public static readonly GridPoint DefaultSize = new(2, 2);
    private readonly Dictionary<ResourceName, int> _inventory = [];
    private int _inventoryTotal;

    public Plow(GameSession session)
        : base(
            "Plow",
            "A ranch plow that carries one farmer, works every covered soil tile, and brings the yield back to the garage.",
            "Plow",
            "farmer",
            DefaultSize,
            40,
            1,
            [new VehicleStationSlot(new Vector2(FarmerStationDefaultOffsetPixels, 0f), MathF.PI * 0.5f)],
            session)
    {
        Capacity = DefaultCapacity;
    }

    public int Capacity { get; }

    // Plows follow fixed ranch coverage routes and do not abandon a cycle for passing creatures.
    public override bool CanTraverseCreatureCells => true;

    public override int MaximumStraightTileStepDistance => DefaultSize.Y;

    public override int MovementSpeed => (base.MovementSpeed * MovementSpeedMultiplierNumerator) / MovementSpeedMultiplierDenominator;

    public override int RotationTicksPerQuarterTurn => QuarterTurnDurationTicks;

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

    // Work the initial garage-side footprint so even a single soil patch completes a farming pass.
    internal void ProcessCurrentFootprint()
    {
        if (Cave is not { } cave || Location is not { } location)
        {
            return;
        }

        foreach (var point in EnumerateOccupiedPoints(location))
        {
            TryProcessSoilTile(cave, point);
        }

        cave.TryTransferPlowAlgaeToAdjacentSilo(this);
    }

    protected override void OnStationCreature(Creature creature)
    {
    }

    protected override void OnDestationCreature(Creature creature)
    {
    }

    // Work every covered tile after each successful move or turn so corners cannot be skipped.
    protected override void OnMoveSucceeded(GridPoint previousLocation, GridPoint currentLocation)
    {
        ProcessCurrentFootprint();
    }

    protected override void OnVehicleDestroyed(object? source)
    {
        if (source is string reason && string.Equals(reason, "ranchCycleComplete", StringComparison.Ordinal))
        {
            return;
        }

        ClearInventory();
    }

    private bool TryProcessSoilTile(Cave cave, GridPoint location)
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


}
