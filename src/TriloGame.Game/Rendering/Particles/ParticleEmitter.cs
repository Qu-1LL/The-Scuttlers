using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering.Particles;

public sealed class ParticleEmitter
{
    private static readonly MiningParticleEmissionSettings HitMiningDefaults = MiningParticleEmissionSettings.CreateHitDefaults();
    private static readonly MiningParticleEmissionSettings DestroyedMiningDefaults = MiningParticleEmissionSettings.CreateDestroyedDefaults();

    private static readonly Point[] AdjacentTileOffsets =
    [
        new Point(0, -1),
        new Point(1, -1),
        new Point(1, 0),
        new Point(1, 1),
        new Point(0, 1),
        new Point(-1, 1),
        new Point(-1, 0),
        new Point(-1, -1)
    ];

    private readonly ParticleSystem _particleSystem;

    public ParticleEmitter(ParticleSystem particleSystem)
    {
        _particleSystem = particleSystem;
    }

    public int EmitAroundAdjacentTiles(Point sourceTile, int tileSize, Texture2D texture, ParticleSpraySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Span<Vector2> adjacentTileCenters = stackalloc Vector2[8];
        WriteAdjacentTileCenters(sourceTile, tileSize, adjacentTileCenters);

        var sourceWorld = new Vector2(sourceTile.X * tileSize, sourceTile.Y * tileSize);
        var emitted = 0;
        for (var index = 0; index < adjacentTileCenters.Length; index++)
        {
            var origin = adjacentTileCenters[index];
            var outwardDirection = origin - sourceWorld;
            if (outwardDirection != Vector2.Zero)
            {
                outwardDirection.Normalize();
            }

            emitted += EmitBurst(origin, outwardDirection, texture, settings, settings.ParticlesPerTile);
        }

        return emitted;
    }

    public int EmitBurst(Vector2 worldOrigin, Vector2 outwardDirection, Texture2D texture, ParticleSpraySettings settings, int particleCount)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (particleCount <= 0)
        {
            return 0;
        }

        var emitted = 0;
        var normalizedDirection = outwardDirection;
        if (normalizedDirection != Vector2.Zero)
        {
            normalizedDirection.Normalize();
        }

        for (var index = 0; index < particleCount; index++)
        {
            if (!TryEmitParticle(
                    worldOrigin + RandomInCircle(settings.SpawnJitterPixels),
                    normalizedDirection,
                    texture,
                    settings,
                    settings.StartColor,
                    settings.EndColor))
            {
                break;
            }

            emitted++;
        }

