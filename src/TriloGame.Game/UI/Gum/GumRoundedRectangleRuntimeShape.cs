using Gum.GueDeriving;

namespace TriloGame.Game.UI.Gum;

internal static class GumRoundedRectangleRuntimeShape
{
    public static void Apply(RectangleRuntime rectangle, int radius, bool isFilled, float strokeWidth)
    {
        rectangle.CornerRadius = Math.Max(0, radius);
        rectangle.IsFilled = isFilled;
        rectangle.StrokeWidth = Math.Max(1f, strokeWidth);
    }
}
