using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class MiningPostTests
{
    [Fact]
    public void InventoryChanges_UpdateSessionStoredResourceTotalsAndEmitStorageEvents()
    {
        var session = new GameSession();
        var post = new MiningPost(session);
        var creature = new Trilobite("Miner", GridPoint.Zero, session);
        var observedDeltas = new List<int>();
        var observedSources = new List<object?>();
        var unsubscribe = session.On(
            GameEvents.StorageInventoryChanged,
            payload =>
            {
                if (payload.ResourceType != ResourceName.Sandstone)
                {
                    return;
                }

                observedDeltas.Add(payload.ResourceDelta);
                observedSources.Add(payload.Source);
            });

        try
        {
            Assert.IsAssignableFrom<IStorage>(post);

            Assert.Equal(15, post.Deposit(ResourceName.Sandstone, 15));
            Assert.Equal(15, session.GetStoredResourceTotal("Sandstone"));

            Assert.Equal(5, post.Withdraw(ResourceName.Sandstone, 5));
            Assert.Equal(10, session.GetStoredResourceTotal("sandstone"));

            Assert.Equal(10, post.ReserveMaterial(creature, ResourceName.Sandstone, 10));
            var withdrawn = post.WithdrawReservedMaterial(creature);

            Assert.NotNull(withdrawn);
            Assert.Equal(0, session.GetStoredResourceTotal("Sandstone"));
            Assert.Equal([15, -5, -10], observedDeltas);
            Assert.All(observedSources, source => Assert.Same(post, source));
        }
        finally
        {
            unsubscribe();
        }
    }

    [Fact]
    public void ReservedWithdrawals_DoNotOverdrawMiningPostInventory()
    {
        var session = new GameSession();
        var post = new MiningPost(session);
        var creature = new Trilobite("Miner", GridPoint.Zero, session);

        Assert.Equal(15, post.Deposit(ResourceName.Sandstone, 15));
        Assert.Equal(10, post.ReserveMaterial(creature, ResourceName.Sandstone, 10));

        var withdrawn = post.WithdrawReservedMaterial(creature);

        Assert.NotNull(withdrawn);
        Assert.Equal(ResourceName.Sandstone, withdrawn.ResourceType);
        Assert.Equal(10, withdrawn.Amount);
        Assert.Equal(5, post.GetInventory()[ResourceName.Sandstone]);
    }

    [Fact]
    public void CategoryQueries_SelectLargestAvailableMatchingResource()
    {
        var session = new GameSession();
        var post = new MiningPost(session);

        Assert.Equal(3, post.Deposit(ResourceName.Sandstone, 3));
        Assert.Equal(7, post.Deposit(ResourceName.Malachite, 7));

        Assert.Equal(10, post.GetAvailableInventory(ResourceCategory.Rock));

        var match = post.FindAvailableResource(ResourceRequirement.ForCategory(ResourceCategory.Rock, 5), 5);

        Assert.NotNull(match);
        Assert.Equal(ResourceName.Malachite, match.Value.ResourceType);
        Assert.Equal(5, match.Value.Amount);
    }

    [Fact]
    public void MiningPostInventoryChanges_UpdateGlobalAvailabilityCache()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        SetTileBase(cave, new GridPoint(3, 10), "Sandstone");
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));

        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        Assert.Equal(post.Capacity, post.Deposit(ResourceName.Sandstone, post.Capacity));
        Assert.True(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        Assert.Equal(1, post.Withdraw(ResourceName.Sandstone, 1));
        Assert.True(cave.HasAvailableMiningPostAssignments);
    }

    [Fact]
    public void RemovingMiningPost_ClearsItsStoredResourcesFromGlobalTotals()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));

        Assert.Equal(25, post.Deposit(ResourceName.Sandstone, 25));
        Assert.Equal(25, session.GetStoredResourceTotal("Sandstone"));

        Assert.True(cave.RemoveBuilding(post, "test"));

        Assert.Equal(0, session.GetStoredResourceTotal("Sandstone"));
    }

    [Fact]
    public void SharedMineableTargets_StayAvailableForMultipleAssignments()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var oreLocation = new GridPoint(3, 10);
        SetTileBase(cave, oreLocation, OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var firstMiner = new Trilobite("First Miner", GridPoint.Zero, session);
        var secondMiner = new Trilobite("Second Miner", GridPoint.Zero, session);

        var firstTile = post.GrabMineableTile(cave, firstMiner);
        var secondTile = post.GrabMineableTile(cave, secondMiner);

        Assert.NotNull(firstTile);
        Assert.NotNull(secondTile);
        Assert.Equal(oreLocation.ToString(), firstTile!.Key);
        Assert.Equal(firstTile.Key, secondTile!.Key);
        Assert.Equal(firstTile.Key, post.GetAssignment(firstMiner));
        Assert.Equal(secondTile.Key, post.GetAssignment(secondMiner));
        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);
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
    public void GetNavigationTarget_UsesNeighborForWalkableOreTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var oreLocation = new GridPoint(5, 8);
        var ore = cave.GetTile(oreLocation)!;
        ore.SetBase(OreType.CHITINSTONE.Name);
        ore.CreatureCanFit = true;
        ore.ConfigureOre(1, 1);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(3, 5));

        var target = post.GetNavigationTarget(cave, ore);

        Assert.NotNull(target);
        Assert.NotEqual(oreLocation, target.Value);
        Assert.Contains(ore.Neighbors, neighbor => neighbor.Coordinates == target.Value);
    }

    [Fact]
    public void AssignmentsAvailable_RemainsTrue_WhenMineableTargetIsShared()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        SetTileBase(cave, new GridPoint(3, 10), OreType.CHITINSTONE.Name);
        var availablePost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var emptyPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var reservingMiner = new Trilobite("Miner", GridPoint.Zero, session);
        var secondMiner = new Trilobite("Second Miner", GridPoint.Zero, session);

        Assert.True(availablePost.AssignmentsAvailable);
        Assert.False(emptyPost.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);

        var reservedTile = availablePost.GrabMineableTile(cave, reservingMiner);
        var secondTile = availablePost.GrabMineableTile(cave, secondMiner);

        Assert.NotNull(reservedTile);
        Assert.NotNull(secondTile);
        Assert.Equal(reservedTile!.Key, secondTile!.Key);
        Assert.True(availablePost.AssignmentsAvailable);
        Assert.False(emptyPost.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);
        Assert.Equal(2, cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(availablePost.GetVolume(), cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(0, cave.GetMiningPostAssignmentCounts()[emptyPost]);

        availablePost.RemoveAssignment(reservingMiner);

        Assert.True(availablePost.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);
        Assert.Equal(1, cave.GetMiningPostAssignmentCounts()[availablePost]);
        Assert.Equal(availablePost.GetVolume(), cave.GetMiningPostAssignmentCounts()[availablePost]);
    }

    [Fact]
    public void BuildingNewAvailableMiningPost_ReactivatesGlobalAssignmentsAvailable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 14, new GridPoint(12, 0));
        var depletedPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 7));

        Assert.False(depletedPost.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        SetTileBase(cave, new GridPoint(24, 11), OreType.CHITINSTONE.Name);
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
        SetTileBase(cave, oreLocation, OreType.CHITINSTONE.Name);

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
        SetTileBase(cave, oreLocation, OreType.CHITINSTONE.Name);
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
