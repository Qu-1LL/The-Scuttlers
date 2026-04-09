using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class AlgaeFarmTests
{
    [Fact]
    public void TraversalRing_FollowsNumberedPathMap()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 20, new GridPoint(0, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(10, 10));

        var expected = new[]
        {
            new GridPoint(11, 10),
            new GridPoint(11, 11),
            new GridPoint(11, 12),
            new GridPoint(10, 12),
            new GridPoint(10, 11),
            new GridPoint(10, 10)
        };

        var current = farm.GetTraversalStartLocation();
        Assert.NotNull(current);

        var visited = new List<GridPoint>();
        for (var index = 0; index < expected.Length; index++)
        {
            visited.Add(current!.Value);
            current = farm.GetNextTraversalLocation(current.Value);
            Assert.NotNull(current);
        }

        Assert.Equal(expected, visited);
        Assert.Equal(expected[0], current!.Value);
    }

    [Fact]
    public void TraversalRing_RotatesWithFarmFootprint()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 20, new GridPoint(0, 0));
        var farm = new AlgaeFarm(session);
        farm.RotateMap();

        Assert.True(cave.Build(farm, new GridPoint(10, 10)));

        var expected = new[]
        {
            new GridPoint(12, 11),
            new GridPoint(11, 11),
            new GridPoint(10, 11),
            new GridPoint(10, 10),
            new GridPoint(11, 10),
            new GridPoint(12, 10)
        };

        var current = farm.GetTraversalStartLocation();
        Assert.NotNull(current);

        var visited = new List<GridPoint>();
        for (var index = 0; index < expected.Length; index++)
        {
            visited.Add(current!.Value);
            current = farm.GetNextTraversalLocation(current.Value);
            Assert.NotNull(current);
        }

        Assert.Equal(expected, visited);
        Assert.Equal(expected[0], current!.Value);
    }

    [Fact]
    public void FarmPath_LoopsBackToStartingFarmTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var start = Assert.IsType<GridPoint>(farm.GetApproachTile(buildLocation));
        var path = farm.GetPath(start);

        Assert.NotEmpty(path);
        Assert.Equal(start, path[0]);
        Assert.Equal(start, path[^1]);
    }

    [Fact]
    public void Assign_DefaultCapacityAllowsTwoTrilobites()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var first = new Trilobite("Farmer 1", buildLocation, session);
        var second = new Trilobite("Farmer 2", buildLocation, session);
        var third = new Trilobite("Farmer 3", buildLocation, session);

        Assert.True(farm.Assign(first));
        Assert.True(farm.Assign(second));
        Assert.False(farm.Assign(third));
        Assert.Equal(2, farm.GetVolume());
        Assert.Equal(0, farm.GetAvailableAssignmentSlots());
    }

    [Fact]
    public void IncreaseAssignmentCapacity_AllowsAdditionalTrilobites()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var first = new Trilobite("Farmer 1", buildLocation, session);
        var second = new Trilobite("Farmer 2", buildLocation, session);
        var third = new Trilobite("Farmer 3", buildLocation, session);

        Assert.True(farm.Assign(first));
        Assert.True(farm.Assign(second));
        farm.IncreaseAssignmentCapacity();

        Assert.True(farm.Assign(third));
        Assert.Equal(3, farm.GetVolume());
        Assert.Equal(0, farm.GetAvailableAssignmentSlots());
    }
}
