using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Storage : Building, IResourceStorage
{
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private int _inventoryTotal;

    public Storage(GameSession session)
        : base("Storage", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Storage";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        Capacity = 20;
        Description = $"A container that can hold up to {Capacity} items.";
    }

    public int Capacity { get; }

    public IReadOnlyDictionary<string, int> GetStoredResources() => _inventory;

    public int GetInventoryTotal() => _inventoryTotal;

    public int GetInventorySpace() => Math.Max(0, Capacity - _inventoryTotal);

    public int Deposit(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
        {
            return 0;
        }

        var accepted = Math.Min(GetInventorySpace(), amount);
        if (accepted <= 0)
        {
            return 0;
        }

        _inventory.TryAdd(resourceType, 0);
        _inventory[resourceType] += accepted;
        _inventoryTotal += accepted;
        return accepted;
    }

    public int Withdraw(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
        {
            return 0;
        }

        var taken = Math.Min(_inventory.GetValueOrDefault(resourceType, 0), amount);
        if (taken <= 0)
        {
            return 0;
        }

        _inventory[resourceType] -= taken;
        _inventoryTotal -= taken;
        return taken;
    }
}
