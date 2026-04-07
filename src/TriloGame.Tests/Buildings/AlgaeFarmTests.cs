using TriloGame.Game.Core.Buildings;
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
}
