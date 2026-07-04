using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;

namespace TriloGame.Game.Runtime.Systems;

internal static class ResearchDraftPlacementValidator
{
    public const string BoundaryCollisionFailureReason = "That branch collides with the adaptation boundary.";
    public const string TreeCollisionFailureReason = "That branch collides with the existing skill tree.";

    // Include the rendered two-pixel border so visually touching nodes are collisions too.
    private const float NodeRadius = 19f / 92f;
    private const float ConnectorThickness = 3f / 92f;
    private const float ConnectorNodeInset = 2f / 92f;
    private const float BranchOriginInset = 7f / 92f;
    private const float CollisionPadding = 2f / 92f;

    public static ResearchDraftPlacementValidation Validate(
        SkillTree skillTree,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        ArgumentNullException.ThrowIfNull(skillTree);
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(anchorNode);

        var structurallyValid = skillTree.CanPlaceResearchBranch(branch, anchorNode, out var structuralFailureReason);
        var collision = DetectPlacementCollision(skillTree, branch, anchorNode);
        var canPlace = structurallyValid && !collision.HasCollision;
        return new ResearchDraftPlacementValidation(
            canPlace,
            structurallyValid,
            canPlace
                ? null
                : collision.HasCollision
                    ? collision.HasBoundaryCollision
                        ? BoundaryCollisionFailureReason
                        : TreeCollisionFailureReason
                    : structuralFailureReason ?? "That branch cannot be placed there.",
            collision);
    }

    public static ResearchDraftPlacementCollision DetectPlacementCollision(
        SkillTree skillTree,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        ArgumentNullException.ThrowIfNull(skillTree);
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(anchorNode);

        if (skillTree.Root is null || branch.Root is null)
        {
            return ResearchDraftPlacementCollision.Empty;
        }

        var projectedLayout = BuildProjectedPlacementLayout(skillTree.Root, branch, anchorNode);
        var hitboxes = new List<ResearchDraftPlacementHitbox>(
            (projectedLayout.Nodes.Count * 2) + ResearchDraftBoundaryProfile.Default.SegmentCount);

        for (var nodeIndex = 0; nodeIndex < projectedLayout.Nodes.Count; nodeIndex++)
        {
            var node = projectedLayout.Nodes[nodeIndex];
            hitboxes.Add(ResearchDraftPlacementHitbox.Node(
                GetProjectedNodeHitboxId(node),
                GetProjectedNodeHitboxOwner(node),
                node.Position,
                NodeRadius));

            if (node.Parent is null)
            {
                continue;
            }

            var isMovingLine = node.IsBranchNode || node.Parent.IsBranchNode;
            var startInset = isMovingLine && !node.Parent.IsBranchNode
                ? NodeRadius + BranchOriginInset
                : NodeRadius + ConnectorNodeInset;
            var start = node.Parent.Position;
            var end = node.Position;
            if (!TryInsetConnector(ref start, ref end, startInset, NodeRadius + ConnectorNodeInset))
            {
                continue;
            }

            hitboxes.Add(ResearchDraftPlacementHitbox.Connector(
                isMovingLine ? node.BranchNodeId : node.FixedNodeId,
                isMovingLine ? ResearchDraftPlacementHitboxOwner.Moving : ResearchDraftPlacementHitboxOwner.Fixed,
                start,
                end,
                ConnectorThickness,
                GetProjectedNodeEndpoint(node.Parent),
                GetProjectedNodeEndpoint(node)));
        }

        foreach (var boundarySegment in ResearchDraftBoundaryProfile.Default.CreateSegments())
        {
            hitboxes.Add(ResearchDraftPlacementHitbox.Connector(
                boundarySegment.Id,
                ResearchDraftPlacementHitboxOwner.Boundary,
                boundarySegment.Start,
                boundarySegment.End,
                ResearchDraftBoundaryProfile.Default.Thickness,
                ResearchDraftPlacementHitboxEndpoint.None,
                ResearchDraftPlacementHitboxEndpoint.None));
        }

        return DetectHitboxes(hitboxes, CollisionPadding);
    }

