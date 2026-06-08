using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Debug;

public static class RoundDebugWidgetLayout
{
    public static RoundDebugWidgetLayoutInfo Build(Point viewport)
    {
        var settingsBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        const int outerMargin = 18;
        const int sectionGap = 8;
        const int minTimerWidth = 112;
        const int maxTimerWidth = 176;
        const int minRoundWidth = 84;
        const int maxRoundWidth = 128;

        var left = settingsBounds.Right + 12;
        var availableWidth = Math.Max(0, viewport.X - left - outerMargin);

        var roundWidth = Math.Clamp(availableWidth / 3, minRoundWidth, maxRoundWidth);
        var timerWidth = Math.Clamp(availableWidth - sectionGap - roundWidth, minTimerWidth, maxTimerWidth);

        if (timerWidth + sectionGap + roundWidth > availableWidth)
        {
            timerWidth = Math.Max(0, availableWidth - sectionGap - roundWidth);
        }

        if (timerWidth < minTimerWidth)
        {
            roundWidth = Math.Max(0, availableWidth - sectionGap - minTimerWidth);
            timerWidth = Math.Max(0, availableWidth - sectionGap - roundWidth);
        }

        var timerBounds = new Rectangle(left, settingsBounds.Y, timerWidth, settingsBounds.Height);
        var roundBounds = new Rectangle(timerBounds.Right + sectionGap, settingsBounds.Y, roundWidth, settingsBounds.Height);

        var timerLabelBounds = new Rectangle(timerBounds.X + 10, timerBounds.Y + 4, Math.Max(0, timerBounds.Width - 20), 12);
        var timerValueBounds = new Rectangle(timerBounds.X + 10, timerBounds.Y + 16, Math.Max(0, timerBounds.Width - 20), 20);
        var roundValueBounds = new Rectangle(roundBounds.X + 8, roundBounds.Y + 6, Math.Max(0, roundBounds.Width - 16), Math.Max(0, roundBounds.Height - 12));

        return new RoundDebugWidgetLayoutInfo(
            TimerBounds: timerBounds,
            RoundBounds: roundBounds,
            TimerLabelBounds: timerLabelBounds,
            TimerValueBounds: timerValueBounds,
            RoundValueBounds: roundValueBounds);
    }
}

public readonly record struct RoundDebugWidgetLayoutInfo(
    Rectangle TimerBounds,
    Rectangle RoundBounds,
    Rectangle TimerLabelBounds,
    Rectangle TimerValueBounds,
    Rectangle RoundValueBounds);
