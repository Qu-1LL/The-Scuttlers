using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Traits;

public enum TrilobiteTrait
{
    Explosive
}

public static class TrilobiteTraits
{
    private static readonly TrilobiteTrait[] AllTraits =
    [
        TrilobiteTrait.Explosive
    ];

    public static IReadOnlyList<TrilobiteTrait> All => AllTraits;

    public static IReadOnlyList<TrilobiteTrait> CreateRandomStarterTraits(int count)
    {
        var safeCount = Math.Clamp(count, 0, AllTraits.Length);
        if (safeCount == 0)
        {
            return [];
        }

        return [.. RandomUtil.Shuffle(AllTraits).Take(safeCount)];
    }

    public static string GetDisplayName(this TrilobiteTrait trait)
    {
        return trait switch
        {
            TrilobiteTrait.Explosive => "Explosive",
            _ => trait.ToString()
        };
    }
}
