using Microsoft.Xna.Framework;
using TriloGame.Game.Rendering.Particles;

namespace TriloGame.Tests.Rendering;

public sealed class ParticleEmitterTests
{
    [Fact]
    public void WriteAdjacentTileCenters_WritesAllEightNeighborTileCenters()
    {
        Span<Vector2> centers = stackalloc Vector2[8];

        ParticleEmitter.WriteAdjacentTileCenters(new Point(10, 6), 80, centers);

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "800,400",
            "880,400",
            "880,480",
            "880,560",
            "800,560",
            "720,560",
            "720,480",
            "720,400"
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

        var emitted = emitter.EmitAroundAdjacentTiles(new Point(10, 6), 80, null!, settings);

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

        emitter.EmitAroundAdjacentTiles(new Point(0, 0), 80, null!, settings);
        Assert.Equal(8, system.ActiveCount);

        system.Update(0.2f);

        Assert.Equal(0, system.ActiveCount);
    }
}
