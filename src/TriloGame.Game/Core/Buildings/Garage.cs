using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Garage : Building, IResourceStorage, IStorage
{
    private static readonly IReadOnlyList<InteractionZoneDefinition> ZoneDefinitions =
    [
        new("Resource transfer", InteractionZonePurpose.ResourceTransfer, new GridPoint(0, -1), new GridPoint(2, 1),
            [new GridPoint(0, -1), new GridPoint(1, -1)])
    ];
    private readonly Dictionary<ResourceName, int> _inventory = [];
    private readonly HashSet<Silo> _adjacentSilos = [];
    private int _inventoryTotal;

    public Garage(GameSession session)
        : base("Garage", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Garage";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Capacity = 1000;
        ChosenResource = GrowableResourceType.ALGAE;
        Description = $"A high-capacity algae garage that stores up to {Capacity} harvested resources and can anchor one ranch.";
    }

    public int Capacity { get; }

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => ZoneDefinitions;

    public GrowableResourceType ChosenResource { get; }

    public Ranch? Ranch { get; internal set; }

    internal IReadOnlyCollection<Silo> AdjacentSilos => _adjacentSilos;

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

    public int GetInventorySpace() => Math.Max(0, Capacity - _inventoryTotal);

    public int Deposit(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        var accepted = Math.Min(GetInventorySpace(), amount);
        if (accepted <= 0)
        {
            return 0;
        }

        _inventory[resourceType] += accepted;
        _inventoryTotal += accepted;
        EmitStorageInventoryChanged(resourceType, accepted);
        if (resourceType == ResourceName.Algae)
        {
            TryOffloadAlgaeToAdjacentSilos();
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
        var taken = Math.Min(_inventory[resourceType], amount);
        if (taken <= 0)
        {
            return 0;
        }

        _inventory[resourceType] -= taken;
        _inventoryTotal -= taken;
        EmitStorageInventoryChanged(resourceType, -taken);
        return taken;
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        _adjacentSilos.Clear();
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

    internal void AddAdjacentSilo(Silo silo)
    {
        _adjacentSilos.Add(silo);
    }

    internal void RemoveAdjacentSilo(Silo silo)
    {
        _adjacentSilos.Remove(silo);
    }

    internal int TryOffloadAlgaeToAdjacentSilos()
    {
        var transferred = 0;
        while (TrySelectSiloForAlgaeOffload(out var silo, out var requestedTransfer))
        {
            var withdrawn = Withdraw(ResourceName.Algae, requestedTransfer);
            if (withdrawn <= 0)
            {
                break;
            }

            var accepted = silo!.Deposit(ResourceName.Algae, withdrawn);
            transferred += accepted;
            if (accepted >= withdrawn)
            {
                continue;
            }

            var remainder = withdrawn - accepted;
            _inventory[ResourceName.Algae] += remainder;
            _inventoryTotal += remainder;
            EmitStorageInventoryChanged(ResourceName.Algae, remainder);
            break;
        }

        return transferred;
    }

    private bool TrySelectSiloForAlgaeOffload(out Silo? silo, out int transferAmount)
    {
        silo = null;
        transferAmount = 0;
        var availableAlgae = _inventory.GetValueOrDefault(ResourceName.Algae, 0);
        if (availableAlgae <= 0)
        {
            return false;
        }

        Silo? lowest = null;
        Silo? secondLowest = null;
        foreach (var adjacentSilo in _adjacentSilos)
        {
            if (adjacentSilo.Cave != Cave || adjacentSilo.GetInventorySpace() <= 0)
            {
                continue;
            }

            if (lowest is null ||
                adjacentSilo.GetInventoryTotal() < lowest.GetInventoryTotal() ||
                (adjacentSilo.GetInventoryTotal() == lowest.GetInventoryTotal() && Silo.CompareStableOrder(adjacentSilo, lowest) < 0))
            {
                secondLowest = lowest;
                lowest = adjacentSilo;
                continue;
            }

            if (secondLowest is null ||
                adjacentSilo.GetInventoryTotal() < secondLowest.GetInventoryTotal() ||
                (adjacentSilo.GetInventoryTotal() == secondLowest.GetInventoryTotal() && Silo.CompareStableOrder(adjacentSilo, secondLowest) < 0))
            {
                secondLowest = adjacentSilo;
            }
        }

        if (lowest is null)
        {
            return false;
        }

        silo = lowest;
        transferAmount = Math.Min(availableAlgae, lowest.GetInventorySpace());
        if (secondLowest is null)
        {
            return transferAmount > 0;
        }

        var gapToNextLowest = secondLowest.GetInventoryTotal() - lowest.GetInventoryTotal();
        transferAmount = Math.Min(transferAmount, Math.Max(1, gapToNextLowest));
        return transferAmount > 0;
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
