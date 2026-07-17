using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Entities;

public sealed class CreatureNavigationTests
{
    [Fact]
    public void NavigateToBuilding_FollowsSmoothedContinuousRouteToBuilding()
    {
        var (session, cave, _, post, _) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(0);
        var spawnTile = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && post.TileArray.All(postTile => postTile.Coordinates != tile.Coordinates))
            .OrderByDescending(tile => GridPoint.ManhattanDistance(tile.Coordinates, post.TileArray[0].Coordinates))
            .First();
        var trilobite = new Trilobite("Navigator", spawnTile.Coordinates, session)
        {
            Assignment = "miner"
        };

        Assert.True(cave.Spawn(trilobite, spawnTile));

        var expectedPath = trilobite.BuildNavigationPathToBuilding(post);

        Assert.NotNull(expectedPath);
        Assert.True(expectedPath.Count > 1);

        trilobite.ClearTaskQueue();

        Assert.True(trilobite.NavigateToBuilding(post));
        Assert.NotEmpty(trilobite.DesiredRoute);

        var guard = expectedPath.Count * 4;
        while (trilobite.HasActiveMovement && guard-- > 0)
        {
            cave.AdvanceCreatureMovement();
        }

        Assert.True(trilobite.IsOnPassableBuildingTile(post));
        Assert.False(trilobite.HasActiveMovement);
    }
}
