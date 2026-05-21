using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Garage : Building, IStorage
{
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal)
    {
        [OreType.ALGAE.Name] = 0
    };
    private int _inventoryTotal;

    public Garage(GameSession session)
        : base("Garage", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Garage";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal);
        Capacity = 200;
        Description = $"A ranch garage that stores up to {Capacity} algae and anchors one ranch.";
    }

    public int Capacity { get; }

    public Ranch? Ranch { get; internal set; }

    public IReadOnlyDictionary<string, int> GetInventory() => _inventory;

    public int GetInventoryTotal() => _inventoryTotal;

    public int GetInventorySpace() => System.Math.Max(0, Capacity - _inventoryTotal);

    public int Deposit(string resourceType, int amount)
    {
        if (!IsSupportedResource(resourceType) || amount <= 0)
        {
            return 0;
        }

        var accepted = System.Math.Min(GetInventorySpace(), amount);
        if (accepted <= 0)
        {
            return 0;
        }

        _inventory[OreType.ALGAE.Name] += accepted;
        _inventoryTotal += accepted;
        EmitStorageInventoryChanged(OreType.ALGAE.Name, accepted);
        return accepted;
    }

    public int Withdraw(string resourceType, int amount)
    {
        if (!IsSupportedResource(resourceType) || amount <= 0)
        {
            return 0;
        }

        var taken = System.Math.Min(_inventory[OreType.ALGAE.Name], amount);
        if (taken <= 0)
        {
            return 0;
        }

        _inventory[OreType.ALGAE.Name] -= taken;
        _inventoryTotal -= taken;
        EmitStorageInventoryChanged(OreType.ALGAE.Name, -taken);
        return taken;
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        if (_inventoryTotal > 0)
        {
            EmitStorageInventoryChanged(OreType.ALGAE.Name, -_inventoryTotal);
            _inventory[OreType.ALGAE.Name] = 0;
            _inventoryTotal = 0;
        }

        base.CleanupBeforeRemoval(source);
    }

    private static bool IsSupportedResource(string resourceType)
    {
        return !string.IsNullOrWhiteSpace(resourceType) &&
               string.Equals(resourceType, OreType.ALGAE.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void EmitStorageInventoryChanged(string resourceType, int resourceDelta)
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
