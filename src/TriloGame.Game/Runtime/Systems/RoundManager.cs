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
    private bool _isInitialized;
    private bool _isGracePeriodActive;

    public event Action<RoundInfo>? RoundStarted;
    public event Action<RoundInfo>? RoundEnded;
    public event Action<RoundInfo>? GracePeriodStarted;
    public event Action<RoundInfo>? GracePeriodEnded;
    public event Action<RoundInfo>? DraftRequested;

    public int CurrentRoundNumber => _currentRoundNumber;

    public double CurrentRoundElapsedGameTimeMs => _currentRoundElapsedGameTimeMs;

    public bool IsGracePeriodActive => _isGracePeriodActive;

    public RoundInfo CurrentRound => BuildRoundInfo(null);

    public void Reset(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _currentRoundNumber = 0;
        _currentRoundElapsedGameTimeMs = 0d;
        _isInitialized = true;
        _isGracePeriodActive = true;

        var round = BuildRoundInfo(session);
        Log(session, $"Round {round.RoundNumber} start. Ants requested: {round.AntsToSpawn}.");
        RoundStarted?.Invoke(round);
        Log(session, $"Round {round.RoundNumber} grace period start ({GameConstants.RoundZeroGraceDurationMs / 1000d:0}s).");
        GracePeriodStarted?.Invoke(round);
    }

    public void Advance(GameSession session, double gameElapsedMs)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isInitialized)
        {
            Reset(session);
        }

        var remainingGameTimeMs = Math.Max(0d, gameElapsedMs);
        while (remainingGameTimeMs > 0d)
        {
            var currentRound = BuildRoundInfo(session);
            var timeToGraceEndMs = currentRound.GracePeriodActive
                ? Math.Max(0d, currentRound.SpawnWindowStartMs - _currentRoundElapsedGameTimeMs)
                : double.PositiveInfinity;
            var timeToRoundEndMs = Math.Max(0d, currentRound.DurationMs - _currentRoundElapsedGameTimeMs);
            var stepMs = Math.Min(remainingGameTimeMs, Math.Min(timeToGraceEndMs, timeToRoundEndMs));
            if (double.IsInfinity(stepMs) || stepMs <= 0d)
            {
                stepMs = remainingGameTimeMs;
            }

            _currentRoundElapsedGameTimeMs += stepMs;
            remainingGameTimeMs -= stepMs;

            if (_isGracePeriodActive &&
                _currentRoundNumber == 0 &&
                _currentRoundElapsedGameTimeMs >= GameConstants.RoundZeroGraceDurationMs)
            {
                _isGracePeriodActive = false;
                var graceEnded = BuildRoundInfo(session);
                Log(session, $"Round 0 grace period end at {graceEnded.ElapsedGameTimeMs / 1000d:0.0}s.");
                GracePeriodEnded?.Invoke(graceEnded);
            }

            if (_currentRoundElapsedGameTimeMs < GameConstants.RoundDurationMs)
            {
                continue;
            }

            CompleteCurrentRound(session);
        }
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

    private RoundInfo BuildRoundInfo(GameSession? session)
    {
        var spawnWindowStartMs = _currentRoundNumber == 0
            ? GameConstants.RoundZeroGraceDurationMs
            : 0d;
        var spawnWindowDurationMs = session is null
            ? GameConstants.RoundSpawnWindowDurationMs
            : Math.Clamp(session.Runtime.RoundSpawnWindowDurationMs, 0d, GameConstants.RoundDurationMs);
        return new RoundInfo(
            _currentRoundNumber,
            _currentRoundElapsedGameTimeMs,
            GameConstants.RoundDurationMs,
            spawnWindowStartMs,
            spawnWindowDurationMs,
            GameConstants.RoundBaseAntCount + (GameConstants.RoundAntGrowthPerRound * _currentRoundNumber),
            _isGracePeriodActive);
    }

    private void CompleteCurrentRound(GameSession session)
    {
        var endedRound = BuildRoundInfo(session);
        Log(session, $"Round {endedRound.RoundNumber} end.");
        RoundEnded?.Invoke(endedRound);
        DraftRequested?.Invoke(endedRound);

        _currentRoundNumber++;
        _currentRoundElapsedGameTimeMs = 0d;
        _isGracePeriodActive = _currentRoundNumber == 0;

        var nextRound = BuildRoundInfo(session);
        Log(session, $"Round {nextRound.RoundNumber} start. Ants requested: {nextRound.AntsToSpawn}.");
        RoundStarted?.Invoke(nextRound);

        if (_isGracePeriodActive)
        {
            Log(session, $"Round {nextRound.RoundNumber} grace period start ({GameConstants.RoundZeroGraceDurationMs / 1000d:0}s).");
            GracePeriodStarted?.Invoke(nextRound);
        }
    }

    private static void Log(GameSession session, string message)
    {
        Trace.WriteLine($"[RoundManager][Tick {session.TickCount}] {message}");
    }
}
