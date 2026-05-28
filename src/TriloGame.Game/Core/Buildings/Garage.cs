using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Garage : Building, IStorage
{
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private readonly HashSet<Silo> _adjacentSilos = [];
    private int _inventoryTotal;

    public Garage(GameSession session)
        : base("Garage", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Garage";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [OreType.SANDSTONE.Name] = 20
        };
        Capacity = 1000;
        ChosenResource = GrowableResourceType.ALGAE;
        Description = $"A ranch garage that stores up to {Capacity} harvested resources and anchors one ranch.";
    }

    public int Capacity { get; }

    public GrowableResourceType ChosenResource { get; }

    public Ranch? Ranch { get; internal set; }

    internal IReadOnlyCollection<Silo> AdjacentSilos => _adjacentSilos;

    public IReadOnlyDictionary<string, int> GetInventory() => _inventory;

    public int GetInventoryTotal() => _inventoryTotal;

    public int GetInventorySpace() => System.Math.Max(0, Capacity - _inventoryTotal);

    public int Deposit(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
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
        EmitStorageInventoryChanged(resourceType, accepted);
        if (accepted > 0 && string.Equals(resourceType, OreType.ALGAE.Name, StringComparison.Ordinal))
        {
            TryOffloadAlgaeToAdjacentSilos();
        }

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

            var resourceTypes = _inventory.Keys.ToArray();
            for (var index = 0; index < resourceTypes.Length; index++)
            {
                _inventory[resourceTypes[index]] = 0;
            }

            _inventoryTotal = 0;
        }

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

    // Garages spill stored algae into their adjacent silos without touching non-algae resources.
    internal int TryOffloadAlgaeToAdjacentSilos()
    {
        var transferred = 0;
        while (TrySelectSiloForAlgaeOffload(out var silo, out var requestedTransfer))
        {
            var withdrawn = Withdraw(OreType.ALGAE.Name, requestedTransfer);
            if (withdrawn <= 0)
            {
                break;
            }

            var accepted = silo!.Deposit(OreType.ALGAE.Name, withdrawn);
            transferred += accepted;
            if (accepted >= withdrawn)
            {
                continue;
            }

            var remainder = withdrawn - accepted;
            _inventory[OreType.ALGAE.Name] += remainder;
            _inventoryTotal += remainder;
            EmitStorageInventoryChanged(OreType.ALGAE.Name, remainder);
            break;
        }

        return transferred;
    }

    private bool TrySelectSiloForAlgaeOffload(out Silo? silo, out int transferAmount)
    {
        silo = null;
        transferAmount = 0;
        var availableAlgae = _inventory.GetValueOrDefault(OreType.ALGAE.Name, 0);
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
        transferAmount = System.Math.Min(availableAlgae, lowest.GetInventorySpace());
        if (secondLowest is null)
        {
            return transferAmount > 0;
        }

        var gapToNextLowest = secondLowest.GetInventoryTotal() - lowest.GetInventoryTotal();
        transferAmount = System.Math.Min(transferAmount, System.Math.Max(1, gapToNextLowest));
        return transferAmount > 0;
    }

    private void EmitStorageInventoryChanged(string resourceType, int resourceDelta)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || resourceDelta == 0)
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
