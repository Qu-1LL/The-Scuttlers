using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering.Particles;

public readonly record struct ParticleWorldBounds(float X, float Y, float Width, float Height)
{
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public Vector2 Center => new(X + (Width * 0.5f), Y + (Height * 0.5f));

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public static ParticleWorldBounds FromRectangle(Rectangle rectangle)
    {
        return new ParticleWorldBounds(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    public static ParticleWorldBounds Centered(Vector2 center, float width, float height)
    {
        return new ParticleWorldBounds(
            center.X - (width * 0.5f),
            center.Y - (height * 0.5f),
            width,
            height);
    }

    public Vector2 Clamp(Vector2 point)
    {
        return new Vector2(
            MathHelper.Clamp(point.X, Left, Right),
            MathHelper.Clamp(point.Y, Top, Bottom));
    }
}
