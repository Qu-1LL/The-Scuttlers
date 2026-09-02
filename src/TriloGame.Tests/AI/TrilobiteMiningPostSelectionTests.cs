using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
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
    public void MinerSelection_DistributesNewMinersEvenlyAcrossMiningPosts()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(33, 12, new GridPoint(15, 0));
        SetTileBase(cave, new GridPoint(2, 10), OreType.CHITINSTONE.Name);
        SetTileBase(cave, new GridPoint(30, 10), OreType.CHITINSTONE.Name);
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(27, 6));
        var miners = new List<Trilobite>(4);
        for (var index = 0; index < 4; index++)
        {
            miners.Add(TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 9), $"Miner {index}", "miner"));
        }

        for (var index = 0; index < miners.Count; index++)
        {
            Assert.True(miners[index].RunRoleState(MinerState.SelectPost));
        }

        Assert.Equal(2, cave.GetMiningPostAssignmentCounts()[leftPost]);
        Assert.Equal(2, cave.GetMiningPostAssignmentCounts()[rightPost]);
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
    public void MinerSelectPost_SkipsMiningPostSearch_WhenNoAssignmentsAreAvailable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(7, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 9), "Miner", "miner");

        Assert.False(post.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);
        Assert.Null(miner.LastMiningPostSelectionMetrics);

        Assert.False(miner.RunRoleState(MinerState.SelectPost));
        Assert.Null(miner.GetAssignedMiningPost());
        Assert.Null(miner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerSelectPost_SkipsMiningPostSearch_WhenMiningPostInventoryIsFull()
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

        Assert.False(miner.RunRoleState(MinerState.SelectPost));
        Assert.Null(miner.GetAssignedMiningPost());
        Assert.Null(miner.LastMiningPostSelectionMetrics);
        Assert.Equal(MinerState.WaitForStorage, miner.MinerState);
        Assert.Equal(WorkerRoleFailureReason.NoStorage, miner.LastRoleFailure);
    }

    [Fact]
    public void MinerDepositInventory_ZeroAcceptedDeposit_WaitsForStorageWithoutStayingDepositing()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 6), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);
        Assert.Equal(post.Capacity, post.Deposit(ResourceName.Sandstone, post.Capacity));
        Assert.Equal(1, miner.AddToInventory(ResourceName.Sandstone, 1));

        Assert.True(miner.IsAtBuildingInteractionTile(post));

        var depositCueCount = 0;
        session.AudioCueRequested += cue => depositCueCount += cue == GameAudioCue.CreatureDeposit ? 1 : 0;

        Assert.False(miner.RunRoleState(MinerState.DepositInventory));

        Assert.Equal(0, depositCueCount);
        Assert.Equal(1, miner.Inventory.Amount);
        Assert.Equal(CreatureActivity.WaitingForSlot, miner.Activity);
        Assert.Equal(MinerState.WaitForStorage, miner.MinerState);
        Assert.Equal(WorkerRoleFailureReason.NoStorage, miner.LastRoleFailure);
    }

    [Fact]
    public void MinerDepositInventory_PartialAcceptedDeposit_WaitsWithoutStayingDepositing()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 6), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);
        Assert.Equal(post.Capacity - 1, post.Deposit(ResourceName.Sandstone, post.Capacity - 1));
        Assert.Equal(5, miner.AddToInventory(ResourceName.Sandstone, 5));

        Assert.True(miner.IsAtBuildingInteractionTile(post));

        Assert.False(miner.RunRoleState(MinerState.DepositInventory));

        Assert.Equal(4, miner.Inventory.Amount);
        Assert.Equal(CreatureActivity.WaitingForSlot, miner.Activity);
        Assert.Equal(MinerState.WaitForStorage, miner.MinerState);
        Assert.Equal(WorkerRoleFailureReason.NoStorage, miner.LastRoleFailure);
    }

    [Fact]
    public void MinerDepositInventory_PlaysCreatureDepositSoundOnlyWhenAccepted()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 6), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);
        Assert.Equal(1, miner.AddToInventory(ResourceName.Sandstone, 1));

        Assert.True(miner.IsAtBuildingInteractionTile(post));

        var depositCueCount = 0;
        session.AudioCueRequested += cue => depositCueCount += cue == GameAudioCue.CreatureDeposit ? 1 : 0;

        miner.RunRoleState(MinerState.DepositInventory);

        Assert.Equal(1, depositCueCount);
        Assert.False(miner.HasInventory());
    }

    [Fact]
    public void MinerSelectPost_WaitsOnAssignedExhaustedPost_WhenGlobalAssignmentsAreUnavailable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(26, 14, new GridPoint(10, 0));
        SetTileBase(cave, new GridPoint(3, 11), OreType.CHITINSTONE.Name);
        var exhaustedPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 7));
        var waitingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Waiting Miner", "miner");

        Assert.Equal(exhaustedPost.Capacity, exhaustedPost.Deposit(ResourceName.Chitinstone, exhaustedPost.Capacity));
        Assert.True(exhaustedPost.AssignmentsAvailable);
        Assert.False(cave.HasAvailableMiningPostAssignments);

        waitingMiner.SetAssignedBuilding(exhaustedPost);
        exhaustedPost.Assign(waitingMiner, null);

        Assert.False(waitingMiner.RunRoleState(MinerState.SelectPost));
        Assert.Same(exhaustedPost, waitingMiner.GetAssignedMiningPost());
        Assert.Null(waitingMiner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerSelectPost_ReleasesExhaustedAssignedPost_WhenGlobalAssignmentsResume()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(34, 14, new GridPoint(14, 0));
        SetTileBase(cave, new GridPoint(3, 11), OreType.CHITINSTONE.Name);
        var exhaustedPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 7));
        var waitingMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Waiting Miner", "miner");

        Assert.Equal(exhaustedPost.Capacity, exhaustedPost.Deposit(ResourceName.Chitinstone, exhaustedPost.Capacity));
        waitingMiner.SetAssignedBuilding(exhaustedPost);
        exhaustedPost.Assign(waitingMiner, null);

        SetTileBase(cave, new GridPoint(30, 11), OreType.CHITINSTONE.Name);
        var freshPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(28, 7));

        Assert.True(cave.HasAvailableMiningPostAssignments);

        waitingMiner.RunRoleState(MinerState.SelectPost);

        Assert.Same(freshPost, waitingMiner.GetAssignedMiningPost());
        Assert.NotNull(waitingMiner.LastMiningPostSelectionMetrics);
    }

    [Fact]
    public void MinerAcquireClaim_UsesClaimRouteWithoutPointRouteBfs()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        SetTileBase(cave, new GridPoint(14, 11), OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.RunRoleState(MinerState.AcquireClaim));

        var claim = miner.ActiveMiningClaim;
        Assert.True(claim.HasValue);
        var route = claim.Value.Route;
        Assert.NotNull(route);
        Assert.Equal(claim.Value.ApproachPoint.ToGridPoint(), route![^1]);
        Assert.Equal(claim.Value.TileKey, post.GetAssignment(miner));
        Assert.NotEmpty(miner.DesiredRoute);
        Assert.Equal(0, cave.PointRouteFieldCacheCount);
        Assert.Equal(RouteContinuationKind.None, miner.ActiveRouteContinuationKind);
    }

    [Fact]
    public void MinerAcquireClaim_CreatesAConcreteOreClaimAndReservation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 18, new GridPoint(12, 0));
        var fartherOre = new GridPoint(9, 9);
        var nearerOre = new GridPoint(18, 12);
        SetTileBase(cave, fartherOre, OreType.CHITINSTONE.Name);
        SetTileBase(cave, nearerOre, OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(12, 8));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(19, 12), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.RunRoleState(MinerState.AcquireClaim));

        Assert.Equal(nearerOre.ToString(), miner.PendingMineTileKey);
        Assert.Equal(nearerOre.ToString(), miner.ActiveMiningClaim?.TileKey);
        Assert.Equal(nearerOre.ToString(), post.GetAssignment(miner));

        Assert.True(
            miner.DesiredRoute.Count > 0 ||
            MiningStrikeSystem.CanMineReach(miner, nearerOre.ToString()));
    }

    [Fact]
    public void MinerMoveToClaim_RetargetsWhenReservedTileWasAlreadyMined()
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

        Assert.True(miner.RunRoleState(MinerState.AcquireClaim));

        var staleTargetKey = miner.PendingMineTileKey;
        Assert.Equal(firstOre.ToString(), staleTargetKey);
        Assert.Equal(staleTargetKey, post.GetAssignment(miner));

        Assert.True(session.MineTile(cave, staleTargetKey!, "test").TileDepleted);

        Assert.True(miner.RunRoleState(MinerState.MoveToClaim));
        Assert.Equal(secondOre.ToString(), miner.PendingMineTileKey);
        Assert.Equal(secondOre.ToString(), post.GetAssignment(miner));
        Assert.NotEqual(staleTargetKey, miner.PendingMineTileKey);
    }

    [Fact]
    public void MinerAcquireClaim_AllowsMultipleMinersOnSameMineableTarget()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var oreLocation = new GridPoint(14, 11);
        SetTileBase(cave, oreLocation, OreType.CHITINSTONE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 6));
        var firstMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "First Miner", "miner");
        var secondMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "Second Miner", "miner");
        firstMiner.SetAssignedBuilding(post);
        secondMiner.SetAssignedBuilding(post);
        post.Assign(firstMiner, null);
        post.Assign(secondMiner, null);

        Assert.True(firstMiner.RunRoleState(MinerState.AcquireClaim));
        Assert.True(secondMiner.RunRoleState(MinerState.AcquireClaim));

        Assert.Equal(oreLocation.ToString(), firstMiner.ActiveMiningClaim?.TileKey);
        Assert.Equal(firstMiner.ActiveMiningClaim?.TileKey, secondMiner.ActiveMiningClaim?.TileKey);
        Assert.Equal(firstMiner.ActiveMiningClaim?.TileKey, post.GetAssignment(firstMiner));
        Assert.Equal(secondMiner.ActiveMiningClaim?.TileKey, post.GetAssignment(secondMiner));
        Assert.True(post.AssignmentsAvailable);
    }

    [Fact]
    public void MinerMineClaim_AfterDepletingTile_ClaimsNextCompatibleTargetBeforeDepositingPartialInventory()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var firstWall = new GridPoint(10, 8);
        var secondWall = new GridPoint(11, 8);
        SetTileBase(cave, firstWall, "wall");
        SetTileBase(cave, secondWall, "wall");
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(9, 8), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.RunRoleState(MinerState.AcquireClaim));
        Assert.Equal(firstWall.ToString(), miner.PendingMineTileKey);

        var firstResult = miner.MineTile(firstWall.ToString());
        Assert.True(firstResult.TileDepleted);
        miner.RecordMiningStrikeResult(firstResult);

        Assert.True(miner.RunRoleState(MinerState.MineClaim));

        Assert.Equal(1, miner.Inventory.Amount);
        Assert.Equal(secondWall.ToString(), miner.PendingMineTileKey);
        Assert.Equal(secondWall.ToString(), post.GetAssignment(miner));
    }

    [Fact]
    public void MinerMineClaim_AfterDepletingTile_ContinuesToDifferentResourceWhenCapacityRemains()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var sandstoneWall = new GridPoint(10, 8);
        var malachiteOre = new GridPoint(11, 8);
        SetTileBase(cave, sandstoneWall, "wall");
        SetTileBase(cave, malachiteOre, OreType.MALACHITE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(9, 8), "Miner", "miner");
        miner.SetAssignedBuilding(post);
        post.Assign(miner, null);

        Assert.True(miner.RunRoleState(MinerState.AcquireClaim));
        Assert.Equal(sandstoneWall.ToString(), miner.PendingMineTileKey);

        var firstResult = miner.MineTile(sandstoneWall.ToString());
        Assert.True(firstResult.TileDepleted);
        miner.RecordMiningStrikeResult(firstResult);

        Assert.True(miner.RunRoleState(MinerState.MineClaim));

        Assert.Equal(malachiteOre.ToString(), miner.PendingMineTileKey);
        Assert.Equal(malachiteOre.ToString(), post.GetAssignment(miner));
        Assert.True(miner.ActiveMiningClaim.HasValue);
        Assert.True(post.AssignmentsAvailable);
    }

    [Fact]
    public void MiningClaimAllocator_FilteredClaim_DoesNotMarkPostUnavailableForOtherResources()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var malachiteOre = new GridPoint(11, 8);
        SetTileBase(cave, malachiteOre, OreType.MALACHITE.Name);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 8), "Miner", "miner");

        Assert.Null(MiningClaimAllocator.TryClaimNext(miner, post, ResourceName.Sandstone));
        Assert.True(post.AssignmentsAvailable);

        var claim = MiningClaimAllocator.TryClaimNext(miner, post);

        Assert.NotNull(claim);
        Assert.Equal(malachiteOre.ToString(), claim.Value.TileKey);
    }

    [Fact]
    public void MinerAcquireClaim_FortyMinersCanClaimWithoutUniqueApproachSlots()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(36, 30, new GridPoint(1, 1));
        var postLocation = new GridPoint(14, 12);
        var postCenter = new GridPoint(postLocation.X + 1, postLocation.Y + 1);
        var oreLocations = new List<GridPoint>(40);
        for (var y = 4; y < 24 && oreLocations.Count < 40; y++)
        {
            for (var x = 5; x < 27 && oreLocations.Count < 40; x++)
            {
                var location = new GridPoint(x, y);
                if (x >= postLocation.X && x < postLocation.X + 3 &&
                    y >= postLocation.Y && y < postLocation.Y + 3)
                {
                    continue;
                }

                if (GridPoint.SquaredDistance(location, postCenter) > 100)
                {
                    continue;
                }

                SetTileBase(cave, location, OreType.CHITINSTONE.Name);
                oreLocations.Add(location);
            }
        }

        Assert.Equal(40, oreLocations.Count);
        var post = TestWorldFactory.BuildMiningPost(cave, session, postLocation);
        var spawn = post.TileArray[0].Coordinates;
        var miners = new List<Trilobite>(40);
        for (var index = 0; index < 40; index++)
        {
            var miner = TestWorldFactory.SpawnTrilobite(cave, session, spawn, $"Miner {index}", "miner");
            miner.SetAssignedBuilding(post);
            post.Assign(miner, null);
            miners.Add(miner);
        }

        for (var index = 0; index < miners.Count; index++)
        {
            miners[index].RunRoleState(MinerState.AcquireClaim);
        }

        Assert.All(miners, miner => Assert.NotNull(miner.ActiveMiningClaim));
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
