using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering.Particles;

public struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float AgeSeconds;
    public float LifetimeSeconds;
    public float Drag;
    public float Rotation;
    public float RotationSpeed;
    public float StartScale;
    public float EndScale;
    public float FadeOutFraction;
    public Color StartColor;
    public Color EndColor;
    public Texture2D? Texture;
    public Rectangle? SourceRectangle;
    public Vector2 Origin;
    public ParticleBlendMode BlendMode;

    public readonly bool IsAlive => AgeSeconds < LifetimeSeconds;

    public static Particle Create(
        Vector2 position,
        Vector2 velocity,
        float lifetimeSeconds,
        float drag,
        Texture2D? texture,
        Rectangle? sourceRectangle,
        Color startColor,
        Color endColor,
        float startScale,
        float endScale,
        float fadeOutFraction,
        float rotation,
        float rotationSpeed,
        ParticleBlendMode blendMode)
    {
        var sourceWidth = sourceRectangle?.Width ?? texture?.Width ?? 0;
        var sourceHeight = sourceRectangle?.Height ?? texture?.Height ?? 0;

        return new Particle
        {
            Position = position,
            Velocity = velocity,
            AgeSeconds = 0f,
            LifetimeSeconds = MathF.Max(0.001f, lifetimeSeconds),
            Drag = MathF.Max(0f, drag),
            Rotation = rotation,
            RotationSpeed = rotationSpeed,
            StartScale = startScale,
            EndScale = endScale,
            FadeOutFraction = MathHelper.Clamp(fadeOutFraction, 0f, 1f),
            StartColor = startColor,
            EndColor = endColor,
            Texture = texture,
            SourceRectangle = sourceRectangle,
            Origin = new Vector2(sourceWidth * 0.5f, sourceHeight * 0.5f),
            BlendMode = blendMode
        };
    }

    public void Update(float elapsedSeconds)
    {
        AgeSeconds += elapsedSeconds;
        if (!IsAlive)
        {
            return;
        }

        if (Drag > 0f)
        {
            Velocity *= 1f / (1f + (Drag * elapsedSeconds));
        }

        Position += Velocity * elapsedSeconds;
        Rotation += RotationSpeed * elapsedSeconds;
    }

    public readonly float GetDrawScale()
    {
        var age = MathHelper.Clamp(AgeSeconds / LifetimeSeconds, 0f, 1f);
        return MathHelper.Lerp(StartScale, EndScale, age);
    }

    public readonly Color GetDrawColor()
    {
        var age = MathHelper.Clamp(AgeSeconds / LifetimeSeconds, 0f, 1f);
        var color = Vector4.Lerp(StartColor.ToVector4(), EndColor.ToVector4(), age);

        if (FadeOutFraction > 0f)
        {
            var fadeStart = 1f - FadeOutFraction;
            if (age >= fadeStart)
            {
                var fadeProgress = (age - fadeStart) / FadeOutFraction;
                color.W *= 1f - MathHelper.Clamp(fadeProgress, 0f, 1f);
            }
        }

        return new Color(color);
    }
}
