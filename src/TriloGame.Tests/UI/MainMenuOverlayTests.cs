using Microsoft.Xna.Framework;
using TriloGame.Game.UI.MainMenu;

namespace TriloGame.Tests.UI;

public sealed class MainMenuOverlayTests
{
    [Fact]
    public void Build_UsesMainMenuCardLayoutForButtons()
    {
        var viewport = new Point(1440, 900);

        var layout = MainMenuOverlay.Build(viewport);

        Assert.True(layout.OverlayBounds.Contains(layout.CardBounds));
        Assert.True(layout.CardBounds.Contains(layout.StartButtonBounds));
        Assert.True(layout.CardBounds.Contains(layout.SettingsButtonBounds));
        Assert.True(layout.CardBounds.Contains(layout.TrilodexButtonBounds));
        Assert.True(layout.CardBounds.Contains(layout.QuitButtonBounds));
    }

    [Fact]
    public void HitTest_ReturnsExpectedActionForEachButton()
    {
        var layout = MainMenuOverlay.Build(new Point(1440, 900));

        Assert.Equal(MainMenuOverlayAction.StartGame, layout.HitTest(layout.StartButtonBounds.Center));
        Assert.Equal(MainMenuOverlayAction.OpenSettings, layout.HitTest(layout.SettingsButtonBounds.Center));
        Assert.Equal(MainMenuOverlayAction.OpenTrilodex, layout.HitTest(layout.TrilodexButtonBounds.Center));
        Assert.Equal(MainMenuOverlayAction.QuitGame, layout.HitTest(layout.QuitButtonBounds.Center));
        Assert.Equal(MainMenuOverlayAction.None, layout.HitTest(Point.Zero));
    }
}
