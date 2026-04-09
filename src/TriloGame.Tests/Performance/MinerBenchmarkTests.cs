using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Diagnostics;
using System.Reflection;

namespace TriloGame.Tests.Performance;

[Collection(PerformanceBenchmarkCollection.Name)]
public sealed class MinerBenchmarkTests
{
    private const int LiveLikeMinerCount = 200;
    private const int WarmupTicks = 6;

    [Fact]
    [Trait("Category", "Benchmark")]
    public void BenchmarkScenario_ReportsTickTimingFor200LiveLikeMiners()
    {
        var (session, _, queen, _, _) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(LiveLikeMinerCount);
        var clock = new GameSimulationClockSystem();
        FreezeQueenBirths(queen);

        clock.RunSingleTick(session);
        var setupTick = session.Runtime.TickProfiler.Last;
        for (var index = 0; index < WarmupTicks; index++)
        {
            clock.RunSingleTick(session);
        }

        clock.RunSingleTick(session);
        var snapshot = session.Runtime.TickProfiler.Last;

        Console.WriteLine($"Miner setup tick: {setupTick.TotalMs:0.00} ms");
        Console.WriteLine($"Miner steady tick: {snapshot.TotalMs:0.00} ms");
        Console.WriteLine($"Miner counts => trilobites {snapshot.TrilobiteCount}, buildings {snapshot.BuildingCount}");
        Console.WriteLine($"Miner breakdown => trilobites {snapshot.TrilobiteMoveMs:0.00} ms, buildings {snapshot.BuildingTickMs:0.00} ms, bfs {snapshot.TotalBfsMs:0.00} ms");

        Assert.Equal(LiveLikeMinerCount, snapshot.TrilobiteCount);
        Assert.True(snapshot.TotalMs >= 0d);
    }

    private static void FreezeQueenBirths(Queen queen)
    {
        var property = typeof(Queen).GetProperty(nameof(Queen.AlgaeQuota), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(queen, int.MaxValue / 4);
    }
}
