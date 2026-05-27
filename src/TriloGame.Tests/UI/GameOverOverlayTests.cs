using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Overlays;

namespace TriloGame.Tests.UI;

public sealed class GameOverOverlayTests
{
    [Fact]
    public void Build_CentersGameOverCardAndButtons()
    {
        var viewport = new Point(1440, 900);

        var layout = GameOverOverlay.Build(viewport);

        Assert.Equal((viewport.X - layout.CardBounds.Width) / 2, layout.CardBounds.X);
        Assert.Equal((viewport.Y - layout.CardBounds.Height) / 2, layout.CardBounds.Y);
        Assert.True(layout.CardBounds.Contains(layout.PlayAgainButtonBounds.Center));
        Assert.True(layout.CardBounds.Contains(layout.QuitToMainMenuButtonBounds.Center));
    }

    [Fact]
    public void HitTest_ReturnsExpectedActionForButtons()
    {
        var layout = GameOverOverlay.Build(new Point(1280, 720));

        Assert.Equal(GameOverOverlayAction.PlayAgain, layout.HitTest(layout.PlayAgainButtonBounds.Center));
        Assert.Equal(GameOverOverlayAction.ReturnToMainMenu, layout.HitTest(layout.QuitToMainMenuButtonBounds.Center));
        Assert.Equal(GameOverOverlayAction.None, layout.HitTest(Point.Zero));
    }
}
