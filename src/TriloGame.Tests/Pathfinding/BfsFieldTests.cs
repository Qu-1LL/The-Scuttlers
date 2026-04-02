using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class BfsFieldTests
{
    [Fact]
    public void PointField_BuildsAContiguousPathAcrossReachableTiles()
    {
        var (_, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var destination = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString())
            .OrderByDescending(tile => GridPoint.ManhattanDistance(GridPoint.Parse(tile.Key), trilobite.Location))
            .First();
        var destinationPoint = GridPoint.Parse(destination.Key);

        var field = cave.BuildPointBfsField(destinationPoint);
        var path = cave.BuildPathFromField(field, trilobite.Location);

        Assert.NotNull(field);
        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.Equal(trilobite.Location, path[0]);
        Assert.Equal(destinationPoint, path[^1]);
        for (var index = 1; index < path.Count; index++)
        {
            Assert.Equal(1, GridPoint.ManhattanDistance(path[index - 1], path[index]));
        }
    }
}
