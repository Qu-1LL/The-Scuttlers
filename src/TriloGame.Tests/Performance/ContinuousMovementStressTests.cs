using System.Diagnostics;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Performance;

[Collection(PerformanceBenchmarkCollection.Name)]
public sealed class ContinuousMovementStressTests
{
    private const int TrilobiteCount = 200;
    private const int AntCount = 50;
    private const int WarmupTicks = 40;
    private const int MeasuredTicks = 30;

    [Fact]
    [Trait("Category", "Benchmark")]
    public void OpposingFlow_ReportsBudgets()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(70, 40, GridPoint.Zero);
        var creatures = new List<Creature>(TrilobiteCount + AntCount);
        SpawnFlowCreatures(session, cave, creatures);
        cave.RevealedTiles.Clear();
        session.Danger = false;

        var observer = new MovementPhaseObserver();
        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            TickRunner.RunTick(session, observer);
        }

        observer.ResetSamples();
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            TickRunner.RunTick(session, observer);
        }

        var movementP95 = Percentile95(observer.MovementMilliseconds);
        var fullTickP95 = Percentile95(observer.TickMilliseconds);
        var maximumTick = observer.TickMilliseconds.Max();
        var maximumMovementAllocation = observer.MovementAllocatedBytes.Max();
        Console.WriteLine($"Continuous movement p95: {movementP95:0.00} ms");
        Console.WriteLine($"Full tick p95/max: {fullTickP95:0.00}/{maximumTick:0.00} ms");
        Console.WriteLine($"Maximum movement allocation: {maximumMovementAllocation} bytes");

        Assert.Equal(TrilobiteCount, cave.GetTrilobiteList().Count);
        Assert.Equal(AntCount, cave.GetEnemyList().Count);

        if (string.Equals(Environment.GetEnvironmentVariable("TRILO_ENFORCE_PERF_BUDGETS"), "1", StringComparison.Ordinal))
        {
            Assert.Equal(0L, maximumMovementAllocation);
            Assert.True(movementP95 <= 4d, $"Movement p95 was {movementP95:0.00} ms.");
            Assert.True(fullTickP95 <= 10d, $"Full-tick p95 was {fullTickP95:0.00} ms.");
            Assert.True(maximumTick <= 20d, $"Maximum tick was {maximumTick:0.00} ms.");
        }
    }

    private static void SpawnFlowCreatures(
        GameSession session,
        Cave cave,
        ICollection<Creature> creatures)
    {
        for (var index = 0; index < TrilobiteCount; index++)
        {
            var start = FlowStart(index, offset: 0);
            var creature = TestWorldFactory.SpawnTrilobite(cave, session, start, $"Stress trilobite {index}");
            Assert.True(creature.NavigateTo(FlowDestination(start)));
            creatures.Add(creature);
        }

        for (var index = 0; index < AntCount; index++)
        {
            var start = FlowStart(index, offset: 1);
            var enemy = new Enemy($"Stress ant {index}", start, session);
            Assert.True(cave.Spawn(enemy, cave.GetTile(start)!));
            Assert.True(enemy.NavigateTo(FlowDestination(start)));
            creatures.Add(enemy);
        }
    }

    private static GridPoint FlowStart(int index, int offset)
    {
        return new GridPoint(5 + ((index % 20) * 3) + offset, 5 + ((index / 20) * 3) + offset);
    }

    private static GridPoint FlowDestination(GridPoint start)
    {
        return new GridPoint(67 - start.X, start.Y);
    }

    private static double Percentile95(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return ordered[(int)Math.Ceiling(ordered.Length * 0.95d) - 1];
    }

    private sealed class MovementPhaseObserver : ITickPhaseObserver
    {
        private long _tickStart;
        private long _phaseStart;
        private long _movementAllocatedStart;

        public List<double> MovementMilliseconds { get; } = [];

        public List<double> TickMilliseconds { get; } = [];

        public List<long> MovementAllocatedBytes { get; } = [];

        public void ResetSamples()
        {
            MovementMilliseconds.Clear();
            TickMilliseconds.Clear();
            MovementAllocatedBytes.Clear();
        }

        public void OnTickStarted(GameSession session)
        {
            _tickStart = Stopwatch.GetTimestamp();
        }

        public void OnPhaseStarted(TickPhase phase)
        {
            if (phase != TickPhase.CreatureMovement)
            {
                return;
            }

            _phaseStart = Stopwatch.GetTimestamp();
            _movementAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
        }

        public void OnPhaseCompleted(TickPhase phase)
        {
            if (phase != TickPhase.CreatureMovement)
            {
                return;
            }

            var allocatedEnd = GC.GetAllocatedBytesForCurrentThread();
            MovementMilliseconds.Add(Stopwatch.GetElapsedTime(_phaseStart).TotalMilliseconds);
            MovementAllocatedBytes.Add(allocatedEnd - _movementAllocatedStart);
        }

        public void OnTrilobiteMoveStarted(string assignment)
        {
        }

        public void OnTrilobiteMoveCompleted(string assignment)
        {
        }

        public void OnTickCompleted(GameSession session)
        {
            TickMilliseconds.Add(Stopwatch.GetElapsedTime(_tickStart).TotalMilliseconds);
        }
    }
}
