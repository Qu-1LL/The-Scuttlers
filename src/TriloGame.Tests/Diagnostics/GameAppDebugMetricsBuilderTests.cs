using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Tests.Diagnostics;

public sealed class GameAppDebugMetricsBuilderTests
{
    [Fact]
    public void BuildLines_ProducesRunStateAndPerformanceSections()
    {
        var snapshot = new GameAppDebugMetricsSnapshot(
            Paused: true,
            Danger: false,
            TickCount: 12,
            TickSpeedMs: 100d,
            ActiveBfsDebugField: "enemy",
            ShowRoleLabels: true,
            NoCostBuildPlacement: false,
            LastTick: new TickTimingSnapshot(
                5d,
                1d,
                1d,
                1d,
                1d,
                1d,
                1d,
                1d,
                2048,
                1,
                0,
                0,
                4,
                2,
                3,
                NavigationTickMetrics.Empty,
                new RoleTimingSnapshot(0.5d, 1),
                new RoleTimingSnapshot(0.6d, 1),
                new RoleTimingSnapshot(0.7d, 1),
                new RoleTimingSnapshot(0.8d, 1),
                true),
            AverageTick: new TickTimingSnapshot(
                4d,
                0.5d,
                0.5d,
                0.5d,
                0.5d,
                0.5d,
                0.5d,
                0.5d,
                1024,
                0,
                0,
                0,
                4,
                2,
                3,
                NavigationTickMetrics.Empty,
                new RoleTimingSnapshot(0.4d, 1),
                new RoleTimingSnapshot(0.5d, 1),
                new RoleTimingSnapshot(0.6d, 1),
                new RoleTimingSnapshot(0.7d, 1),
                true),
            AverageMinerMsPerTrilobite: 0.4d,
            AverageBuilderMsPerTrilobite: 0.5d,
            AverageFarmerMsPerTrilobite: 0.6d,
            AverageFighterMsPerTrilobite: 0.7d);

        var lines = GameAppDebugMetricsBuilder.BuildLines(snapshot);

        Assert.Contains("RUN STATE", lines);
        Assert.Contains("PERFORMANCE", lines);
        Assert.Contains(lines, line => line.Contains("BFS View: enemy"));
        Assert.Contains(lines, line => line.Contains("Miner role: avg 0.40 ms/tri"));
        Assert.Contains(lines, line => line.Contains("Stats: Alloc 2.0 KB"));
    }
}
