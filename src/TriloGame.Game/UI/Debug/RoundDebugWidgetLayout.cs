using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Debug;

public static class RoundDebugWidgetLayout
{
    public static RoundDebugWidgetLayoutInfo Build(Point viewport)
    {
        var timerBounds = SettingsMenuLayout.GetTopHudButtonBounds(viewport, 1);
        var roundBounds = SettingsMenuLayout.GetTopHudButtonBounds(viewport, 2);

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
