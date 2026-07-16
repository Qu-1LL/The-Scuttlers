using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class BuildingFieldSelectionTests
{
    [Fact]
    public void NearestBuildingLookup_UsesEachBuildingField()
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
        Assert.Equal(cave.GetBuildingBfsFieldValue(post, tile), nearestDistances["Mining Post"]);
        Assert.Equal(cave.GetBuildingBfsFieldValue(farm, tile), nearestDistances["Algae Farm"]);
        Assert.Equal(cave.GetBuildingBfsFieldValue(barracks, tile), nearestDistances["Barracks"]);
    }

    [Fact]
    public void NearestBuildingTieBreak_UsesStableTopLeftLocation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(11, 10, new GridPoint(4, 0));
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(8, 4));
        var leftBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(0, 4));

        Assert.Same(leftBarracks, cave.GetNearestBarracks(new GridPoint(5, 7)));
    }

    [Fact]
    public void RemovingLastBuilding_RemovesItsNearestBuildingLookup()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 14, new GridPoint(8, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(8, 6));
        var probeTile = new GridPoint(9, 10);

        Assert.Same(barracks, cave.GetNearestBarracks(probeTile));
        Assert.True(cave.RemoveBuilding(barracks));

        Assert.Null(cave.GetNearestBarracks(probeTile));
        Assert.DoesNotContain("Barracks", cave.GetNearestBuildings(probeTile).Keys);
    }

    [Fact]
    public void NonNavigableBuildings_DoNotCreateBuildingBfsFields()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var radar = new Radar(session);
        var wall = new Wall(session);
        var soilPatch = new SoilPatch(session);
        var soilArea = new SoilArea(session);

        Assert.True(cave.Build(radar, new GridPoint(1, 6)));
        Assert.True(cave.Build(wall, new GridPoint(7, 6)));
        Assert.True(cave.Build(soilPatch, new GridPoint(10, 6)));

        Assert.False(radar.Navigable);
        Assert.False(wall.Navigable);
        Assert.False(soilPatch.Navigable);
        Assert.False(soilArea.Navigable);
        Assert.Null(cave.GetBuildingBfsFieldObject(radar));
        Assert.Null(cave.GetBuildingBfsFieldObject(wall));
        Assert.Null(cave.GetBuildingBfsFieldObject(soilPatch));
        Assert.Null(cave.GetBuildingBfsFieldObject(soilArea));
        Assert.Equal(int.MaxValue, cave.GetBuildingBfsFieldValue(radar, new GridPoint(3, 8)));
        Assert.Equal(int.MaxValue, cave.GetBuildingBfsFieldValue(wall, new GridPoint(3, 8)));
        Assert.Equal(int.MaxValue, cave.GetBuildingBfsFieldValue(soilPatch, new GridPoint(3, 8)));
    }
}
