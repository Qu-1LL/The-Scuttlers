using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering.Particles;

public sealed class ParticleSpraySettings
{
    public int ParticlesPerTile { get; set; } = 6;

    public float MinLifetimeSeconds { get; set; } = 0.9f;

    public float MaxLifetimeSeconds { get; set; } = 1.6f;

    public float MinSpeed { get; set; } = 4f;

    public float MaxSpeed { get; set; } = 16f;

    public float DirectionalSpreadRadians { get; set; } = MathHelper.ToRadians(18f);

    public float DriftAmount { get; set; } = 8f;

    public float Drag { get; set; } = 4f;

    public float SpawnJitterPixels { get; set; } = 6f;

    public float StartScale { get; set; } = 0.8f;

    public float EndScale { get; set; } = 1.15f;

    public Color StartColor { get; set; } = new Color(220, 246, 226, 210);

    public Color EndColor { get; set; } = new Color(160, 186, 168, 0);

    public float FadeOutFraction { get; set; } = 0.35f;

    public float MinRotationSpeed { get; set; } = -0.55f;

    public float MaxRotationSpeed { get; set; } = 0.55f;

    public ParticleBlendMode BlendMode { get; set; } = ParticleBlendMode.Alpha;

    public Rectangle? SourceRectangle { get; set; }
}
