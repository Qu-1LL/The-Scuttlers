namespace TriloGame.Game.Core.Economy;

public sealed record GrowableResourceType(ItemType HarvestedItem)
{
    public static readonly GrowableResourceType ALGAE = new(ItemCatalog.Algae);

    private static readonly GrowableResourceType[] All =
    [
        ALGAE
    ];

    public string Name => HarvestedItem.Name;

    public static IReadOnlyList<GrowableResourceType> GetAll() => All;

    public string GetSoilTileTextureKey(int growthLevel) => $"SoilTile_{Name}_{growthLevel}";

    public override string ToString() => Name;
}
