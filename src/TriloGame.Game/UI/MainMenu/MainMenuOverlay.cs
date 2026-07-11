using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.MainMenu;

public enum MainMenuOverlayAction
{
    None,
    StartGame,
    OpenSettings,
    OpenTrilodex,
    QuitGame
}

public readonly record struct MainMenuOverlayLayoutInfo(
    Rectangle OverlayBounds,
    Rectangle CardBounds,
    Rectangle TitleBounds,
    Rectangle StartButtonBounds,
    Rectangle SettingsButtonBounds,
    Rectangle TrilodexButtonBounds,
    Rectangle QuitButtonBounds)
{
    public MainMenuOverlayAction HitTest(Point point)
    {
        if (StartButtonBounds.Contains(point))
        {
            return MainMenuOverlayAction.StartGame;
        }

        if (SettingsButtonBounds.Contains(point))
        {
            return MainMenuOverlayAction.OpenSettings;
        }

        if (TrilodexButtonBounds.Contains(point))
        {
            return MainMenuOverlayAction.OpenTrilodex;
        }

        return QuitButtonBounds.Contains(point)
            ? MainMenuOverlayAction.QuitGame
            : MainMenuOverlayAction.None;
    }
}

public static class MainMenuOverlay
{
    public static MainMenuOverlayLayoutInfo Build(Point viewport)
    {
        var cardBounds = MainMenuLayout.GetCardBounds(viewport);
        return new MainMenuOverlayLayoutInfo(
            new Rectangle(0, 0, viewport.X, viewport.Y),
            cardBounds,
            MainMenuLayout.GetTitleBounds(cardBounds),
            MainMenuLayout.GetStartGameButtonBounds(cardBounds),
            MainMenuLayout.GetSettingsButtonBounds(cardBounds),
            MainMenuLayout.GetTrilodexButtonBounds(cardBounds),
            MainMenuLayout.GetQuitGameButtonBounds(cardBounds));
    }
}

public sealed class MainMenuOverlayRenderer
{
    private static readonly GumUiFrameStyle CardFrameStyle = new(new Color(16, 30, 42, 244), new Color(141, 199, 219), 3, 22);
    private static readonly GumUiButtonStyle StartButtonStyle = new(
        new GumUiFrameStyle(new Color(201, 173, 118), new Color(238, 215, 164), 2, 14),
        new GumUiFrameStyle(Color.Lerp(new Color(201, 173, 118), Color.White, 0.14f), Color.Lerp(new Color(238, 215, 164), Color.White, 0.22f), 2, 14),
        new Color(10, 23, 34));
    private static readonly GumUiButtonStyle SettingsButtonStyle = new(
        new GumUiFrameStyle(new Color(33, 75, 95), new Color(140, 207, 224), 2, 14),
        new GumUiFrameStyle(Color.Lerp(new Color(33, 75, 95), Color.White, 0.14f), Color.Lerp(new Color(140, 207, 224), Color.White, 0.22f), 2, 14),
        Color.White);
    private static readonly GumUiButtonStyle TrilodexButtonStyle = new(
        new GumUiFrameStyle(new Color(152, 125, 74), new Color(233, 201, 143), 2, 14),
        new GumUiFrameStyle(Color.Lerp(new Color(152, 125, 74), Color.White, 0.14f), Color.Lerp(new Color(233, 201, 143), Color.White, 0.22f), 2, 14),
        new Color(18, 26, 34));
    private static readonly GumUiButtonStyle QuitButtonStyle = new(
        new GumUiFrameStyle(new Color(67, 102, 84), new Color(137, 190, 161), 2, 14),
        new GumUiFrameStyle(Color.Lerp(new Color(67, 102, 84), Color.White, 0.14f), Color.Lerp(new Color(137, 190, 161), Color.White, 0.22f), 2, 14),
        Color.White);

    public void Draw(GumUiRenderer gumUi, Point viewport, Point pointer)
    {
        var layout = MainMenuOverlay.Build(viewport);
        gumUi.AddRoundedRectangle(layout.OverlayBounds, new Color(6, 11, 16), 0);
        GumUiChrome.DrawFrame(gumUi, layout.CardBounds, CardFrameStyle);
        GumUiChrome.DrawButton(gumUi, layout.StartButtonBounds, "Start Game", layout.StartButtonBounds.Contains(pointer), StartButtonStyle);
        GumUiChrome.DrawButton(gumUi, layout.SettingsButtonBounds, "Settings", layout.SettingsButtonBounds.Contains(pointer), SettingsButtonStyle);
        GumUiChrome.DrawButton(gumUi, layout.TrilodexButtonBounds, "Trilodex", layout.TrilodexButtonBounds.Contains(pointer), TrilodexButtonStyle);
        GumUiChrome.DrawButton(gumUi, layout.QuitButtonBounds, "Quit Game", layout.QuitButtonBounds.Contains(pointer), QuitButtonStyle);
        GumUiText.AddFittedCentered(gumUi, layout.TitleBounds, "Welcome to The Scuttlers", Color.White, GumTextStyle.UiLarge);
    }
}
