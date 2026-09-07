namespace TriloGame.Game.Core.Economy;

public sealed record GrowableResourceType(ItemType HarvestedItem)
{
    public static readonly GrowableResourceType ALGAE = new(ItemCatalog.Algae);
    public static readonly GrowableResourceType GLOOP = new(ItemCatalog.Gloop);

    private static readonly GrowableResourceType[] All =
    [
        ALGAE,
        GLOOP
    ];

    public string Name => HarvestedItem.Name;

    public ResourceName Resource => HarvestedItem.Resource;

    public ItemType HarvestedOre => HarvestedItem;

    public static IReadOnlyList<GrowableResourceType> GetAll() => All;

    public string GetSoilTileTextureKey(int growthLevel) => $"SoilTile_{Name}_{growthLevel}";

    public override string ToString() => Name;
}
