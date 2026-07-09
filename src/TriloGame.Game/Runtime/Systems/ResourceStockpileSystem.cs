using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class ResourceStockpileSystem
{
    private readonly Dictionary<ResourceName, int> _totals = [];
    private readonly List<ResourceStockpileEntry> _entries = [];
    private readonly List<ResourceName> _extraResourceNames = [];

    public ResourceStockpileSnapshot Current { get; private set; } = ResourceStockpileSnapshot.Empty;

    public static int GetStoredAmount(GameSession session, ResourceName resourceType)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Cave is null)
        {
            return 0;
        }

        var total = 0;
        foreach (var storage in EnumerateResourceStorages(session))
        {
            total += storage.GetStoredResources().GetValueOrDefault(resourceType, 0);
        }

        return total;
    }

    public static int GetStoredAmount(GameSession session, ResourceCategory resourceCategory)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Cave is null)
        {
            return 0;
        }

        var total = 0;
        foreach (var storage in EnumerateResourceStorages(session))
        {
            total += storage.GetStoredAmount(resourceCategory);
        }

        return total;
    }

    public static bool TryWithdrawStoredResource(GameSession session, ResourceName resourceType, int amount)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (amount <= 0)
        {
            return false;
        }

        var available = GetStoredAmount(session, resourceType);
        if (available < amount)
        {
            return false;
        }

        var remaining = amount;
        foreach (var storage in EnumerateResourceStorages(session))
        {
            var withdrawn = storage.Withdraw(resourceType, remaining);
            remaining -= withdrawn;
            if (remaining <= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryWithdrawStoredResource(GameSession session, ResourceCategory resourceCategory, int amount)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (amount <= 0)
        {
            return false;
        }

        var available = GetStoredAmount(session, resourceCategory);
        if (available < amount)
        {
            return false;
        }

        var remaining = amount;
        foreach (var storage in EnumerateResourceStorages(session))
        {
            var match = storage.FindStoredResource(ResourceRequirement.ForCategory(resourceCategory, remaining), remaining);
            if (match is null)
            {
                continue;
            }

            var withdrawn = storage.Withdraw(match.Value.ResourceType, remaining);
            remaining -= withdrawn;
            if (remaining <= 0)
            {
                return true;
            }
        }

        return false;
    }

    public ResourceStockpileSnapshot Refresh(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _totals.Clear();
        _entries.Clear();
        _extraResourceNames.Clear();

        var cave = session.Cave;
        if (cave is not null)
        {
            foreach (var building in cave.GetBuildingList())
            {
                if (building is IResourceStorage storage)
                {
                    AddStorage(storage);
                }
            }
        }

        AddKnownEntries();
        AddExtraEntries();

        Current = _entries.Count == 0
            ? ResourceStockpileSnapshot.Empty
            : new ResourceStockpileSnapshot(_entries.ToArray());
        return Current;
    }

    private void AddStorage(IResourceStorage storage)
    {
        foreach (var pair in storage.GetStoredResources())
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            _totals[pair.Key] = _totals.GetValueOrDefault(pair.Key, 0) + pair.Value;
        }
    }

    private void AddKnownEntries()
    {
        foreach (var itemType in ItemCatalog.GetStockpileOrder())
        {
            var resourceType = itemType.Name;
            var amount = _totals.GetValueOrDefault(itemType.Resource, 0);
            if (amount <= 0)
            {
                continue;
            }

            _entries.Add(new ResourceStockpileEntry(itemType.Resource, amount));
        }
    }

    private void AddExtraEntries()
    {
        foreach (var pair in _totals)
        {
            if (pair.Value <= 0 || IsKnownResource(pair.Key))
            {
                continue;
            }

            _extraResourceNames.Add(pair.Key);
        }

        _extraResourceNames.Sort();
        foreach (var resourceType in _extraResourceNames)
        {
            _entries.Add(new ResourceStockpileEntry(resourceType, _totals[resourceType]));
        }
    }

    private static bool IsKnownResource(ResourceName resourceType)
    {
        foreach (var itemType in ItemCatalog.GetStockpileOrder())
        {
            if (itemType.Resource == resourceType)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IResourceStorage> EnumerateResourceStorages(GameSession session)
    {
        if (session.Cave is null)
        {
            yield break;
        }

        foreach (var building in session.Cave.GetBuildingList())
        {
            if (building is IResourceStorage storage)
            {
                yield return storage;
            }
        }
    }
}

public readonly record struct ResourceStockpileSnapshot(IReadOnlyList<ResourceStockpileEntry> Entries)
{
    public static ResourceStockpileSnapshot Empty { get; } = new([]);

    public int GetAmount(ResourceName resourceType)
    {
        foreach (var entry in Entries)
        {
            if (entry.ResourceType == resourceType)
            {
                return entry.Amount;
            }
        }

        return 0;
    }
}

public readonly record struct ResourceStockpileEntry(ResourceName ResourceType, int Amount);
