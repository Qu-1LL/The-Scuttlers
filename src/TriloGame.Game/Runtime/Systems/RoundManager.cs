using System.Diagnostics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public readonly record struct RoundInfo(
    int RoundNumber,
    double ElapsedGameTimeMs,
    double DurationMs,
    double SpawnWindowStartMs,
    double SpawnWindowDurationMs,
    int AntsToSpawn,
    bool GracePeriodActive)
{
    public double SpawnWindowEndMs => SpawnWindowStartMs + SpawnWindowDurationMs;

    public double RemainingDurationMs => Math.Max(0d, DurationMs - ElapsedGameTimeMs);
}

public sealed class RoundManager
{
    private int _currentRoundNumber;
    private double _currentRoundElapsedGameTimeMs;
    private double _currentRoundSpawnWindowDurationMs = GameConstants.RoundSpawnWindowDurationMs;
    private bool _isInitialized;
    private bool _isGracePeriodActive;
    private bool _deferNextRoundStart;
    private bool _hasDeferredNextRoundStart;

    public event Action<RoundInfo>? RoundStarted;
    public event Action<RoundInfo>? RoundEnded;
    public event Action<RoundInfo>? GracePeriodStarted;
    public event Action<RoundInfo>? GracePeriodEnded;
    public event Action<RoundInfo>? DraftRequested;

    public int CurrentRoundNumber => _currentRoundNumber;

    public double CurrentRoundElapsedGameTimeMs => _currentRoundElapsedGameTimeMs;

    public bool IsGracePeriodActive => _isGracePeriodActive;

    public bool HasDeferredNextRoundStart => _hasDeferredNextRoundStart;

    public RoundInfo CurrentRound => BuildRoundInfo(null);

    // Start round zero in its grace phase and notify listeners of the fresh round state.
    public void Reset(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _currentRoundNumber = 0;
        _currentRoundElapsedGameTimeMs = 0d;
        _isInitialized = true;
        _isGracePeriodActive = true;
        _deferNextRoundStart = false;
        _hasDeferredNextRoundStart = false;

        var round = BuildRoundInfo(session);
        StartCurrentRound(session, round);
    }

