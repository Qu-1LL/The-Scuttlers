using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Debug;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class RoundDebugWidgetLayoutTests
{
    [Fact]
    public void Build_PlacesTimerAndRoundInTopHudRow()
    {
        var viewport = new Point(1440, 900);

        var settingsBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var layout = RoundDebugWidgetLayout.Build(viewport);

        Assert.True(layout.TimerBounds.Left > settingsBounds.Right);
        Assert.Equal(settingsBounds.Size, layout.TimerBounds.Size);
        Assert.Equal(settingsBounds.Size, layout.RoundBounds.Size);
        Assert.Equal(settingsBounds.Y, layout.TimerBounds.Y);
        Assert.Equal(settingsBounds.Y, layout.RoundBounds.Y);
        Assert.True(layout.RoundBounds.Left > layout.TimerBounds.Right);
    }

    [Fact]
    public void Build_KeepsTimerAndRoundBoundsInsideViewport()
    {
        var viewport = new Point(800, 240);

        var layout = RoundDebugWidgetLayout.Build(viewport);
        var viewportBounds = new Rectangle(0, 0, viewport.X, viewport.Y);

        Assert.True(viewportBounds.Contains(layout.TimerBounds));
        Assert.True(viewportBounds.Contains(layout.RoundBounds));
    }
}
