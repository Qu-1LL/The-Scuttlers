namespace TriloGame.Game.Core.World;

public static class BiomeNames
{
    public const string Sand = "Sand";
    public const string Lush = "Lush";
    public const string Green = "Green";
    public const string Lava = "Lava";

    public static IReadOnlyList<string> All { get; } = [Sand, Lush, Green, Lava];
}
