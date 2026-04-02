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
}
