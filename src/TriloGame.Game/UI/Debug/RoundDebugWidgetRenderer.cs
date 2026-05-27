using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Debug;

public sealed class RoundDebugWidgetRenderer
{
    private static readonly GumUiFrameStyle TimerNormalFrameStyle = new(new Color(16, 38, 54), new Color(54, 88, 107), 2, 14);
    private static readonly GumUiFrameStyle TimerHoverFrameStyle = new(new Color(22, 50, 71), new Color(125, 179, 196), 2, 14);
    private static readonly GumUiFrameStyle RoundNormalFrameStyle = new(new Color(48, 74, 61), new Color(132, 173, 150), 2, 14);
    private static readonly GumUiFrameStyle RoundHoverFrameStyle = new(new Color(74, 104, 87), new Color(207, 242, 220), 2, 14);
    private static readonly GumUiFrameStyle DisabledRoundFrameStyle = new(new Color(33, 40, 44), new Color(92, 104, 112), 2, 14);

    public void Draw(
        GumUiRenderer gumUi,
        RoundDebugWidgetLayoutInfo layout,
        Point pointer,
        RoundInfo round,
        bool canSkipRound)
    {
        if (layout.TimerBounds.Width <= 0 || layout.RoundBounds.Width <= 0)
        {
            return;
        }

        var timerHovered = layout.TimerBounds.Contains(pointer);
        var roundHovered = canSkipRound && layout.RoundBounds.Contains(pointer);

        GumUiChrome.DrawFrame(gumUi, layout.TimerBounds, timerHovered ? TimerHoverFrameStyle : TimerNormalFrameStyle);
        GumUiChrome.DrawFrame(gumUi, layout.RoundBounds, !canSkipRound ? DisabledRoundFrameStyle : roundHovered ? RoundHoverFrameStyle : RoundNormalFrameStyle);

        GumUiText.AddFittedCentered(gumUi, layout.TimerLabelBounds, "Next Round", Color.White, GumTextStyle.Compact);
        GumUiText.AddFittedCentered(gumUi, layout.TimerValueBounds, FormatRoundCountdown(GetRoundWidgetCountdownMs(round)), Color.White, GumTextStyle.Small);
        GumUiText.AddFittedCentered(
            gumUi,
            layout.RoundValueBounds,
            GetRoundBadgeLabel(round),
            canSkipRound ? Color.White : new Color(183, 191, 196),
            GumTextStyle.Small);
    }

    private static string GetRoundBadgeLabel(RoundInfo round)
    {
        return round.RoundNumber == 0 && round.GracePeriodActive
            ? "Grace Period"
            : $"Round {round.RoundNumber}";
    }

    private static double GetRoundWidgetCountdownMs(RoundInfo round)
    {
        if (round.RoundNumber == 0 && round.GracePeriodActive)
        {
            return Math.Max(0d, round.SpawnWindowStartMs - round.ElapsedGameTimeMs);
        }

        return round.RemainingDurationMs;
    }

    private static string FormatRoundCountdown(double remainingDurationMs)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remainingDurationMs / 1000d));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
    }
}
