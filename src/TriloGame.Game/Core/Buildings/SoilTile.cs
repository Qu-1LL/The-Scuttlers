using System;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class SoilTile
{
    private const double DefaultGrowthConstant = 0d;
    private const double RanchGrowthMedian = 0.35d;
    private const double RanchGrowthStandardDeviation = 0.2d;
    private const double MaxRanchGrowthConstant = 0.99d;
    private const int DormantGrowthLevel = 0;
    private const int MinActiveGrowthLevel = 1;
    private const int MaxGrowthLevel = 3;
    private const int DefaultReturnedAlgaeAmount = 5;
    private const double GrowthChanceThreshold = 0.7d;
    private const string DormantTextureKey = "SoilTile_0";

    public SoilTile(SoilPatch parentPatch, GridPoint localOffset)
    {
        ParentPatch = parentPatch;
        LocalOffset = localOffset;
        GrowthConstant = DefaultGrowthConstant;
        GrowthLevel = DormantGrowthLevel;
        ReturnedAlgaeAmount = DefaultReturnedAlgaeAmount;
        LastTickMod = 0;
        TextureKey = DormantTextureKey;
    }

    public SoilPatch ParentPatch { get; }

    public GridPoint LocalOffset { get; }

    public Ranch? Ranch { get; internal set; }

    public double GrowthConstant { get; private set; }

    public int GrowthLevel { get; private set; }

    public GrowableResourceType? PlantedResource { get; private set; }

    public int ReturnedAlgaeAmount { get; private set; }

    public int LastTickMod { get; private set; }

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

    // Each soil tile only rolls growth on matching tick digits, then succeeds on a strict > 0.7 hit.
    internal int Tick(Random random, int currentTickMod)
    {
        if (GrowthLevel <= DormantGrowthLevel ||
            PlantedResource is null ||
            GrowthLevel >= MaxGrowthLevel ||
            currentTickMod != LastTickMod)
        {
            return 0;
        }

        var roll = random.NextDouble();
        if (roll <= GrowthChanceThreshold)
        {
            return 0;
        }

        SetGrowthLevel(GrowthLevel + 1);
        return 1;
    }

    public int Harvest()
    {
        if (GrowthLevel < MaxGrowthLevel || PlantedResource is null)
        {
            return 0;
        }

        var harvested = ReturnedAlgaeAmount;
        SetGrowthLevel(MinActiveGrowthLevel);
        LastTickMod = ParentPatch.Session.TickCount % 10;
        return harvested;
    }

    public bool TryGetHarvest(out GrowableResourceType? resourceType, out int amount)
    {
        resourceType = null;
        amount = 0;
        if (GrowthLevel < MaxGrowthLevel || PlantedResource is null || ReturnedAlgaeAmount <= 0)
        {
            return false;
        }

        resourceType = PlantedResource;
        amount = ReturnedAlgaeAmount;
        return true;
    }

    // Planting activates dormant soil and retargets future harvests to the garage's chosen growable crop.
    public bool Plant(GrowableResourceType resourceType)
    {
        var changed = !Equals(PlantedResource, resourceType);
        PlantedResource = resourceType;
        if (GrowthLevel <= DormantGrowthLevel)
        {
            SetGrowthLevel(MinActiveGrowthLevel);
            LastTickMod = ParentPatch.Session.TickCount % 10;
            return true;
        }

        RefreshTextureKey();
        return changed;
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

    internal void SetPlantedResource(GrowableResourceType? resourceType)
    {
        PlantedResource = resourceType;
        RefreshTextureKey();
    }

    internal void SetGrowthLevel(int level)
    {
        GrowthLevel = System.Math.Clamp(level, DormantGrowthLevel, MaxGrowthLevel);
        RefreshTextureKey();
    }

    private void RefreshTextureKey()
    {
        TextureKey = BuildTextureKey(GrowthLevel, PlantedResource);
    }

    private static string BuildTextureKey(int growthLevel, GrowableResourceType? resourceType)
    {
        if (growthLevel <= DormantGrowthLevel || resourceType is null)
        {
            return DormantTextureKey;
        }

        return resourceType.GetSoilTileTextureKey(growthLevel);
    }
}
