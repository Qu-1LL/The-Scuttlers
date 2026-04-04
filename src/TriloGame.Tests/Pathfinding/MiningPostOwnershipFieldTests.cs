using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class MiningPostOwnershipFieldTests
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
}
