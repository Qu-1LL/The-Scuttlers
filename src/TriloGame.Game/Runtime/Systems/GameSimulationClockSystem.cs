using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class GameSimulationClockSystem : IDisposable
{
    private readonly TickProfilingObserver _tickProfilingObserver = new();
    private readonly ProjectileFlightSystem _projectileFlights = new();
    private readonly BuildingBfsFieldMaintenanceSystem _buildingBfsMaintenance = new();

    public bool IsPaused { get; set; }

    public double TickSpeedMs { get; set; } = GameConstants.TickSpeedFast;

    public double TickAccumulatorMs { get; set; }

    // Restore pause state, tick speed, and accumulated budget to their baseline values.
    public void ResetToDefaults(bool paused = false, double tickSpeedMs = GameConstants.TickSpeedFast)
    {
        IsPaused = paused;
        TickSpeedMs = tickSpeedMs;
        TickAccumulatorMs = 0d;
    }

    // Advance projectile travel and then run exactly one deterministic simulation tick.
    public void RunSingleTick(GameSession session, Action<GameSession>? afterTick = null)
    {
        session.Runtime.CurrentTickSpeedMs = TickSpeedMs;
        _projectileFlights.Advance(session, TickSpeedMs);
        TickRunner.RunTick(session, _tickProfilingObserver);
        _buildingBfsMaintenance.Update(session);
        afterTick?.Invoke(session);
    }

    // Consume elapsed wall time into projectile motion and fixed-step simulation ticks.
    public int Advance(GameSession session, double elapsedMs, Func<bool>? shouldStop = null, Action<GameSession>? afterTick = null)
    {
        if (IsPaused)
        {
            _buildingBfsMaintenance.Update(session, isPaused: true);
            return 0;
        }

        session.Runtime.CurrentTickSpeedMs = TickSpeedMs;
        var executedTicks = 0;
        var remainingElapsedMs = Math.Max(0d, elapsedMs);
        // Spend elapsed time in fixed-step chunks so draw cadence never changes sim behavior.
        while (remainingElapsedMs > 0d)
        {
            var timeToNextTick = Math.Max(0d, TickSpeedMs - TickAccumulatorMs);
            var stepMs = timeToNextTick <= 0d
                ? remainingElapsedMs
                : Math.Min(remainingElapsedMs, timeToNextTick);
            _projectileFlights.Advance(session, stepMs);
            TickAccumulatorMs += stepMs;
            remainingElapsedMs -= stepMs;

            if (TickAccumulatorMs < TickSpeedMs)
            {
                continue;
            }

            TickRunner.RunTick(session, _tickProfilingObserver);
            TickAccumulatorMs -= TickSpeedMs;
            executedTicks++;
            _buildingBfsMaintenance.Update(session, isPaused: false);
            afterTick?.Invoke(session);

            if (shouldStop is not null && shouldStop())
            {
                break;
            }
        }

        return executedTicks;
    }

    public void Dispose()
    {
        _buildingBfsMaintenance.Dispose();
    }
}
