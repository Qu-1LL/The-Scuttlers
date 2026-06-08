namespace TriloGame.Game.Core.Economy;

public sealed record OreType(string Name)
{
    public static readonly OreType ALGAE = new("Algae");
    public static readonly OreType SANDSTONE = new("Sandstone");
    public static readonly OreType MAGNETITE = new("Magnetite");
    public static readonly OreType MALACHITE = new("Malachite");
    public static readonly OreType PEROTENE = new("Perotene");
    public static readonly OreType ILMENITE = new("Ilmenite");
    public static readonly OreType COCHINIUM = new("Cochinium");

    private static readonly IReadOnlyList<OreType> All = new[]
    {
        ALGAE,
        SANDSTONE,
        MAGNETITE,
        MALACHITE,
        PEROTENE,
        ILMENITE,
        COCHINIUM
    };

    public static IReadOnlyList<OreType> GetOres() => All;

    public override string ToString() => Name;
}
