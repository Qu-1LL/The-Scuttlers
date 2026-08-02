using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class MiningPostOwnershipLookupTests
{
    [Fact]
    public void NearestOwner_AssignsExpectedPostsAndDistances_OnOpenGrid()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(11, 9, new GridPoint(4, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 4));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(8, 4));

        var leftOwnership = cave.GetMiningPostOwnership(new GridPoint(3, 6));
        var tieOwnership = cave.GetMiningPostOwnership(new GridPoint(5, 7));
        var rightOwnership = cave.GetMiningPostOwnership(new GridPoint(10, 7));

        Assert.Same(leftPost, leftOwnership.Post);
        Assert.Equal(1, leftOwnership.Distance);

        Assert.Same(leftPost, tieOwnership.Post);
        Assert.Equal(4, tieOwnership.Distance);

        Assert.Same(rightPost, rightOwnership.Post);
        Assert.Equal(1, rightOwnership.Distance);
    }

    [Fact]
    public void NearestOwner_TieBreak_UsesStableLocationOrdering_InsteadOfBuildOrder()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(11, 9, new GridPoint(4, 0));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(8, 4));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 4));

        var ownership = cave.GetMiningPostOwnership(new GridPoint(5, 7));

        Assert.Same(leftPost, ownership.Post);
        Assert.Equal(4, ownership.Distance);
        Assert.NotSame(rightPost, ownership.Post);
    }

    [Fact]
    public void AdjacencyGraph_RefreshesAfterMiningPostMutation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(13, 12, new GridPoint(5, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(10, 6));

        Assert.Collection(
            cave.GetAdjacentMiningPosts(leftPost),
            post => Assert.Same(rightPost, post));
        Assert.Collection(
            cave.GetAdjacentMiningPosts(rightPost),
            post => Assert.Same(leftPost, post));

        var centerPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(5, 6));
        var centerOwnership = cave.GetMiningPostOwnership(new GridPoint(6, 5));

        Assert.Same(centerPost, centerOwnership.Post);
        Assert.Equal(1, centerOwnership.Distance);

        Assert.Collection(
            cave.GetAdjacentMiningPosts(leftPost),
            post => Assert.Same(centerPost, post));
        Assert.Collection(
            cave.GetAdjacentMiningPosts(centerPost),
            post => Assert.Same(leftPost, post),
            post => Assert.Same(rightPost, post));
        Assert.Collection(
            cave.GetAdjacentMiningPosts(rightPost),
            post => Assert.Same(centerPost, post));
        Assert.DoesNotContain(rightPost, cave.GetAdjacentMiningPosts(leftPost));
        Assert.DoesNotContain(leftPost, cave.GetAdjacentMiningPosts(rightPost));
    }

    [Fact]
    public void MiningWall_PatchesOpenedOwnershipAndAddsMissingAdjacency()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(13, 6));

        for (var y = 0; y < 12; y++)
        {
            var barrierTile = cave.GetTile(new GridPoint(9, y).ToString())
                ?? throw new InvalidOperationException("Expected barrier tile to exist.");
            barrierTile.SetBase("wall");
            barrierTile.CreatureCanFit = false;
            barrierTile.ConfigureWall(1);
        }

        cave.RefreshReachableTiles();
        Assert.Empty(cave.GetAdjacentMiningPosts(leftPost));
        Assert.Empty(cave.GetAdjacentMiningPosts(rightPost));

        var openedLocation = new GridPoint(9, 6);
        Assert.True(session.MineTile(cave, openedLocation.ToString(), "test").TileDepleted);

        var openedTile = cave.GetTile(openedLocation.ToString())
            ?? throw new InvalidOperationException("Expected opened tile to exist.");
        var expectedNeighborOwnership = openedTile.Neighbors
            .Select(neighbor => cave.GetMiningPostOwnership(neighbor.Coordinates))
            .Where(ownership => ownership.IsOwned)
            .OrderBy(ownership => ownership.Distance)
            .ThenBy(ownership => ownership.Post!.Location!.ToString(), StringComparer.Ordinal)
            .First();
        var openedOwnership = cave.GetMiningPostOwnership(openedLocation);

        Assert.True(openedOwnership.IsOwned);
        Assert.Same(expectedNeighborOwnership.Post, openedOwnership.Post);
        Assert.Equal(expectedNeighborOwnership.Distance + 1, openedOwnership.Distance);
        Assert.Contains(rightPost, cave.GetAdjacentMiningPosts(leftPost));
        Assert.Contains(leftPost, cave.GetAdjacentMiningPosts(rightPost));
    }
}
