namespace TriloGame.Game.Core.Economy;

public sealed record OreType(string Name, ResourceName Resource)
{
    public static readonly OreType SANDSTONE = new("Sandstone", ResourceName.Sandstone);
    public static readonly OreType LUMENITE = new("Lumenite", ResourceName.Lumenite);
    public static readonly OreType CHITINSTONE = new("Chitinstone", ResourceName.Chitinstone);
    public static readonly OreType MYCOCORE = new("Mycocore", ResourceName.Mycocore);
    public static readonly OreType MAGNETITE = new("Magnetite", ResourceName.Magnetite);
    public static readonly OreType MALACHITE = new("Malachite", ResourceName.Malachite);
    public static readonly OreType PEROTENE = new("Perotene", ResourceName.Perotene);
    public static readonly OreType ILMENITE = new("Ilmenite", ResourceName.Ilmenite);
    public static readonly OreType COCHINIUM = new("Cochinium", ResourceName.Cochinium);

    private static readonly IReadOnlyList<OreType> All = new[]
    {
        SANDSTONE,
        CHITINSTONE,
        MAGNETITE,
        MALACHITE,
        PEROTENE,
        ILMENITE,
        COCHINIUM,
        LUMENITE,
        MYCOCORE
    };

    private static readonly Dictionary<string, OreType> ByName = new(StringComparer.Ordinal)
    {
        [SANDSTONE.Name] = SANDSTONE,
        [CHITINSTONE.Name] = CHITINSTONE,
        [MAGNETITE.Name] = MAGNETITE,
        [MALACHITE.Name] = MALACHITE,
        [PEROTENE.Name] = PEROTENE,
        [ILMENITE.Name] = ILMENITE,
        [COCHINIUM.Name] = COCHINIUM,
        [LUMENITE.Name] = LUMENITE,
        [MYCOCORE.Name] = MYCOCORE
    };

    public static IReadOnlyList<OreType> GetOres() => All;

    public static bool TryGet(string oreName, out OreType oreType)
    {
        return ByName.TryGetValue(oreName, out oreType!);
    }

    public override string ToString() => Name;
}
