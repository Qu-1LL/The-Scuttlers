using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeCollisionDetector
{
    public static ResearchTreeCollisionResult DetectHitboxes(
        IReadOnlyList<ResearchTreeHitbox> hitboxes,
        bool includeFixedFixedPairs,
        bool includeMovingMovingPairs,
        float padding = 0f)
    {
        ArgumentNullException.ThrowIfNull(hitboxes);

        var result = new ResearchTreeCollisionResult();
        for (var firstIndex = 0; firstIndex < hitboxes.Count; firstIndex++)
        {
            var first = hitboxes[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < hitboxes.Count; secondIndex++)
            {
                var second = hitboxes[secondIndex];
                if (!ShouldTestPair(first, second, includeFixedFixedPairs, includeMovingMovingPairs) ||
                    ShouldIgnoreEndpointPair(first, second) ||
                    !HitboxesCollide(first, second, padding))
                {
                    continue;
                }

                result.AddHitbox(first);
                result.AddHitbox(second);
            }
        }

        return result;
    }

    public static ResearchTreeCollisionResult Detect(
        IReadOnlyList<ResearchTreeCollisionNode> movingNodes,
        IReadOnlyList<ResearchTreeCollisionLine> movingLines,
        IReadOnlyList<ResearchTreeCollisionNode> fixedNodes,
        IReadOnlyList<ResearchTreeCollisionLine> fixedLines,
        float padding = 0f)
    {
        var hitboxes = new List<ResearchTreeHitbox>(
            movingNodes.Count + movingLines.Count + fixedNodes.Count + fixedLines.Count);
        for (var index = 0; index < movingNodes.Count; index++)
        {
            var node = movingNodes[index];
            hitboxes.Add(ResearchTreeHitbox.Node(node.Id, ResearchTreeHitboxOwner.Moving, node.Center, node.Radius));
        }

        for (var index = 0; index < movingLines.Count; index++)
        {
            var line = movingLines[index];
            hitboxes.Add(ResearchTreeHitbox.Connector(
                line.Id,
                ResearchTreeHitboxOwner.Moving,
                line.Start,
                line.End,
                line.Thickness,
                new ResearchTreeHitboxEndpoint(ResearchTreeHitboxOwner.Fixed, line.IgnoredFixedNodeId),
                ResearchTreeHitboxEndpoint.None));
        }

        for (var index = 0; index < fixedNodes.Count; index++)
        {
            var node = fixedNodes[index];
            hitboxes.Add(ResearchTreeHitbox.Node(node.Id, ResearchTreeHitboxOwner.Fixed, node.Center, node.Radius));
        }

        for (var index = 0; index < fixedLines.Count; index++)
        {
            var line = fixedLines[index];
            hitboxes.Add(ResearchTreeHitbox.Connector(
                line.Id,
                ResearchTreeHitboxOwner.Fixed,
                line.Start,
                line.End,
                line.Thickness,
                ResearchTreeHitboxEndpoint.None,
                ResearchTreeHitboxEndpoint.None));
        }

        return DetectHitboxes(
            hitboxes,
            includeFixedFixedPairs: false,
            includeMovingMovingPairs: false,
            padding);
    }

    private static bool ShouldTestPair(
        ResearchTreeHitbox first,
        ResearchTreeHitbox second,
        bool includeFixedFixedPairs,
        bool includeMovingMovingPairs)
    {
        if (first.Owner != second.Owner)
        {
            return true;
        }

        return first.Owner == ResearchTreeHitboxOwner.Fixed
            ? includeFixedFixedPairs
            : includeMovingMovingPairs;
    }

    private static bool ShouldIgnoreEndpointPair(ResearchTreeHitbox first, ResearchTreeHitbox second)
    {
        return IsLineEndpointForNode(first, second) ||
            IsLineEndpointForNode(second, first) ||
            LinesShareEndpoint(first, second);
    }

    private static bool IsLineEndpointForNode(ResearchTreeHitbox maybeLine, ResearchTreeHitbox maybeNode)
    {
        return maybeLine.Kind == ResearchTreeHitboxKind.Connector &&
            maybeNode.Kind == ResearchTreeHitboxKind.Node &&
            (maybeLine.StartNode.Matches(maybeNode.Owner, maybeNode.Id) ||
                maybeLine.EndNode.Matches(maybeNode.Owner, maybeNode.Id));
    }

    private static bool LinesShareEndpoint(ResearchTreeHitbox first, ResearchTreeHitbox second)
    {
        return first.Kind == ResearchTreeHitboxKind.Connector &&
            second.Kind == ResearchTreeHitboxKind.Connector &&
            (first.StartNode.Matches(second.StartNode) ||
                first.StartNode.Matches(second.EndNode) ||
                first.EndNode.Matches(second.StartNode) ||
                first.EndNode.Matches(second.EndNode));
    }

    private static bool HitboxesCollide(ResearchTreeHitbox first, ResearchTreeHitbox second, float padding)
    {
        if (first.Kind == ResearchTreeHitboxKind.Node && second.Kind == ResearchTreeHitboxKind.Node)
        {
            return CirclesCollide(first.Center, first.Radius, second.Center, second.Radius, padding);
        }

        if (first.Kind == ResearchTreeHitboxKind.Node && second.Kind == ResearchTreeHitboxKind.Connector)
        {
            return NodeLineCollides(first, second, padding);
        }

        if (first.Kind == ResearchTreeHitboxKind.Connector && second.Kind == ResearchTreeHitboxKind.Node)
        {
            return NodeLineCollides(second, first, padding);
        }

        return LinesCollide(first, second, padding);
    }

    private static bool CirclesCollide(Vector2 firstCenter, float firstRadius, Vector2 secondCenter, float secondRadius, float padding)
    {
        var radius = MathF.Max(0f, firstRadius) + MathF.Max(0f, secondRadius) + MathF.Max(0f, padding);
        return Vector2.DistanceSquared(firstCenter, secondCenter) <= radius * radius;
    }

    private static bool NodeLineCollides(ResearchTreeHitbox node, ResearchTreeHitbox line, float padding)
    {
        var radius = MathF.Max(0f, node.Radius) + (MathF.Max(0f, line.Thickness) * 0.5f) + MathF.Max(0f, padding);
        return DistanceSquaredPointToSegment(node.Center, line.Start, line.End) < radius * radius;
    }

    private static bool LinesCollide(ResearchTreeHitbox first, ResearchTreeHitbox second, float padding)
    {
        var radius = (MathF.Max(0f, first.Thickness) * 0.5f) + (MathF.Max(0f, second.Thickness) * 0.5f) + MathF.Max(0f, padding);
        return SegmentDistanceSquared(first.Start, first.End, second.Start, second.End) < radius * radius;
    }

    private static float SegmentDistanceSquared(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        if (SegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd))
        {
            return 0f;
        }

        return MathF.Min(
            MathF.Min(
                DistanceSquaredPointToSegment(firstStart, secondStart, secondEnd),
                DistanceSquaredPointToSegment(firstEnd, secondStart, secondEnd)),
            MathF.Min(
                DistanceSquaredPointToSegment(secondStart, firstStart, firstEnd),
                DistanceSquaredPointToSegment(secondEnd, firstStart, firstEnd)));
    }

    private static float DistanceSquaredPointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        var segment = segmentEnd - segmentStart;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return Vector2.DistanceSquared(point, segmentStart);
        }

        var t = Math.Clamp(Vector2.Dot(point - segmentStart, segment) / lengthSquared, 0f, 1f);
        var projection = segmentStart + (segment * t);
        return Vector2.DistanceSquared(point, projection);
    }

    private static bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        var first = firstEnd - firstStart;
        var second = secondEnd - secondStart;
        var denominator = Cross(first, second);
        var betweenStarts = secondStart - firstStart;

        if (MathF.Abs(denominator) <= 0.0001f)
        {
            if (MathF.Abs(Cross(betweenStarts, first)) > 0.0001f)
            {
                return false;
            }

            return RangesOverlap(firstStart.X, firstEnd.X, secondStart.X, secondEnd.X) &&
                   RangesOverlap(firstStart.Y, firstEnd.Y, secondStart.Y, secondEnd.Y);
        }

        var firstRatio = Cross(betweenStarts, second) / denominator;
        var secondRatio = Cross(betweenStarts, first) / denominator;
        return firstRatio is >= 0f and <= 1f &&
               secondRatio is >= 0f and <= 1f;
    }

    private static bool RangesOverlap(float firstStart, float firstEnd, float secondStart, float secondEnd)
    {
        var firstMin = MathF.Min(firstStart, firstEnd);
        var firstMax = MathF.Max(firstStart, firstEnd);
        var secondMin = MathF.Min(secondStart, secondEnd);
        var secondMax = MathF.Max(secondStart, secondEnd);
        return firstMin <= secondMax && secondMin <= firstMax;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return (first.X * second.Y) - (first.Y * second.X);
    }
}

