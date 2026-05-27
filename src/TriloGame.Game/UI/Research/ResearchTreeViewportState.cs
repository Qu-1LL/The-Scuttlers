using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;

namespace TriloGame.Game.UI.Research;

internal sealed class ResearchTreeViewportState
{
    private const float TreeDragThresholdPixels = 10f;
    private const float MinimumTreeEdgeLength = 80f;
    private const float MaximumTreeEdgeLength = 108f;
    private const float MinimumZoom = 0.55f;
    private const float MaximumZoom = 2.25f;

    private Point _panStartPointer;
    private Vector2 _panStartOffset;
    private bool _panCandidate;
    private bool _panning;

    public Vector2 PanOffset { get; private set; }

    public float Zoom { get; private set; } = 1f;

    public void Reset()
    {
        PanOffset = Vector2.Zero;
        _panStartPointer = Point.Zero;
        _panStartOffset = Vector2.Zero;
        Zoom = 1f;
        _panCandidate = false;
        _panning = false;
    }

    public void BeginPan(Point point)
    {
        _panCandidate = true;
        _panning = false;
        _panStartPointer = point;
        _panStartOffset = PanOffset;
    }

    public void DragPan(Point point)
    {
        if (!_panCandidate)
        {
            return;
        }

        var dragDelta = point - _panStartPointer;
        if (!_panning && dragDelta.ToVector2().Length() >= TreeDragThresholdPixels)
        {
            _panning = true;
        }

        if (_panning)
        {
            PanOffset = _panStartOffset + dragDelta.ToVector2();
        }
    }

    public bool EndPan(Rectangle treeBounds, SkillTree skillTree)
    {
        var wasPanning = _panning;
        _panCandidate = false;
        _panning = false;
        if (wasPanning)
        {
            PanOffset = ResolvePanAfterRelease(treeBounds, skillTree, PanOffset, Zoom);
        }

        return wasPanning;
    }

    public void ZoomAt(Point point, int wheelDelta, Rectangle treeBounds, SkillTree skillTree)
    {
        var previousZoom = Zoom;
        Zoom = ClampZoom(Zoom + (-wheelDelta * 0.0015f));
        if (MathF.Abs(Zoom - previousZoom) <= float.Epsilon)
        {
            return;
        }

        var metricsBefore = BuildMetrics(treeBounds, skillTree, previousZoom);
        var pointToOrigin = point.ToVector2() - metricsBefore.Origin - PanOffset;
        if (previousZoom > float.Epsilon)
        {
            PanOffset += pointToOrigin - (pointToOrigin * (Zoom / previousZoom));
        }
    }

    public ResearchTreeViewportMetrics BuildMetrics(Rectangle bounds, SkillTree skillTree)
    {
        return BuildMetrics(bounds, skillTree, Zoom);
    }

    public static ResearchTreeViewportMetrics BuildMetrics(Rectangle bounds, SkillTree skillTree, float zoom)
    {
        const int sidePadding = 12;
        const int topPadding = 8;
        const int bottomPadding = 12;

        var contentBounds = new Rectangle(
            bounds.X + sidePadding,
            bounds.Y + topPadding,
            Math.Max(120, bounds.Width - (sidePadding * 2)),
            Math.Max(120, bounds.Height - topPadding - bottomPadding));
        var edgeLength = Math.Clamp(
            MathF.Min(contentBounds.Width, contentBounds.Height) * 0.18f,
            MinimumTreeEdgeLength,
            MaximumTreeEdgeLength) * ClampZoom(zoom);
        var nodeRadius = Math.Clamp((int)MathF.Round(edgeLength * 0.18f), 9, 18);
        var origin = new Vector2(contentBounds.Center.X, contentBounds.Bottom - nodeRadius - 8f);
        var baseBounds = skillTree.Root is null
            ? new ResearchTreeViewportBounds(0f, 0f, 0f, 0f)
            : BuildTreeBounds(origin, edgeLength, skillTree.Root);

        return new ResearchTreeViewportMetrics(bounds, contentBounds, origin, edgeLength, nodeRadius, baseBounds);
    }

    public static Vector2 ResolvePanAfterRelease(
        Rectangle treeBounds,
        SkillTree skillTree,
        Vector2 panOffset,
        float zoom)
    {
        if (skillTree.Root is null)
        {
            return Vector2.Zero;
        }

        var metrics = BuildMetrics(treeBounds, skillTree, zoom);
        var pannedBounds = BuildVisibleContentBounds(metrics).Offset(panOffset);
        if (pannedBounds.Intersects(metrics.ContentBounds))
        {
            return panOffset;
        }

        return CalculateTreeCenteringPanOffset(metrics);
    }

    public static ResearchTreeViewportBounds BuildVisibleContentBounds(Rectangle treeBounds, SkillTree skillTree, float zoom = 1f)
    {
        var metrics = BuildMetrics(treeBounds, skillTree, zoom);
        return BuildVisibleContentBounds(metrics);
    }

    public static ResearchTreeViewportBounds BuildVisibleContentBounds(ResearchTreeViewportMetrics metrics)
    {
        return metrics.BaseBounds.Expand(metrics.NodeRadius);
    }

    public static float ClampZoom(float zoom)
    {
        return Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }

    private static ResearchTreeViewportBounds BuildTreeBounds(Vector2 origin, float edgeLength, TreeInstanceNode root)
    {
        var layout = UniversalTreeLayout.Layout(BuildTreeRenderNode(root), new UniversalTreeLayoutSettings(edgeLength));
        return new ResearchTreeViewportBounds(
            origin.X + layout.MinX,
            origin.X + layout.MaxX,
            origin.Y + layout.MinY,
            origin.Y + layout.MaxY);
    }

    private static TreeRenderNode<TreeInstanceNode> BuildTreeRenderNode(TreeInstanceNode node)
    {
        var renderNode = new TreeRenderNode<TreeInstanceNode>(node);
        foreach (var child in node.Children)
        {
            renderNode.AddChild(BuildTreeRenderNode(child));
        }

        return renderNode;
    }

    private static Vector2 CalculateTreeCenteringPanOffset(ResearchTreeViewportMetrics metrics)
    {
        return metrics.ContentBounds.Center.ToVector2() - metrics.BaseBounds.Center;
    }
}

internal readonly record struct ResearchTreeViewportMetrics(
    Rectangle Bounds,
    Rectangle ContentBounds,
    Vector2 Origin,
    float EdgeLength,
    int NodeRadius,
    ResearchTreeViewportBounds BaseBounds);

internal readonly record struct ResearchTreeViewportBounds(
    float MinX,
    float MaxX,
    float MinY,
    float MaxY)
{
    public float Width => MaxX - MinX;

    public float Height => MaxY - MinY;

    public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

    public ResearchTreeViewportBounds Expand(float padding)
    {
        return new ResearchTreeViewportBounds(
            MinX - padding,
            MaxX + padding,
            MinY - padding,
            MaxY + padding);
    }

    public ResearchTreeViewportBounds Offset(Vector2 offset)
    {
        return new ResearchTreeViewportBounds(
            MinX + offset.X,
            MaxX + offset.X,
            MinY + offset.Y,
            MaxY + offset.Y);
    }

    public bool Intersects(Rectangle rectangle)
    {
        return MaxX >= rectangle.Left &&
            MinX <= rectangle.Right &&
            MaxY >= rectangle.Top &&
            MinY <= rectangle.Bottom;
    }
}
