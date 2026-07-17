using System.Numerics;

namespace TriloGame.Game.Shared.Math;

// Simulation positions use integer subpixels so movement and collision decisions remain deterministic.
public readonly record struct WorldPoint(int X, int Y)
{
    public static readonly WorldPoint Zero = new(0, 0);

    public static WorldPoint FromGridPoint(GridPoint point)
    {
        return new WorldPoint(
            checked(point.X * WorldUnits.UnitsPerTile),
            checked(point.Y * WorldUnits.UnitsPerTile));
    }

    public static WorldPoint FromWorldPixels(Vector2 pixels)
    {
        return new WorldPoint(
            checked((int)MathF.Round(pixels.X * WorldUnits.UnitsPerPixel)),
            checked((int)MathF.Round(pixels.Y * WorldUnits.UnitsPerPixel)));
    }

    public GridPoint ToGridPoint()
    {
        return new GridPoint(
            WorldUnits.FloorDiv(X + WorldUnits.UnitsPerHalfTile, WorldUnits.UnitsPerTile),
            WorldUnits.FloorDiv(Y + WorldUnits.UnitsPerHalfTile, WorldUnits.UnitsPerTile));
    }

    public Vector2 ToWorldPixels()
    {
        return new Vector2(
            X / (float)WorldUnits.UnitsPerPixel,
            Y / (float)WorldUnits.UnitsPerPixel);
    }

    public static WorldPoint operator +(WorldPoint point, WorldVector vector) =>
        new(checked(point.X + vector.X), checked(point.Y + vector.Y));

    public static WorldPoint operator -(WorldPoint point, WorldVector vector) =>
        new(checked(point.X - vector.X), checked(point.Y - vector.Y));

    public static WorldVector operator -(WorldPoint left, WorldPoint right) =>
        new(checked(left.X - right.X), checked(left.Y - right.Y));
}

public readonly record struct WorldVector(int X, int Y)
{
    public static readonly WorldVector Zero = new(0, 0);

    public long LengthSquared => ((long)X * X) + ((long)Y * Y);

    public int Length => WorldUnits.IntegerSqrt(LengthSquared);

    public bool IsZero => X == 0 && Y == 0;

    public WorldVector ClampMagnitude(int maximum)
    {
        if (maximum <= 0 || IsZero)
        {
            return Zero;
        }

        var length = Length;
        if (length <= maximum)
        {
            return this;
        }

        return new WorldVector(
            (int)(((long)X * maximum) / length),
            (int)(((long)Y * maximum) / length));
    }

    public WorldVector WithMagnitude(int magnitude)
    {
        if (magnitude <= 0 || IsZero)
        {
            return Zero;
        }

        var length = Length;
        return new WorldVector(
            (int)(((long)X * magnitude) / length),
            (int)(((long)Y * magnitude) / length));
    }

    public static WorldVector operator +(WorldVector left, WorldVector right) =>
        new(checked(left.X + right.X), checked(left.Y + right.Y));

    public static WorldVector operator -(WorldVector left, WorldVector right) =>
        new(checked(left.X - right.X), checked(left.Y - right.Y));

    public static WorldVector operator -(WorldVector value) => new(-value.X, -value.Y);

    public static WorldVector operator /(WorldVector value, int divisor) =>
        divisor == 0 ? Zero : new(value.X / divisor, value.Y / divisor);
}

public static class WorldUnits
{
    public const int UnitsPerPixel = 16;
    public const int UnitsPerTile = Core.Constants.TileConstants.TileSize * UnitsPerPixel;
    public const int UnitsPerHalfTile = UnitsPerTile / 2;

    public static int FromPixels(int pixels) => checked(pixels * UnitsPerPixel);

    public static int ToPixelsRounded(int units)
    {
        return units >= 0
            ? (units + (UnitsPerPixel / 2)) / UnitsPerPixel
            : (units - (UnitsPerPixel / 2)) / UnitsPerPixel;
    }

    internal static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    internal static int IntegerSqrt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var remainder = (ulong)value;
        ulong root = 0;
        ulong bit = 1UL << 62;
        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }

            bit >>= 2;
        }

        return root > int.MaxValue ? int.MaxValue : (int)root;
    }
}