    // Advance the current round clock and flip into the spawn phase when grace expires.
    public void Advance(GameSession session, double gameElapsedMs)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isInitialized)
        {
            Reset(session);
        }

        UpdateCurrentRoundSpawnWindowDuration(session);

        if (_hasDeferredNextRoundStart)
        {
            return;
        }

        var remainingGameTimeMs = Math.Max(0d, gameElapsedMs);
        // Spend incoming game time against the current round phase until no budget remains.
        while (remainingGameTimeMs > 0d)
        {
            if (_isGracePeriodActive)
            {
                var timeToGraceEndMs = Math.Max(0d, GameConstants.RoundGraceDurationMs - _currentRoundElapsedGameTimeMs);
                var stepMs = Math.Min(remainingGameTimeMs, timeToGraceEndMs);
                if (stepMs <= 0d)
                {
                    stepMs = remainingGameTimeMs;
                }

                _currentRoundElapsedGameTimeMs += stepMs;
                remainingGameTimeMs -= stepMs;

                if (_currentRoundElapsedGameTimeMs < GameConstants.RoundGraceDurationMs)
                {
                    continue;
                }

                EndCurrentGracePeriod(session);
                continue;
            }

            _currentRoundElapsedGameTimeMs += remainingGameTimeMs;
            remainingGameTimeMs = 0d;
        }
    }

    // End the current grace period immediately when runtime state allows it.
    public bool TrySkipCurrentGracePeriod(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isInitialized)
        {
            Reset(session);
            return false;
        }

        if (_hasDeferredNextRoundStart || !_isGracePeriodActive)
        {
            return false;
        }

        Log(session, $"Skipping grace period for round {_currentRoundNumber}.");
        EndCurrentGracePeriod(session);
        return true;
    }

    // Force the active round to complete immediately.
    public void SkipCurrentRound(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isInitialized)
        {
            Reset(session);
            return;
        }

        Log(session, $"Skipping round {_currentRoundNumber}.");
        CompleteCurrentRound(session);
    }

    // Hold the next round start until another system explicitly releases it.
    public void DeferNextRoundStart()
    {
        if (!_isInitialized)
        {
            return;
        }

        _deferNextRoundStart = true;
    }

    // Start a previously deferred round once the gating system has cleared it.
    public bool TryStartDeferredNextRound(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_hasDeferredNextRoundStart)
        {
            return false;
        }

        _hasDeferredNextRoundStart = false;
        StartCurrentRound(session, BuildRoundInfo(session));
        return true;
    }

    // Close out the active round, fire lifecycle events, and start or defer the next one.
    public void CompleteCurrentRound(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isInitialized)
        {
            Reset(session);
            return;
        }

        var endedRound = BuildRoundInfo(session);
        Log(session, $"Round {endedRound.RoundNumber} end.");
        RoundEnded?.Invoke(endedRound);
        DraftRequested?.Invoke(endedRound);

        _currentRoundNumber++;
        _currentRoundElapsedGameTimeMs = 0d;
        _isGracePeriodActive = true;

        if (_deferNextRoundStart)
        {
            _deferNextRoundStart = false;
            _hasDeferredNextRoundStart = true;
            Log(session, $"Round {_currentRoundNumber} start deferred until research drafting is resolved.");
            return;
        }

        StartCurrentRound(session, BuildRoundInfo(session));
    }

    // Package the current round state into a stable data snapshot for listeners.
    private RoundInfo BuildRoundInfo(GameSession? session)
    {
        if (session is not null)
        {
            UpdateCurrentRoundSpawnWindowDuration(session);
        }

        var roundPhaseDurationMs = _isGracePeriodActive
            ? GameConstants.RoundGraceDurationMs
            : _currentRoundSpawnWindowDurationMs;
        var spawnWindowStartMs = _isGracePeriodActive
            ? GameConstants.RoundGraceDurationMs
            : 0d;
        return new RoundInfo(
            _currentRoundNumber,
            _currentRoundElapsedGameTimeMs,
            roundPhaseDurationMs,
            spawnWindowStartMs,
            _currentRoundSpawnWindowDurationMs,
            GameConstants.RoundBaseAntCount + (GameConstants.RoundAntGrowthPerRound * _currentRoundNumber),
            _isGracePeriodActive);
    }

    private void UpdateCurrentRoundSpawnWindowDuration(GameSession session)
    {
        _currentRoundSpawnWindowDurationMs = Math.Clamp(
            session.Runtime.RoundSpawnWindowDurationMs,
            0d,
            GameConstants.RoundGraceDurationMs);
    }

    // Write a round-system trace message with the current tick for debugging.
    private static void Log(GameSession session, string message)
    {
        Trace.WriteLine($"[RoundManager][Tick {session.TickCount}] {message}");
    }

    // Flip the round from grace into the spawn phase and notify downstream systems once.
    private void EndCurrentGracePeriod(GameSession session)
    {
        _isGracePeriodActive = false;
        _currentRoundElapsedGameTimeMs = 0d;
        var graceEnded = BuildRoundInfo(session);
        Log(session, $"Round {graceEnded.RoundNumber} spawn phase start.");
        GracePeriodEnded?.Invoke(graceEnded);
    }

    // Broadcast the start of the current round and its grace-phase state when applicable.
    private void StartCurrentRound(GameSession session, RoundInfo round)
    {
        Log(session, $"Round {round.RoundNumber} start. Ants requested: {round.AntsToSpawn}.");
        RoundStarted?.Invoke(round);

        if (_isGracePeriodActive)
        {
            Log(session, $"Round {round.RoundNumber} grace period start ({GameConstants.RoundGraceDurationMs / 1000d:0}s).");
            GracePeriodStarted?.Invoke(round);
        }
    }
}
