using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Traits;
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
    public void MiningPostInventoryChanges_UpdateGlobalAvailabilityCache()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        SetTileBase(cave, new GridPoint(3, 10), "Sandstone");
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));

        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        Assert.Equal(post.Capacity, post.Deposit("Sandstone", post.Capacity));
        Assert.True(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        Assert.Equal(1, post.Withdraw("Sandstone", 1));
        Assert.True(cave.HasAvailableMiningPostAssignments);
    }

    [Fact]
    public void ExhaustedAssignmentsRemainFalse_AfterReservationReleaseAndInvalidation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        SetTileBase(cave, new GridPoint(3, 10), "Sandstone");
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));

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

        Assert.Null(recoveredTile);
        Assert.False(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
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
    public void AssignmentsAvailable_RemainsFalse_AfterReservedTilesAreReleased()
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

        Assert.False(availablePost.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
        Assert.Equal(0, cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(availablePost.GetVolume(), cave.GetMiningPostAssignmentCounts()[availablePost]);
    }

    [Fact]
    public void BuildingNewAvailableMiningPost_ReactivatesGlobalAssignmentsAvailable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 14, new GridPoint(12, 0));
        var depletedPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 7));

        Assert.False(depletedPost.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        SetTileBase(cave, new GridPoint(24, 11), "Sandstone");
        var freshPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(22, 7));

        Assert.True(freshPost.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);
    }

    [Fact]
    public void OnBuilt_PopulatesPossibleAssignmentsBeforeInitialReveal()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(18, 12);
        var queen = new Queen(session);
        Assert.True(cave.Build(queen, new GridPoint(1, 1)));

        var oreLocation = new GridPoint(8, 6);
        SetTileBase(cave, oreLocation, "Sandstone");

        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(5, 4));

        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        var assignedTile = post.GrabMineableTile(cave, new Trilobite("Startup Miner", GridPoint.Zero, session));

        Assert.NotNull(assignedTile);
        Assert.Equal(oreLocation.ToString(), assignedTile!.Key);
    }

    [Fact]
    public void MiningOre_RemovesTileFromPossibleAssignmentsImmediately()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(18, 12);
        var queen = new Queen(session);
        Assert.True(cave.Build(queen, new GridPoint(1, 1)));

        var oreLocation = new GridPoint(8, 6);
        SetTileBase(cave, oreLocation, "Sandstone");
        cave.RevealTile(cave.GetTile(oreLocation.ToString())!);

        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(5, 4));

        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        Assert.True(session.MineTile(cave, oreLocation.ToString(), "test").TileDepleted);

        Assert.False(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
        Assert.Null(post.GrabMineableTile(cave, new Trilobite("Replacement Miner", GridPoint.Zero, session)));
    }

    [Fact]
    public void MiningWall_AddsNewlyRevealedWallsWithinRadiusToPossibleAssignments()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(20, 12);
        var queen = new Queen(session);
        Assert.True(cave.Build(queen, new GridPoint(1, 1)));

        var minedWall = new GridPoint(9, 6);
        var newlyRevealedWall = new GridPoint(10, 6);
        SetTileBase(cave, minedWall, "wall");
        SetTileBase(cave, newlyRevealedWall, "wall");
        cave.RevealTile(cave.GetTile(minedWall.ToString())!);

        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 4));

        Assert.True(post.AssignmentsAvailable);
        Assert.False(cave.IsTileRevealed(cave.GetTile(newlyRevealedWall.ToString())!));

        Assert.True(session.MineTile(cave, minedWall.ToString(), "test").TileDepleted);

        Assert.True(cave.IsTileRevealed(cave.GetTile(newlyRevealedWall.ToString())!));
        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        var nextTile = post.GrabMineableTile(cave, new Trilobite("Replacement Miner", GridPoint.Zero, session));

        Assert.NotNull(nextTile);
        Assert.Equal(newlyRevealedWall.ToString(), nextTile!.Key);
    }

    [Fact]
    public void AssignedCreature_IsTrackedAndRemovedWhenItDies()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(1, 1));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(10, 10));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(9, 10), "Miner", "miner");
        miner.SetTraits(Array.Empty<TrilobiteTrait>());

        post.Assign(miner, null);
        Assert.Contains(post, miner.TrackedBy);
        Assert.Equal(1, post.GetVolume());

        miner.TakeDamage(miner.Health, "test");

        Assert.Equal(0, post.GetVolume());
        Assert.Empty(miner.TrackedBy);
    }

    private static void SetTileBase(TriloGame.Game.Core.World.Cave cave, GridPoint location, string tileBase)
    {
        var tile = cave.GetTile(location.ToString())
            ?? throw new InvalidOperationException($"No tile exists at {location}.");
        tile.SetBase(tileBase);
        tile.CreatureCanFit = !string.Equals(tileBase, "wall", StringComparison.Ordinal);
        if (string.Equals(tileBase, "wall", StringComparison.Ordinal))
        {
            tile.ConfigureWall(1);
        }
        else
        {
            tile.ConfigureOre(1, 1);
        }
    }
}
