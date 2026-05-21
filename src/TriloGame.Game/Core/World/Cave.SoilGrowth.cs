using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private const double TickGrowthMean = 0d;
    private const double TickGrowthStandardDeviation = 0.2d;

    public double TickGrowthMin { get; private set; }

    // Roll the shared soil-growth threshold once so every soil tile evaluates against the same tick sample.
    internal double RollTickGrowthMin()
    {
        TickGrowthMin = System.Math.Abs(RandomUtil.NextNormal(TickGrowthMean, TickGrowthStandardDeviation));
        return TickGrowthMin;
    }

    internal void SetTickGrowthMin(double value)
    {
        TickGrowthMin = System.Math.Max(0d, value);
    }
}
