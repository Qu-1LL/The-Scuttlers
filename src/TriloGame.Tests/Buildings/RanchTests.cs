using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class RanchTests
{
    [Fact]
    public void GaragePlacedNextToConnectedSoilPatchesCreatesSingleRanch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var firstPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        var secondPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var thirdPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));

        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, garage.Ranch);
        Assert.Same(ranch, firstPatch.Ranch);
        Assert.Same(ranch, secondPatch.Ranch);
        Assert.Same(ranch, thirdPatch.Ranch);
        Assert.Same(garage, ranch.Garage);
        Assert.Equal(12, ranch.SoilTiles.Count);
        Assert.Single(ranch.SoilAreas);
        Assert.Equal(16, ranch.TileArray.Count);
        Assert.All(firstPatch.SoilTiles, soilTile => Assert.InRange(soilTile.GrowthConstant, 0d, 0.99d));
        Assert.All(secondPatch.SoilTiles, soilTile => Assert.InRange(soilTile.GrowthConstant, 0d, 0.99d));
        Assert.All(thirdPatch.SoilTiles, soilTile => Assert.InRange(soilTile.GrowthConstant, 0d, 0.99d));
    }

    [Fact]
    public void SoilPatchesPlacedAsOneAreaStayGroupedWhenClaimedByRanch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var area = new SoilArea(session);
        var firstPatch = new SoilPatch(session);
        var secondPatch = new SoilPatch(session);
        area.AddSoilPatch(firstPatch);
        area.AddSoilPatch(secondPatch);

        Assert.True(cave.Build(firstPatch, new GridPoint(4, 6)));
        Assert.True(cave.Build(secondPatch, new GridPoint(6, 6)));

        var ranch = Assert.Single(cave.GetRanches());
        Assert.Same(area, firstPatch.SoilArea);
        Assert.Same(area, secondPatch.SoilArea);
        Assert.Same(ranch, area.Ranch);
        Assert.Same(area, Assert.Single(ranch.SoilAreas));
        Assert.Equal(8, ranch.SoilTiles.Count);
        Assert.Equal(8, area.SoilTiles.Count);
    }

    [Fact]
    public void EqualLengthAdjacentSoilAreasMergeWhenAtMostOneHasRanch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var ranchArea = BuildSoilArea(cave, session, new GridPoint(4, 6), widthInPatches: 1, heightInPatches: 2);
        var ranch = Assert.Single(cave.GetRanches());

        var newArea = BuildSoilArea(cave, session, new GridPoint(6, 6), widthInPatches: 1, heightInPatches: 2);

        Assert.Empty(newArea.SoilPatches);
        Assert.Same(ranch, ranchArea.Ranch);
        Assert.Same(ranchArea, Assert.Single(ranch.SoilAreas));
        Assert.Equal(4, ranchArea.SoilPatches.Count);
        Assert.Equal(16, ranch.SoilTiles.Count);
        Assert.All(cave.GetSoilPatches(), patch => Assert.Same(ranchArea, patch.SoilArea));
    }

    [Fact]
    public void UnequalLengthAdjacentSoilAreasDoNotMerge()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var tallArea = BuildSoilArea(cave, session, new GridPoint(4, 6), widthInPatches: 1, heightInPatches: 2);
        var shortArea = BuildSoilArea(cave, session, new GridPoint(6, 6), widthInPatches: 1, heightInPatches: 1);
        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, tallArea.Ranch);
        Assert.Same(ranch, shortArea.Ranch);
        Assert.Equal(2, ranch.SoilAreas.Count);
        Assert.Equal(2, tallArea.SoilPatches.Count);
        Assert.Single(shortArea.SoilPatches);
    }

    [Fact]
    public void SoilPatchPlacedNextToGarageAbsorbsConnectedRanchlessPatches()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var firstPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var secondPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));

        var bridgePatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, garage.Ranch);
        Assert.Same(ranch, bridgePatch.Ranch);
        Assert.Same(ranch, firstPatch.Ranch);
        Assert.Same(ranch, secondPatch.Ranch);
        Assert.Equal(12, ranch.SoilTiles.Count);
    }

    [Fact]
    public void GaragePlacedAdjacentToSingleSoilTileStillClaimsWholeSoilPatch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 7));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));

        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, garage.Ranch);
        Assert.Same(ranch, soilPatch.Ranch);
        Assert.Equal(4, ranch.SoilTiles.Count);
        Assert.All(soilPatch.SoilTiles, soilTile => Assert.Same(ranch, soilTile.Ranch));
    }

    [Fact]
    public void RemovingSoilPatchPrunesDisconnectedPatchesFromRanch()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var connectedPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        var bridgePatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var disconnectedPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(cave.RemoveBuilding(bridgePatch, "test"));

        Assert.Null(bridgePatch.Cave);
        Assert.All(bridgePatch.SoilTiles, soilTile => Assert.Equal(0d, soilTile.GrowthConstant));
        Assert.Same(ranch, connectedPatch.Ranch);
        Assert.All(connectedPatch.SoilTiles, soilTile => Assert.InRange(soilTile.GrowthConstant, 0d, 0.99d));
        Assert.Null(disconnectedPatch.Ranch);
        Assert.All(disconnectedPatch.SoilTiles, soilTile => Assert.Equal(0d, soilTile.GrowthConstant));
        Assert.Equal(4, ranch.SoilTiles.Count);
        Assert.Equal(8, ranch.TileArray.Count);
    }

    [Fact]
    public void SoilPatchBetweenTwoRanches_JoinsOneWithoutMergingThem()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var leftPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(10, 6));
        var rightPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));
        var bridgePatch = new SoilPatch(session);

        Assert.True(cave.CanBuild(bridgePatch, new GridPoint(6, 6)));
        Assert.True(cave.Build(bridgePatch, new GridPoint(6, 6)));
        Assert.NotNull(bridgePatch.Ranch);
        Assert.Equal(2, cave.GetRanches().Count);
        Assert.True(
            ReferenceEquals(bridgePatch.Ranch, leftPatch.Ranch) ||
            ReferenceEquals(bridgePatch.Ranch, rightPatch.Ranch));
        Assert.NotSame(leftPatch.Ranch, rightPatch.Ranch);
    }

    [Fact]
    public void RemovingGarageResetsGrowthConstantsForAllRanchPatches()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var firstPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        var secondPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));

        Assert.True(cave.RemoveBuilding(garage, "test"));

        Assert.Empty(cave.GetRanches());
        Assert.Null(firstPatch.Ranch);
        Assert.Null(secondPatch.Ranch);
        Assert.All(firstPatch.SoilTiles, soilTile => Assert.Equal(0d, soilTile.GrowthConstant));
        Assert.All(secondPatch.SoilTiles, soilTile => Assert.Equal(0d, soilTile.GrowthConstant));
    }

    [Fact]
    public void RemovingGarageReattachesDisconnectedPatchesToReachableGarage()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(0, 0));
        var leftGarage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var leftPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        var rightGarage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(12, 6));
        var rightPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(10, 6));
        var firstBridge = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var secondBridge = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));

        Assert.Equal(2, cave.GetRanches().Count);

        Assert.True(cave.RemoveBuilding(leftGarage, "test"));

        var remainingRanch = Assert.Single(cave.GetRanches());
        Assert.Same(rightGarage.Ranch, remainingRanch);
        Assert.Same(remainingRanch, leftPatch.Ranch);
        Assert.Same(remainingRanch, firstBridge.Ranch);
        Assert.Same(remainingRanch, secondBridge.Ranch);
        Assert.Same(remainingRanch, rightPatch.Ranch);
        Assert.Equal(16, remainingRanch.SoilTiles.Count);
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

    private static SoilArea BuildSoilArea(
        TriloGame.Game.Core.World.Cave cave,
        GameSession session,
        GridPoint location,
        int widthInPatches,
        int heightInPatches)
    {
        var soilArea = new SoilArea(session);
        for (var y = 0; y < heightInPatches; y++)
        {
            for (var x = 0; x < widthInPatches; x++)
            {
                var soilPatch = new SoilPatch(session);
                soilArea.AddSoilPatch(soilPatch, new GridPoint(x * SoilPatch.DefaultSize.X, y * SoilPatch.DefaultSize.Y));
            }
        }

        if (!cave.BuildSoilArea(soilArea, location))
        {
            throw new InvalidOperationException($"Failed to build soil area at {location}.");
        }

        return soilArea;
    }
}
