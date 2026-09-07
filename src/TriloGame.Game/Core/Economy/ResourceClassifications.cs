namespace TriloGame.Game.Core.Economy;

// Central names keep classification-based recipes typed and consistent across processors.
public static class ResourceClassificationKeys
{
    public const string ResourceType = "resource type";
    public const string PlantType = "plant type";
    public const string FoodType = "food type";
}

public static class ResourceClassificationValues
{
    public const string Organic = "organic";
    public const string Rock = "rock";
    public const string Gravel = "gravel";
    public const string Metal = "metal";
    public const string Chemical = "chemical";
    public const string Algae = "algae";
    public const string Gloop = "gloop";
    public const string Raw = "raw";
    public const string Meal = "meal";
    public const string Pie = "pie";

    public static string FromCategory(ResourceCategory category)
    {
        return category switch
        {
            ResourceCategory.Rock => Rock,
            ResourceCategory.Gravel => Gravel,
            ResourceCategory.Metal => Metal,
            ResourceCategory.Organic => Organic,
            ResourceCategory.Chemical => Chemical,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown resource category.")
        };
    }
}

public static class ResourceClassifications
{
    public static IReadOnlyDictionary<string, string> Create(
        ResourceCategory category,
        string? plantType = null,
        string? foodType = null)
    {
        var classifications = new Dictionary<string, string>(3, StringComparer.Ordinal)
        {
            [ResourceClassificationKeys.ResourceType] = ResourceClassificationValues.FromCategory(category)
        };

        if (!string.IsNullOrWhiteSpace(plantType))
        {
            classifications.Add(ResourceClassificationKeys.PlantType, plantType);
        }

        if (!string.IsNullOrWhiteSpace(foodType))
        {
            classifications.Add(ResourceClassificationKeys.FoodType, foodType);
        }

        return classifications;
    }
}
