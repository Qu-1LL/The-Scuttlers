using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

public enum CombatShapeKind
{
    Circle,
    Aabb,
    Capsule
}

// Fixed-point collision primitives used by both the broadphase and narrow phase.
public readonly struct CombatShape
{
    private CombatShape(CombatShapeKind kind, WorldPoint first, WorldPoint second, int radius, WorldRectangle bounds)
    {
        Kind = kind;
        First = first;
        Second = second;
        Radius = radius;
        Bounds = bounds;
    }

    public CombatShapeKind Kind { get; }
    public WorldPoint First { get; }
    public WorldPoint Second { get; }
    public int Radius { get; }
    public WorldRectangle Bounds { get; }

    public static CombatShape Circle(WorldPoint center, int radius) =>
        new(CombatShapeKind.Circle, center, center, Math.Max(0, radius), default);

    public static CombatShape Aabb(WorldRectangle bounds) =>
        new(CombatShapeKind.Aabb, default, default, 0, bounds);

    public static CombatShape Capsule(WorldPoint start, WorldPoint end, int radius) =>
        new(CombatShapeKind.Capsule, start, end, Math.Max(0, radius), default);

    public WorldRectangle GetBounds()
    {
        return Kind switch
        {
            CombatShapeKind.Circle => new WorldRectangle(First.X - Radius, First.Y - Radius, Radius * 2, Radius * 2),
            CombatShapeKind.Aabb => Bounds,
            _ => new WorldRectangle(
                Math.Min(First.X, Second.X) - Radius,
                Math.Min(First.Y, Second.Y) - Radius,
                Math.Abs(First.X - Second.X) + (Radius * 2),
                Math.Abs(First.Y - Second.Y) + (Radius * 2))
        };
    }

    public bool Intersects(in CombatShape other)
    {
        if (Kind == CombatShapeKind.Circle && other.Kind == CombatShapeKind.Circle)
        {
            return DistanceSquared(First, other.First) <= Square((long)Radius + other.Radius);
        }

        if (Kind == CombatShapeKind.Aabb && other.Kind == CombatShapeKind.Aabb)
        {
            return Bounds.X <= other.Bounds.Right && Bounds.Right >= other.Bounds.X &&
                   Bounds.Y <= other.Bounds.Bottom && Bounds.Bottom >= other.Bounds.Y;
        }

        if (Kind == CombatShapeKind.Circle && other.Kind == CombatShapeKind.Aabb)
        {
            return CircleIntersectsAabb(First, Radius, other.Bounds);
        }

        if (Kind == CombatShapeKind.Aabb && other.Kind == CombatShapeKind.Circle)
        {
            return CircleIntersectsAabb(other.First, other.Radius, Bounds);
        }

        if (Kind == CombatShapeKind.Capsule && other.Kind == CombatShapeKind.Circle)
        {
            return DistanceSquaredToSegment(other.First, First, Second) <= Square((long)Radius + other.Radius);
        }

        if (Kind == CombatShapeKind.Circle && other.Kind == CombatShapeKind.Capsule)
        {
            return other.Intersects(this);
        }

        if (Kind == CombatShapeKind.Capsule && other.Kind == CombatShapeKind.Aabb)
        {
            return CapsuleIntersectsAabb(First, Second, Radius, other.Bounds);
        }

        if (Kind == CombatShapeKind.Aabb && other.Kind == CombatShapeKind.Capsule)
        {
            return other.Intersects(this);
        }

        return DistanceSquaredToSegments(First, Second, other.First, other.Second) <=
               Square((long)Radius + other.Radius);
    }

    private static bool CircleIntersectsAabb(WorldPoint center, int radius, WorldRectangle bounds)
    {
        var x = Math.Clamp(center.X, bounds.X, bounds.Right);
        var y = Math.Clamp(center.Y, bounds.Y, bounds.Bottom);
        return DistanceSquared(center, new WorldPoint(x, y)) <= Square(radius);
    }

    private static bool CapsuleIntersectsAabb(WorldPoint start, WorldPoint end, int radius, WorldRectangle bounds)
    {
        if (PointInside(start, bounds) || PointInside(end, bounds) ||
            SegmentsIntersect(start, end, new WorldPoint(bounds.X, bounds.Y), new WorldPoint(bounds.Right, bounds.Y)) ||
            SegmentsIntersect(start, end, new WorldPoint(bounds.Right, bounds.Y), new WorldPoint(bounds.Right, bounds.Bottom)) ||
            SegmentsIntersect(start, end, new WorldPoint(bounds.Right, bounds.Bottom), new WorldPoint(bounds.X, bounds.Bottom)) ||
            SegmentsIntersect(start, end, new WorldPoint(bounds.X, bounds.Bottom), new WorldPoint(bounds.X, bounds.Y)))
        {
            return true;
        }

        var closestX = Math.Clamp(start.X, bounds.X, bounds.Right);
        var closestY = Math.Clamp(start.Y, bounds.Y, bounds.Bottom);
        if (DistanceSquaredToSegment(new WorldPoint(closestX, closestY), start, end) <= Square(radius))
        {
            return true;
        }

        closestX = Math.Clamp(end.X, bounds.X, bounds.Right);
        closestY = Math.Clamp(end.Y, bounds.Y, bounds.Bottom);
        return DistanceSquaredToSegment(new WorldPoint(closestX, closestY), start, end) <= Square(radius);
    }

    private static bool PointInside(WorldPoint point, WorldRectangle bounds) =>
        point.X >= bounds.X && point.X <= bounds.Right && point.Y >= bounds.Y && point.Y <= bounds.Bottom;

    private static long DistanceSquared(WorldPoint left, WorldPoint right)
    {
        var dx = (long)left.X - right.X;
        var dy = (long)left.Y - right.Y;
        return (dx * dx) + (dy * dy);
    }

    private static long DistanceSquaredToSegment(WorldPoint point, WorldPoint start, WorldPoint end)
    {
        var dx = (long)end.X - start.X;
        var dy = (long)end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0)
        {
            return DistanceSquared(point, start);
        }

        var projection = (((long)point.X - start.X) * dx) + (((long)point.Y - start.Y) * dy);
        projection = Math.Clamp(projection, 0, lengthSquared);
        var closest = new WorldPoint(
            start.X + (int)((dx * projection) / lengthSquared),
            start.Y + (int)((dy * projection) / lengthSquared));
        return DistanceSquared(point, closest);
    }

    private static long DistanceSquaredToSegments(WorldPoint aStart, WorldPoint aEnd, WorldPoint bStart, WorldPoint bEnd)
    {
        if (SegmentsIntersect(aStart, aEnd, bStart, bEnd))
        {
            return 0;
        }

        return Math.Min(
            Math.Min(DistanceSquaredToSegment(aStart, bStart, bEnd), DistanceSquaredToSegment(aEnd, bStart, bEnd)),
            Math.Min(DistanceSquaredToSegment(bStart, aStart, aEnd), DistanceSquaredToSegment(bEnd, aStart, aEnd)));
    }

    private static bool SegmentsIntersect(WorldPoint a, WorldPoint b, WorldPoint c, WorldPoint d)
    {
        var ab = Orientation(a, b, c);
        var ab2 = Orientation(a, b, d);
        var cd = Orientation(c, d, a);
        var cd2 = Orientation(c, d, b);
        return ((ab > 0 && ab2 < 0) || (ab < 0 && ab2 > 0) || (ab == 0 && OnSegment(a, b, c))) &&
               ((cd > 0 && cd2 < 0) || (cd < 0 && cd2 > 0) || (cd == 0 && OnSegment(c, d, a)));
    }

    private static long Orientation(WorldPoint a, WorldPoint b, WorldPoint c) =>
        ((long)b.X - a.X) * (c.Y - a.Y) - ((long)b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(WorldPoint a, WorldPoint b, WorldPoint point) =>
        point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
        point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);

    private static long Square(long value) => value * value;
}
