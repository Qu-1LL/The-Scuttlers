using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering.Particles;

public readonly struct MiningParticleEmissionRequest
{
    public Texture2D? Texture { get; init; }

    public Rectangle TextureSourceBounds { get; init; }

    public ParticleWorldBounds WorldBounds { get; init; }

    public Vector2 ImpactPosition { get; init; }

    public MiningParticleEmissionMode Mode { get; init; }

    public int? ParticleCount { get; init; }

    public int MinFragmentSize { get; init; }

    public int MaxFragmentSize { get; init; }

    public Color Tint { get; init; }

    public float LayerDepth { get; init; }

    public MiningParticleEmissionSettings? Settings { get; init; }
}
