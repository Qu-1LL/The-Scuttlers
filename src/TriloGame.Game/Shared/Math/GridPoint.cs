using Microsoft.Xna.Framework;
using System.Globalization;

namespace TriloGame.Game.Shared.Math;

public readonly record struct GridPoint(int X, int Y)
{
    public static GridPoint Zero => new(0, 0);

    public static GridPoint Parse(string key)
    {
        if (TryParse(key, out var point))
        {
            return point;
        }

        throw new FormatException($"Invalid grid key '{key}'.");
    }

    public static bool TryParse(string? key, out GridPoint point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var pieces = key.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        point = new GridPoint(x, y);
        return true;
    }

    public static int SquaredDistance(GridPoint a, GridPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    public static int ManhattanDistance(GridPoint a, GridPoint b)
    {
        return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
    }

    public Vector2 ToVector2()
    {
        return new Vector2(X, Y);
    }

    public override string ToString() => $"{X},{Y}";
}
