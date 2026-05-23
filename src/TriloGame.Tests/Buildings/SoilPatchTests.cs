using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class SoilPatchTests
{
    [Fact]
    public void SoilPatch_DefaultsToFourIndependentTierOneTiles()
    {
        var soilPatch = new SoilPatch(new GameSession());

        Assert.Equal(new GridPoint(2, 2), soilPatch.Size);
        Assert.Equal(2, soilPatch.OpenMap.Length);
        Assert.Equal([1, 1], soilPatch.OpenMap[0]);
        Assert.Equal([1, 1], soilPatch.OpenMap[1]);
        Assert.Equal("Soil Patch", soilPatch.Name);
        Assert.Equal(4, soilPatch.SoilTiles.Count);
        Assert.NotNull(soilPatch.SoilArea);
        Assert.Same(soilPatch.SoilArea, Assert.Single(soilPatch.SoilArea!.SoilPatches).SoilArea);
        Assert.Equal(4, soilPatch.SoilArea.SoilTiles.Count);
        Assert.All(soilPatch.SoilTiles, soilTile =>
        {
            Assert.Equal(0d, soilTile.GrowthConstant);
            Assert.Equal(1, soilTile.GrowthLevel);
            Assert.Equal(5, soilTile.ReturnedAlgaeAmount);
            Assert.Equal("SoilTile_1", soilTile.TextureKey);
        });
    }

    [Fact]
    public void Tick_AdvancesOnlyTilesWhoseGrowthConstantsBeatTheSharedRoll()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        soilPatch.SetAllGrowthConstants(0.2d);
        soilPatch.SetGrowthConstant(new GridPoint(0, 0), 0.6d);
        soilPatch.SetGrowthConstant(new GridPoint(0, 1), 0.4d);
        soilPatch.SetGrowthConstant(new GridPoint(1, 1), 0.9d);
        soilPatch.SetGrowthLevel(new GridPoint(1, 1), 3);

        cave.SetTickGrowthMin(0.25d);

        Assert.Equal(2, soilPatch.Tick(cave));
        Assert.Equal(2, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal("SoilTile_2", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal("SoilTile_1", soilPatch.GetSoilTile(new GridPoint(1, 0))!.TextureKey);
        Assert.Equal(2, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal("SoilTile_2", soilPatch.GetSoilTile(new GridPoint(0, 1))!.TextureKey);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
        Assert.Equal("SoilTile_3", soilPatch.GetSoilTile(new GridPoint(1, 1))!.TextureKey);
    }

    [Fact]
    public void HarvestAtWorldTile_ReturnsConfiguredAmountAndResetsOnlyTheHarvestedTile()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        soilPatch.SetReturnedAlgaeAmount(new GridPoint(0, 0), 8);
        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 2);
        soilPatch.SetGrowthLevel(new GridPoint(1, 1), 3);

        Assert.Equal(0, soilPatch.HarvestAtWorldTile(new GridPoint(4, 4)));
        Assert.Equal(2, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);

        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 3);

        Assert.Equal(8, soilPatch.HarvestAtWorldTile(new GridPoint(4, 4)));
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal("SoilTile_1", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
        Assert.Equal("SoilTile_3", soilPatch.GetSoilTile(new GridPoint(1, 1))!.TextureKey);
    }

    [Fact]
    public void RunTick_RollsNonNegativeTickGrowthMin()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        cave.SetTickGrowthMin(double.PositiveInfinity);

        TickRunner.RunTick(session);

        Assert.True(double.IsFinite(cave.TickGrowthMin));
        Assert.True(cave.TickGrowthMin >= 0d);
    }
}
