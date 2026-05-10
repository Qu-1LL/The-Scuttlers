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

    public double RemainingDurationMs => GracePeriodActive
        ? Math.Max(0d, DurationMs - ElapsedGameTimeMs)
        : 0d;
}

public sealed class RoundManager
{
    private int _currentRoundNumber;
    private double _currentRoundElapsedGameTimeMs;
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

    public void Advance(GameSession session, double gameElapsedMs)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isInitialized)
        {
            Reset(session);
        }

        if (_hasDeferredNextRoundStart)
        {
            return;
        }

        var remainingGameTimeMs = Math.Max(0d, gameElapsedMs);
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

    public void DeferNextRoundStart()
    {
        if (!_isInitialized)
        {
            return;
        }

        _deferNextRoundStart = true;
    }

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

    private RoundInfo BuildRoundInfo(GameSession? session)
    {
        var roundPhaseDurationMs = _isGracePeriodActive
            ? GameConstants.RoundGraceDurationMs
            : 0d;
        var spawnWindowStartMs = _isGracePeriodActive
            ? GameConstants.RoundGraceDurationMs
            : 0d;
        var spawnWindowDurationMs = session is null
            ? GameConstants.RoundSpawnWindowDurationMs
            : Math.Clamp(session.Runtime.RoundSpawnWindowDurationMs, 0d, GameConstants.RoundGraceDurationMs);
        return new RoundInfo(
            _currentRoundNumber,
            _currentRoundElapsedGameTimeMs,
            roundPhaseDurationMs,
            spawnWindowStartMs,
            spawnWindowDurationMs,
            GameConstants.RoundBaseAntCount + (GameConstants.RoundAntGrowthPerRound * _currentRoundNumber),
            _isGracePeriodActive);
    }

    private static void Log(GameSession session, string message)
    {
        Trace.WriteLine($"[RoundManager][Tick {session.TickCount}] {message}");
    }

    private void EndCurrentGracePeriod(GameSession session)
    {
        _isGracePeriodActive = false;
        _currentRoundElapsedGameTimeMs = 0d;
        var graceEnded = BuildRoundInfo(session);
        Log(session, $"Round {graceEnded.RoundNumber} defend phase start.");
        GracePeriodEnded?.Invoke(graceEnded);
    }

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
