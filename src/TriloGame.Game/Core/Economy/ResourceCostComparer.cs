namespace TriloGame.Game.Core.Economy;

public readonly record struct ResourceShortfall(string ResourceType, int MissingAmount);

public static class ResourceCostComparer
{
    public static bool HasRequiredResources(
        IReadOnlyDictionary<string, int> availableResources,
        IReadOnlyDictionary<string, int> requiredCosts)
    {
        return !TryFindFirstShortfall(availableResources, requiredCosts, out _);
    }

    public static bool TryFindFirstShortfall(
        IReadOnlyDictionary<string, int> availableResources,
        IReadOnlyDictionary<string, int> requiredCosts,
        out ResourceShortfall shortfall)
    {
        ArgumentNullException.ThrowIfNull(availableResources);
        ArgumentNullException.ThrowIfNull(requiredCosts);

        foreach (var pair in requiredCosts)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
            {
                continue;
            }

            var availableAmount = availableResources.GetValueOrDefault(pair.Key, 0);
            if (availableAmount >= pair.Value)
            {
                continue;
            }

            shortfall = new ResourceShortfall(pair.Key, pair.Value - availableAmount);
            return true;
        }

        shortfall = default;
        return false;
    }
}
