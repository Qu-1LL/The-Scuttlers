using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class ResourceStockpileSystem
{
    private readonly Dictionary<string, int> _totals = new(StringComparer.Ordinal);
    private readonly List<ResourceStockpileEntry> _entries = [];
    private readonly List<string> _extraResourceNames = [];

    public ResourceStockpileSnapshot Current { get; private set; } = ResourceStockpileSnapshot.Empty;

    public static int GetStoredAmount(GameSession session, string resourceType)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(resourceType) || session.Cave is null)
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

    public static bool TryWithdrawStoredResource(GameSession session, string resourceType, int amount)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
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
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
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
            var amount = _totals.GetValueOrDefault(resourceType, 0);
            if (amount <= 0)
            {
                continue;
            }

            _entries.Add(new ResourceStockpileEntry(resourceType, amount));
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

        _extraResourceNames.Sort(StringComparer.Ordinal);
        foreach (var resourceType in _extraResourceNames)
        {
            _entries.Add(new ResourceStockpileEntry(resourceType, _totals[resourceType]));
        }
    }

    private static bool IsKnownResource(string resourceType)
    {
        foreach (var itemType in ItemCatalog.GetStockpileOrder())
        {
            var knownResource = itemType.Name;
            if (string.Equals(knownResource, resourceType, StringComparison.Ordinal))
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

    public int GetAmount(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return 0;
        }

        foreach (var entry in Entries)
        {
            if (string.Equals(entry.ResourceType, resourceType, StringComparison.Ordinal))
            {
                return entry.Amount;
            }
        }

        return 0;
    }
}

public readonly record struct ResourceStockpileEntry(string ResourceType, int Amount);
