using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Storage : Building, IResourceStorage
{
    private readonly Dictionary<ResourceName, int> _inventory = [];
    private int _inventoryTotal;

    public Storage(GameSession session)
        : base("Storage", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Storage";
        Recipe = new Dictionary<ResourceName, int> { [ResourceName.Sandstone] = 20 };
        Capacity = 20;
        Description = $"A container that can hold up to {Capacity} items.";
    }

    public int Capacity { get; }

    public IReadOnlyDictionary<ResourceName, int> GetStoredResources() => _inventory;

    public int GetInventoryTotal() => _inventoryTotal;

    public int GetInventorySpace() => Math.Max(0, Capacity - _inventoryTotal);

    public int Deposit(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
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
        EmitStorageInventoryChanged(resourceType, accepted);
        return accepted;
    }

    public int Withdraw(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
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
        EmitStorageInventoryChanged(resourceType, -taken);
        return taken;
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
