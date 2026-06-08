namespace TriloGame.Game.Core.Economy;

public sealed record GrowableResourceType(OreType HarvestedOre)
{
    public static readonly GrowableResourceType ALGAE = new(OreType.ALGAE);

    private static readonly IReadOnlyList<GrowableResourceType> All = new[]
    {
        ALGAE
    };

    public string Name => HarvestedOre.Name;

    public static IReadOnlyList<GrowableResourceType> GetAll() => All;

    public string GetSoilTileTextureKey(int growthLevel) => $"SoilTile_{Name}_{growthLevel}";

    public override string ToString() => Name;
}
