using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering.Particles;

public sealed class MiningParticleEmissionSettings
{
    public int MinParticleCount { get; init; }

    public int MaxParticleCount { get; init; }

    public float MinLifetime { get; init; }

    public float MaxLifetime { get; init; }

    public float MinSpeed { get; init; }

    public float MaxSpeed { get; init; }

    public float MinAngularVelocity { get; init; }

    public float MaxAngularVelocity { get; init; }

    public int MinFragmentSize { get; init; }

    public int MaxFragmentSize { get; init; }

    public float MinScale { get; init; }

    public float MaxScale { get; init; }

    public float EndScaleMultiplier { get; init; }

    public float GroundFriction { get; init; }

    public float MinHeightVelocity { get; init; }

    public float MaxHeightVelocity { get; init; }

    public float VisualGravity { get; init; }

    public float FadeStartProgress { get; init; }

    public float DirectionalSpreadRadians { get; init; }

    public float BrownianMotion { get; init; }

    public bool DrawShadow { get; init; }

    public bool CollidesWithTiles { get; init; }

    public ParticleBlendMode BlendMode { get; init; } = ParticleBlendMode.Alpha;

    public static MiningParticleEmissionSettings CreateHitDefaults()
    {
        return new MiningParticleEmissionSettings
        {
            MinParticleCount = 2,
            MaxParticleCount = 5,
            MinLifetime = 0.3f,
            MaxLifetime = 0.55f,
            MinSpeed = 300f,
            MaxSpeed = 620f,
            MinAngularVelocity = -2.2f,
            MaxAngularVelocity = 2.2f,
            MinFragmentSize = 10,
            MaxFragmentSize = 24,
            MinScale = 2.4f,
            MaxScale = 3.6f,
            EndScaleMultiplier = 1f,
            GroundFriction = 2.2f,
            MinHeightVelocity = 16f,
            MaxHeightVelocity = 34f,
            VisualGravity = 145f,
            FadeStartProgress = 1f,
            DirectionalSpreadRadians = MathHelper.ToRadians(32f),
            BrownianMotion = 2f,
            DrawShadow = true,
            CollidesWithTiles = true
        };
    }

    public static MiningParticleEmissionSettings CreateDestroyedDefaults()
    {
        return new MiningParticleEmissionSettings
        {
            MinParticleCount = 8,
            MaxParticleCount = 18,
            MinLifetime = 0.4f,
            MaxLifetime = 0.8f,
            MinSpeed = 360f,
            MaxSpeed = 760f,
            MinAngularVelocity = -5.4f,
            MaxAngularVelocity = 5.4f,
            MinFragmentSize = 12,
            MaxFragmentSize = 24,
            MinScale = 2.8f,
            MaxScale = 4.3f,
            EndScaleMultiplier = 1f,
            GroundFriction = 2f,
            MinHeightVelocity = 26f,
            MaxHeightVelocity = 64f,
            VisualGravity = 170f,
            FadeStartProgress = 1f,
            DirectionalSpreadRadians = MathHelper.ToRadians(58f),
            BrownianMotion = 3.5f,
            DrawShadow = true,
            CollidesWithTiles = true
        };
    }
}
