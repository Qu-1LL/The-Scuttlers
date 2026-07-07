using System.Numerics;

namespace TriloGame.Game.Shared.Utilities;

public static class RandomUtil
{
    private static readonly Random SharedRandom = new();

    public static Random Shared => SharedRandom;

    // Draw a unit-interval sample from the shared RNG.
    public static double NextDouble() => Shared.NextDouble();

    // Draw an integer from zero up to, but not including, the requested maximum.
    public static int NextInt(int maxExclusive) => Shared.Next(maxExclusive);

    // Draw an integer from the requested inclusive-exclusive range.
    public static int NextInt(int minInclusive, int maxExclusive) => Shared.Next(minInclusive, maxExclusive);

    // Return a shuffled copy of the provided sequence.
    public static T[] Shuffle<T>(IEnumerable<T> source)
    {
        var values = source.ToArray();
        // Use an in-place Fisher-Yates shuffle over the copied array.
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = Shared.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }

        return values;
    }

    // Sample a normal distribution using the Box-Muller transform.
    public static double NextNormal(double mean, double standardDeviation)
    {
        var u = 1d - Shared.NextDouble();
        var v = 1d - Shared.NextDouble();
        var z = System.Math.Sqrt(-2d * System.Math.Log(u)) * System.Math.Cos(2d * System.Math.PI * v);
        return (z * standardDeviation) + mean;
    }

    // Pick a random world-space offset inside the requested radial band.
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
