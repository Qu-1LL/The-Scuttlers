namespace TriloGame.Game.Core.Economy;

public sealed record OreType(string Name)
{
    public static readonly OreType SANDSTONE = new("Sandstone");
    public static readonly OreType LUMENITE = new("Lumenite");
    public static readonly OreType CHITINSTONE = new("Chitinstone");
    public static readonly OreType MYCOCORE = new("Mycocore");

    private static readonly IReadOnlyList<OreType> All = new[]
    {
        LUMENITE,
        CHITINSTONE,
        MYCOCORE
    };

    public static IReadOnlyList<OreType> GetOres() => All;

    public override string ToString() => Name;
}
