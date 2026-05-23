using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class SoilTile
{
    private const double DefaultGrowthConstant = 0d;
    private const double RanchGrowthMedian = 0.65d;
    private const double RanchGrowthStandardDeviation = 0.1d;
    private const double MaxRanchGrowthConstant = 0.99d;
    private const int MinGrowthLevel = 1;
    private const int MaxGrowthLevel = 3;
    private const int DefaultReturnedAlgaeAmount = 5;

    public SoilTile(SoilPatch parentPatch, GridPoint localOffset)
    {
        ParentPatch = parentPatch;
        LocalOffset = localOffset;
        GrowthConstant = DefaultGrowthConstant;
        GrowthLevel = MinGrowthLevel;
        ReturnedAlgaeAmount = DefaultReturnedAlgaeAmount;
        TextureKey = BuildTextureKey(GrowthLevel);
    }

    public SoilPatch ParentPatch { get; }

    public GridPoint LocalOffset { get; }

    public Ranch? Ranch { get; internal set; }

    public double GrowthConstant { get; private set; }

    public int GrowthLevel { get; private set; }

    public int ReturnedAlgaeAmount { get; private set; }

    public string TextureKey { get; private set; }

    public GridPoint? WorldLocation
    {
        get
        {
            if (ParentPatch.Location is not { } patchLocation)
            {
                return null;
            }

            return new GridPoint(patchLocation.X + LocalOffset.X, patchLocation.Y + LocalOffset.Y);
        }
    }

    // Soil tiles compare against the cave-wide growth roll once per tick until they reach the harvestable stage.
    public int Tick(World.Cave cave)
    {
        if (GrowthLevel >= MaxGrowthLevel || cave.TickGrowthMin >= GrowthConstant)
        {
            return 0;
        }

        SetGrowthLevel(GrowthLevel + 1);
        return 1;
    }

    public int Harvest()
    {
        if (GrowthLevel < MaxGrowthLevel)
        {
            return 0;
        }

        var harvested = ReturnedAlgaeAmount;
        SetGrowthLevel(MinGrowthLevel);
        return harvested;
    }

    internal void TileAddedToRanch()
    {
        GrowthConstant = System.Math.Clamp(
            RandomUtil.NextNormal(RanchGrowthMedian, RanchGrowthStandardDeviation),
            0d,
            MaxRanchGrowthConstant);
    }

    internal void TileRemovedFromRanch()
    {
        GrowthConstant = DefaultGrowthConstant;
    }

    internal void SetGrowthConstant(double value)
    {
        GrowthConstant = System.Math.Max(0d, value);
    }

    internal void SetReturnedAlgaeAmount(int amount)
    {
        ReturnedAlgaeAmount = System.Math.Max(0, amount);
    }

    internal void SetGrowthLevel(int level)
    {
        GrowthLevel = System.Math.Clamp(level, MinGrowthLevel, MaxGrowthLevel);
        TextureKey = BuildTextureKey(GrowthLevel);
    }

    private static string BuildTextureKey(int growthLevel)
    {
        return $"SoilTile_{growthLevel}";
    }
}