        return emitted;
    }

    public int EmitFromCircleEdge(
        Vector2 center,
        float radius,
        Texture2D texture,
        Color startColor,
        Color endColor,
        ParticleSpraySettings settings,
        int particleCount)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (particleCount <= 0)
        {
            return 0;
        }

        var emitted = 0;
        var safeRadius = MathF.Max(0f, radius);
        for (var index = 0; index < particleCount; index++)
        {
            var outwardDirection = RandomUnitVector();
            var position = center + (outwardDirection * safeRadius) + RandomInCircle(settings.SpawnJitterPixels);
            if (!TryEmitParticle(position, outwardDirection, texture, settings, startColor, endColor))
            {
                break;
            }

            emitted++;
        }

        return emitted;
    }

    public int EmitMiningParticles(MiningParticleEmissionRequest request)
    {
        if (!TryPrepareMiningRequest(request, out var texture, out var sourceBounds, out var settings))
        {
            return 0;
        }

        var particleCount = GetMiningParticleCount(request, settings);
        if (particleCount <= 0)
        {
            return 0;
        }

        var minFragmentSize = request.MinFragmentSize > 0 ? request.MinFragmentSize : settings.MinFragmentSize;
        var maxFragmentSize = request.MaxFragmentSize > 0 ? request.MaxFragmentSize : settings.MaxFragmentSize;
        var worldBounds = request.WorldBounds;
        var impact = request.ImpactPosition;
        var tint = request.Tint.PackedValue == 0u ? Color.White : request.Tint;
        var fadeOutFraction = 1f - MathHelper.Clamp(settings.FadeStartProgress, 0f, 1f);
        var emitted = 0;

        for (var index = 0; index < particleCount; index++)
        {
            var spawn = RandomInBounds(worldBounds);
            var direction = request.Mode == MiningParticleEmissionMode.Destroyed
                ? RandomDestroyedDirection()
                : spawn - impact;

            direction = NormalizeOrRandom(Rotate(direction, RenderingRandom.NextRange(-settings.DirectionalSpreadRadians, settings.DirectionalSpreadRadians)));
            var speed = RenderingRandom.NextRange(settings.MinSpeed, settings.MaxSpeed);
            var scale = RenderingRandom.NextRange(settings.MinScale, settings.MaxScale);
            var heightVelocity = RenderingRandom.NextRange(settings.MinHeightVelocity, settings.MaxHeightVelocity);
            var particle = Particle.Create(
                spawn,
                direction * speed,
                RenderingRandom.NextRange(settings.MinLifetime, settings.MaxLifetime),
                0f,
                settings.BrownianMotion,
                texture,
                SampleFragmentRectangle(sourceBounds, minFragmentSize, maxFragmentSize),
                tint,
                tint,
                scale,
                scale * MathF.Max(0f, settings.EndScaleMultiplier),
                fadeOutFraction,
                RenderingRandom.NextRange(0f, MathF.Tau),
                RenderingRandom.NextRange(settings.MinAngularVelocity, settings.MaxAngularVelocity),
                settings.BlendMode,
                settings.GroundFriction,
                0f,
                heightVelocity,
                settings.VisualGravity,
                heightVelocity > 0f || settings.VisualGravity > 0f,
                settings.DrawShadow,
                request.LayerDepth,
                settings.CollidesWithTiles);

            if (!_particleSystem.TryAdd(particle))
            {
                break;
            }

            emitted++;
        }

        return emitted;
    }

    public static void WriteAdjacentTileCenters(Point sourceTile, int tileSize, Span<Vector2> destination)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        if (destination.Length < AdjacentTileOffsets.Length)
        {
            throw new ArgumentException("Destination must have room for 8 adjacent tile centers.", nameof(destination));
        }

        for (var index = 0; index < AdjacentTileOffsets.Length; index++)
        {
            var offset = AdjacentTileOffsets[index];
            destination[index] = new Vector2(
                (sourceTile.X + offset.X) * tileSize,
                (sourceTile.Y + offset.Y) * tileSize);
        }
    }

    internal static bool TryClampSourceBounds(Rectangle requestedBounds, int textureWidth, int textureHeight, out Rectangle clampedBounds)
    {
        clampedBounds = Rectangle.Empty;
        if (textureWidth <= 0 ||
            textureHeight <= 0 ||
            requestedBounds.Width <= 0 ||
            requestedBounds.Height <= 0)
        {
            return false;
        }

        var left = Math.Clamp(requestedBounds.Left, 0, textureWidth);
        var top = Math.Clamp(requestedBounds.Top, 0, textureHeight);
        var right = Math.Clamp(requestedBounds.Right, 0, textureWidth);
        var bottom = Math.Clamp(requestedBounds.Bottom, 0, textureHeight);
        if (right <= left || bottom <= top)
        {
            return false;
        }

        clampedBounds = new Rectangle(left, top, right - left, bottom - top);
        return true;
    }

    internal static Rectangle SampleFragmentRectangle(Rectangle sourceBounds, int minFragmentSize, int maxFragmentSize)
    {
        var safeMin = Math.Max(1, Math.Min(minFragmentSize, maxFragmentSize));
        var safeMax = Math.Max(safeMin, Math.Max(minFragmentSize, maxFragmentSize));
        var widthMax = Math.Max(1, Math.Min(sourceBounds.Width, safeMax));
        var heightMax = Math.Max(1, Math.Min(sourceBounds.Height, safeMax));
        var widthMin = Math.Min(widthMax, safeMin);
        var heightMin = Math.Min(heightMax, safeMin);
        var width = RenderingRandom.NextInt(widthMin, widthMax);
        var height = RenderingRandom.NextInt(heightMin, heightMax);
        var x = RenderingRandom.NextInt(sourceBounds.Left, sourceBounds.Right - width);
        var y = RenderingRandom.NextInt(sourceBounds.Top, sourceBounds.Bottom - height);

        return new Rectangle(x, y, width, height);
    }

    private bool TryEmitParticle(
        Vector2 position,
        Vector2 outwardDirection,
        Texture2D texture,
        ParticleSpraySettings settings,
        Color startColor,
        Color endColor)
    {
        var velocityDirection = Rotate(outwardDirection, RenderingRandom.NextRange(-settings.DirectionalSpreadRadians, settings.DirectionalSpreadRadians));
        if (velocityDirection == Vector2.Zero)
        {
            velocityDirection = RandomUnitVector();
        }

        var speed = RenderingRandom.NextRange(settings.MinSpeed, settings.MaxSpeed);
        var drift = RandomInCircle(settings.DriftAmount);
        var particle = Particle.Create(
            position,
            (velocityDirection * speed) + drift,
            RenderingRandom.NextRange(settings.MinLifetimeSeconds, settings.MaxLifetimeSeconds),
            settings.Drag,
            settings.BrownianMotion,
            texture,
            settings.SourceRectangle,
            startColor,
            endColor,
            settings.StartScale,
            settings.EndScale,
            settings.FadeOutFraction,
            RenderingRandom.NextRange(0f, MathF.Tau),
            RenderingRandom.NextRange(settings.MinRotationSpeed, settings.MaxRotationSpeed),
            settings.BlendMode);

        return _particleSystem.TryAdd(particle);
    }

    private static bool TryPrepareMiningRequest(
        MiningParticleEmissionRequest request,
        out Texture2D texture,
        out Rectangle sourceBounds,
        out MiningParticleEmissionSettings settings)
    {
        texture = request.Texture!;
        sourceBounds = Rectangle.Empty;
        settings = request.Settings ?? GetDefaultMiningSettings(request.Mode);
        if (request.Texture is null ||
            request.Texture.IsDisposed ||
            request.WorldBounds.IsEmpty ||
            !TryClampSourceBounds(request.TextureSourceBounds, request.Texture.Width, request.Texture.Height, out sourceBounds))
        {
            return false;
        }

        texture = request.Texture;
        return true;
    }

    private static MiningParticleEmissionSettings GetDefaultMiningSettings(MiningParticleEmissionMode mode)
    {
        return mode == MiningParticleEmissionMode.Destroyed
            ? DestroyedMiningDefaults
            : HitMiningDefaults;
    }

    private static int GetMiningParticleCount(MiningParticleEmissionRequest request, MiningParticleEmissionSettings settings)
    {
        if (request.ParticleCount is { } requestedCount)
        {
            return Math.Max(0, requestedCount);
        }

        var min = Math.Max(0, settings.MinParticleCount);
        var max = Math.Max(min, settings.MaxParticleCount);
        return RenderingRandom.NextInt(min, max);
    }

    private static Vector2 RandomInBounds(ParticleWorldBounds bounds)
    {
        return new Vector2(
            RenderingRandom.NextRange(bounds.Left, bounds.Right),
            RenderingRandom.NextRange(bounds.Top, bounds.Bottom));
    }

    private static Vector2 RandomDestroyedDirection()
    {
        return NormalizeOrRandom(RandomUnitVector() + (RandomInCircle(0.45f) * 0.75f));
    }

    private static Vector2 RandomInCircle(float radius)
    {
        if (radius <= 0f)
        {
            return Vector2.Zero;
        }

        var angle = RenderingRandom.NextRange(0f, MathF.Tau);
        var distance = radius * MathF.Sqrt(RenderingRandom.NextUnit());
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }

    private static Vector2 RandomUnitVector()
    {
        var angle = RenderingRandom.NextRange(0f, MathF.Tau);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static Vector2 NormalizeOrRandom(Vector2 value)
    {
        if (value.LengthSquared() <= 0.0001f)
        {
            return RandomUnitVector();
        }

        value.Normalize();
        return value;
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        if (value == Vector2.Zero || MathF.Abs(radians) <= float.Epsilon)
        {
            return value;
        }

        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(
            (value.X * cos) - (value.Y * sin),
            (value.X * sin) + (value.Y * cos));
    }
}
