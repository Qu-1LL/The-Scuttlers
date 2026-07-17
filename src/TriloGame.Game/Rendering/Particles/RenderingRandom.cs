namespace TriloGame.Game.Rendering.Particles;

internal static class RenderingRandom
{
    private static readonly Random Shared = new();

    public static float NextRange(float minValue, float maxValue)
    {
        var safeMin = MathF.Min(minValue, maxValue);
        var safeMax = MathF.Max(minValue, maxValue);
        if (MathF.Abs(safeMax - safeMin) <= float.Epsilon)
        {
            return safeMin;
        }

        return safeMin + ((float)Shared.NextDouble() * (safeMax - safeMin));
    }

    public static int NextInt(int maxExclusive)
    {
        return maxExclusive <= 0 ? 0 : Shared.Next(maxExclusive);
    }

    public static int NextInt(int minInclusive, int maxInclusive)
    {
        var safeMin = Math.Min(minInclusive, maxInclusive);
        var safeMax = Math.Max(minInclusive, maxInclusive);
        return safeMin == safeMax ? safeMin : Shared.Next(safeMin, safeMax + 1);
    }

    public static float NextUnit()
    {
        return (float)Shared.NextDouble();
    }
}