internal readonly record struct ResearchTreeCollisionNode(
    int Id,
    Vector2 Center,
    float Radius);

internal readonly record struct ResearchTreeCollisionLine(
    int Id,
    Vector2 Start,
    Vector2 End,
    float Thickness,
    int IgnoredFixedNodeId = -1);

internal enum ResearchTreeHitboxOwner
{
    Fixed,
    Moving
}

internal enum ResearchTreeHitboxKind
{
    Node,
    Connector
}

internal readonly record struct ResearchTreeHitboxEndpoint(
    ResearchTreeHitboxOwner Owner,
    int NodeId)
{
    public static ResearchTreeHitboxEndpoint None => new(ResearchTreeHitboxOwner.Fixed, -1);

    public bool IsValid => NodeId >= 0;

    public bool Matches(ResearchTreeHitboxOwner owner, int nodeId)
    {
        return IsValid && Owner == owner && NodeId == nodeId;
    }

    public bool Matches(ResearchTreeHitboxEndpoint other)
    {
        return IsValid && other.IsValid && Owner == other.Owner && NodeId == other.NodeId;
    }
}

internal readonly record struct ResearchTreeHitbox(
    int Id,
    ResearchTreeHitboxOwner Owner,
    ResearchTreeHitboxKind Kind,
    Vector2 Center,
    float Radius,
    Vector2 Start,
    Vector2 End,
    float Thickness,
    ResearchTreeHitboxEndpoint StartNode,
    ResearchTreeHitboxEndpoint EndNode)
{
    public static ResearchTreeHitbox Node(
        int id,
        ResearchTreeHitboxOwner owner,
        Vector2 center,
        float radius)
    {
        return new ResearchTreeHitbox(
            id,
            owner,
            ResearchTreeHitboxKind.Node,
            center,
            MathF.Max(0f, radius),
            Vector2.Zero,
            Vector2.Zero,
            0f,
            ResearchTreeHitboxEndpoint.None,
            ResearchTreeHitboxEndpoint.None);
    }

    public static ResearchTreeHitbox Connector(
        int id,
        ResearchTreeHitboxOwner owner,
        Vector2 start,
        Vector2 end,
        float thickness,
        ResearchTreeHitboxEndpoint startNode,
        ResearchTreeHitboxEndpoint endNode)
    {
        return new ResearchTreeHitbox(
            id,
            owner,
            ResearchTreeHitboxKind.Connector,
            Vector2.Zero,
            0f,
            start,
            end,
            MathF.Max(0f, thickness),
            startNode,
            endNode);
    }
}

