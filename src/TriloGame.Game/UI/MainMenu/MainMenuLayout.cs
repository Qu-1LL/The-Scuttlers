using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.MainMenu;

public static class MainMenuLayout
{
    public static Rectangle GetCardBounds(Point viewport)
    {
        var width = Math.Min(640, Math.Max(420, viewport.X - 160));
        var height = Math.Min(472, Math.Max(368, viewport.Y - 140));
        return new Rectangle((viewport.X - width) / 2, (viewport.Y - height) / 2, width, height);
    }

    public static Rectangle GetTitleBounds(Rectangle cardBounds)
    {
        return new Rectangle(cardBounds.X + 32, cardBounds.Y + 34, cardBounds.Width - 64, 80);
    }

    public static Rectangle GetStartGameButtonBounds(Rectangle cardBounds)
    {
        return new Rectangle(cardBounds.X + 72, cardBounds.Y + 154, cardBounds.Width - 144, 58);
    }

    public static Rectangle GetQuitGameButtonBounds(Rectangle cardBounds)
    {
        var trilodexBounds = GetTrilodexButtonBounds(cardBounds);
        return new Rectangle(trilodexBounds.X, trilodexBounds.Bottom + 18, trilodexBounds.Width, trilodexBounds.Height);
    }

    public static Rectangle GetSettingsButtonBounds(Rectangle cardBounds)
    {
        var startBounds = GetStartGameButtonBounds(cardBounds);
        return new Rectangle(startBounds.X, startBounds.Bottom + 18, startBounds.Width, startBounds.Height);
    }

    public static Rectangle GetTrilodexButtonBounds(Rectangle cardBounds)
    {
        var settingsBounds = GetSettingsButtonBounds(cardBounds);
        return new Rectangle(settingsBounds.X, settingsBounds.Bottom + 18, settingsBounds.Width, settingsBounds.Height);
    }
}