    public static ResearchDraftProjectedLayout BuildProjectedPlacementLayout(
        TreeInstanceNode root,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(anchorNode);

        if (branch.Root is null)
        {
            return new ResearchDraftProjectedLayout([]);
        }

        var renderRoot = BuildProjectedTreeRenderNode(root, branch.Root, anchorNode);
        var layoutNodes = new List<ResearchDraftProjectedNode>();
        var fixedNodeId = 0;
        var branchNodeId = 0;
        LayoutProjectedNode(
            renderRoot,
            parent: null,
            localPosition: Vector2.Zero,
            medialDegrees: -90f,
            layoutNodes,
            ref fixedNodeId,
            ref branchNodeId);
        return new ResearchDraftProjectedLayout(layoutNodes);
    }

    private static ResearchDraftProjectedSourceNode BuildProjectedTreeRenderNode(
        TreeInstanceNode node,
        TreeInstanceNode branchRoot,
        TreeInstanceNode anchorNode)
    {
        var renderNode = new ResearchDraftProjectedSourceNode(node, isBranchNode: false);
        foreach (var child in node.Children)
        {
            renderNode.Children.Add(BuildProjectedTreeRenderNode(child, branchRoot, anchorNode));
        }

        if (ReferenceEquals(node, anchorNode))
        {
            renderNode.Children.Add(BuildProjectedBranchRenderNode(branchRoot));
        }

        return renderNode;
    }

    private static ResearchDraftProjectedSourceNode BuildProjectedBranchRenderNode(TreeInstanceNode node)
    {
        var renderNode = new ResearchDraftProjectedSourceNode(node, isBranchNode: true);
        foreach (var child in node.Children)
        {
            renderNode.Children.Add(BuildProjectedBranchRenderNode(child));
        }

        return renderNode;
    }

    private static ResearchDraftProjectedNode LayoutProjectedNode(
        ResearchDraftProjectedSourceNode source,
        ResearchDraftProjectedNode? parent,
        Vector2 localPosition,
        float medialDegrees,
        List<ResearchDraftProjectedNode> nodes,
        ref int fixedNodeId,
        ref int branchNodeId)
    {
        var isBranchNode = source.IsBranchNode;
        var layoutNode = new ResearchDraftProjectedNode(
            source.Node,
            parent,
            localPosition,
            NormalizeDegrees(medialDegrees),
            isBranchNode,
            isBranchNode ? -1 : fixedNodeId++,
            isBranchNode ? branchNodeId++ : -1);
        nodes.Add(layoutNode);

        var childCount = source.Children.Count;
        if (childCount == 0)
        {
            return layoutNode;
        }

        var stepDegrees = 180f / (childCount + 1f);
        for (var index = 0; index < childCount; index++)
        {
            var relativeDegrees = -90f + ((index + 1) * stepDegrees);
            var childDegrees = medialDegrees + relativeDegrees;
            var childPosition = localPosition + (DegreesToUnitVector(childDegrees) * 1f);
            LayoutProjectedNode(
                source.Children[index],
                layoutNode,
                childPosition,
                childDegrees,
                nodes,
                ref fixedNodeId,
                ref branchNodeId);
        }

        return layoutNode;
    }

