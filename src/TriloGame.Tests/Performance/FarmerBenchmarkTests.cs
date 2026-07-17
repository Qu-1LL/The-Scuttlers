using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Diagnostics;
using System.Reflection;

namespace TriloGame.Tests.Performance;

[Collection(PerformanceBenchmarkCollection.Name)]
public sealed class FarmerBenchmarkTests
{
    private const int FarmerCount = 200;
    private const int LiveLikeFarmerCount = 200;
    private const double GoalTickMs = 10d;
    private const int WarmupTicks = 6;

    [Fact]
    public void BenchmarkScenario_CreatesBuiltFarmAnd200FarmerSwarm()
    {
        var (_, _, _, farm, farmers) = TestWorldFactory.CreateSessionWithFarmAndFarmers(FarmerCount);

        Assert.Equal(FarmerCount, farmers.Count);
        Assert.All(farmers, farmer => Assert.Equal("farmer", farmer.Assignment));
        Assert.NotEmpty(farm.TileArray);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void BenchmarkScenario_ReportsTickTimingFor200Farmers()
    {
        var (session, _, queen, farm, _) = TestWorldFactory.CreateSessionWithFarmAndFarmers(FarmerCount);
        var clock = new GameSimulationClockSystem();
        FreezeQueenBirths(queen);
        SuppressFarmHarvests(farm);

        clock.RunSingleTick(session);
        var setupTick = session.Runtime.TickProfiler.Last;
        for (var index = 0; index < WarmupTicks; index++)
        {
            clock.RunSingleTick(session);
        }

        clock.RunSingleTick(session);
        var snapshot = session.Runtime.TickProfiler.Last;

        Console.WriteLine($"Goal: {GoalTickMs:0.00} ms");
        Console.WriteLine($"Setup tick: {setupTick.TotalMs:0.00} ms");
        Console.WriteLine($"Measured steady tick: {snapshot.TotalMs:0.00} ms after {WarmupTicks} warmup ticks");
        Console.WriteLine($"Trilobites: {snapshot.TrilobiteCount}, Buildings: {snapshot.BuildingCount}");
        Console.WriteLine($"Breakdown => trilobites {snapshot.TrilobiteMoveMs:0.00} ms, buildings {snapshot.BuildingTickMs:0.00} ms, bfs {snapshot.TotalBfsMs:0.00} ms");
        Console.WriteLine($"GoalMet: {snapshot.TotalMs <= GoalTickMs}");

        Assert.Equal(FarmerCount, snapshot.TrilobiteCount);
        Assert.True(snapshot.TotalMs >= 0d);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void BenchmarkScenario_ReportsTickTimingFor200LiveLikeFarmers()
    {
        var (session, _, queen, _, _) = TestWorldFactory.CreateSessionWithFarmAndFarmers(LiveLikeFarmerCount);
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

        Console.WriteLine($"Live-like setup tick: {setupTick.TotalMs:0.00} ms");
        Console.WriteLine($"Live-like steady tick: {snapshot.TotalMs:0.00} ms");
        Console.WriteLine($"Live-like counts => trilobites {snapshot.TrilobiteCount}, buildings {snapshot.BuildingCount}");
        Console.WriteLine($"Live-like breakdown => trilobites {snapshot.TrilobiteMoveMs:0.00} ms, buildings {snapshot.BuildingTickMs:0.00} ms, bfs {snapshot.TotalBfsMs:0.00} ms");

        Assert.Equal(LiveLikeFarmerCount, snapshot.TrilobiteCount);
        Assert.True(snapshot.TotalMs >= 0d);
    }

    private static void FreezeQueenBirths(Queen queen)
    {
        var property = typeof(Queen).GetProperty(nameof(Queen.AlgaeQuota), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(queen, int.MaxValue / 4);
    }

    private static void SuppressFarmHarvests(AlgaeFarm farm)
    {
        typeof(AlgaeFarm)
            .GetField("<Period>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(farm, int.MaxValue / 4);
    }
}
