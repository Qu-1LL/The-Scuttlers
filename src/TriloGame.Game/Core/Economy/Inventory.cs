namespace TriloGame.Game.Core.Economy;

public sealed class Inventory
{
    private readonly Dictionary<ResourceName, int> _amounts = [];
    private readonly List<ResourceName> _resourceOrder = [];

    public ResourceName? Type
    {
        get
        {
            for (var index = 0; index < _resourceOrder.Count; index++)
            {
                var resourceType = _resourceOrder[index];
                if (_amounts.GetValueOrDefault(resourceType) > 0)
                {
                    return resourceType;
                }
            }

            return null;
        }
    }

    public int Amount { get; private set; }

    public bool HasItems => Amount > 0;

    public int ResourceTypeCount => _resourceOrder.Count;

    public ResourceName GetResourceTypeAt(int index) => _resourceOrder[index];

    public int GetAmount(ResourceName resourceType) => _amounts.GetValueOrDefault(resourceType, 0);

    public int Add(ResourceName resourceType, int amount, int capacity)
    {
        if (amount <= 0 || capacity <= 0)
        {
            return 0;
        }

        var accepted = System.Math.Min(System.Math.Max(0, capacity - Amount), amount);
        if (accepted <= 0)
        {
            return 0;
        }

        if (!_amounts.ContainsKey(resourceType))
        {
            _amounts[resourceType] = 0;
            _resourceOrder.Add(resourceType);
        }

        _amounts[resourceType] += accepted;
        Amount += accepted;
        return accepted;
    }

    public int Add(ItemType itemType, int amount, int capacity)
    {
        ArgumentNullException.ThrowIfNull(itemType);
        return Add(itemType.Resource, amount, capacity);
    }

    public int Remove(int amount)
    {
        var resourceType = Type;
        return resourceType.HasValue ? Remove(resourceType.Value, amount) : 0;
    }

    public int Remove(ResourceName resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var current = _amounts.GetValueOrDefault(resourceType, 0);
        if (current <= 0)
        {
            return 0;
        }

        var removed = System.Math.Min(current, amount);
        var remaining = current - removed;
        if (remaining <= 0)
        {
            _amounts.Remove(resourceType);
            _resourceOrder.Remove(resourceType);
        }
        else
        {
            _amounts[resourceType] = remaining;
        }

        Amount -= removed;
        return removed;
    }

    public void Clear()
    {
        _amounts.Clear();
        _resourceOrder.Clear();
        Amount = 0;
    }
}