    internal static ResearchDraftPlacementCollision DetectHitboxes(
        IReadOnlyList<ResearchDraftPlacementHitbox> hitboxes,
        float padding)
    {
        var result = new ResearchDraftPlacementCollision();
        for (var firstIndex = 0; firstIndex < hitboxes.Count; firstIndex++)
        {
            var first = hitboxes[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < hitboxes.Count; secondIndex++)
            {
                var second = hitboxes[secondIndex];
                if (!ShouldTestPair(first, second) ||
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

    private static bool ShouldTestPair(
        ResearchDraftPlacementHitbox first,
        ResearchDraftPlacementHitbox second)
    {
        if (first.Owner == ResearchDraftPlacementHitboxOwner.Boundary &&
            second.Owner == ResearchDraftPlacementHitboxOwner.Boundary)
        {
            return false;
        }

        if (first.Owner == ResearchDraftPlacementHitboxOwner.Boundary ||
            second.Owner == ResearchDraftPlacementHitboxOwner.Boundary)
        {
            return true;
        }

        return true;
    }

    private static bool ShouldIgnoreEndpointPair(
        ResearchDraftPlacementHitbox first,
        ResearchDraftPlacementHitbox second)
    {
        // Connectors are inset from both endpoint nodes before hitboxes are built, so
        // connector/connector pairs remain safe to test even when they share a node.
        // Ignoring the whole pair can hide a later crossing or collinear overlap.
        return IsLineEndpointForNode(first, second) ||
            IsLineEndpointForNode(second, first);
    }

    private static bool IsLineEndpointForNode(
        ResearchDraftPlacementHitbox maybeLine,
        ResearchDraftPlacementHitbox maybeNode)
    {
        return maybeLine.Kind == ResearchDraftPlacementHitboxKind.Connector &&
            maybeNode.Kind == ResearchDraftPlacementHitboxKind.Node &&
            (maybeLine.StartNode.Matches(maybeNode.Owner, maybeNode.Id) ||
                maybeLine.EndNode.Matches(maybeNode.Owner, maybeNode.Id));
    }

    private static bool HitboxesCollide(
        ResearchDraftPlacementHitbox first,
        ResearchDraftPlacementHitbox second,
        float padding)
    {
        if (first.Kind == ResearchDraftPlacementHitboxKind.Node &&
            second.Kind == ResearchDraftPlacementHitboxKind.Node)
        {
            return CirclesCollide(first.Center, first.Radius, second.Center, second.Radius, padding);
        }

        if (first.Kind == ResearchDraftPlacementHitboxKind.Node &&
            second.Kind == ResearchDraftPlacementHitboxKind.Connector)
        {
            return NodeLineCollides(first, second, padding);
        }

        if (first.Kind == ResearchDraftPlacementHitboxKind.Connector &&
            second.Kind == ResearchDraftPlacementHitboxKind.Node)
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

    private static bool NodeLineCollides(ResearchDraftPlacementHitbox node, ResearchDraftPlacementHitbox line, float padding)
    {
        var radius = MathF.Max(0f, node.Radius) + (MathF.Max(0f, line.Thickness) * 0.5f) + MathF.Max(0f, padding);
        return DistanceSquaredPointToSegment(node.Center, line.Start, line.End) <= radius * radius;
    }

    private static bool LinesCollide(ResearchDraftPlacementHitbox first, ResearchDraftPlacementHitbox second, float padding)
    {
        var radius = (MathF.Max(0f, first.Thickness) * 0.5f) + (MathF.Max(0f, second.Thickness) * 0.5f) + MathF.Max(0f, padding);
        return SegmentDistanceSquared(first.Start, first.End, second.Start, second.End) <= radius * radius;
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

    private static bool TryInsetConnector(ref Vector2 start, ref Vector2 end, float startInset, float endInset)
    {
        var delta = end - start;
        var distance = delta.Length();
        if (distance <= float.Epsilon || distance <= startInset + endInset)
        {
            return false;
        }

        var direction = delta / distance;
        start += direction * MathF.Max(0f, startInset);
        end -= direction * MathF.Max(0f, endInset);
        return true;
    }

    private static int GetProjectedNodeHitboxId(ResearchDraftProjectedNode node)
    {
        return node.IsBranchNode ? node.BranchNodeId : node.FixedNodeId;
    }

    private static ResearchDraftPlacementHitboxOwner GetProjectedNodeHitboxOwner(ResearchDraftProjectedNode node)
    {
        return node.IsBranchNode ? ResearchDraftPlacementHitboxOwner.Moving : ResearchDraftPlacementHitboxOwner.Fixed;
    }

    private static ResearchDraftPlacementHitboxEndpoint GetProjectedNodeEndpoint(ResearchDraftProjectedNode node)
    {
        return new ResearchDraftPlacementHitboxEndpoint(GetProjectedNodeHitboxOwner(node), GetProjectedNodeHitboxId(node));
    }

    private static Vector2 DegreesToUnitVector(float degrees)
    {
        var radians = MathHelper.ToRadians(degrees);
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    private static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360f;
        return normalized <= -180f
            ? normalized + 360f
            : normalized > 180f
                ? normalized - 360f
                : normalized;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return (first.X * second.Y) - (first.Y * second.X);
    }
}

internal readonly record struct ResearchDraftPlacementValidation(
    bool CanPlace,
    bool IsStructurallyValid,
    string? FailureReason,
    ResearchDraftPlacementCollision Collision);

internal readonly record struct ResearchDraftBoundaryProfile(
    float StartX,
    float StartY,
    float FirstControlX,
    float FirstControlY,
    float SecondControlX,
    float SecondControlY,
    float EndX,
    float EndY,
    int SamplesPerSide,
    float Thickness)
{
    public static ResearchDraftBoundaryProfile Default { get; } = new(
        StartX: 0.6f,
        StartY: -0.14f,
        FirstControlX: 0.92f,
        FirstControlY: -1.08f,
        SecondControlX: 5.8f,
        SecondControlY: -4.55f,
        EndX: 9.2f,
        EndY: -9.4f,
        SamplesPerSide: 24,
        Thickness: 0.04f);

    public int SegmentCount => SamplesPerSide * 2;

    public IReadOnlyList<ResearchDraftBoundarySegment> CreateSegments(float scale = 1f, Vector2 offset = default)
    {
        var safeSamples = Math.Max(1, SamplesPerSide);
        var segments = new List<ResearchDraftBoundarySegment>(safeSamples * 2);
        AddSideSegments(segments, mirrorX: -1f, safeSamples, scale, offset);
        AddSideSegments(segments, mirrorX: 1f, safeSamples, scale, offset);
        return segments;
    }

    private void AddSideSegments(
        List<ResearchDraftBoundarySegment> segments,
        float mirrorX,
        int samples,
        float scale,
        Vector2 offset)
    {
        var previous = Transform(Evaluate(mirrorX, 0f), scale, offset);
        for (var sample = 1; sample <= samples; sample++)
        {
            var current = Transform(Evaluate(mirrorX, sample / (float)samples), scale, offset);
            segments.Add(new ResearchDraftBoundarySegment(segments.Count, previous, current));
            previous = current;
        }
    }

    private Vector2 Evaluate(float mirrorX, float t)
    {
        var start = new Vector2(StartX * mirrorX, StartY);
        var firstControl = new Vector2(FirstControlX * mirrorX, FirstControlY);
        var secondControl = new Vector2(SecondControlX * mirrorX, SecondControlY);
        var end = new Vector2(EndX * mirrorX, EndY);
        var inverse = 1f - t;
        return (start * inverse * inverse * inverse) +
            (firstControl * 3f * inverse * inverse * t) +
            (secondControl * 3f * inverse * t * t) +
            (end * t * t * t);
    }

    private static Vector2 Transform(Vector2 point, float scale, Vector2 offset)
    {
        return (point * scale) + offset;
    }
}

internal readonly record struct ResearchDraftBoundarySegment(
    int Id,
    Vector2 Start,
    Vector2 End);

internal sealed record ResearchDraftProjectedNode(
    TreeInstanceNode SkillNode,
    ResearchDraftProjectedNode? Parent,
    Vector2 Position,
    float MedialDegrees,
    bool IsBranchNode,
    int FixedNodeId,
    int BranchNodeId);

internal sealed record ResearchDraftProjectedLayout(
    IReadOnlyList<ResearchDraftProjectedNode> Nodes);

internal sealed class ResearchDraftPlacementCollision
{
    private readonly HashSet<int> _movingNodeIds = [];
    private readonly HashSet<int> _movingLineIds = [];
    private readonly HashSet<int> _fixedNodeIds = [];
    private readonly HashSet<int> _fixedLineIds = [];
    private readonly HashSet<int> _boundaryLineIds = [];

    public static ResearchDraftPlacementCollision Empty => new();

    public bool HasCollision =>
        _movingNodeIds.Count > 0 ||
        _movingLineIds.Count > 0 ||
        _fixedNodeIds.Count > 0 ||
        _fixedLineIds.Count > 0 ||
        _boundaryLineIds.Count > 0;

    public bool HasBoundaryCollision => _boundaryLineIds.Count > 0;

    public IReadOnlySet<int> MovingNodeIds => _movingNodeIds;

    public IReadOnlySet<int> MovingLineIds => _movingLineIds;

    public IReadOnlySet<int> FixedNodeIds => _fixedNodeIds;

    public IReadOnlySet<int> FixedLineIds => _fixedLineIds;

    public IReadOnlySet<int> BoundaryLineIds => _boundaryLineIds;

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

    public bool ContainsBoundaryLine(int id)
    {
        return _boundaryLineIds.Contains(id);
    }

    internal void AddHitbox(ResearchDraftPlacementHitbox hitbox)
    {
        if (hitbox.Owner == ResearchDraftPlacementHitboxOwner.Boundary)
        {
            _boundaryLineIds.Add(hitbox.Id);
            return;
        }

        if (hitbox.Owner == ResearchDraftPlacementHitboxOwner.Moving)
        {
            if (hitbox.Kind == ResearchDraftPlacementHitboxKind.Node)
            {
                _movingNodeIds.Add(hitbox.Id);
            }
            else
            {
                _movingLineIds.Add(hitbox.Id);
            }

            return;
        }

        if (hitbox.Kind == ResearchDraftPlacementHitboxKind.Node)
        {
            _fixedNodeIds.Add(hitbox.Id);
        }
        else
        {
            _fixedLineIds.Add(hitbox.Id);
        }
    }
}

internal sealed class ResearchDraftProjectedSourceNode
{
    public ResearchDraftProjectedSourceNode(TreeInstanceNode node, bool isBranchNode)
    {
        Node = node;
        IsBranchNode = isBranchNode;
    }

    public TreeInstanceNode Node { get; }

    public bool IsBranchNode { get; }

    public List<ResearchDraftProjectedSourceNode> Children { get; } = [];
}

internal enum ResearchDraftPlacementHitboxOwner
{
    Fixed,
    Moving,
    Boundary
}

internal enum ResearchDraftPlacementHitboxKind
{
    Node,
    Connector
}

internal readonly record struct ResearchDraftPlacementHitboxEndpoint(
    ResearchDraftPlacementHitboxOwner Owner,
    int NodeId)
{
    public static ResearchDraftPlacementHitboxEndpoint None => new(ResearchDraftPlacementHitboxOwner.Fixed, -1);

    public bool IsValid => NodeId >= 0;

    public bool Matches(ResearchDraftPlacementHitboxOwner owner, int nodeId)
    {
        return IsValid && Owner == owner && NodeId == nodeId;
    }

    public bool Matches(ResearchDraftPlacementHitboxEndpoint other)
    {
        return IsValid && other.IsValid && Owner == other.Owner && NodeId == other.NodeId;
    }
}

internal readonly record struct ResearchDraftPlacementHitbox(
    int Id,
    ResearchDraftPlacementHitboxOwner Owner,
    ResearchDraftPlacementHitboxKind Kind,
    Vector2 Center,
    float Radius,
    Vector2 Start,
    Vector2 End,
    float Thickness,
    ResearchDraftPlacementHitboxEndpoint StartNode,
    ResearchDraftPlacementHitboxEndpoint EndNode)
{
    public static ResearchDraftPlacementHitbox Node(
        int id,
        ResearchDraftPlacementHitboxOwner owner,
        Vector2 center,
        float radius)
    {
        return new ResearchDraftPlacementHitbox(
            id,
            owner,
            ResearchDraftPlacementHitboxKind.Node,
            center,
            MathF.Max(0f, radius),
            Vector2.Zero,
            Vector2.Zero,
            0f,
            ResearchDraftPlacementHitboxEndpoint.None,
            ResearchDraftPlacementHitboxEndpoint.None);
    }

    public static ResearchDraftPlacementHitbox Connector(
        int id,
        ResearchDraftPlacementHitboxOwner owner,
        Vector2 start,
        Vector2 end,
        float thickness,
        ResearchDraftPlacementHitboxEndpoint startNode,
        ResearchDraftPlacementHitboxEndpoint endNode)
    {
        return new ResearchDraftPlacementHitbox(
            id,
            owner,
            ResearchDraftPlacementHitboxKind.Connector,
            Vector2.Zero,
            0f,
            start,
            end,
            MathF.Max(0f, thickness),
            startNode,
            endNode);
    }
}
