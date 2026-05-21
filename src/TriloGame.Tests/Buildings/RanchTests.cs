using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class RanchTests
{
    [Fact]
    public void GaragePlacedNextToConnectedSoilsCreatesSingleRanch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var firstSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));
        var secondSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(5, 6));
        var thirdSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(6, 6));

        Assert.Null(firstSoil.Ranch);
        Assert.Null(secondSoil.Ranch);
        Assert.Null(thirdSoil.Ranch);
        Assert.Equal(0d, firstSoil.GrowthConstant);
        Assert.Equal(0d, secondSoil.GrowthConstant);
        Assert.Equal(0d, thirdSoil.GrowthConstant);

        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));

        var ranch = Assert.Single(cave.GetRanches());
        Assert.Same(ranch, garage.Ranch);
        Assert.Same(ranch, firstSoil.Ranch);
        Assert.Same(ranch, secondSoil.Ranch);
        Assert.Same(ranch, thirdSoil.Ranch);
        Assert.Same(garage, ranch.Garage);
        Assert.Equal(3, ranch.SoilTiles.Count);
        Assert.Equal(7, ranch.TileArray.Count);
        Assert.InRange(firstSoil.GrowthConstant, 0d, 0.99d);
        Assert.InRange(secondSoil.GrowthConstant, 0d, 0.99d);
        Assert.InRange(thirdSoil.GrowthConstant, 0d, 0.99d);
    }

    [Fact]
    public void SoilPlacedNextToGarageAbsorbsConnectedRanchlessSoils()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var firstSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(5, 6));
        var secondSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(6, 6));

        Assert.Null(firstSoil.Ranch);
        Assert.Null(secondSoil.Ranch);

        var bridgeSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, garage.Ranch);
        Assert.Same(ranch, bridgeSoil.Ranch);
        Assert.Same(ranch, firstSoil.Ranch);
        Assert.Same(ranch, secondSoil.Ranch);
        Assert.Equal(3, ranch.SoilTiles.Count);
    }

    [Fact]
    public void RemovingSoilPrunesDisconnectedSoilsFromRanch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var connectedSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));
        var bridgeSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(5, 6));
        var disconnectedSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(6, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(cave.RemoveBuilding(bridgeSoil, "test"));

        Assert.Null(bridgeSoil.Cave);
        Assert.Equal(0d, bridgeSoil.GrowthConstant);
        Assert.Same(ranch, connectedSoil.Ranch);
        Assert.InRange(connectedSoil.GrowthConstant, 0d, 0.99d);
        Assert.Null(disconnectedSoil.Ranch);
        Assert.Equal(0d, disconnectedSoil.GrowthConstant);
        Assert.Single(ranch.SoilTiles);
        Assert.Equal(5, ranch.TileArray.Count);
    }

    [Fact]
    public void SoilBetweenTwoRanches_JoinsOneWithoutMergingThem()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var leftSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(7, 6));
        var rightSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(6, 6));
        var bridgeSoil = new Soil(session);

        Assert.True(cave.CanBuild(bridgeSoil, new GridPoint(5, 6)));
        Assert.True(cave.Build(bridgeSoil, new GridPoint(5, 6)));
        Assert.NotNull(bridgeSoil.Ranch);
        Assert.Equal(2, cave.GetRanches().Count);
        Assert.True(
            ReferenceEquals(bridgeSoil.Ranch, leftSoil.Ranch) ||
            ReferenceEquals(bridgeSoil.Ranch, rightSoil.Ranch));
        Assert.NotSame(leftSoil.Ranch, rightSoil.Ranch);
    }

    [Fact]
    public void RemovingGarageResetsGrowthConstantForAllRanchSoils()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var firstSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));
        var secondSoil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(5, 6));

        Assert.InRange(firstSoil.GrowthConstant, 0d, 0.99d);
        Assert.InRange(secondSoil.GrowthConstant, 0d, 0.99d);

        Assert.True(cave.RemoveBuilding(garage, "test"));

        Assert.Empty(cave.GetRanches());
        Assert.Null(firstSoil.Ranch);
        Assert.Null(secondSoil.Ranch);
        Assert.Equal(0d, firstSoil.GrowthConstant);
        Assert.Equal(0d, secondSoil.GrowthConstant);
    }

    [Fact]
    public void GarageStoresOnlyAlgaeAndUpdatesSessionTotals()
    {
        var session = new GameSession();
        var garage = new Garage(session);

        Assert.IsAssignableFrom<IStorage>(garage);
        Assert.Equal(200, garage.Deposit(OreType.ALGAE.Name, 250));
        Assert.Equal(200, session.GetStoredResourceTotal(OreType.ALGAE.Name));
        Assert.Equal(0, garage.Deposit(OreType.SANDSTONE.Name, 10));

        Assert.Equal(40, garage.Withdraw(OreType.ALGAE.Name, 40));
        Assert.Equal(160, garage.GetInventoryTotal());
        Assert.Equal(160, session.GetStoredResourceTotal(OreType.ALGAE.Name));
        Assert.Equal(0, garage.Withdraw(OreType.SANDSTONE.Name, 10));
    }
}
