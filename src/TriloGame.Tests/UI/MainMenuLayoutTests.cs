using Microsoft.Xna.Framework;
using TriloGame.Game.UI.MainMenu;

namespace TriloGame.Tests.UI;

public sealed class MainMenuLayoutTests
{
    [Fact]
    public void GetCardBounds_CentersMenuCardInViewport()
    {
        var viewport = new Point(1440, 900);

        var cardBounds = MainMenuLayout.GetCardBounds(viewport);

        Assert.Equal((viewport.X - cardBounds.Width) / 2, cardBounds.X);
        Assert.Equal((viewport.Y - cardBounds.Height) / 2, cardBounds.Y);
    }

    [Fact]
    public void Buttons_AreOrderedVerticallyInsideMenuCard()
    {
        var cardBounds = MainMenuLayout.GetCardBounds(new Point(1440, 900));

        var titleBounds = MainMenuLayout.GetTitleBounds(cardBounds);
        var startBounds = MainMenuLayout.GetStartGameButtonBounds(cardBounds);
        var quitBounds = MainMenuLayout.GetQuitGameButtonBounds(cardBounds);
        var comingSoonBounds = MainMenuLayout.GetComingSoonBounds(cardBounds);

        Assert.True(cardBounds.Contains(startBounds));
        Assert.True(cardBounds.Contains(quitBounds));
        Assert.True(cardBounds.Contains(comingSoonBounds));
        Assert.True(titleBounds.Bottom < startBounds.Y);
        Assert.True(startBounds.Bottom < quitBounds.Y);
        Assert.True(quitBounds.Bottom < comingSoonBounds.Y);
    }
}
