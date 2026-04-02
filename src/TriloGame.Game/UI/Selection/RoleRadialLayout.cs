using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Selection;

public static class RoleRadialLayout
{
    public const int DefaultButtonWidth = 116;
    public const int DefaultButtonHeight = 40;
    public const float DefaultButtonRadius = 114f;
    public const int DefaultMargin = 8;

    public static Rectangle GetButtonBounds(
        Vector2 radialCenter,
        float angle,
        Rectangle gameplayBounds,
        int buttonWidth = DefaultButtonWidth,
        int buttonHeight = DefaultButtonHeight,
        float buttonRadius = DefaultButtonRadius,
        int margin = DefaultMargin)
    {
        var center = radialCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * buttonRadius;
        var bounds = new Rectangle(
            (int)MathF.Round(center.X - (buttonWidth / 2f)),
            (int)MathF.Round(center.Y - (buttonHeight / 2f)),
            buttonWidth,
            buttonHeight);
        return ClampToBounds(bounds, gameplayBounds, margin);
    }

    public static Rectangle GetLabelBounds(
        Vector2 radialCenter,
        Point measuredTextSize,
        Rectangle gameplayBounds,
        int horizontalPadding = 14,
        int verticalPadding = 8,
        float buttonRadius = DefaultButtonRadius,
        int buttonHeight = DefaultButtonHeight,
        int margin = DefaultMargin)
    {
        var labelHeight = measuredTextSize.Y + (verticalPadding * 2);
        var upperSideAngle = (-MathF.PI / 2f) + (MathF.Tau / 5f);
        var lowerSideAngle = (-MathF.PI / 2f) + ((MathF.Tau / 5f) * 2f);
        var bandTop = radialCenter.Y + (MathF.Sin(upperSideAngle) * buttonRadius) + (buttonHeight / 2f) + 8f;
        var bandBottom = radialCenter.Y + (MathF.Sin(lowerSideAngle) * buttonRadius) - (buttonHeight / 2f) - 8f;
        var availableHeight = Math.Max(0f, bandBottom - bandTop);
        var y = bandTop + MathF.Max(0f, (availableHeight - labelHeight) / 2f);

        var bounds = new Rectangle(
            (int)MathF.Round(radialCenter.X - ((measuredTextSize.X + (horizontalPadding * 2)) / 2f)),
            (int)MathF.Round(y),
            measuredTextSize.X + (horizontalPadding * 2),
            labelHeight);
        return ClampToBounds(bounds, gameplayBounds, margin);
    }

    public static Rectangle ClampToBounds(Rectangle bounds, Rectangle gameplayBounds, int margin = DefaultMargin)
    {
        var left = gameplayBounds.Left + margin;
        var top = gameplayBounds.Top + margin;
        var right = gameplayBounds.Right - margin;
        var bottom = gameplayBounds.Bottom - margin;

        var width = Math.Min(bounds.Width, Math.Max(1, right - left));
        var height = Math.Min(bounds.Height, Math.Max(1, bottom - top));
        var maxX = Math.Max(left, right - width);
        var maxY = Math.Max(top, bottom - height);
        var x = Math.Clamp(bounds.X, left, maxX);
        var y = Math.Clamp(bounds.Y, top, maxY);
        return new Rectangle(x, y, width, height);
    }
}
