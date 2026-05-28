using System;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class SoilPatchTests
{
    [Fact]
    public void Building_DefaultsToNotIgnoredByAnts()
    {
        var building = new Building("Test Building", new GridPoint(1, 1), [[1]], new GameSession(), false);

        Assert.False(building.IgnoredByAnts);
    }

    [Fact]
    public void SoilPatch_DefaultsToFourIndependentDormantTiles()
    {
        var soilPatch = new SoilPatch(new GameSession());

        Assert.Equal(new GridPoint(2, 2), soilPatch.Size);
        Assert.Equal(2, soilPatch.OpenMap.Length);
        Assert.Equal([1, 1], soilPatch.OpenMap[0]);
        Assert.Equal([1, 1], soilPatch.OpenMap[1]);
        Assert.Equal("Soil Patch", soilPatch.Name);
        Assert.Equal(4, soilPatch.SoilTiles.Count);
        Assert.True(soilPatch.IgnoredByAnts);
        Assert.NotNull(soilPatch.SoilArea);
        Assert.Same(soilPatch.SoilArea, Assert.Single(soilPatch.SoilArea!.SoilPatches).SoilArea);
        Assert.Equal(4, soilPatch.SoilArea.SoilTiles.Count);
        Assert.All(soilPatch.SoilTiles, soilTile =>
        {
            Assert.Equal(0d, soilTile.GrowthConstant);
            Assert.Equal(0, soilTile.GrowthLevel);
            Assert.Null(soilTile.PlantedResource);
            Assert.Equal(5, soilTile.ReturnedAlgaeAmount);
            Assert.Equal(0, soilTile.LastTickMod);
            Assert.Equal("SoilTile_0", soilTile.TextureKey);
        });
    }

    [Fact]
    public void Tick_OnlyRollsOnMatchingTickDigitsAndSkipsDormantTiles()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        soilPatch.SetPlantedResource(new GridPoint(0, 0), GrowableResourceType.ALGAE);
        soilPatch.SetPlantedResource(new GridPoint(0, 1), GrowableResourceType.ALGAE);
        soilPatch.SetPlantedResource(new GridPoint(1, 1), GrowableResourceType.ALGAE);
        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 1);
        soilPatch.SetGrowthLevel(new GridPoint(0, 1), 1);
        soilPatch.SetGrowthLevel(new GridPoint(1, 1), 3);

        var mismatchedRandom = new SequenceRandom(0.99d);

        Assert.Equal(0, soilPatch.Tick(mismatchedRandom, 1));
        Assert.Equal(0, mismatchedRandom.Calls);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(0, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);

        var matchingRandom = new SequenceRandom(0.71d, 0.7d);

        Assert.Equal(1, soilPatch.Tick(matchingRandom, 0));
        Assert.Equal(2, matchingRandom.Calls);
        Assert.Equal(2, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal("SoilTile_Algae_2", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(0, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal("SoilTile_0", soilPatch.GetSoilTile(new GridPoint(1, 0))!.TextureKey);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 1))!.TextureKey);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
        Assert.Equal("SoilTile_Algae_3", soilPatch.GetSoilTile(new GridPoint(1, 1))!.TextureKey);
    }

    [Fact]
    public void HarvestAtWorldTile_ReturnsConfiguredAmountAndResetsOnlyTheHarvestedTile()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        soilPatch.SetPlantedResource(new GridPoint(0, 0), GrowableResourceType.ALGAE);
        soilPatch.SetPlantedResource(new GridPoint(1, 1), GrowableResourceType.ALGAE);
        soilPatch.SetReturnedAlgaeAmount(new GridPoint(0, 0), 8);
        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 2);
        soilPatch.SetGrowthLevel(new GridPoint(1, 1), 3);

        Assert.Equal(0, soilPatch.HarvestAtWorldTile(new GridPoint(4, 4)));
        Assert.Equal(2, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);

        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 3);

        Assert.Equal(8, soilPatch.HarvestAtWorldTile(new GridPoint(4, 4)));
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 0))!.PlantedResource);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(0, soilPatch.GetSoilTile(new GridPoint(0, 0))!.LastTickMod);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
        Assert.Equal("SoilTile_Algae_3", soilPatch.GetSoilTile(new GridPoint(1, 1))!.TextureKey);
    }

    [Fact]
    public void HarvestAtWorldTile_StoresTheCurrentTickDigitAsTheNewGrowthPhase()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        soilPatch.SetPlantedResource(new GridPoint(0, 0), GrowableResourceType.ALGAE);
        soilPatch.SetReturnedAlgaeAmount(new GridPoint(0, 0), 8);
        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 3);
        session.TickCount = 17;

        Assert.Equal(8, soilPatch.HarvestAtWorldTile(new GridPoint(4, 4)));
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(7, soilPatch.GetSoilTile(new GridPoint(0, 0))!.LastTickMod);
        Assert.Equal(1, soilPatch.Tick(new SequenceRandom(0.8d), 7));
        Assert.Equal(2, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
    }

    private sealed class SequenceRandom : Random
    {
        private readonly double[] _values;
        private int _index;

        public SequenceRandom(params double[] values)
        {
            _values = values;
        }

        public int Calls => _index;

        public override double NextDouble()
        {
            if (_index >= _values.Length)
            {
                throw new InvalidOperationException("SequenceRandom ran out of configured values.");
            }

            return _values[_index++];
        }
    }
}
