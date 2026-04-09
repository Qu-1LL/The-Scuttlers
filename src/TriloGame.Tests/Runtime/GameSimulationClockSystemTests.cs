using TriloGame.Game.Core.Constants;
using TriloGame.Game.Runtime.Systems;

namespace TriloGame.Tests.Runtime;

public sealed class GameSimulationClockSystemTests
{
    [Fact]
    public void Advance_WaitsForFullTickBudgetBeforeRunningSimulation()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var system = new GameSimulationClockSystem();
        system.ResetToDefaults();

        system.Advance(bootstrap.Session, GameConstants.TickSpeedFast - 1d);

        Assert.Equal(0, bootstrap.Session.TickCount);

        system.Advance(bootstrap.Session, 1d);

        Assert.Equal(1, bootstrap.Session.TickCount);
    }

    [Fact]
    public void Advance_StopsWhenStopConditionReturnsTrue()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var system = new GameSimulationClockSystem();
        system.ResetToDefaults();
        var stopChecks = 0;

        var executed = system.Advance(
            bootstrap.Session,
            GameConstants.TickSpeedFast * 3d,
            () =>
            {
                stopChecks++;
                return stopChecks == 1;
            });

        Assert.Equal(1, executed);
        Assert.Equal(1, bootstrap.Session.TickCount);
    }

    [Fact]
    public void RunSingleTick_RecordsProfilerSnapshotThroughRuntimeClock()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var system = new GameSimulationClockSystem();

        system.RunSingleTick(bootstrap.Session);

        Assert.Equal(1, bootstrap.Session.Runtime.TickProfiler.SampleCount);
        Assert.True(bootstrap.Session.Runtime.TickProfiler.Last.TotalMs >= 0d);
    }
}
