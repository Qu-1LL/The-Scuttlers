namespace TriloGame.Game.Core.Economy;

public static class ItemCatalog
{
    public static readonly ItemType Algae = new(OreType.ALGAE.Name, "SoilTile_Algae_3");
    public static readonly ItemType Sandstone = new(OreType.SANDSTONE.Name, "wall");
    public static readonly ItemType Lumenite = new(OreType.LUMENITE.Name, OreType.LUMENITE.Name);
    public static readonly ItemType Chitinstone = new(OreType.CHITINSTONE.Name, OreType.CHITINSTONE.Name);
    public static readonly ItemType Mycocore = new(OreType.MYCOCORE.Name, OreType.MYCOCORE.Name);

    private static readonly ItemType[] StockpileOrder =
    [
        Algae,
        Sandstone,
        Lumenite,
        Chitinstone,
        Mycocore
    ];

    private static readonly Dictionary<string, ItemType> ByName = new(StringComparer.Ordinal)
    {
        [Algae.Name] = Algae,
        [Sandstone.Name] = Sandstone,
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
