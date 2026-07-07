namespace TriloGame.Game.Core.Economy;

public static class ItemCatalog
{
    public static readonly ItemType Algae = new(OreType.ALGAE.Name, "SoilTile_Algae_3");
    public static readonly ItemType Sandstone = new(OreType.SANDSTONE.Name, "wall");
    public static readonly ItemType Magnetite = new(OreType.MAGNETITE.Name, OreType.MAGNETITE.Name);
    public static readonly ItemType Malachite = new(OreType.MALACHITE.Name, OreType.MALACHITE.Name);
    public static readonly ItemType Perotene = new(OreType.PEROTENE.Name, OreType.PEROTENE.Name);
    public static readonly ItemType Ilmenite = new(OreType.ILMENITE.Name, OreType.ILMENITE.Name);
    public static readonly ItemType Cochinium = new(OreType.COCHINIUM.Name, OreType.COCHINIUM.Name);
    public static readonly ItemType Lumenite = new(OreType.LUMENITE.Name, OreType.LUMENITE.Name);
    public static readonly ItemType Chitinstone = new(OreType.CHITINSTONE.Name, OreType.CHITINSTONE.Name);
    public static readonly ItemType Mycocore = new(OreType.MYCOCORE.Name, OreType.MYCOCORE.Name);

    private static readonly ItemType[] StockpileOrder =
    [
        Algae,
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

    private static readonly Dictionary<string, ItemType> ByName = new(StringComparer.Ordinal)
    {
        [Algae.Name] = Algae,
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

    public static bool TryGet(string resourceType, out ItemType itemType)
    {
        return ByName.TryGetValue(resourceType, out itemType!);
    }

    public static string GetTextureKey(string resourceType)
    {
        return TryGet(resourceType, out var itemType)
            ? itemType.TextureKey
            : resourceType;
    }
}
