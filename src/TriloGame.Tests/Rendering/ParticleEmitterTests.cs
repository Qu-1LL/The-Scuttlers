using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Rendering.Particles;

namespace TriloGame.Tests.Rendering;

public sealed class ParticleEmitterTests
{
    [Fact]
    public void WriteAdjacentTileCenters_WritesAllEightNeighborTileCenters()
    {
        Span<Vector2> centers = stackalloc Vector2[8];

        ParticleEmitter.WriteAdjacentTileCenters(new Point(10, 6), TileConstants.TileSize, centers);

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "5120,2560",
            "5632,2560",
            "5632,3072",
            "5632,3584",
            "5120,3584",
            "4608,3584",
            "4608,3072",
            "4608,2560"
        };

        for (var index = 0; index < centers.Length; index++)
        {
            var key = $"{centers[index].X:0},{centers[index].Y:0}";
            Assert.True(expected.Remove(key), $"Unexpected adjacent tile center '{key}'.");
        }

        Assert.Empty(expected);
    }

    [Fact]
    public void EmitAroundAdjacentTiles_RespectsParticleCap()
    {
        var system = new ParticleSystem(maxParticles: 10);
        var emitter = new ParticleEmitter(system);
        var settings = new ParticleSpraySettings
        {
            ParticlesPerTile = 4,
            MinLifetimeSeconds = 1f,
            MaxLifetimeSeconds = 1f
        };

        var emitted = emitter.EmitAroundAdjacentTiles(new Point(10, 6), TileConstants.TileSize, null!, settings);

        Assert.Equal(10, emitted);
        Assert.Equal(10, system.ActiveCount);
    }

    [Fact]
    public void Update_RemovesExpiredParticles()
    {
        var system = new ParticleSystem(maxParticles: 16);
        var emitter = new ParticleEmitter(system);
        var settings = new ParticleSpraySettings
        {
            ParticlesPerTile = 1,
            MinLifetimeSeconds = 0.1f,
            MaxLifetimeSeconds = 0.1f,
            MinSpeed = 0f,
            MaxSpeed = 0f,
            DriftAmount = 0f,
            Drag = 0f
        };

        emitter.EmitAroundAdjacentTiles(new Point(0, 0), TileConstants.TileSize, null!, settings);
        Assert.Equal(8, system.ActiveCount);

        system.Update(0.2f);

        Assert.Equal(0, system.ActiveCount);
    }

    [Fact]
    public void ParticleUpdate_AppliesBrownianDisplacementInTwoDimensions()
    {
        var particle = Particle.Create(
            Vector2.Zero,
            Vector2.Zero,
            lifetimeSeconds: 1f,
            drag: 0f,
            brownianMotion: 80f,
            texture: null,
            sourceRectangle: null,
            startColor: Color.White,
            endColor: Color.White,
            startScale: 1f,
            endScale: 1f,
            fadeOutFraction: 0f,
            rotation: 0f,
            rotationSpeed: 0f,
            blendMode: ParticleBlendMode.Alpha);

        particle.Update(0.1f);

        Assert.Equal(Vector2.Zero, particle.Velocity);
        Assert.True(particle.Position.LengthSquared() > 0f);
    }

    [Fact]
    public void EmitFromCircleEdge_SpawnsParticlesOnEdgeWithOutwardVelocity()
    {
        var system = new ParticleSystem(maxParticles: 6);
        var emitter = new ParticleEmitter(system);
        var settings = new ParticleSpraySettings
        {
            MinLifetimeSeconds = 1f,
            MaxLifetimeSeconds = 1f,
            MinSpeed = 10f,
            MaxSpeed = 10f,
            DirectionalSpreadRadians = 0f,
            DriftAmount = 0f,
            Drag = 0f,
            SpawnJitterPixels = 0f
        };
        var center = new Vector2(100f, 80f);
        const float radius = 18f;

        var emitted = emitter.EmitFromCircleEdge(center, radius, null!, Color.White, Color.Transparent, settings, particleCount: 6);

        Assert.Equal(6, emitted);
        Assert.Equal(6, system.ActiveCount);
        foreach (ref readonly var particle in system.ActiveParticles)
        {
            var fromCenter = particle.Position - center;
            Assert.InRange(MathF.Abs(fromCenter.Length() - radius), 0f, 0.001f);
            Assert.True(Vector2.Dot(fromCenter, particle.Velocity) > 0f);
            Assert.Equal(1f, particle.LifetimeSeconds);
        }
    }
}
