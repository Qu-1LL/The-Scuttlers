using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Game.Shared.State;

public sealed class GameSessionRuntimeState
{
    private int _nextDebugEnemyId = 1;
    private int _nextDebugTrilobiteId = 1;

    public TickProfiler TickProfiler { get; } = new();

    public double CurrentTickSpeedMs { get; set; } = GameConstants.TickSpeedFast;

    public bool FreezeOpalProgression { get; set; }

    public bool DisableEnemySpawns { get; set; } = true;

    public bool NoCostBuildPlacement { get; set; }

    public List<ProjectileFlight> ActiveProjectileFlights { get; } = [];

    public int PeekNextDebugEnemyId()
    {
        return _nextDebugEnemyId;
    }

    public int AllocateDebugEnemyId()
    {
        return _nextDebugEnemyId++;
    }

    public void ResetDebugEnemyIds(int startAt = 1)
    {
        _nextDebugEnemyId = System.Math.Max(1, startAt);
    }

    public int PeekNextDebugTrilobiteId()
    {
        return _nextDebugTrilobiteId;
    }

    public int AllocateDebugTrilobiteId()
    {
        return _nextDebugTrilobiteId++;
    }

    public void ResetDebugTrilobiteIds(int startAt = 1)
    {
        _nextDebugTrilobiteId = System.Math.Max(1, startAt);
    }
}
