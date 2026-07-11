using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class MiningPostNavigationCacheTests
{
    [Fact]
    public void MovementCache_ReusesMiningPostFieldAcrossUnitsAndTicks_WhenStillValid()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var first = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "First", "miner");
        var second = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 11), "Second", "builder");

        session.MiningPostMovementTelemetry.Reset();

        var initialPath = first.BuildNavigationPathToBuilding(post);
        Assert.NotNull(initialPath);
        Assert.True(first.NavigateToBuilding(post));
        var reusedPath = second.BuildNavigationPathToBuilding(post);

        Assert.NotNull(reusedPath);
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheMisses);
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheRebuildCount);
        Assert.Equal(2, session.MiningPostMovementTelemetry.CacheHits);
        Assert.Empty(first.GetQueuedPathPreview());
    }

    [Fact]
    public void MovementCache_DoesNotRebuildOnStructuralChangeUntilMoveFailure()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Tester", "miner");

        var initialPath = trilobite.BuildNavigationPathToBuilding(post);
        Assert.NotNull(initialPath);
        session.MiningPostMovementTelemetry.Reset();

        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(24, 11)));

        var stalePath = trilobite.BuildNavigationPathToBuilding(post);

        Assert.NotNull(stalePath);
        Assert.Equal(0, session.MiningPostMovementTelemetry.CacheMisses);
        Assert.Equal(0, session.MiningPostMovementTelemetry.CacheRebuildCount);
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheHits);
        Assert.True(cave.TopologyVersion > 0);
    }

    [Fact]
    public void NavigateToBuilding_UsesSingleBfsStepWithoutQueuedPreview()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Tester", "miner");

        session.MiningPostMovementTelemetry.Reset();

        var startingLocation = trilobite.Location;
        Assert.True(trilobite.NavigateToBuilding(post));

        Assert.NotEqual(startingLocation, trilobite.Location);
        Assert.Empty(trilobite.GetQueuedPathPreview());
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheMisses);
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheRebuildCount);
    }

    [Fact]
    public void SelectionGraphCounter_RemainsDeterministicAcrossFallbackSelections()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(27, 12, new GridPoint(12, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(20, 6));
        leftPost.Deposit(ResourceName.Sandstone, leftPost.Capacity);
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");

        session.MiningPostMovementTelemetry.Reset();

        var firstSelection = builder.SelectMiningPostForInventoryDeposit();
        var secondSelection = builder.SelectMiningPostForInventoryDeposit();

        Assert.Same(rightPost, firstSelection);
        Assert.Same(firstSelection, secondSelection);
        Assert.Equal(2, session.MiningPostMovementTelemetry.SelectionGraphBfsCount);
    }
}
