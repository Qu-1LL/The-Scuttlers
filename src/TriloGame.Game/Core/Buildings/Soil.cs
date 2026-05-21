using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class Soil : Building
{
    private const double DefaultGrowthConstant = 0d;
    private const double RanchGrowthMedian = 0.65d;
    private const double RanchGrowthStandardDeviation = 0.1d;
    private const double MaxRanchGrowthConstant = 0.99d;
    private const int MinGrowthLevel = 1;
    private const int MaxGrowthLevel = 3;
    private const int DefaultReturnedAlgaeAmount = 5;

    public Soil(GameSession session)
        : base("Soil", new GridPoint(1, 1), [[1]], session, false)
    {
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [OreType.ALGAE.Name] = 5
        };
        Description = "A passable soil tile. Soil connected to a garage joins that garage's ranch.";
        GrowthConstant = DefaultGrowthConstant;
        ReturnedAlgaeAmount = DefaultReturnedAlgaeAmount;
        SetGrowthLevel(MinGrowthLevel);
    }

    public Ranch? Ranch { get; internal set; }

    public double GrowthConstant { get; private set; }

    public int GrowthLevel { get; private set; }

    public int ReturnedAlgaeAmount { get; private set; }

    // Soil tiles compare against the cave-wide growth roll once per tick until they reach the harvestable stage.
    public override int Tick(World.Cave cave)
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

    public void SetReturnedAlgaeAmount(int amount)
    {
        ReturnedAlgaeAmount = System.Math.Max(0, amount);
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

    internal void SetGrowthLevel(int level)
    {
        GrowthLevel = System.Math.Clamp(level, MinGrowthLevel, MaxGrowthLevel);
        TextureKey = $"SoilTile_{GrowthLevel}";
    }
}
