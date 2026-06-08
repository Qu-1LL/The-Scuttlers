using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Entities;

public sealed class CreatureNavigationTests
{
    [Fact]
    public void NavigateToBuilding_QueuesEquivalentPathAndReachesBuilding()
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

        trilobite.ClearActionQueue();

        Assert.True(trilobite.NavigateToBuilding(post));
        Assert.Equal(expectedPath.Skip(1), trilobite.PathPreview);

        var guard = expectedPath.Count + 2;
        while (!trilobite.IsOnPassableBuildingTile(post) && guard-- > 0)
        {
            trilobite.Move();
        }

        Assert.True(trilobite.IsOnPassableBuildingTile(post));
        Assert.Empty(trilobite.PathPreview);
    }
}
