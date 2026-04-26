using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Research;

internal sealed class TreeRenderNode<TPayload>
{
    private readonly List<TreeRenderNode<TPayload>> _children = [];

    public TreeRenderNode(TPayload payload)
    {
        Payload = payload;
    }

    public TPayload Payload { get; }

    public IReadOnlyList<TreeRenderNode<TPayload>> Children => _children;

    public TreeRenderNode<TPayload> AddChild(TreeRenderNode<TPayload> child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
        return child;
    }
}

internal sealed class TreeLayoutNode<TPayload>
{
    private readonly List<TreeLayoutNode<TPayload>> _children = [];

    public TreeLayoutNode(
        TPayload payload,
        Vector2 localPosition,
        float medialDegrees,
        TreeLayoutNode<TPayload>? parent)
    {
        Payload = payload;
        LocalPosition = localPosition;
        MedialDegrees = medialDegrees;
        Parent = parent;
    }

    public TPayload Payload { get; }

    public Vector2 LocalPosition { get; }

    public float MedialDegrees { get; }

    public TreeLayoutNode<TPayload>? Parent { get; }

    public IReadOnlyList<TreeLayoutNode<TPayload>> Children => _children;

    internal void AddChild(TreeLayoutNode<TPayload> child)
    {
        _children.Add(child);
    }
}

internal sealed class UniversalTreeLayoutResult<TPayload>
{
    public UniversalTreeLayoutResult(
        TreeLayoutNode<TPayload> root,
        IReadOnlyList<TreeLayoutNode<TPayload>> nodes,
        float minX,
        float maxX,
        float minY,
        float maxY)
    {
        Root = root;
        Nodes = nodes;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public TreeLayoutNode<TPayload> Root { get; }

    public IReadOnlyList<TreeLayoutNode<TPayload>> Nodes { get; }

    public float MinX { get; }

    public float MaxX { get; }

    public float MinY { get; }

    public float MaxY { get; }

    public float Width => MaxX - MinX;

    public float Height => MaxY - MinY;
}

internal readonly record struct UniversalTreeLayoutSettings(
    float EdgeLength,
    float RootMedialDegrees = -90f);

internal static class UniversalTreeLayout
{
    public static UniversalTreeLayoutResult<TPayload> Layout<TPayload>(
        TreeRenderNode<TPayload> root,
        UniversalTreeLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (settings.EdgeLength <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Edge length must be positive.");
        }

        var nodes = new List<TreeLayoutNode<TPayload>>();
        var minX = 0f;
        var maxX = 0f;
        var minY = 0f;
        var maxY = 0f;
        var layoutRoot = LayoutNode(
            root,
            parent: null,
            localPosition: Vector2.Zero,
            settings.RootMedialDegrees,
            settings.EdgeLength,
            nodes,
            ref minX,
            ref maxX,
            ref minY,
            ref maxY);
        return new UniversalTreeLayoutResult<TPayload>(layoutRoot, nodes, minX, maxX, minY, maxY);
    }

    private static TreeLayoutNode<TPayload> LayoutNode<TPayload>(
        TreeRenderNode<TPayload> source,
        TreeLayoutNode<TPayload>? parent,
        Vector2 localPosition,
        float medialDegrees,
        float edgeLength,
        List<TreeLayoutNode<TPayload>> nodes,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY)
    {
        var layoutNode = new TreeLayoutNode<TPayload>(source.Payload, localPosition, NormalizeDegrees(medialDegrees), parent);
        nodes.Add(layoutNode);
        minX = MathF.Min(minX, localPosition.X);
        maxX = MathF.Max(maxX, localPosition.X);
        minY = MathF.Min(minY, localPosition.Y);
        maxY = MathF.Max(maxY, localPosition.Y);

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
            var childPosition = localPosition + DegreesToUnitVector(childDegrees) * edgeLength;
            var childLayoutNode = LayoutNode(
                source.Children[index],
                layoutNode,
                childPosition,
                childDegrees,
                edgeLength,
                nodes,
                ref minX,
                ref maxX,
                ref minY,
                ref maxY);
            layoutNode.AddChild(childLayoutNode);
        }

        return layoutNode;
    }

    internal static float GetChildAngleDegrees(int childIndex, int childCount)
    {
        if (childIndex < 0 || childIndex >= childCount)
        {
            throw new ArgumentOutOfRangeException(nameof(childIndex));
        }

        var stepDegrees = 180f / (childCount + 1f);
        return -90f + ((childIndex + 1) * stepDegrees);
    }

    internal static Vector2 DegreesToUnitVector(float degrees)
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
}
