using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Game.Shared.State;

public sealed class GameSessionRuntimeState
{
    public const double DamageFlashDurationMs = 150d;
    public const float DamageFlashOpacityMultiplier = 1.35f;
    private int _nextDebugEnemyId = 1;
    private int _nextDebugTrilobiteId = 1;

    public TickProfiler TickProfiler { get; } = new();

    public double CurrentTickSpeedMs { get; set; } = GameConstants.TickSpeedFast;

    public double RoundSpawnWindowDurationMs { get; set; } = GameConstants.RoundSpawnWindowDurationMs;

    public bool DisableEnemySpawns { get; set; } = true;

    public bool NoCostBuildPlacement { get; set; }

    public bool AllowManualMining { get; set; }

    public bool ShowHitboxes { get; set; }

    public List<ProjectileFlight> ActiveProjectileFlights { get; } = [];

    private readonly Dictionary<int, double> _damageFlashRemainingMs = [];

    public void RestartDamageFlash(int creatureId)
    {
        _damageFlashRemainingMs[creatureId] = DamageFlashDurationMs;
    }

    public float GetDamageFlashAlpha(int creatureId)
    {
        return _damageFlashRemainingMs.TryGetValue(creatureId, out var remaining)
            ? (float)System.Math.Clamp(
                (remaining / DamageFlashDurationMs) * DamageFlashOpacityMultiplier,
                0d,
                1d)
            : 0f;
    }

    public void AdvancePresentation(double elapsedMs)
    {
        if (elapsedMs <= 0d || _damageFlashRemainingMs.Count == 0)
        {
            return;
        }

        var ids = _damageFlashRemainingMs.Keys.ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            var id = ids[index];
            var remaining = _damageFlashRemainingMs[id] - elapsedMs;
            if (remaining <= 0d)
            {
                _damageFlashRemainingMs.Remove(id);
            }
            else
            {
                _damageFlashRemainingMs[id] = remaining;
            }
        }
    }

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
