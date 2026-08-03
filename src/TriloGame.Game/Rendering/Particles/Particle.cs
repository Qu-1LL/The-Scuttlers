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
    public float GroundFriction;
    public float BrownianMotion;
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
    public float Height;
    public float HeightVelocity;
    public float VisualGravity;
    public bool UseVisualHeight;
    public bool DrawShadow;
    public bool UseTileCollision;
    public float LayerDepth;

    public readonly bool IsAlive => AgeSeconds < LifetimeSeconds;

    public static Particle Create(
        Vector2 position,
        Vector2 velocity,
        float lifetimeSeconds,
        float drag,
        float brownianMotion,
        Texture2D? texture,
        Rectangle? sourceRectangle,
        Color startColor,
        Color endColor,
        float startScale,
        float endScale,
        float fadeOutFraction,
        float rotation,
        float rotationSpeed,
        ParticleBlendMode blendMode,
        float groundFriction = 0f,
        float height = 0f,
        float heightVelocity = 0f,
        float visualGravity = 0f,
        bool useVisualHeight = false,
        bool drawShadow = false,
        float layerDepth = 0f,
        bool useTileCollision = false)
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
            GroundFriction = MathF.Max(0f, groundFriction),
            BrownianMotion = MathF.Max(0f, brownianMotion),
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
            BlendMode = blendMode,
            Height = MathF.Max(0f, height),
            HeightVelocity = heightVelocity,
            VisualGravity = MathF.Max(0f, visualGravity),
            UseVisualHeight = useVisualHeight,
            DrawShadow = drawShadow,
            UseTileCollision = useTileCollision,
            LayerDepth = layerDepth
        };
    }

    public void Update(float elapsedSeconds)
    {
        AgeSeconds += elapsedSeconds;
        if (!IsAlive)
        {
            return;
        }

        if (GroundFriction > 0f)
        {
            Velocity *= MathF.Exp(-GroundFriction * elapsedSeconds);
        }
        else if (Drag > 0f)
        {
            Velocity *= 1f / (1f + (Drag * elapsedSeconds));
        }

        Position += Velocity * elapsedSeconds;

        if (BrownianMotion > 0f)
        {
            var angle = RenderingRandom.NextRange(0f, MathF.Tau);
            var scale = BrownianMotion * MathF.Sqrt(elapsedSeconds);
            Position += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * scale;
        }

        if (UseVisualHeight)
        {
            var wasAboveGround = Height > 0f;
            Height += HeightVelocity * elapsedSeconds;
            HeightVelocity -= VisualGravity * elapsedSeconds;
            if (Height <= 0f)
            {
                Height = 0f;
                if (HeightVelocity < 0f)
                {
                    HeightVelocity = 0f;
                }

                if (wasAboveGround)
                {
                    Velocity *= 0.82f;
                }
            }
        }

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

    public readonly Vector2 GetDrawPosition()
    {
        return UseVisualHeight && Height > 0f
            ? Position + new Vector2(0f, -Height)
            : Position;
    }
}
