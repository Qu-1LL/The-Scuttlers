namespace TriloGame.Game.Core.Economy;

public static class ResourceInventoryHelper
{
    private static readonly ResourceName[] AllResources = Enum.GetValues<ResourceName>();

    public static int GetStoredAmount(ResourceCategory resourceCategory, Func<ResourceName, int> amountResolver)
    {
        ArgumentNullException.ThrowIfNull(amountResolver);

        var total = 0;
        for (var index = 0; index < AllResources.Length; index++)
        {
            var resourceType = AllResources[index];
            if (ItemCatalog.GetCategory(resourceType) != resourceCategory)
            {
                continue;
            }

            total += Math.Max(0, amountResolver(resourceType));
        }

        return total;
    }

    public static ResourceStorageMatch? FindStoredResource(
        ResourceRequirement requirement,
        int maxAmount,
        Func<ResourceName, int> amountResolver)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(amountResolver);

        if (maxAmount <= 0)
        {
            return null;
        }

        if (requirement.SpecificResource is { } specificResource)
        {
            var availableAmount = Math.Min(maxAmount, Math.Max(0, amountResolver(specificResource)));
            return availableAmount > 0
                ? new ResourceStorageMatch(specificResource, availableAmount)
                : null;
        }

        ResourceName? bestResource = null;
        var bestAmount = 0;
        for (var index = 0; index < AllResources.Length; index++)
        {
            var resourceType = AllResources[index];
            if (!requirement.Matches(resourceType))
            {
                continue;
            }

            var availableAmount = Math.Min(maxAmount, Math.Max(0, amountResolver(resourceType)));
            if (availableAmount <= bestAmount)
            {
                continue;
            }

            bestResource = resourceType;
            bestAmount = availableAmount;
        }

        return bestResource.HasValue && bestAmount > 0
            ? new ResourceStorageMatch(bestResource.Value, bestAmount)
            : null;
    }
}
