using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;

namespace TriloGame.Tests.Runtime;

public sealed class RoundManagerTests
{
    [Fact]
    public void Reset_StartsRoundZero_WithGracePeriodAndExpectedAntCount()
    {
        var session = new GameSession();
        var manager = new RoundManager();
        RoundInfo? started = null;
        RoundInfo? graceStarted = null;
        manager.RoundStarted += round => started = round;
        manager.GracePeriodStarted += round => graceStarted = round;

        manager.Reset(session);

        var round = Assert.IsType<RoundInfo>(started);
        Assert.Equal(0, round.RoundNumber);
        Assert.Equal(GameConstants.RoundBaseAntCount, round.AntsToSpawn);
        Assert.Equal(GameConstants.RoundZeroGraceDurationMs, round.SpawnWindowStartMs);
        Assert.True(round.GracePeriodActive);

        var graceRound = Assert.IsType<RoundInfo>(graceStarted);
        Assert.Equal(0, graceRound.RoundNumber);
        Assert.True(graceRound.GracePeriodActive);
    }

    [Fact]
    public void Advance_EndsGracePeriodAfterTwoMinutesOfGameTime()
    {
        var session = new GameSession();
        var manager = new RoundManager();
        RoundInfo? graceEnded = null;
        manager.GracePeriodEnded += round => graceEnded = round;
        manager.Reset(session);

        manager.Advance(session, GameConstants.RoundZeroGraceDurationMs);

        var round = Assert.IsType<RoundInfo>(graceEnded);
        Assert.Equal(0, round.RoundNumber);
        Assert.False(round.GracePeriodActive);
        Assert.Equal(GameConstants.RoundZeroGraceDurationMs, round.ElapsedGameTimeMs);
    }

    [Fact]
    public void Reset_UsesEditableSpawnWindowDurationFromRuntimeState()
    {
        var session = new GameSession();
        session.Runtime.RoundSpawnWindowDurationMs = 12000d;
        var manager = new RoundManager();
        RoundInfo? started = null;
        manager.RoundStarted += round => started = round;

        manager.Reset(session);

        var round = Assert.IsType<RoundInfo>(started);
        Assert.Equal(12000d, round.SpawnWindowDurationMs);
    }

    [Fact]
    public void Advance_AfterRoundDuration_EndsCurrentRound_RaisesDraftHook_AndStartsNextRound()
    {
        var session = new GameSession();
        var manager = new RoundManager();
        RoundInfo? ended = null;
        RoundInfo? drafted = null;
        var startedRounds = new List<RoundInfo>();
        manager.RoundStarted += round => startedRounds.Add(round);
        manager.RoundEnded += round => ended = round;
        manager.DraftRequested += round => drafted = round;
        manager.Reset(session);

        manager.Advance(session, GameConstants.RoundDurationMs);

        var endedRound = Assert.IsType<RoundInfo>(ended);
        Assert.Equal(0, endedRound.RoundNumber);
        Assert.Equal(GameConstants.RoundDurationMs, endedRound.ElapsedGameTimeMs);

        var draftedRound = Assert.IsType<RoundInfo>(drafted);
        Assert.Equal(0, draftedRound.RoundNumber);

        Assert.Equal(2, startedRounds.Count);
        Assert.Equal(0, startedRounds[0].RoundNumber);
        Assert.Equal(1, startedRounds[1].RoundNumber);
        Assert.Equal(1, manager.CurrentRoundNumber);
        Assert.Equal(0d, manager.CurrentRoundElapsedGameTimeMs);
        Assert.Equal(GameConstants.RoundBaseAntCount + GameConstants.RoundAntGrowthPerRound, manager.CurrentRound.AntsToSpawn);
    }

    [Fact]
    public void SkipCurrentRound_EndsCurrentRound_AndStartsTheNextRoundImmediately()
    {
        var session = new GameSession();
        var manager = new RoundManager();
        RoundInfo? ended = null;
        RoundInfo? drafted = null;
        var startedRounds = new List<RoundInfo>();
        manager.RoundStarted += round => startedRounds.Add(round);
        manager.RoundEnded += round => ended = round;
        manager.DraftRequested += round => drafted = round;
        manager.Reset(session);
        manager.Advance(session, 60000d);

        manager.SkipCurrentRound(session);

        var endedRound = Assert.IsType<RoundInfo>(ended);
        Assert.Equal(0, endedRound.RoundNumber);
        Assert.Equal(60000d, endedRound.ElapsedGameTimeMs);

        var draftedRound = Assert.IsType<RoundInfo>(drafted);
        Assert.Equal(0, draftedRound.RoundNumber);

        Assert.Equal(2, startedRounds.Count);
        Assert.Equal(1, manager.CurrentRoundNumber);
        Assert.Equal(0d, manager.CurrentRoundElapsedGameTimeMs);
    }

    [Fact]
    public void DeferNextRoundStart_HoldsTheNextWaveUntilResearchIsResolved()
    {
        var session = new GameSession();
        var manager = new RoundManager();
        var startedRounds = new List<RoundInfo>();
        RoundInfo? drafted = null;
        manager.RoundStarted += round => startedRounds.Add(round);
        manager.DraftRequested += round =>
        {
            drafted = round;
            manager.DeferNextRoundStart();
        };
        manager.Reset(session);

        manager.Advance(session, GameConstants.RoundDurationMs);

        Assert.Equal(0, Assert.IsType<RoundInfo>(drafted).RoundNumber);
        Assert.True(manager.HasDeferredNextRoundStart);
        Assert.Single(startedRounds);
        Assert.Equal(1, manager.CurrentRoundNumber);
        Assert.Equal(0d, manager.CurrentRoundElapsedGameTimeMs);

        manager.Advance(session, 60000d);

        Assert.Single(startedRounds);
        Assert.Equal(0d, manager.CurrentRoundElapsedGameTimeMs);

        var startedDeferredRound = manager.TryStartDeferredNextRound(session);

        Assert.True(startedDeferredRound);
        Assert.False(manager.HasDeferredNextRoundStart);
        Assert.Equal(2, startedRounds.Count);
        Assert.Equal(1, startedRounds[1].RoundNumber);
    }
}
