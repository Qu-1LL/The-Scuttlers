using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Gum;

internal readonly record struct GumUiSurface(Rectangle ScreenBounds)
{
    public Rectangle ToLocal(Rectangle bounds)
    {
        return new Rectangle(
            bounds.X - ScreenBounds.X,
            bounds.Y - ScreenBounds.Y,
            bounds.Width,
            bounds.Height);
    }

    public Point ToLocal(Point point)
    {
        return new Point(
            point.X - ScreenBounds.X,
            point.Y - ScreenBounds.Y);
    }

    public Vector2 ToLocal(Vector2 point)
    {
        return new Vector2(
            point.X - ScreenBounds.X,
            point.Y - ScreenBounds.Y);
    }

    public bool Intersects(Rectangle bounds)
    {
        return Rectangle.Intersect(ScreenBounds, bounds) is { Width: > 0, Height: > 0 };
    }
}
