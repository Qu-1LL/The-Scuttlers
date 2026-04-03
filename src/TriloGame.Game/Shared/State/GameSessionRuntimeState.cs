using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Game.Shared.State;

public sealed class GameSessionRuntimeState
{
    private int _nextDebugEnemyId = 1;

    public TickProfiler TickProfiler { get; } = new();

    public bool FreezeOpalProgression { get; set; }

    public bool DisableEnemySpawns { get; set; }

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
}
