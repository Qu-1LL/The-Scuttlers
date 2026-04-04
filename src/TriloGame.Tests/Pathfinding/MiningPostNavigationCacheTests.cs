using TriloGame.Game.Core.Buildings;
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
        first.Move();
        var reusedPath = second.BuildNavigationPathToBuilding(post);

        Assert.NotNull(reusedPath);
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheMisses);
        Assert.Equal(1, session.MiningPostMovementTelemetry.CacheRebuildCount);
        Assert.Equal(2, session.MiningPostMovementTelemetry.CacheHits);
    }

    [Fact]
    public void MovementCache_InvalidatesOnStructuralChange()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Tester", "miner");

        session.MiningPostMovementTelemetry.Reset();

        var initialPath = trilobite.BuildNavigationPathToBuilding(post);
        Assert.NotNull(initialPath);

        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(9, 8)));

        var rebuiltPath = trilobite.BuildNavigationPathToBuilding(post);

        Assert.NotNull(rebuiltPath);
        Assert.Equal(2, session.MiningPostMovementTelemetry.CacheMisses);
        Assert.Equal(2, session.MiningPostMovementTelemetry.CacheRebuildCount);
        Assert.Equal(0, session.MiningPostMovementTelemetry.CacheHits);
        Assert.True(cave.TopologyVersion > 0);
    }

    [Fact]
    public void MovementCache_StalePathFailureInvalidatesAndReroutes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Tester", "miner");

        session.MiningPostMovementTelemetry.Reset();

        Assert.True(trilobite.NavigateToBuilding(post));
        var queuedPath = trilobite.GetQueuedPathPreview();
        Assert.True(queuedPath.Count > 1);

        var blockedStep = queuedPath[1];
        var blockerLocation = GetBlockingStorageTopLeft(trilobite.Location, blockedStep);
        Assert.True(cave.Build(new Storage(session), blockerLocation));

        var startingLocation = trilobite.Location;
        trilobite.Move();
        var reroutedPreview = trilobite.GetQueuedPathPreview();

        Assert.Equal(startingLocation, trilobite.Location);
        Assert.Equal(1, session.MiningPostMovementTelemetry.StalePathInvalidationCount);
        Assert.Equal(2, session.MiningPostMovementTelemetry.CacheMisses);
        Assert.Equal(2, session.MiningPostMovementTelemetry.CacheRebuildCount);
        Assert.NotEmpty(reroutedPreview);
        Assert.DoesNotContain(blockedStep, reroutedPreview.Skip(1).Take(1));
    }

    [Fact]
    public void SelectionGraphCounter_RemainsDeterministicAcrossFallbackSelections()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(27, 12, new GridPoint(12, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(20, 6));
        leftPost.Deposit("Sandstone", leftPost.Capacity);
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");

        session.MiningPostMovementTelemetry.Reset();

        var firstSelection = builder.SelectMiningPostForInventoryDeposit();
        var secondSelection = builder.SelectMiningPostForInventoryDeposit();

        Assert.Same(rightPost, firstSelection);
        Assert.Same(firstSelection, secondSelection);
        Assert.Equal(2, session.MiningPostMovementTelemetry.SelectionGraphBfsCount);
    }

    private static GridPoint GetBlockingStorageTopLeft(GridPoint current, GridPoint next)
    {
        if (next.X == current.X + 1 && next.Y == current.Y)
        {
            return next;
        }

        if (next.X == current.X - 1 && next.Y == current.Y)
        {
            return new GridPoint(next.X - 1, next.Y);
        }

        if (next.X == current.X && next.Y == current.Y + 1)
        {
            return next;
        }

        if (next.X == current.X && next.Y == current.Y - 1)
        {
            return new GridPoint(next.X, next.Y - 1);
        }

        throw new InvalidOperationException($"Cannot derive a blocking storage location from {current} -> {next}.");
    }
}
