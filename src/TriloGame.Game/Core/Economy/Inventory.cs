namespace TriloGame.Game.Core.Economy;

public sealed class Inventory
{
    public string? Type { get; private set; }

    public int Amount { get; private set; }

    public bool HasItems => Amount > 0 && !string.IsNullOrWhiteSpace(Type);

    public int Add(string resourceType, int amount, int capacity)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0 || capacity <= 0)
        {
            return 0;
        }

        if (!HasItems)
        {
            Type = resourceType;
        }

        if (!string.Equals(Type, resourceType, StringComparison.Ordinal))
        {
            return 0;
        }

        var accepted = System.Math.Min(System.Math.Max(0, capacity - Amount), amount);
        Amount += accepted;
        return accepted;
    }

    public int Remove(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var removed = System.Math.Min(Amount, amount);
        Amount -= removed;
        if (Amount == 0)
        {
            Type = null;
        }

        return removed;
    }

    public void Clear()
    {
        Type = null;
        Amount = 0;
    }
}
