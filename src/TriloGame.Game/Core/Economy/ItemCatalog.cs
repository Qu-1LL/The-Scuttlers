namespace TriloGame.Game.Core.Economy;

public static class ItemCatalog
{
    public static readonly ItemType Algae = new(
        ResourceName.Algae,
        "Algae",
        "Algae",
        ResourceCategory.Organic,
        nutritionValue: 1,
        ResourceClassifications.Create(ResourceCategory.Organic, ResourceClassificationValues.Algae, ResourceClassificationValues.Raw));
    public static readonly ItemType AlgaeMeal = new(
        ResourceName.AlgaeMeal,
        "Algae Meal",
        "Algae_Meal",
        ResourceCategory.Organic,
        nutritionValue: 2,
        ResourceClassifications.Create(ResourceCategory.Organic, ResourceClassificationValues.Algae, ResourceClassificationValues.Meal));
    public static readonly ItemType AlgaePie = new(
        ResourceName.AlgaePie,
        "Algae Pie",
        "Algae_Pie",
        ResourceCategory.Organic,
        nutritionValue: 4,
        ResourceClassifications.Create(ResourceCategory.Organic, ResourceClassificationValues.Algae, ResourceClassificationValues.Pie));
    public static readonly ItemType Gloop = new(
        ResourceName.Gloop,
        "Gloop",
        "Gloop",
        ResourceCategory.Organic,
        nutritionValue: 1,
        ResourceClassifications.Create(ResourceCategory.Organic, ResourceClassificationValues.Gloop, ResourceClassificationValues.Raw));
    public static readonly ItemType GloopMeal = new(
        ResourceName.GloopMeal,
        "Gloop Meal",
        "Gloop_Meal",
        ResourceCategory.Organic,
        nutritionValue: 2,
        ResourceClassifications.Create(ResourceCategory.Organic, ResourceClassificationValues.Gloop, ResourceClassificationValues.Meal));
    public static readonly ItemType GloopPie = new(
        ResourceName.GloopPie,
        "Gloop Pie",
        "Gloop_Pie",
        ResourceCategory.Organic,
        nutritionValue: 4,
        ResourceClassifications.Create(ResourceCategory.Organic, ResourceClassificationValues.Gloop, ResourceClassificationValues.Pie));
    public static readonly ItemType Sandstone = new(ResourceName.Sandstone, "Sandstone", OreType.SANDSTONE.Name, ResourceCategory.Rock);
    public static readonly ItemType Magnetite = new(ResourceName.Magnetite, "Magnetite", OreType.MAGNETITE.Name, ResourceCategory.Gravel);
    public static readonly ItemType Malachite = new(ResourceName.Malachite, "Malachite", OreType.MALACHITE.Name, ResourceCategory.Rock);
    public static readonly ItemType Perotene = new(ResourceName.Perotene, "Perotene", OreType.PEROTENE.Name, ResourceCategory.Chemical);
    public static readonly ItemType Ilmenite = new(ResourceName.Ilmenite, "Ilmenite", OreType.ILMENITE.Name, ResourceCategory.Rock);
    public static readonly ItemType Cochinium = new(ResourceName.Cochinium, "Cochinium", OreType.COCHINIUM.Name, ResourceCategory.Chemical);
    public static readonly ItemType Lumenite = new(ResourceName.Lumenite, "Lumenite", OreType.LUMENITE.Name, ResourceCategory.Gravel);
    public static readonly ItemType Chitinstone = new(ResourceName.Chitinstone, "Chitinstone", OreType.CHITINSTONE.Name, ResourceCategory.Organic);
    public static readonly ItemType Mycocore = new(ResourceName.Mycocore, "Mycocore", OreType.MYCOCORE.Name, ResourceCategory.Chemical);

    private static readonly ItemType[] StockpileOrder =
    [
        Algae,
        AlgaeMeal,
        AlgaePie,
        Gloop,
        GloopMeal,
        GloopPie,
        Sandstone,
        Magnetite,
        Malachite,
        Perotene,
        Ilmenite,
        Cochinium,
        Lumenite,
        Chitinstone,
        Mycocore
    ];

