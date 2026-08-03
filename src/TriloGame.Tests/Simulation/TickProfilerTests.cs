using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Simulation;

public sealed class TickProfilerTests
{
    [Fact]
    public void RunTick_RecordsPhaseTimingAndEntityCounts()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var clock = new GameSimulationClockSystem();

        clock.RunSingleTick(session);

        Assert.Equal(1, session.Runtime.TickProfiler.SampleCount);
        Assert.True(session.Runtime.TickProfiler.Last.TotalMs >= 0d);
        Assert.True(session.Runtime.TickProfiler.Last.BuildingCount >= 1);
        Assert.Equal(0, session.Runtime.TickProfiler.Last.EnemyCount);
        Assert.Equal(session.Runtime.TickProfiler.Last.BuildingCount, session.Runtime.TickProfiler.Average.BuildingCount);
    }

    [Fact]
    public void RunTick_RecordsNavigationInstrumentationForQueuedPointNavigation()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var spawnTile = queen.GetFeedTiles().First(tile => tile.CreatureFits());
        var start = spawnTile.Coordinates;
        var destination = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && tile.Key != start.ToString())
            .OrderByDescending(tile => GridPoint.ManhattanDistance(GridPoint.Parse(tile.Key), start))
            .Select(tile => GridPoint.Parse(tile.Key))
            .First();
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, start, "Profiler");
        var clock = new GameSimulationClockSystem();

        trilobite.EnqueueTask(CreatureTask.NavigateTo(destination));

        clock.RunSingleTick(session);

        var navigation = session.Runtime.TickProfiler.Last.Navigation;
        Assert.True(navigation.PointPathRequestCount > 0);
        Assert.Equal(0, navigation.BuildPathFromFieldCallCount);
        Assert.True(navigation.BuildPointBfsFieldCallCount > 0);
        Assert.True(navigation.QueuedNavigationSteps > 0);
        Assert.Equal(start, trilobite.Location);
        Assert.NotEmpty(trilobite.DesiredRoute);
    }

    [Fact]
    public void RunTick_RecordsRoleTimingSnapshotsForStarterAssignments()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var clock = new GameSimulationClockSystem();

        clock.RunSingleTick(bootstrap.Session);

        var last = bootstrap.Session.Runtime.TickProfiler.Last;
        Assert.True(last.RoleTimingsCaptured);
        Assert.Equal(1, last.MinerTiming.Count);
        Assert.Equal(1, last.BuilderTiming.Count);
        Assert.Equal(1, last.FarmerTiming.Count);
        Assert.Equal(1, last.FighterTiming.Count);
        Assert.True(last.MinerTiming.TotalMs >= 0d);
        Assert.True(last.BuilderTiming.TotalMs >= 0d);
        Assert.True(last.FarmerTiming.TotalMs >= 0d);
        Assert.True(last.FighterTiming.TotalMs >= 0d);
    }

    [Fact]
    public void RunTick_WithoutProfilingObserver_DoesNotRecordProfilerSnapshot()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();

        TickRunner.RunTick(session);

        Assert.Equal(0, session.Runtime.TickProfiler.SampleCount);
        Assert.Equal(TickTimingSnapshot.Empty, session.Runtime.TickProfiler.Last);
    }

    [Fact]
    public void DescribeDominantWork_ReportsSlowTickCauseForDominantPhase()
    {
        var snapshot = new TickTimingSnapshot(
            128d,
            12d,
            81d,
            0d,
            0d,
            8d,
            10d,
            6d,
            0L,
            0,
            0,
            0,
            37,
            4,
            6,
            NavigationTickMetrics.Empty);

        var description = snapshot.DescribeDominantWork();

        Assert.Contains("Slow tick cause", description);
        Assert.Contains("iterating trilobites", description);
        Assert.Contains("81.00 ms", description);
    }

    [Fact]
    public void DescribeDominantWork_ReportsBfsRecalculationWhenBfsDominates()
    {
        var snapshot = new TickTimingSnapshot(
            42d,
            4d,
            9d,
            0d,
            0d,
            21d,
            3d,
            2d,
            0L,
            0,
            0,
            0,
            12,
            1,
            5,
            NavigationTickMetrics.Empty);

        var description = snapshot.DescribeDominantWork();

        Assert.Contains("Dominant tick work", description);
        Assert.Contains("recalculating colony BFS/path fields", description);
    }

    [Fact]
    public void DescribeDominantWorkShort_UsesCompactStableDebugLabel()
    {
        var snapshot = new TickTimingSnapshot(
            118d,
            6d,
            74d,
            0d,
            0d,
            9d,
            7d,
            5d,
            0L,
            0,
            0,
            0,
            48,
            3,
            8,
            NavigationTickMetrics.Empty);

        var description = snapshot.DescribeDominantWorkShort();

        Assert.Contains("Slow:", description);
        Assert.Contains("tri AI/move", description);
        Assert.Contains("74.00 ms", description);
    }

    [Fact]
    public void Record_AverageNavigationMetrics_UsesRecordedSamples()
    {
        var profiler = new TickProfiler();
        var firstNavigation = new NavigationTickMetrics(
            2,
            1,
            10L,
            20L,
            3,
            0.5d,
            30L,
            4,
            0.75d,
            40L,
            0,
            0d,
            0L,
            0,
            2,
            12,
            8,
            1,
            6);
        var secondNavigation = new NavigationTickMetrics(
            4,
            3,
            30L,
            40L,
            5,
            1.5d,
            50L,
            6,
            2.75d,
            60L,
            2,
            1d,
            10L,
            20,
            4,
            18,
            10,
            3,
            8);

        profiler.Record(new TickTimingSnapshot(
            18d,
            1d,
            5d,
            0d,
            0d,
            2d,
            3d,
            4d,
            100L,
            1,
            0,
            0,
            5,
            0,
            1,
            firstNavigation));
        profiler.Record(new TickTimingSnapshot(
            16d,
            2d,
            4d,
            0d,
            0d,
            1d,
            2d,
            5d,
            300L,
            2,
            1,
            0,
            6,
            1,
            2,
            secondNavigation));

        Assert.Equal(17d, profiler.Average.TotalMs);
        Assert.Equal(3, profiler.Average.Navigation.PointPathRequestCount);
        Assert.Equal(2, profiler.Average.Navigation.BuildingPathRequestCount);
        Assert.Equal(1d, profiler.Average.Navigation.BuildPathFromFieldMs);
        Assert.Equal(1, profiler.Average.Navigation.DroppedResourceScanCount);
        Assert.Equal(7, profiler.Average.Navigation.QueuedNavigationSteps);
        Assert.Equal(9, profiler.Average.Navigation.MaxPathLength);
        Assert.Equal(200L, profiler.Average.AllocatedBytes);
        Assert.Equal(6, profiler.Average.TrilobiteCount);
    }
}
