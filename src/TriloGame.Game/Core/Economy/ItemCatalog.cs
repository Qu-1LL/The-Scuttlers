namespace TriloGame.Game.Core.Economy;

public static class ItemCatalog
{
    public static readonly ItemType Algae = new(ResourceName.Algae, "Algae", "SoilTile_Algae_3", ResourceCategory.Organic, NutritionValue: 1);
    public static readonly ItemType AlgaeMeal = new(ResourceName.AlgaeMeal, "Algae Meal", "Algae_Meal", ResourceCategory.Organic, NutritionValue: 2);
    public static readonly ItemType AlgaePie = new(ResourceName.AlgaePie, "Algae Pie", "Algae_Pie", ResourceCategory.Organic, NutritionValue: 4);
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
