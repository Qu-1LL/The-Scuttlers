using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;
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
    public void ParticleUpdate_AppliesExponentialGroundFriction()
    {
        var particle = Particle.Create(
            Vector2.Zero,
            new Vector2(100f, 0f),
            lifetimeSeconds: 1f,
            drag: 0f,
            brownianMotion: 0f,
            texture: null,
            sourceRectangle: null,
            startColor: Color.White,
            endColor: Color.White,
            startScale: 1f,
            endScale: 1f,
            fadeOutFraction: 0f,
            rotation: 0f,
            rotationSpeed: 0f,
            blendMode: ParticleBlendMode.Alpha,
            groundFriction: 2f);

        particle.Update(0.25f);

        Assert.Equal(100f * MathF.Exp(-0.5f), particle.Velocity.X, precision: 4);
        Assert.Equal(0f, particle.Velocity.Y);
    }

    [Fact]
    public void ParticleUpdate_VisualHeightAffectsDrawPositionUntilLanding()
    {
        var particle = Particle.Create(
            new Vector2(10f, 20f),
            new Vector2(8f, 0f),
            lifetimeSeconds: 1f,
            drag: 0f,
            brownianMotion: 0f,
            texture: null,
            sourceRectangle: null,
            startColor: Color.White,
            endColor: Color.White,
            startScale: 1f,
            endScale: 1f,
            fadeOutFraction: 0f,
            rotation: 0f,
            rotationSpeed: 0f,
            blendMode: ParticleBlendMode.Alpha,
            heightVelocity: 10f,
            visualGravity: 100f,
            useVisualHeight: true);

        particle.Update(0.05f);

        Assert.True(particle.Height > 0f);
        Assert.True(particle.GetDrawPosition().Y < particle.Position.Y);

        particle.Update(0.2f);
        particle.Update(0.2f);

        Assert.Equal(0f, particle.Height);
        Assert.Equal(0f, particle.HeightVelocity);
        Assert.Equal(particle.Position, particle.GetDrawPosition());
        Assert.Equal(8f * 0.82f, particle.Velocity.X, precision: 4);
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

    [Fact]
    public void ClampSourceBounds_RestrictsPartiallyOutsideSpriteRegions()
    {
        var clamped = ParticleEmitter.TryClampSourceBounds(
            new Rectangle(30, -4, 20, 18),
            textureWidth: 40,
            textureHeight: 32,
            out var result);

        Assert.True(clamped);
        Assert.Equal(new Rectangle(30, 0, 10, 14), result);
    }

    [Fact]
    public void ClampSourceBounds_RejectsEmptySpriteRegions()
    {
        var clamped = ParticleEmitter.TryClampSourceBounds(
            new Rectangle(4, 4, 0, 8),
            textureWidth: 40,
            textureHeight: 32,
            out var result);

        Assert.False(clamped);
        Assert.Equal(Rectangle.Empty, result);
    }

    [Fact]
    public void SampleFragmentRectangle_StaysInsideSuppliedSpriteRegion()
    {
        var source = new Rectangle(12, 18, 7, 5);

        for (var index = 0; index < 128; index++)
        {
            var fragment = ParticleEmitter.SampleFragmentRectangle(source, minFragmentSize: 2, maxFragmentSize: 4);

            Assert.InRange(fragment.Width, 2, 4);
            Assert.InRange(fragment.Height, 2, 4);
            Assert.True(source.Contains(fragment.Left, fragment.Top));
            Assert.True(fragment.Right <= source.Right);
            Assert.True(fragment.Bottom <= source.Bottom);
        }
    }

    [Fact]
    public void SampleFragmentRectangle_HandlesSpriteRegionsSmallerThanRequestedFragments()
    {
        var fragment = ParticleEmitter.SampleFragmentRectangle(new Rectangle(5, 6, 1, 1), minFragmentSize: 3, maxFragmentSize: 4);

        Assert.Equal(new Rectangle(5, 6, 1, 1), fragment);
    }

    [Fact]
    public void MiningParticleDefaults_UseVisibleFragmentsForFiveHundredTwelvePixelTiles()
    {
        var hit = MiningParticleEmissionSettings.CreateHitDefaults();
        var destroyed = MiningParticleEmissionSettings.CreateDestroyedDefaults();

        Assert.InRange(hit.MaxFragmentSize, 1, 24);
        Assert.InRange(destroyed.MaxFragmentSize, 1, 24);
        Assert.True(hit.MaxFragmentSize > hit.MinFragmentSize);
        Assert.True(destroyed.MinFragmentSize >= hit.MinFragmentSize);
        Assert.True(hit.MinScale > 2f);
        Assert.True(destroyed.MinScale > hit.MinScale);
        Assert.Equal(1f, hit.EndScaleMultiplier);
        Assert.Equal(1f, hit.FadeStartProgress);
        Assert.True(hit.CollidesWithTiles);
    }

    [Fact]
    public void MiningParticleDefaults_DisappearWithoutFade()
    {
        var particle = Particle.Create(
            Vector2.Zero,
            Vector2.Zero,
            lifetimeSeconds: 1f,
            drag: 0f,
            brownianMotion: 0f,
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

        particle.Update(0.99f);

        Assert.Equal(byte.MaxValue, particle.GetDrawColor().A);
    }

    [Fact]
    public void Update_WithCaveReflectsTileCollidingParticles()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(6, 6, GridPoint.Zero);
        var blockedTile = cave.GetTile(new GridPoint(2, 2))
            ?? throw new InvalidOperationException("Expected rectangular test cave tile.");
        blockedTile.SetBase("wall");
        blockedTile.CreatureCanFit = false;
        blockedTile.ConfigureWall(1);

        var previousPosition = new Vector2((2 * TileConstants.TileSize) - TileConstants.TileHalfSize - 10f, 2 * TileConstants.TileSize);
        var system = new ParticleSystem(maxParticles: 1);
        system.TryAdd(
            Particle.Create(
                previousPosition,
                new Vector2(200f, 0f),
                lifetimeSeconds: 1f,
                drag: 0f,
                brownianMotion: 0f,
                texture: null,
                sourceRectangle: null,
                startColor: Color.White,
                endColor: Color.White,
                startScale: 1f,
                endScale: 1f,
                fadeOutFraction: 0f,
                rotation: 0f,
                rotationSpeed: 0f,
                blendMode: ParticleBlendMode.Alpha,
                useTileCollision: true));

        system.Update(0.1f, cave);

        var particle = system.ActiveParticles[0];
        Assert.Equal(previousPosition.X, particle.Position.X);
        Assert.True(particle.Velocity.X < 0f);
    }

    [Fact]
    public void EmitMiningParticles_InvalidTextureFailsSafely()
    {
        var system = new ParticleSystem(maxParticles: 4);
        var emitter = new ParticleEmitter(system);

        var emitted = emitter.EmitMiningParticles(
            new MiningParticleEmissionRequest
            {
                Texture = null,
                TextureSourceBounds = new Rectangle(0, 0, 8, 8),
                WorldBounds = new ParticleWorldBounds(0f, 0f, 16f, 16f),
                ImpactPosition = new Vector2(8f, 8f),
                ParticleCount = 4,
                Tint = Color.White
            });

        Assert.Equal(0, emitted);
        Assert.Equal(0, system.ActiveCount);
    }
}
