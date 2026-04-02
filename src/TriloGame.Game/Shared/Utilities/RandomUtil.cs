using Microsoft.Xna.Framework;

namespace TriloGame.Game.Shared.Utilities;

public static class RandomUtil
{
    private static readonly Random SharedRandom = new();

    public static Random Shared => SharedRandom;

    public static double NextDouble() => Shared.NextDouble();

    public static int NextInt(int maxExclusive) => Shared.Next(maxExclusive);

    public static int NextInt(int minInclusive, int maxExclusive) => Shared.Next(minInclusive, maxExclusive);

    public static T[] Shuffle<T>(IEnumerable<T> source)
    {
        var values = source.ToArray();
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = Shared.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }

        return values;
    }

    public static double NextNormal(double mean, double standardDeviation)
    {
        var u = 1d - Shared.NextDouble();
        var v = 1d - Shared.NextDouble();
        var z = System.Math.Sqrt(-2d * System.Math.Log(u)) * System.Math.Cos(2d * System.Math.PI * v);
        return (z * standardDeviation) + mean;
    }

    public static Vector2 NextMovementOffset(float minDistance, float maxDistance)
    {
        var safeMax = System.Math.Max(minDistance, maxDistance);
        var angle = Shared.NextDouble() * System.Math.PI * 2d;
        var distance = minDistance + (Shared.NextDouble() * (safeMax - minDistance));

        return new Vector2(
            (float)(System.Math.Cos(angle) * distance),
            (float)(System.Math.Sin(angle) * distance));
    }
}
