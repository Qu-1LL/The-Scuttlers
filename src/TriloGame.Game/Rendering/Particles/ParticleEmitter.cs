using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Rendering.Particles;

public sealed class ParticleEmitter
{
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
            var position = worldOrigin + RandomInCircle(settings.SpawnJitterPixels);
            var velocityDirection = Rotate(normalizedDirection, NextRange(-settings.DirectionalSpreadRadians, settings.DirectionalSpreadRadians));
            if (velocityDirection == Vector2.Zero)
            {
                velocityDirection = RandomUnitVector();
            }

            var speed = NextRange(settings.MinSpeed, settings.MaxSpeed);
            var drift = RandomInCircle(settings.DriftAmount);
            var particle = Particle.Create(
                position,
                (velocityDirection * speed) + drift,
                NextRange(settings.MinLifetimeSeconds, settings.MaxLifetimeSeconds),
                settings.Drag,
                texture,
                settings.SourceRectangle,
                settings.StartColor,
                settings.EndColor,
                settings.StartScale,
                settings.EndScale,
                settings.FadeOutFraction,
                NextRange(0f, MathF.Tau),
                NextRange(settings.MinRotationSpeed, settings.MaxRotationSpeed),
                settings.BlendMode);

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

    private static float NextRange(float minValue, float maxValue)
    {
        var safeMin = MathF.Min(minValue, maxValue);
        var safeMax = MathF.Max(minValue, maxValue);
        if (MathF.Abs(safeMax - safeMin) <= float.Epsilon)
        {
            return safeMin;
        }

        return safeMin + ((float)RandomUtil.NextDouble() * (safeMax - safeMin));
    }

    private static Vector2 RandomInCircle(float radius)
    {
        if (radius <= 0f)
        {
            return Vector2.Zero;
        }

        var angle = NextRange(0f, MathF.Tau);
        var distance = radius * MathF.Sqrt((float)RandomUtil.NextDouble());
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }

    private static Vector2 RandomUnitVector()
    {
        var angle = NextRange(0f, MathF.Tau);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
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
