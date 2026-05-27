using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Overlays;

public enum GameOverOverlayAction
{
    None,
    PlayAgain,
    ReturnToMainMenu
}

public readonly record struct GameOverOverlayLayoutInfo(
    Rectangle OverlayBounds,
    Rectangle CardBounds,
    Rectangle PlayAgainButtonBounds,
    Rectangle QuitToMainMenuButtonBounds)
{
    public GameOverOverlayAction HitTest(Point point)
    {
        if (PlayAgainButtonBounds.Contains(point))
        {
            return GameOverOverlayAction.PlayAgain;
        }

        return QuitToMainMenuButtonBounds.Contains(point)
            ? GameOverOverlayAction.ReturnToMainMenu
            : GameOverOverlayAction.None;
    }
}

public static class GameOverOverlay
{
    public static GameOverOverlayLayoutInfo Build(Point viewport)
    {
        var cardBounds = GetCardBounds(viewport);
        return new GameOverOverlayLayoutInfo(
            new Rectangle(0, 0, viewport.X, viewport.Y),
            cardBounds,
            GetPlayAgainButtonBounds(cardBounds),
            GetQuitToMainMenuButtonBounds(cardBounds));
    }

    private static Rectangle GetCardBounds(Point viewport)
    {
        var width = Math.Min(520, Math.Max(320, viewport.X - 48));
        var height = Math.Min(320, Math.Max(240, viewport.Y - 80));
        return new Rectangle((viewport.X - width) / 2, (viewport.Y - height) / 2, width, height);
    }

    private static Rectangle GetPlayAgainButtonBounds(Rectangle cardBounds)
    {
        const int width = 240;
        const int height = 54;
        return new Rectangle(cardBounds.Center.X - (width / 2), cardBounds.Bottom - 132, width, height);
    }

    private static Rectangle GetQuitToMainMenuButtonBounds(Rectangle cardBounds)
    {
        const int width = 240;
        const int height = 54;
        return new Rectangle(cardBounds.Center.X - (width / 2), cardBounds.Bottom - 66, width, height);
    }
}

public sealed class GameOverOverlayRenderer
{
    private static readonly GumUiFrameStyle CardFrameStyle = new(new Color(18, 31, 42), new Color(196, 172, 121), 2, 18);
    private static readonly GumUiButtonStyle PlayAgainButtonStyle = new(
        new GumUiFrameStyle(new Color(201, 173, 118), new Color(238, 215, 164), 2, 14),
        new GumUiFrameStyle(new Color(218, 190, 132), new Color(255, 230, 176), 2, 14),
        new Color(10, 23, 34));
    private static readonly GumUiButtonStyle ReturnToMenuButtonStyle = new(
        new GumUiFrameStyle(new Color(67, 102, 84), new Color(137, 190, 161), 2, 14),
        new GumUiFrameStyle(new Color(85, 121, 102), new Color(185, 232, 205), 2, 14),
        Color.White);

    public void Draw(GumUiRenderer gumUi, Point viewport, Point pointer)
    {
        var layout = GameOverOverlay.Build(viewport);
        var playAgainHovered = layout.PlayAgainButtonBounds.Contains(pointer);
        var quitHovered = layout.QuitToMainMenuButtonBounds.Contains(pointer);

        gumUi.AddRoundedRectangle(layout.OverlayBounds, new Color(7, 11, 16) * 0.82f, 0);
        GumUiChrome.DrawFrame(gumUi, layout.CardBounds, CardFrameStyle);
        GumUiChrome.DrawButton(gumUi, layout.PlayAgainButtonBounds, "Play Again", playAgainHovered, PlayAgainButtonStyle);
        GumUiChrome.DrawButton(gumUi, layout.QuitToMainMenuButtonBounds, "Quit to Main Menu", quitHovered, ReturnToMenuButtonStyle);

        GumUiText.AddFittedCentered(
            gumUi,
            new Rectangle(layout.CardBounds.X + 24, layout.CardBounds.Y + 24, layout.CardBounds.Width - 48, 42),
            "Game Over",
            Color.White,
            GumTextStyle.UiLarge);
        GumUiText.AddFittedCentered(
            gumUi,
            new Rectangle(layout.CardBounds.X + 24, layout.CardBounds.Y + 76, layout.CardBounds.Width - 48, 24),
            "The Queen has died.",
            new Color(255, 214, 150),
            GumTextStyle.Ui);
        GumUiText.AddFittedCentered(
            gumUi,
            new Rectangle(layout.CardBounds.X + 24, layout.CardBounds.Y + 104, layout.CardBounds.Width - 48, 34),
            "Start a fresh colony or return to the main menu.",
            new Color(171, 198, 208),
            GumTextStyle.Small);
    }
}
