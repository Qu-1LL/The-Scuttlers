using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Diagnostics;

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
    public void RunTick_RecordsNavigationInstrumentationForWorkerMovement()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var clock = new GameSimulationClockSystem();

        clock.RunSingleTick(bootstrap.Session);

        var navigation = bootstrap.Session.Runtime.TickProfiler.Last.Navigation;
        Assert.True(navigation.BuildingPathRequestCount > 0 || navigation.PointPathRequestCount > 0);
        Assert.True(navigation.BuildPathFromFieldCallCount > 0 || navigation.BuildPointBfsFieldCallCount > 0);
    }

    [Fact]
    public void DescribeDominantWork_ReportsSlowTickCauseForDominantPhase()
    {
        var snapshot = new TickTimingSnapshot(
            128d,
            12d,
            81d,
            8d,
            10d,
            6d,
            0L,
            0,
            0,
            0,
            37,
            4,
            6);

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
            21d,
            3d,
            2d,
            0L,
            0,
            0,
            0,
            12,
            1,
            5);

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
            9d,
            7d,
            5d,
            0L,
            0,
            0,
            0,
            48,
            3,
            8);

        var description = snapshot.DescribeDominantWorkShort();

        Assert.Contains("Slow:", description);
        Assert.Contains("tri AI/move", description);
        Assert.Contains("74.00 ms", description);
    }
}
