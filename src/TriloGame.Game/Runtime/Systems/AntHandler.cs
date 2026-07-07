using System.Diagnostics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class AntHandler
{
    private readonly IAntHoleSpawner _antHoleSpawner;
    private readonly List<ScheduledAntSpawn> _scheduledSpawns = [];
    private readonly Dictionary<int, HashSet<Enemy>> _spawnedAntsByRound = [];
    private readonly Dictionary<int, int> _remainingKillsByRound = [];
    private readonly HashSet<int> _killTargetsArmedRounds = [];
    private int _activeRoundNumber = -1;
    private double _lastObservedRoundElapsedGameTimeMs;

    public AntHandler(IAntHoleSpawner antHoleSpawner)
    {
        _antHoleSpawner = antHoleSpawner ?? throw new ArgumentNullException(nameof(antHoleSpawner));
    }

    public void Reset()
    {
        _scheduledSpawns.Clear();
        _spawnedAntsByRound.Clear();
        _remainingKillsByRound.Clear();
        _killTargetsArmedRounds.Clear();
        _activeRoundNumber = -1;
        _lastObservedRoundElapsedGameTimeMs = 0d;
    }

    public void HandleRoundStarted(RoundInfo round)
    {
        _scheduledSpawns.Clear();
        _spawnedAntsByRound[round.RoundNumber] = [];
        _remainingKillsByRound[round.RoundNumber] = round.GracePeriodActive ? 0 : round.AntsToSpawn;
        if (round.GracePeriodActive)
        {
            _killTargetsArmedRounds.Remove(round.RoundNumber);
        }
        else
        {
            _killTargetsArmedRounds.Add(round.RoundNumber);
        }

        _activeRoundNumber = round.RoundNumber;
        _lastObservedRoundElapsedGameTimeMs = 0d;

        if (round.AntsToSpawn <= 0)
        {
            return;
        }

        var spawnWindowStartMs = round.SpawnWindowStartMs;
        var spawnWindowDurationMs = round.SpawnWindowDurationMs;
        var batchedSpawnCounts = BuildSpawnBatchSizes(round.RoundNumber, round.AntsToSpawn);
        var firstAntOrdinal = 0;
        for (var spawnIndex = 0; spawnIndex < batchedSpawnCounts.Count; spawnIndex++)
        {
            var spawnOffsetMs = spawnWindowStartMs + GetSpawnOffsetMs(round.RoundNumber, spawnIndex, batchedSpawnCounts.Count, spawnWindowDurationMs);
            var antHoleCount = batchedSpawnCounts[spawnIndex];
            _scheduledSpawns.Add(new ScheduledAntSpawn(round.RoundNumber, firstAntOrdinal, antHoleCount, spawnOffsetMs));
            firstAntOrdinal += antHoleCount;
        }
    }

    public void HandleRoundEnded(RoundInfo round)
    {
        if (_activeRoundNumber != round.RoundNumber)
        {
            return;
        }

        _scheduledSpawns.Clear();
        _spawnedAntsByRound.Remove(round.RoundNumber);
        _remainingKillsByRound.Remove(round.RoundNumber);
        _killTargetsArmedRounds.Remove(round.RoundNumber);
        _activeRoundNumber = -1;
        _lastObservedRoundElapsedGameTimeMs = 0d;
    }

    public bool CanSkipCurrentRound(GameSession session, RoundInfo round)
    {
        return CanCompleteCurrentRound(session, round);
    }

    public bool CanCompleteCurrentRound(GameSession session, RoundInfo round)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_activeRoundNumber != round.RoundNumber || round.GracePeriodActive)
        {
            return false;
        }

        EnsureRoundKillTargetState(round);
        PruneDefeatedRoundAnts(session, round.RoundNumber);
        return AllScheduledSpawnsHaveBeenAttempted(round.RoundNumber) &&
               GetRemainingKills(round.RoundNumber) == 0;
    }

    public void Advance(GameSession session, RoundInfo round)
    {
        ArgumentNullException.ThrowIfNull(session);

        EnsureRoundKillTargetState(round);
        PruneDefeatedRoundAnts(session, round.RoundNumber);

        if (session.Runtime.DisableEnemySpawns)
        {
            _lastObservedRoundElapsedGameTimeMs = round.ElapsedGameTimeMs;
            return;
        }

        if (_activeRoundNumber != round.RoundNumber || _scheduledSpawns.Count == 0)
        {
            _lastObservedRoundElapsedGameTimeMs = round.ElapsedGameTimeMs;
            return;
        }

        var fromTimeMs = _lastObservedRoundElapsedGameTimeMs;
        var toTimeMs = round.ElapsedGameTimeMs;
        if (toTimeMs < fromTimeMs)
        {
            fromTimeMs = 0d;
        }

        var constraints = new AntSpawnConstraints(
            GameConstants.RoundAntHoleMinDistanceFromQueen,
            GameConstants.RoundAntHoleMaxDistanceFromQueen);

        for (var index = 0; index < _scheduledSpawns.Count; index++)
        {
            var scheduledSpawn = _scheduledSpawns[index];
            if (scheduledSpawn.RoundNumber != round.RoundNumber ||
                scheduledSpawn.HasBeenAttempted ||
                scheduledSpawn.SpawnAtRoundElapsedGameTimeMs > toTimeMs ||
                scheduledSpawn.SpawnAtRoundElapsedGameTimeMs < fromTimeMs)
            {
                continue;
            }

            _scheduledSpawns[index] = scheduledSpawn with { HasBeenAttempted = true };
            var successfulSpawnCount = 0;
            for (var antOffset = 0; antOffset < scheduledSpawn.AntHoleCount; antOffset++)
            {
                var antOrdinal = scheduledSpawn.FirstAntOrdinal + antOffset + 1;
                var result = _antHoleSpawner.TrySpawnAnt(session, constraints);
                if (!result.Success)
                {
                    Trace.WriteLine($"[AntHandler][Tick {session.TickCount}] Failed spawn attempt for round {round.RoundNumber} ant {antOrdinal}/{round.AntsToSpawn}: {result.Message}");
                    continue;
                }

                successfulSpawnCount++;
                if (result.SpawnedEnemy is not null)
                {
                    _spawnedAntsByRound.TryAdd(round.RoundNumber, []);
                    _spawnedAntsByRound[round.RoundNumber].Add(result.SpawnedEnemy);
                }

                Trace.WriteLine($"[AntHandler][Tick {session.TickCount}] Spawned round {round.RoundNumber} ant {antOrdinal}/{round.AntsToSpawn} using hole {result.HoleTileKey} and spawn tile {result.SpawnTileKey}.");
            }

            if (scheduledSpawn.AntHoleCount > 1)
            {
                var firstAntNumber = scheduledSpawn.FirstAntOrdinal + 1;
                var lastAntNumber = scheduledSpawn.FirstAntOrdinal + scheduledSpawn.AntHoleCount;
                Trace.WriteLine($"[AntHandler][Tick {session.TickCount}] Spawn event for round {round.RoundNumber} attempted {scheduledSpawn.AntHoleCount} ant holes for ants {firstAntNumber}-{lastAntNumber}/{round.AntsToSpawn}; successful spawns: {successfulSpawnCount}.");
            }
        }

        _lastObservedRoundElapsedGameTimeMs = toTimeMs;
    }

    public int GetRemainingKillsForRound(GameSession session, RoundInfo round)
    {
        ArgumentNullException.ThrowIfNull(session);

        EnsureRoundKillTargetState(round);
        PruneDefeatedRoundAnts(session, round.RoundNumber);
        return GetRemainingKills(round.RoundNumber);
    }

    private int GetRemainingKills(int roundNumber)
    {
        return _remainingKillsByRound.TryGetValue(roundNumber, out var remainingKills)
            ? Math.Max(0, remainingKills)
            : 0;
    }

    private void EnsureRoundKillTargetState(RoundInfo round)
    {
        if (_killTargetsArmedRounds.Contains(round.RoundNumber) || round.GracePeriodActive)
        {
            return;
        }

        _remainingKillsByRound[round.RoundNumber] = round.AntsToSpawn;
        _killTargetsArmedRounds.Add(round.RoundNumber);
    }

    private void PruneDefeatedRoundAnts(GameSession session, int roundNumber)
    {
        if (!_spawnedAntsByRound.TryGetValue(roundNumber, out var trackedAnts) || trackedAnts.Count == 0)
        {
            return;
        }

        var liveEnemies = session.Cave?.GetEnemyList().ToHashSet() ?? [];
        var defeatedCount = 0;
        trackedAnts.RemoveWhere(enemy =>
        {
            var defeated = enemy.Cave is null || enemy.Health <= 0 || !liveEnemies.Contains(enemy);
            if (defeated)
            {
                defeatedCount++;
            }

            return defeated;
        });

        if (defeatedCount <= 0)
        {
            return;
        }

        _remainingKillsByRound.TryGetValue(roundNumber, out var remainingKills);
        _remainingKillsByRound[roundNumber] = Math.Max(0, remainingKills - defeatedCount);
    }

    private static IReadOnlyList<int> BuildSpawnBatchSizes(int roundNumber, int antsToSpawn)
    {
        if (antsToSpawn <= 0)
        {
            return [];
        }

        var configuredMaxBatchSize = roundNumber is >= 1 and <= GameConstants.RoundSingleAntSpawnMaxRound
            ? 1
            : GameConstants.RoundMaxAntHolesPerSpawnEvent;
        var maxBatchSize = Math.Max(GameConstants.RoundMinAntHolesPerSpawnEvent, configuredMaxBatchSize);
        var minBatchSize = Math.Min(GameConstants.RoundMinAntHolesPerSpawnEvent, maxBatchSize);
        var spawnEventCount = (int)Math.Ceiling(antsToSpawn / (double)maxBatchSize);
        var batchSizes = new List<int>(spawnEventCount);
        var remainingAnts = antsToSpawn;

        for (var eventIndex = 0; eventIndex < spawnEventCount; eventIndex++)
        {
            var remainingEvents = spawnEventCount - eventIndex;
            var minimumRequiredForFutureEvents = minBatchSize * (remainingEvents - 1);
            var antHoleCount = Math.Min(maxBatchSize, remainingAnts - minimumRequiredForFutureEvents);
            antHoleCount = Math.Max(minBatchSize, antHoleCount);
            batchSizes.Add(antHoleCount);
            remainingAnts -= antHoleCount;
        }

        Debug.Assert(remainingAnts == 0, "Spawn batch sizes should consume every ant requested for the round.");
        return batchSizes;
    }

    private static double GetSpawnOffsetMs(int roundNumber, int spawnIndex, int spawnEventCount, double spawnWindowDurationMs)
    {
        if (roundNumber is >= 1 and <= GameConstants.RoundSingleAntSpawnMaxRound)
        {
            const int singleAntCadenceSlots = 8;
            return spawnIndex < singleAntCadenceSlots
                ? ((spawnIndex * 2d) + 1d) * spawnWindowDurationMs / (singleAntCadenceSlots * 2d)
                : spawnWindowDurationMs;
        }

        return ((spawnIndex * 2d) + 1d) * spawnWindowDurationMs / (spawnEventCount * 2d);
    }

    private bool AllScheduledSpawnsHaveBeenAttempted(int roundNumber)
    {
        for (var index = 0; index < _scheduledSpawns.Count; index++)
        {
            var scheduledSpawn = _scheduledSpawns[index];
            if (scheduledSpawn.RoundNumber == roundNumber && !scheduledSpawn.HasBeenAttempted)
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct ScheduledAntSpawn(
        int RoundNumber,
        int FirstAntOrdinal,
        int AntHoleCount,
        double SpawnAtRoundElapsedGameTimeMs,
        bool HasBeenAttempted = false);
}
