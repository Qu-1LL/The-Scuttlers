using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Game.Shared.State;

public sealed class GameSessionRuntimeState
{
    private int _nextDebugEnemyId = 1;
    private int _nextDebugTrilobiteId = 1;

    public TickProfiler TickProfiler { get; } = new();

    public double CurrentTickSpeedMs { get; set; } = GameConstants.TickSpeedFast;

    public double RoundSpawnWindowDurationMs { get; set; } = GameConstants.RoundSpawnWindowDurationMs;

    public bool FreezeOpalProgression { get; set; }

    public bool DisableEnemySpawns { get; set; } = true;

    public bool NoCostBuildPlacement { get; set; }

    public List<ProjectileFlight> ActiveProjectileFlights { get; } = [];

    // Preview the next generated debug enemy id without consuming it.
    public int PeekNextDebugEnemyId()
    {
        return _nextDebugEnemyId;
    }

    // Allocate and consume the next debug enemy id.
    public int AllocateDebugEnemyId()
    {
        return _nextDebugEnemyId++;
    }

    // Reset the debug enemy id counter while keeping it at a valid positive value.
    public void ResetDebugEnemyIds(int startAt = 1)
    {
        _nextDebugEnemyId = System.Math.Max(1, startAt);
    }

    // Preview the next generated debug trilobite id without consuming it.
    public int PeekNextDebugTrilobiteId()
    {
        return _nextDebugTrilobiteId;
    }

    // Allocate and consume the next debug trilobite id.
    public int AllocateDebugTrilobiteId()
    {
        return _nextDebugTrilobiteId++;
    }

    // Reset the debug trilobite id counter while keeping it at a valid positive value.
    public void ResetDebugTrilobiteIds(int startAt = 1)
    {
        _nextDebugTrilobiteId = System.Math.Max(1, startAt);
    }
}
