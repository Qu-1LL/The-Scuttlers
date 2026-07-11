using TriloGame.Game.Runtime.Systems;

namespace TriloGame.Game.UI.Debug;

public enum RoundDebugWidgetAction
{
    None,
    SkipWait,
    SkipRound
}

public readonly record struct RoundDebugWidgetViewModel(
    string TimerLabel,
    string TimerValue,
    string RoundValue,
    bool RoundButtonEnabled);

public static class RoundDebugWidgetPresenter
{
    // Choose the active right-side widget action from the current round state.
    public static RoundDebugWidgetAction GetRoundButtonAction(
        bool gracePeriodActive,
        bool hasDeferredNextRoundStart,
        bool canSkipRound)
    {
        if (gracePeriodActive && !hasDeferredNextRoundStart)
        {
            return RoundDebugWidgetAction.SkipWait;
        }

        return canSkipRound
            ? RoundDebugWidgetAction.SkipRound
            : RoundDebugWidgetAction.None;
    }

    // Build the label/value model so the renderer stays focused on Gum drawing.
    public static RoundDebugWidgetViewModel Build(RoundInfo round, RoundDebugWidgetAction roundButtonAction)
    {
        return new RoundDebugWidgetViewModel(
            TimerLabel: round.GracePeriodActive ? "Next Round" : "Enemy Spawn",
            TimerValue: FormatRoundCountdown(GetRoundWidgetCountdownMs(round)),
            RoundValue: GetRoundBadgeLabel(round, roundButtonAction),
            RoundButtonEnabled: roundButtonAction != RoundDebugWidgetAction.None);
    }

    private static string GetRoundBadgeLabel(RoundInfo round, RoundDebugWidgetAction roundButtonAction)
    {
        return roundButtonAction == RoundDebugWidgetAction.SkipWait
            ? "Skip Wait"
            : $"Round {round.RoundNumber}";
    }

    private static double GetRoundWidgetCountdownMs(RoundInfo round)
    {
        return round.GracePeriodActive
            ? round.RemainingDurationMs
            : Math.Max(0d, round.SpawnWindowDurationMs - round.ElapsedGameTimeMs);
    }

    private static string FormatRoundCountdown(double remainingDurationMs)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remainingDurationMs / 1000d));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
    }
}
