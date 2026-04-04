using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class MiningPostTests
{
    [Fact]
    public void ReservedWithdrawals_DoNotOverdrawMiningPostInventory()
    {
        var session = new GameSession();
        var post = new MiningPost(session);
        var creature = new Trilobite("Miner", GridPoint.Zero, session);

        Assert.Equal(15, post.Deposit("Sandstone", 15));
        Assert.Equal(10, post.ReserveMaterial(creature, "Sandstone", 10));

        var withdrawn = post.WithdrawReservedMaterial(creature);

        Assert.NotNull(withdrawn);
        Assert.Equal("Sandstone", withdrawn.ResourceType);
        Assert.Equal(10, withdrawn.Amount);
        Assert.Equal(5, post.GetInventory()["Sandstone"]);
    }

    [Fact]
    public void FullQueueInvalidation_RebuildsMineableQueuesAfterTheyWereExhausted()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var post = new MiningPost(session);
        var postLocation = TestWorldFactory.FindBuildLocation(cave, post, preserveReachability: true);
        Assert.True(cave.Build(post, postLocation));

        var reservedBy = new List<Trilobite>();
        while (true)
        {
            var creature = new Trilobite($"Miner {reservedBy.Count}", GridPoint.Zero, session);
            var tile = post.GrabMineableTile(cave, creature);
            if (tile is null)
            {
                break;
            }

            reservedBy.Add(creature);
        }

        Assert.NotEmpty(reservedBy);
        Assert.False(post.HasQueuedMineableTiles(cave));

        foreach (var creature in reservedBy)
        {
            post.RemoveAssignment(creature);
        }

        post.InvalidateMineableQueues();

        var recoveredTile = post.GrabMineableTile(cave, new Trilobite("Replacement Miner", GridPoint.Zero, session));

        Assert.NotNull(recoveredTile);
    }

    [Fact]
    public void AssignmentsAvailable_DefaultsFalse_WhenPostHasNoMineablesInRadius()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(7, 6));

        Assert.False(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
    }

    [Fact]
    public void AssignmentsAvailable_TracksReservedTilesAcrossPostsAndAssignments()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        SetTileBase(cave, new GridPoint(3, 10), "Sandstone");
        var availablePost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var emptyPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var reservingMiner = new Trilobite("Miner", GridPoint.Zero, session);

        Assert.True(availablePost.AssignmentsAvailable);
        Assert.False(emptyPost.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        var reservedTile = availablePost.GrabMineableTile(cave, reservingMiner);

        Assert.NotNull(reservedTile);
        Assert.False(availablePost.AssignmentsAvailable);
        Assert.False(emptyPost.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
        Assert.Equal(1, cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(availablePost.GetVolume(), cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(0, cave.GetMiningPostAssignmentCounts()[emptyPost]);

        availablePost.RemoveAssignment(reservingMiner);

        Assert.True(availablePost.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);
        Assert.Equal(0, cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(availablePost.GetVolume(), cave.GetMiningPostAssignmentCounts()[availablePost]);
    }

    private static void SetTileBase(TriloGame.Game.Core.World.Cave cave, GridPoint location, string tileBase)
    {
        var tile = cave.GetTile(location.ToString())
            ?? throw new InvalidOperationException($"No tile exists at {location}.");
        tile.SetBase(tileBase);
    }
}
