using TriloGame.Game.Core.Buildings;
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
        leftPost.Deposit("Sandstone", 25);
        rightPost.Deposit("Sandstone", 25);

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");
        var scaffold = new Scaffolding(session, new Barracks(session));

        var supplyOption = builder.GetBuilderSupplyOptionForScaffold(scaffold);

        Assert.NotNull(supplyOption);
        Assert.Same(leftPost, supplyOption.Value.Post);
        Assert.Equal("Sandstone", supplyOption.Value.ResourceType);
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
        leftPost.Deposit("Sandstone", leftPost.Capacity);

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");

        var selectedPost = builder.SelectMiningPostForInventoryDeposit();

        Assert.Same(rightPost, selectedPost);
        Assert.NotSame(leftPost, selectedPost);

        var metrics = builder.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("builder-deposit", metrics!.Purpose);
        Assert.Equal(2, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.True(metrics.UsedAdjacencyFallback);
    }

    [Fact]
    public void MinerSelection_FallsBackToAdjacentPost_WhenNearestHasNoMineableWork()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(33, 12, new GridPoint(15, 0));
        SetTileBase(cave, new GridPoint(30, 10), "Sandstone");
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
        Assert.True(metrics.UsedAdjacencyFallback);
    }

    [Fact]
    public void MinerSelection_ReusesAssignedPost_WhenItRemainsValid()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(33, 12, new GridPoint(15, 0));
        SetTileBase(cave, new GridPoint(2, 10), "Sandstone");
        SetTileBase(cave, new GridPoint(30, 10), "Sandstone");
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
        SetTileBase(cave, new GridPoint(2, 10), "Sandstone");
        SetTileBase(cave, new GridPoint(30, 10), "Sandstone");
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
        validPost.Deposit("Sandstone", 25);

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 9), "Builder", "builder");
        var scaffold = new Scaffolding(session, new Barracks(session));

        var supplyOption = builder.GetBuilderSupplyOptionForScaffold(scaffold);

        Assert.NotNull(supplyOption);
        Assert.Same(validPost, supplyOption.Value.Post);

        var metrics = builder.LastMiningPostSelectionMetrics;
        Assert.NotNull(metrics);
        Assert.Equal("builder-supply", metrics!.Purpose);
        Assert.Equal(3, metrics.CandidateCount);
        Assert.Equal(0, metrics.FullScanFallbackCount);
        Assert.True(metrics.UsedAdjacencyFallback);
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

    private static void SetTileBase(TriloGame.Game.Core.World.Cave cave, GridPoint location, string tileBase)
    {
        var tile = cave.GetTile(location.ToString())
            ?? throw new InvalidOperationException($"No tile exists at {location}.");
        tile.SetBase(tileBase);
    }
}
