using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class BuildingOwnershipFieldTests
{
    [Fact]
    public void NearestBuildingLookup_ReturnsPerTypeDictionaryForTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(26, 16, new GridPoint(11, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(10, 10));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(18, 6));
        var tile = new GridPoint(12, 8);

        var nearestBuildings = cave.GetNearestBuildings(tile);
        var nearestDistances = cave.GetNearestBuildingDistances(tile);

        Assert.Same(post, nearestBuildings["Mining Post"]);
        Assert.Same(farm, nearestBuildings["Algae Farm"]);
        Assert.Same(barracks, nearestBuildings["Barracks"]);

        Assert.Equal(cave.GetNearestMiningPostDistance(tile), nearestDistances["Mining Post"]);
        Assert.Equal(cave.GetNearestAlgaeFarmDistance(tile), nearestDistances["Algae Farm"]);
        Assert.Equal(cave.GetNearestBarracksDistance(tile), nearestDistances["Barracks"]);
    }

    [Fact]
    public void BarracksOwnership_TieBreak_UsesStableLocationOrdering()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(11, 10, new GridPoint(4, 0));
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(8, 4));
        var leftBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(0, 4));

        var ownership = cave.GetBarracksOwnership(new GridPoint(5, 7));

        Assert.Same(leftBarracks, ownership.Building);
        Assert.Equal(4, ownership.Distance);
    }

    [Fact]
    public void AlgaeFarmAdjacencyGraph_RefreshesAfterMutation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(13, 12, new GridPoint(5, 0));
        var leftFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(0, 6));
        var rightFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(11, 6));

        Assert.Collection(
            cave.GetAdjacentAlgaeFarms(leftFarm),
            farm => Assert.Same(rightFarm, farm));
        Assert.Collection(
            cave.GetAdjacentAlgaeFarms(rightFarm),
            farm => Assert.Same(leftFarm, farm));

        var centerFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(5, 6));
        var centerOwnership = cave.GetAlgaeFarmOwnership(new GridPoint(6, 5));

        Assert.Same(centerFarm, centerOwnership.Building);
        Assert.Equal(1, centerOwnership.Distance);

        Assert.Collection(
            cave.GetAdjacentAlgaeFarms(leftFarm),
            farm => Assert.Same(centerFarm, farm));
        Assert.Collection(
            cave.GetAdjacentAlgaeFarms(centerFarm),
            farm => Assert.Same(leftFarm, farm),
            farm => Assert.Same(rightFarm, farm));
        Assert.Collection(
            cave.GetAdjacentAlgaeFarms(rightFarm),
            farm => Assert.Same(centerFarm, farm));
        Assert.DoesNotContain(rightFarm, cave.GetAdjacentAlgaeFarms(leftFarm));
    }

    [Fact]
    public void BarracksOwnershipField_DeactivatesWhenNoBarracksExist()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 14, new GridPoint(8, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(8, 6));
        var probeTile = new GridPoint(9, 10);

        Assert.Same(barracks, cave.GetNearestBarracks(probeTile));

        Assert.True(cave.RemoveBuilding(barracks));

        var field = cave.RefreshBarracksOwnershipField();
        var ownership = cave.GetBarracksOwnership(probeTile);
        var nearestBuildings = cave.GetNearestBuildings(probeTile);

        Assert.Null(field.Cave);
        Assert.Empty(field.GetOwnershipField(false));
        Assert.False(ownership.IsOwned);
        Assert.Null(ownership.Building);
        Assert.Equal(int.MaxValue, ownership.Distance);
        Assert.Empty(cave.GetBarracksAdjacencyGraph());
        Assert.DoesNotContain("Barracks", nearestBuildings.Keys);
    }
}
