using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class TrilobiteMiningPostSelectionTests
{
    [Fact]
    public void BuilderSupplySelection_UsesNearestValidPost()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(25, 12, new GridPoint(11, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        leftPost.Deposit(ResourceName.Malachite, 25);
        rightPost.Deposit(ResourceName.Sandstone, 25);

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");
        var scaffold = new Scaffolding(session, new Barracks(session));

        var supplyOption = builder.GetBuilderSupplyOptionForScaffold(scaffold);

        Assert.NotNull(supplyOption);
        Assert.Same(leftPost, supplyOption.Value.SourceBuilding);
        Assert.Equal(ResourceName.Malachite, supplyOption.Value.ResourceType);
        Assert.Equal(builder.InventoryCapacity, supplyOption.Value.Amount);

        var metrics = builder.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("builder-supply", metrics!.Purpose);
        Assert.Equal(1, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.False(metrics.UsedAdjacencyFallback);
    }

    [Fact]
    public void BuilderDepositSelection_FallsBackToAdjacentPost_WhenNearestIsFull()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(27, 12, new GridPoint(12, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(20, 6));
        leftPost.Deposit(ResourceName.Sandstone, leftPost.Capacity);

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");

        var selectedPost = builder.SelectMiningPostForInventoryDeposit();

        Assert.Same(rightPost, selectedPost);
        Assert.NotSame(leftPost, selectedPost);

        var metrics = builder.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("builder-deposit", metrics!.Purpose);
        Assert.Equal(2, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.False(metrics.UsedAdjacencyFallback);
    }

    [Fact]
    public void MinerSelection_FallsBackToAdjacentPost_WhenNearestHasNoMineableWork()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(33, 12, new GridPoint(15, 0));
        SetTileBase(cave, new GridPoint(30, 10), OreType.CHITINSTONE.Name);
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(27, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 9), "Miner", "miner");

        var selectedPost = miner.SelectMiningPostForMining();

        Assert.Same(rightPost, selectedPost);
        Assert.NotSame(leftPost, selectedPost);

        var metrics = miner.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("miner", metrics!.Purpose);
        Assert.Equal(2, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.False(metrics.UsedAdjacencyFallback);
    }

    [Fact]
    public void MinerSelection_ReusesAssignedPost_WhenItRemainsValid()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(33, 12, new GridPoint(15, 0));
        SetTileBase(cave, new GridPoint(2, 10), OreType.CHITINSTONE.Name);
        SetTileBase(cave, new GridPoint(30, 10), OreType.CHITINSTONE.Name);
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(27, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(25, 9), "Miner", "miner");
        miner.SetAssignedBuilding(leftPost);
        leftPost.Assign(miner, null);

        var selectedPost = miner.SelectMiningPostForMining();

        Assert.Same(leftPost, selectedPost);

        var metrics = miner.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("miner", metrics!.Purpose);
        Assert.Equal(1, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.True(metrics.ReusedPreferredPost);
    }

    [Fact]
    public void MinerSelection_UsesLeastAssignedAvailablePost_WhenPickingNewAssignment()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(33, 12, new GridPoint(15, 0));
        SetTileBase(cave, new GridPoint(2, 10), OreType.CHITINSTONE.Name);
        SetTileBase(cave, new GridPoint(30, 10), OreType.CHITINSTONE.Name);
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(27, 6));
        var existingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 9), "Existing", "miner");
        leftPost.Assign(existingMiner, null);
        var newMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 9), "New Miner", "miner");

        var selectedPost = newMiner.SelectMiningPostForMining();

        Assert.Same(rightPost, selectedPost);
        Assert.Equal(1, cave.GetMiningPostAssignmentCounts()[leftPost]);
        Assert.Equal(0, cave.GetMiningPostAssignmentCounts()[rightPost]);
    }

    [Fact]
    public void BuilderSupplySelection_StopsAfterFindingFirstValidGraphCandidate()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(45, 12, new GridPoint(21, 0));
        TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(11, 6));
        var validPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(21, 6));
        TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(31, 6));
        validPost.Deposit(ResourceName.Malachite, 25);

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 9), "Builder", "builder");
        var scaffold = new Scaffolding(session, new Barracks(session));

        var supplyOption = builder.GetBuilderSupplyOptionForScaffold(scaffold);

        Assert.NotNull(supplyOption);
        Assert.Same(validPost, supplyOption.Value.SourceBuilding);
        Assert.Equal(ResourceName.Malachite, supplyOption.Value.ResourceType);

        var metrics = builder.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("builder-supply", metrics!.Purpose);
        Assert.Equal(3, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.False(metrics.UsedAdjacencyFallback);
    }

    [Fact]
    public void MinerStep1_SkipsMiningPostSearch_WhenNoAssignmentsAreAvailable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(7, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 9), "Miner", "miner");

        Assert.False(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
        Assert.Null(miner.LastMiningPostSelectionMetrics);

        Assert.False(miner.MinerStep1());
        Assert.Null(miner.GetAssignedMiningPost());
        Assert.Null(miner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerStep1_SkipsMiningPostSearch_WhenMiningPostInventoryIsFull()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        SetTileBase(cave, new GridPoint(3, 10), "Sandstone");
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 9), "Miner", "miner");

        Assert.True(post.AssignmentsAvailable);
        Assert.True(cave.HasAvailableMiningPostAssignments);
        Assert.Equal(post.Capacity, post.Deposit(ResourceName.Sandstone, post.Capacity));
        Assert.True(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        Assert.False(miner.MinerStep1());
        Assert.Null(miner.GetAssignedMiningPost());
        Assert.Null(miner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerStep1_WaitsOnAssignedExhaustedPost_WhenGlobalAssignmentsAreUnavailable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(26, 14, new GridPoint(10, 0));
        SetTileBase(cave, new GridPoint(3, 11), OreType.CHITINSTONE.Name);
        var exhaustedPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 7));
        var reservingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 9), "Reserver", "miner");
        var waitingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Waiting Miner", "miner");

        Assert.NotNull(exhaustedPost.GrabMineableTile(cave, reservingMiner));
        Assert.False(exhaustedPost.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        waitingMiner.SetAssignedBuilding(exhaustedPost);
        exhaustedPost.Assign(waitingMiner, null);

        Assert.False(waitingMiner.MinerStep1());
        Assert.Same(exhaustedPost, waitingMiner.GetAssignedMiningPost());
        Assert.Null(waitingMiner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerStep1_ReleasesExhaustedAssignedPost_WhenGlobalAssignmentsResume()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(34, 14, new GridPoint(14, 0));
        SetTileBase(cave, new GridPoint(3, 11), OreType.CHITINSTONE.Name);
        var exhaustedPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 7));
        var reservingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 9), "Reserver", "miner");
        var waitingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Waiting Miner", "miner");

        Assert.NotNull(exhaustedPost.GrabMineableTile(cave, reservingMiner));
        waitingMiner.SetAssignedBuilding(exhaustedPost);
        exhaustedPost.Assign(waitingMiner, null);

        SetTileBase(cave, new GridPoint(30, 11), OreType.CHITINSTONE.Name);
        var freshPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(28, 7));

        Assert.True(cave.HasAvailableMiningPostAssignments);

        waitingMiner.MinerStep1();

        Assert.Same(freshPost, waitingMiner.GetAssignedMiningPost());
        Assert.NotNull(waitingMiner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerStep3_QueuesRouteToReservedMineTarget()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        SetTileBase(cave, new GridPoint(14, 11), OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.MinerStep3());

        var reservedTargetKey = post.GetAssignment(miner);
        Assert.NotNull(reservedTargetKey);
        var reservedTile = cave.GetTile(reservedTargetKey!);
        Assert.NotNull(reservedTile);
        var navTarget = post.GetNavigationTarget(cave, reservedTile!);
        Assert.NotNull(navTarget);

        var queuedPath = miner.GetQueuedPathPreview();
        Assert.NotEmpty(queuedPath);
        Assert.Equal(miner.Location, queuedPath[0]);
        Assert.Equal(navTarget!.Value, queuedPath[^1]);
    }

    [Fact]
    public void MinerStep3_UsesNearestTileOfAssignedTypeForReservation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 18, new GridPoint(12, 0));
        var fartherOre = new GridPoint(8, 12);
        var nearerOre = new GridPoint(18, 12);
        SetTileBase(cave, fartherOre, OreType.CHITINSTONE.Name);
        SetTileBase(cave, nearerOre, OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(12, 8));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(19, 12), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.MinerStep3());

        Assert.Equal(OreType.CHITINSTONE.Name, miner.PendingMineType);
        Assert.Equal(nearerOre.ToString(), miner.PendingMineTileKey);
        Assert.Equal(nearerOre.ToString(), post.GetAssignment(miner));

        var queuedPath = miner.GetQueuedPathPreview();
        Assert.NotEmpty(queuedPath);
        Assert.Equal(miner.Location, queuedPath[0]);
        Assert.Equal(nearerOre, queuedPath[^1]);
    }

    [Fact]
    public void MinerStep5_RetargetsWhenReservedTileWasAlreadyMined()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var firstOre = new GridPoint(8, 10);
        var secondOre = new GridPoint(10, 10);
        SetTileBase(cave, firstOre, OreType.CHITINSTONE.Name);
        SetTileBase(cave, secondOre, OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(7, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 6), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.MinerStep3());

        var staleTargetKey = miner.PendingMineTileKey;
        Assert.Equal(firstOre.ToString(), staleTargetKey);
        Assert.Equal(staleTargetKey, post.GetAssignment(miner));

        Assert.True(session.MineTile(cave, staleTargetKey!, "test").TileDepleted);

        Assert.True(miner.MinerStep5());
        Assert.Equal(secondOre.ToString(), miner.PendingMineTileKey);
        Assert.Equal(secondOre.ToString(), post.GetAssignment(miner));
        Assert.NotEqual(staleTargetKey, miner.PendingMineTileKey);
    }

    private static void SetTileBase(TriloGame.Game.Core.World.Cave cave, GridPoint location, string tileBase)
    {
        var tile = cave.GetTile(location.ToString())
            ?? throw new InvalidOperationException($"No tile exists at {location}.");
        tile.SetBase(tileBase);
        if (string.Equals(tileBase, "wall", StringComparison.Ordinal))
        {
            tile.CreatureCanFit = false;
            tile.ConfigureWall(1);
        }
        else
        {
            tile.CreatureCanFit = true;
            tile.ConfigureOre(1, 1);
        }
    }
}