    private static readonly Dictionary<ResourceName, ItemType> ByResource = new()
    {
        [Algae.Resource] = Algae,
        [AlgaeMeal.Resource] = AlgaeMeal,
        [AlgaePie.Resource] = AlgaePie,
        [Gloop.Resource] = Gloop,
        [GloopMeal.Resource] = GloopMeal,
        [GloopPie.Resource] = GloopPie,
        [Sandstone.Resource] = Sandstone,
        [Magnetite.Resource] = Magnetite,
        [Malachite.Resource] = Malachite,
        [Perotene.Resource] = Perotene,
        [Ilmenite.Resource] = Ilmenite,
        [Cochinium.Resource] = Cochinium,
        [Lumenite.Resource] = Lumenite,
        [Chitinstone.Resource] = Chitinstone,
        [Mycocore.Resource] = Mycocore
    };

    private static readonly Dictionary<string, ItemType> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        [Algae.Name] = Algae,
        [AlgaeMeal.Name] = AlgaeMeal,
        [AlgaePie.Name] = AlgaePie,
        [Gloop.Name] = Gloop,
        [GloopMeal.Name] = GloopMeal,
        [GloopPie.Name] = GloopPie,
        [Sandstone.Name] = Sandstone,
        [Magnetite.Name] = Magnetite,
        [Malachite.Name] = Malachite,
        [Perotene.Name] = Perotene,
        [Ilmenite.Name] = Ilmenite,
        [Cochinium.Name] = Cochinium,
        [Lumenite.Name] = Lumenite,
        [Chitinstone.Name] = Chitinstone,
        [Mycocore.Name] = Mycocore
    };

    public static IReadOnlyList<ItemType> GetStockpileOrder() => StockpileOrder;

    public static bool TryGet(ResourceName resource, out ItemType itemType)
    {
        return ByResource.TryGetValue(resource, out itemType!);
    }

    public static ItemType Get(ResourceName resource)
    {
        return TryGet(resource, out var itemType)
            ? itemType
            : throw new KeyNotFoundException($"No item metadata is registered for resource {resource}.");
    }

    public static bool TryGet(string resourceType, out ItemType itemType)
    {
        return ByName.TryGetValue(resourceType, out itemType!);
    }

    public static bool TryGetResource(string resourceType, out ResourceName resource)
    {
        if (TryGet(resourceType, out var itemType))
        {
            resource = itemType.Resource;
            return true;
        }

        resource = default;
        return false;
    }

    public static string GetName(ResourceName resource)
    {
        return Get(resource).Name;
    }

    public static ResourceCategory GetCategory(ResourceName resource)
    {
        return Get(resource).Category;
    }

    public static string? GetClassification(ResourceName resource, string? classificationKey)
    {
        return TryGet(resource, out var itemType)
            ? itemType.GetClassification(classificationKey)
            : null;
    }

    public static bool HasClassification(ResourceName resource, string? classificationKey, string? classificationValue)
    {
        return TryGet(resource, out var itemType) && itemType.HasClassification(classificationKey, classificationValue);
    }

    // Resolve another food form from the same plant and resource type without assuming its name.
    public static bool TryGetRelatedPlantResource(ResourceName sourceResource, string targetFoodType, out ResourceName relatedResource)
    {
        relatedResource = default;
        var plantType = GetClassification(sourceResource, ResourceClassificationKeys.PlantType);
        var resourceType = GetClassification(sourceResource, ResourceClassificationKeys.ResourceType);
        if (plantType is null || resourceType is null)
        {
            return false;
        }

        for (var index = 0; index < StockpileOrder.Length; index++)
        {
            var candidate = StockpileOrder[index];
            if (!candidate.HasClassification(ResourceClassificationKeys.PlantType, plantType) ||
                !candidate.HasClassification(ResourceClassificationKeys.ResourceType, resourceType) ||
                !candidate.HasClassification(ResourceClassificationKeys.FoodType, targetFoodType))
            {
                continue;
            }

            relatedResource = candidate.Resource;
            return true;
        }

        return false;
    }

    public static int GetNutritionValue(ResourceName resource)
    {
        return Get(resource).NutritionValue;
    }

    public static string GetTextureKey(ResourceName resource)
    {
        return Get(resource).TextureKey;
    }

    public static string GetTextureKey(string resourceType)
    {
        return TryGet(resourceType, out var itemType)
            ? itemType.TextureKey
            : resourceType;
    }
}
