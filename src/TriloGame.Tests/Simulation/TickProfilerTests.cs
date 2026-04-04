using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Simulation;

public sealed class TickProfilerTests
{
    [Fact]
    public void RunTick_RecordsPhaseTimingAndEntityCounts()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();

        TickRunner.RunTick(session);

        Assert.Equal(1, session.TickProfiler.SampleCount);
        Assert.True(session.TickProfiler.Last.TotalMs >= 0d);
        Assert.True(session.TickProfiler.Last.BuildingCount >= 1);
        Assert.Equal(0, session.TickProfiler.Last.EnemyCount);
        Assert.Equal(session.TickProfiler.Last.BuildingCount, session.TickProfiler.Average.BuildingCount);
    }

    [Fact]
    public void RunTick_CapturesRoleTimingOnlyWhenRequested()
    {
        var (session, _, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        trilobite.Assignment = "miner";

        TickRunner.RunTick(session);
        Assert.False(session.TickProfiler.Last.RoleTimingsCaptured);
        Assert.Equal(RoleTimingSnapshot.Empty, session.TickProfiler.Last.MinerTiming);

        TickRunner.RunTick(session, captureRoleTimings: true);
        Assert.True(session.TickProfiler.Last.RoleTimingsCaptured);
        Assert.Equal(1, session.TickProfiler.Last.MinerTiming.Count);
        Assert.True(session.TickProfiler.Last.MinerTiming.TotalMs >= 0d);
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
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            false,
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
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            false,
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
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            false,
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

    [Fact]
    public void Record_AverageRoleTimingPerTrilobite_UsesOnlyCapturedRoleTimingSamples()
    {
        var profiler = new TickProfiler();

        profiler.Record(new TickTimingSnapshot(
            18d,
            0d,
            5d,
            0d,
            0d,
            1d,
            new RoleTimingSnapshot(9d, 3),
            new RoleTimingSnapshot(8d, 2),
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            true,
            0L,
            0,
            0,
            0,
            5,
            0,
            1));
        profiler.Record(new TickTimingSnapshot(
            16d,
            0d,
            4d,
            0d,
            0d,
            1d,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            RoleTimingSnapshot.Empty,
            false,
            0L,
            0,
            0,
            0,
            5,
            0,
            1));

        Assert.Equal(3d, profiler.AverageMinerMsPerTrilobite);
        Assert.Equal(4d, profiler.AverageBuilderMsPerTrilobite);
        Assert.Equal(0d, profiler.AverageFarmerMsPerTrilobite);
        Assert.Equal(0d, profiler.AverageFighterMsPerTrilobite);
    }
}
