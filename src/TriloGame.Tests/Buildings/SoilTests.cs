using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class SoilTests
{
    [Fact]
    public void Soil_DefaultsGrowthConstantToZero()
    {
        var soil = new TriloGame.Game.Core.Buildings.Soil(new GameSession());

        Assert.Equal(0d, soil.GrowthConstant);
    }

    [Fact]
    public void Tick_AdvancesGrowthLevelAndTexture_WhenTickGrowthMinIsBelowGrowthConstant()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 4));
        soil.SetGrowthConstant(0.5d);

        cave.SetTickGrowthMin(0.25d);
        Assert.Equal(1, soil.Tick(cave));
        Assert.Equal(2, soil.GrowthLevel);
        Assert.Equal("SoilTile_2", soil.TextureKey);

        cave.SetTickGrowthMin(0.1d);
        Assert.Equal(1, soil.Tick(cave));
        Assert.Equal(3, soil.GrowthLevel);
        Assert.Equal("SoilTile_3", soil.TextureKey);
    }

    [Fact]
    public void Tick_DoesNotAdvance_WhenThresholdFailsOrTileIsAlreadyFullyGrown()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 4));
        soil.SetGrowthConstant(0.5d);

        cave.SetTickGrowthMin(0.5d);
        Assert.Equal(0, soil.Tick(cave));
        Assert.Equal(1, soil.GrowthLevel);
        Assert.Equal("SoilTile_1", soil.TextureKey);

        soil.SetGrowthLevel(3);
        cave.SetTickGrowthMin(0.01d);
        Assert.Equal(0, soil.Tick(cave));
        Assert.Equal(3, soil.GrowthLevel);
        Assert.Equal("SoilTile_3", soil.TextureKey);
    }

    [Fact]
    public void Harvest_ReturnsConfiguredAmountAndResetsGrowth_WhenTileIsFullyGrown()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 4));
        soil.SetReturnedAlgaeAmount(8);

        soil.SetGrowthLevel(2);
        Assert.Equal(0, soil.Harvest());
        Assert.Equal(2, soil.GrowthLevel);
        Assert.Equal("SoilTile_2", soil.TextureKey);

        soil.SetGrowthLevel(3);
        Assert.Equal(8, soil.Harvest());
        Assert.Equal(1, soil.GrowthLevel);
        Assert.Equal("SoilTile_1", soil.TextureKey);
    }

    [Fact]
    public void RunTick_RollsNonNegativeTickGrowthMin()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 4));
        cave.SetTickGrowthMin(double.PositiveInfinity);

        TickRunner.RunTick(session);

        Assert.True(double.IsFinite(cave.TickGrowthMin));
        Assert.True(cave.TickGrowthMin >= 0d);
    }
}
