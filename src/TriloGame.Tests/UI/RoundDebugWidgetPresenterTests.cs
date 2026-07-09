using TriloGame.Game.Core.Constants;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class RoundDebugWidgetPresenterTests
{
    [Fact]
    public void GetRoundButtonAction_ReturnsSkipWaitDuringGraceWhenNotDeferred()
    {
        var action = RoundDebugWidgetPresenter.GetRoundButtonAction(
            gracePeriodActive: true,
            hasDeferredNextRoundStart: false,
            canSkipRound: false);

        Assert.Equal(RoundDebugWidgetAction.SkipWait, action);
    }

    [Fact]
    public void GetRoundButtonAction_ReturnsNoneWhenGraceStartIsDeferred()
    {
        var action = RoundDebugWidgetPresenter.GetRoundButtonAction(
            gracePeriodActive: true,
            hasDeferredNextRoundStart: true,
            canSkipRound: false);

        Assert.Equal(RoundDebugWidgetAction.None, action);
    }

    [Fact]
    public void Build_UsesSkipWaitLabelAndRemainingCountdownDuringGrace()
    {
        var round = new RoundInfo(
            RoundNumber: 0,
            ElapsedGameTimeMs: 60000d,
            DurationMs: GameConstants.RoundGraceDurationMs,
            SpawnWindowStartMs: GameConstants.RoundGraceDurationMs,
            SpawnWindowDurationMs: GameConstants.RoundSpawnWindowDurationMs,
            AntsToSpawn: GameConstants.RoundBaseAntCount,
            GracePeriodActive: true);

        var model = RoundDebugWidgetPresenter.Build(round, RoundDebugWidgetAction.SkipWait);

        Assert.Equal("Next Round", model.TimerLabel);
        Assert.Equal("2:00", model.TimerValue);
        Assert.Equal("Skip Wait", model.RoundValue);
        Assert.True(model.RoundButtonEnabled);
    }

    [Fact]
    public void Build_ShowsSpawnCountdownAndRoundLabelDuringSpawnPhase()
    {
        var round = new RoundInfo(
            RoundNumber: 2,
            ElapsedGameTimeMs: 12000d,
            DurationMs: GameConstants.RoundSpawnWindowDurationMs,
            SpawnWindowStartMs: 0d,
            SpawnWindowDurationMs: GameConstants.RoundSpawnWindowDurationMs,
            AntsToSpawn: GameConstants.RoundBaseAntCount + (GameConstants.RoundAntGrowthPerRound * 2),
            GracePeriodActive: false);

        var model = RoundDebugWidgetPresenter.Build(round, RoundDebugWidgetAction.None);

        Assert.Equal("Enemy Spawn", model.TimerLabel);
        Assert.Equal("0:18", model.TimerValue);
        Assert.Equal("Round 2", model.RoundValue);
        Assert.False(model.RoundButtonEnabled);
    }
}
