using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class GameSimulationClockSystem
{
    private readonly TickProfilingObserver _tickProfilingObserver = new();

    public bool IsPaused { get; set; }

    public double TickSpeedMs { get; set; } = GameConstants.TickSpeedFast;

    public double TickAccumulatorMs { get; set; }

    public void ResetToDefaults(bool paused = false, double tickSpeedMs = GameConstants.TickSpeedFast)
    {
        IsPaused = paused;
        TickSpeedMs = tickSpeedMs;
        TickAccumulatorMs = 0d;
    }

    public void RunSingleTick(GameSession session)
    {
        TickRunner.RunTick(session, _tickProfilingObserver);
    }

    public int Advance(GameSession session, double elapsedMs, Func<bool>? shouldStop = null)
    {
        if (IsPaused)
        {
            return 0;
        }

        TickAccumulatorMs += elapsedMs;
        var executedTicks = 0;
        while (TickAccumulatorMs >= TickSpeedMs)
        {
            TickRunner.RunTick(session, _tickProfilingObserver);
            TickAccumulatorMs -= TickSpeedMs;
            executedTicks++;

            if (shouldStop is not null && shouldStop())
            {
                break;
            }
        }

        return executedTicks;
    }
}