internal sealed class ResearchTreeCollisionResult
{
    private readonly HashSet<int> _movingNodeIds = [];
    private readonly HashSet<int> _movingLineIds = [];
    private readonly HashSet<int> _fixedNodeIds = [];
    private readonly HashSet<int> _fixedLineIds = [];

    public static ResearchTreeCollisionResult Empty => new();

    public bool HasCollision =>
        _movingNodeIds.Count > 0 ||
        _movingLineIds.Count > 0 ||
        _fixedNodeIds.Count > 0 ||
        _fixedLineIds.Count > 0;

    public IReadOnlySet<int> MovingNodeIds => _movingNodeIds;

    public IReadOnlySet<int> MovingLineIds => _movingLineIds;

    public IReadOnlySet<int> FixedNodeIds => _fixedNodeIds;

    public IReadOnlySet<int> FixedLineIds => _fixedLineIds;

    public bool ContainsMovingNode(int id)
    {
        return _movingNodeIds.Contains(id);
    }

    public bool ContainsMovingLine(int id)
    {
        return _movingLineIds.Contains(id);
    }

    public bool ContainsFixedNode(int id)
    {
        return _fixedNodeIds.Contains(id);
    }

    public bool ContainsFixedLine(int id)
    {
        return _fixedLineIds.Contains(id);
    }

    internal void AddMovingNode(int id)
    {
        _movingNodeIds.Add(id);
    }

    internal void AddMovingLine(int id)
    {
        _movingLineIds.Add(id);
    }

    internal void AddFixedNode(int id)
    {
        _fixedNodeIds.Add(id);
    }

    internal void AddFixedLine(int id)
    {
        _fixedLineIds.Add(id);
    }

    internal void AddHitbox(ResearchTreeHitbox hitbox)
    {
        if (hitbox.Owner == ResearchTreeHitboxOwner.Moving)
        {
            if (hitbox.Kind == ResearchTreeHitboxKind.Node)
            {
                AddMovingNode(hitbox.Id);
            }
            else
            {
                AddMovingLine(hitbox.Id);
            }

            return;
        }

        if (hitbox.Kind == ResearchTreeHitboxKind.Node)
        {
            AddFixedNode(hitbox.Id);
        }
        else
        {
            AddFixedLine(hitbox.Id);
        }
    }
}
