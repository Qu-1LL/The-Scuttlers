namespace TriloGame.Game.Core.Economy;

public sealed record ResourceRequirement
{
    private ResourceRequirement(int amount, ResourceName? specificResource, ResourceCategory? category)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Resource requirements must request at least one item.");
        }

        if (specificResource.HasValue == category.HasValue)
        {
            throw new ArgumentException("A resource requirement must target either a specific resource or a category.");
        }

        Amount = amount;
        SpecificResource = specificResource;
        Category = category;
    }

    public int Amount { get; }

    public ResourceName? SpecificResource { get; }

    public ResourceCategory? Category { get; }

    public bool IsSpecificResource => SpecificResource.HasValue;

    public bool IsCategory => Category.HasValue;

    public static ResourceRequirement ForResource(ResourceName resourceType, int amount)
    {
        return new ResourceRequirement(amount, resourceType, null);
    }

    public static ResourceRequirement ForCategory(ResourceCategory category, int amount)
    {
        return new ResourceRequirement(amount, null, category);
    }

    public ResourceRequirement WithAmount(int amount)
    {
        return SpecificResource is { } resourceType
            ? ForResource(resourceType, amount)
            : ForCategory(Category!.Value, amount);
    }

    public bool Requires(ResourceName resourceType)
    {
        return SpecificResource == resourceType;
    }

    public bool Matches(ResourceName resourceType)
    {
        return SpecificResource is { } specificResource
            ? specificResource == resourceType
            : ItemCatalog.GetCategory(resourceType) == Category!.Value;
    }
}
