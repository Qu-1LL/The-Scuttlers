using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.UI.Selection;

public static class SelectionFocusLayout
{
    public static Rectangle GetGameplayBounds(Point viewport, float openPanelWidth)
    {
        var reservedWidth = Math.Clamp((int)MathF.Round(openPanelWidth), 0, Math.Max(0, viewport.X - 1));
        return new Rectangle(0, 0, Math.Max(1, viewport.X - reservedWidth), Math.Max(1, viewport.Y));
    }

    public static Vector2 GetGameplayCenter(Point viewport, float openPanelWidth)
    {
        var bounds = GetGameplayBounds(viewport, openPanelWidth);
        return new Vector2(bounds.Center.X, bounds.Center.Y);
    }

    public static Rectangle GetFocusHintBounds(Point viewport, float openPanelWidth)
    {
        var gameplayBounds = GetGameplayBounds(viewport, openPanelWidth);
        var width = Math.Min(188, Math.Max(136, gameplayBounds.Width - 36));
        const int height = 38;
        return new Rectangle(
            gameplayBounds.Center.X - (width / 2),
            gameplayBounds.Y + 18,
            width,
            height);
    }

    public static bool IsInsideGameplayBounds(Vector2 screenPosition, Point viewport, float openPanelWidth, int margin = 12)
    {
        var bounds = GetGameplayBounds(viewport, openPanelWidth);
        var inset = bounds;
        inset.Inflate(-margin, -margin);
        if (inset.Width <= 0 || inset.Height <= 0)
        {
            inset = bounds;
        }

        return screenPosition.X >= inset.Left
            && screenPosition.X <= inset.Right
            && screenPosition.Y >= inset.Top
            && screenPosition.Y <= inset.Bottom;
    }

    public static bool IsNearGameplayCenter(Vector2 screenPosition, Point viewport, float openPanelWidth, float radius = 104f)
    {
        return Vector2.Distance(screenPosition, GetGameplayCenter(viewport, openPanelWidth)) <= radius;
    }
}
