using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum.GueDeriving;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeUiRenderer
{
    public const float MinimumZoom = 0.55f;
    public const float MaximumZoom = 2.25f;
    private const float BaseDetailEdgeLength = 92f;
    private static readonly Color ConnectorColor = new(246, 251, 253);
    private static readonly Color UnlockedConnectorColor = new(247, 221, 92);

    public static readonly ResearchTreeRenderConfig TreeEntryCardConfig = new(
        ShowBackButton: false,
        ShowRootNode: true,
        EnableNodeSelection: true,
        EnableBranchDrafting: false,
        EnablePlacementPreview: false);

    public static readonly ResearchTreeRenderConfig ReadOnlyDetailConfig = new(
        ShowBackButton: true,
        ShowRootNode: true,
        EnableNodeSelection: true,
        EnableBranchDrafting: false,
        EnablePlacementPreview: false);

    public static ResearchTreeViewLayout CalculateCardTreeLayout(
        ResearchTreeViewNode root,
        Rectangle bounds,
        ResearchTreeRenderConfig config = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        const float padding = 14f;
        var layout = BuildLayout(root, edgeLength: 1f, config);
        var availableWidth = Math.Max(60f, bounds.Width - (padding * 2f));
        var availableHeight = Math.Max(60f, bounds.Height - (padding * 2f));
        var scale = MathF.Min(
            availableWidth / MathF.Max(1f, layout.Bounds.Width),
            availableHeight / MathF.Max(1f, layout.Bounds.Height));
        scale = MathF.Max(18f, scale);
        var radius = Math.Clamp((int)MathF.Round(scale * 0.18f), 5, 13);
        var layoutWidth = layout.Bounds.Width * scale;
        var layoutHeight = layout.Bounds.Height * scale;
        var offset = new Vector2(
            bounds.X + padding + radius + ((availableWidth - (radius * 2f) - layoutWidth) / 2f) - (layout.Bounds.MinX * scale),
            bounds.Y + padding + radius + ((availableHeight - (radius * 2f) - layoutHeight) / 2f) - (layout.Bounds.MinY * scale));

        var nodes = new List<ResearchTreeViewLayoutNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new ResearchTreeViewLayoutNode(
                node.Node,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Node)),
                (node.LocalPosition * scale) + offset));
        }

        return new ResearchTreeViewLayout(nodes, radius, bounds);
    }

    public static ResearchTreeViewNode? DrawTreeEntryCard(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeCardData card,
        ResearchTreeRenderConfig config,
        Point pointerPoint)
    {
        return DrawTreeEntryCard(gumUi, parent: null, session, card, config, pointerPoint);
    }

    public static ResearchTreeViewNode? DrawTreeEntryCard(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        GameSession session,
        ResearchTreeCardData card,
        ResearchTreeRenderConfig config,
        Point pointerPoint)
    {
        var fill = card.IsSelected
            ? new Color(34, 70, 92)
            : card.IsHovered ? new Color(20, 45, 63) : new Color(13, 30, 44);
        var border = card.IsSelected
            ? new Color(214, 236, 244)
            : card.IsHovered ? new Color(132, 181, 198) : new Color(66, 101, 118);

        var cardFrameStyle = new GumUiFrameStyle(fill, border, 2, 14);
        DrawCardFrame(gumUi, parent, card.Bounds, cardFrameStyle);
        var layout = ResearchTreeCardRenderer.BuildLayout(card.Bounds);
        DrawCardText(
            gumUi,
            parent,
            layout.TitleBounds,
            card.Title,
            Color.White,
            GumTextStyle.Small);
        DrawCardText(
            gumUi,
            parent,
            layout.SubtitleBounds,
            card.Subtitle,
            new Color(184, 206, 216),
            GumTextStyle.Compact);

        if (card.Root is null)
        {
            DrawCardCenteredText(
                gumUi,
                parent,
                layout.PreviewBounds,
                "Unavailable",
                new Color(191, 204, 211),
                GumTextStyle.Small);
            return null;
        }

        DrawCardTree(gumUi, parent, session, layout.PreviewBounds, card.Root, config);
        if (!config.EnableNodeSelection)
        {
            return null;
        }

        var hoveredNode = TryGetHoveredCardNode(card.Root, layout.PreviewBounds, pointerPoint, config, out var hoveredCenter);
        if (hoveredNode is not null)
        {
            var treeLayout = CalculateCardTreeLayout(card.Root, layout.PreviewBounds, config);
            DrawNodeOutline(gumUi, parent, hoveredCenter, treeLayout.Radius, new Color(255, 255, 255, 240), 6, 2);
        }

        return hoveredNode;
    }

    public static ResearchTreeViewNode? TryGetHoveredCardNode(
        ResearchTreeViewNode root,
        Rectangle bounds,
        Point pointerPoint,
        ResearchTreeRenderConfig config,
        out Vector2 center)
    {
        center = Vector2.Zero;
        var layout = CalculateCardTreeLayout(root, bounds, config);
        var hitRadius = layout.Radius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        ResearchTreeViewNode? hovered = null;
        var pointer = pointerPoint.ToVector2();
        foreach (var node in layout.Nodes)
        {
            var distanceSquared = Vector2.DistanceSquared(node.Position, pointer);
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            hovered = node.Node;
            center = node.Position;
        }

        return hovered;
    }

    public static ResearchTreeDetailMetrics CalculateDetailMetrics(
        Rectangle bounds,
        ResearchTreeViewNode root,
        float zoom,
        ResearchTreeRenderConfig config = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var safeZoom = ClampZoom(zoom);
        var contentBounds = Inset(bounds, 14);
        var edgeLength = BaseDetailEdgeLength * safeZoom;
        var nodeRadius = Math.Clamp((int)MathF.Round(edgeLength * 0.18f), 9, 18);
        var origin = new Vector2(contentBounds.Center.X, contentBounds.Bottom - nodeRadius - 8f);
        var layout = BuildLayout(root, edgeLength, config);
        var baseBounds = new ResearchTreeBounds(
            origin.X + layout.Bounds.MinX - nodeRadius,
            origin.X + layout.Bounds.MaxX + nodeRadius,
            origin.Y + layout.Bounds.MinY - nodeRadius,
            origin.Y + layout.Bounds.MaxY + nodeRadius);

        return new ResearchTreeDetailMetrics(bounds, contentBounds, origin, edgeLength, nodeRadius, baseBounds);
    }

    public static Vector2 ResolvePanAfterRelease(
        Rectangle bounds,
        ResearchTreeViewNode root,
        Vector2 panOffset,
        float zoom,
        ResearchTreeRenderConfig config = default)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom, config);
        var pannedBounds = metrics.BaseBounds.Offset(panOffset);
        if (pannedBounds.Intersects(metrics.ContentBounds))
        {
            return panOffset;
        }

        return metrics.ContentBounds.Center.ToVector2() - metrics.BaseBounds.Center;
    }

    public static float ClampZoom(float zoom)
    {
        return Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }

    public static ResearchTreeViewNode? DrawDetail(
        GumUiRenderer gumUi,
        GameSession session,
        Rectangle bounds,
        ResearchTreeViewNode root,
        Vector2 panOffset,
        float zoom,
        Texture2D? backgroundTexture,
        Point pointerPoint,
        ResearchTreeRenderConfig config)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom, config);
        DrawTiledBackground(gumUi, metrics.ContentBounds, backgroundTexture, panOffset, zoom, metrics.Origin);
        gumUi.AddRoundedOutline(metrics.ContentBounds, new Color(74, 115, 134), 1, 12);

        var layout = BuildLayout(root, metrics.EdgeLength, config);
        var nodes = new List<ResearchTreeDetailNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new ResearchTreeDetailNode(
                node.Node,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Node)),
                metrics.Origin + panOffset + node.LocalPosition));
        }

        var hoveredPosition = Vector2.Zero;
        var hoveredNode = config.EnableNodeSelection
            ? TryGetHoveredDetailNode(metrics, nodes, pointerPoint, out hoveredPosition)
            : null;
        foreach (var node in nodes)
        {
            if (node.Parent is null)
            {
                continue;
            }

            DrawClippedConnector(
                gumUi,
                metrics.ContentBounds,
                node.Parent.Position,
                node.Position,
                node.Node.IsUnlocked ? UnlockedConnectorColor : ConnectorColor,
                3,
                metrics.NodeRadius + 2f,
                metrics.NodeRadius + 2f);
        }

        foreach (var node in nodes)
        {
            if (!IsNodeVisible(metrics.ContentBounds, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            DrawTreeNode(
                gumUi,
                node.Position,
                metrics.NodeRadius,
                GetNodeFillColor(session, node.Node),
                GetNodeBorderColor(session, node.Node));
        }

        if (hoveredNode is not null)
        {
            DrawNodeOutline(gumUi, hoveredPosition, metrics.NodeRadius, new Color(255, 255, 255, 240), 6, 2);
        }

        return hoveredNode;
    }

    public static ResearchNodeInfo BuildNodeInfo(GameSession session, ResearchTreeViewNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        return ResearchNodeTextFormatter.BuildNodeInfo(session, node);
    }

    private static void DrawCardTree(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        GameSession session,
        Rectangle bounds,
        ResearchTreeViewNode root,
        ResearchTreeRenderConfig config)
    {
        DrawCardOutline(gumUi, parent, bounds, new Color(55, 87, 103), 1, 10);
        var preview = CalculateCardTreeLayout(root, bounds, config);
        foreach (var node in preview.Nodes)
        {
            if (node.Parent is null)
            {
                continue;
            }

            DrawCardConnector(
                gumUi,
                parent,
                node.Parent.Position,
                node.Position,
                ConnectorColor,
                2,
                preview.Radius + 2f,
                preview.Radius + 2f);
        }

        foreach (var node in preview.Nodes)
        {
            DrawTreeNode(
                gumUi,
                parent,
                node.Position,
                preview.Radius,
                GetNodeFillColor(session, node.Node),
                GetNodeBorderColor(session, node.Node));
        }
    }

    private static ResearchTreeLayout BuildLayout(
        ResearchTreeViewNode root,
        float edgeLength,
        ResearchTreeRenderConfig config)
    {
        var layoutRoot = config.ShowRootNode || root.Children.Count == 0
            ? BuildRenderNode(root)
            : BuildRenderForestRoot(root);
        var layout = UniversalTreeLayout.Layout(layoutRoot, new UniversalTreeLayoutSettings(edgeLength));
        var nodes = new List<ResearchTreeLayoutNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            if (!config.ShowRootNode && node.Payload.IsSynthetic)
            {
                continue;
            }

            nodes.Add(new ResearchTreeLayoutNode(
                node.Payload.Node,
                node.Parent is null || node.Parent.Payload.IsSynthetic
                    ? null
                    : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Payload.Node)),
                node.LocalPosition));
        }

        return new ResearchTreeLayout(nodes, new ResearchTreeBounds(layout.MinX, layout.MaxX, layout.MinY, layout.MaxY));
    }

    private static TreeRenderNode<ResearchTreeRenderPayload> BuildRenderNode(ResearchTreeViewNode source)
    {
        var node = new TreeRenderNode<ResearchTreeRenderPayload>(new ResearchTreeRenderPayload(source, IsSynthetic: false));
        foreach (var child in source.Children)
        {
            node.AddChild(BuildRenderNode(child));
        }

        return node;
    }

    private static TreeRenderNode<ResearchTreeRenderPayload> BuildRenderForestRoot(ResearchTreeViewNode source)
    {
        var node = new TreeRenderNode<ResearchTreeRenderPayload>(new ResearchTreeRenderPayload(source, IsSynthetic: true));
        foreach (var child in source.Children)
        {
            node.AddChild(BuildRenderNode(child));
        }

        return node;
    }

    private static void DrawTiledBackground(
        GumUiRenderer gumUi,
        Rectangle bounds,
        Texture2D? texture,
        Vector2 panOffset,
        float zoom,
        Vector2 surfaceOrigin)
    {
        if (texture is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            gumUi.AddRoundedRectangle(bounds, new Color(8, 19, 29), 12);
            return;
        }

        var clipLayer = gumUi.AddClippingContainer(bounds);
        var tileSize = new Point(
            Math.Max(1, (int)MathF.Round(texture.Width * ClampZoom(zoom))),
            Math.Max(1, (int)MathF.Round(texture.Height * ClampZoom(zoom))));
        var columns = Math.Max(1, (int)MathF.Ceiling(bounds.Width / (float)tileSize.X) + 2);
        var rows = Math.Max(1, (int)MathF.Ceiling(bounds.Height / (float)tileSize.Y) + 2);
        var anchoredOrigin = surfaceOrigin + panOffset;
        var startX = CalculateBackgroundStartCoordinate(bounds.Left, anchoredOrigin.X, tileSize.X);
        var startY = CalculateBackgroundStartCoordinate(bounds.Top, anchoredOrigin.Y, tileSize.Y);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                gumUi.AddSprite(
                    clipLayer,
                    new Rectangle(
                        startX + (column * tileSize.X) - bounds.X,
                        startY + (row * tileSize.Y) - bounds.Y,
                        tileSize.X,
                        tileSize.Y),
                    texture,
                    new Rectangle(0, 0, texture.Width, texture.Height),
                    Color.White);
            }
        }
    }

    internal static int CalculateBackgroundStartCoordinate(int viewportMinimum, float surfaceOrigin, int tileLength)
    {
        if (tileLength <= 0)
        {
            return viewportMinimum;
        }

        var tileOffset = MathF.Floor((viewportMinimum - surfaceOrigin) / tileLength) * tileLength;
        return (int)MathF.Round(surfaceOrigin + tileOffset);
    }

    private static Color GetNodeFillColor(GameSession session, ResearchTreeViewNode node)
    {
        var baseColor = ResearchTreeColorResolver.GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return node.IsUnlocked
            ? baseColor
            : Color.Lerp(new Color(32, 38, 43), baseColor, 0.82f);
    }

    private static Color GetNodeBorderColor(GameSession session, ResearchTreeViewNode node)
    {
        var fill = ResearchTreeColorResolver.GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return Color.Lerp(fill, Color.White, 0.38f);
    }

    private static void DrawClippedConnector(
        GumUiRenderer gumUi,
        Rectangle bounds,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness,
        float startInset,
        float endInset)
    {
        if (!TryInsetConnector(ref start, ref end, startInset, endInset) ||
            !GumUiViewport.TryClipLine(bounds, ref start, ref end))
        {
            return;
        }

        DrawCrispLine(gumUi, start, end, color, thickness);
    }

    private static void DrawCrispConnector(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness,
        float startInset,
        float endInset)
    {
        if (!TryInsetConnector(ref start, ref end, startInset, endInset))
        {
            return;
        }

        DrawCrispLine(gumUi, parent, start, end, color, thickness);
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

    private static bool IsNodeVisible(Rectangle bounds, Vector2 center, int radius)
    {
        return center.X + radius >= bounds.Left &&
            center.X - radius <= bounds.Right &&
            center.Y + radius >= bounds.Top &&
            center.Y - radius <= bounds.Bottom;
    }

    private static ResearchTreeViewNode? TryGetHoveredDetailNode(
        ResearchTreeDetailMetrics metrics,
        IReadOnlyList<ResearchTreeDetailNode> nodes,
        Point pointerPoint,
        out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = metrics.NodeRadius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        ResearchTreeViewNode? hovered = null;
        var pointer = pointerPoint.ToVector2();
        foreach (var node in nodes)
        {
            if (!IsNodeVisible(metrics.ContentBounds, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(node.Position, pointer);
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            hovered = node.Node;
            center = node.Position;
        }

        return hovered;
    }

    private static void DrawTreeNode(GumUiRenderer gumUi, Vector2 center, int radius, Color fill, Color border)
    {
        DrawTreeNode(gumUi, parent: null, center, radius, fill, border);
    }

    private static void DrawTreeNode(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 center,
        int radius,
        Color fill,
        Color border)
    {
        var outerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius - 2),
            (int)MathF.Round(center.Y - radius - 2),
            (radius + 2) * 2,
            (radius + 2) * 2);
        AddRoundedRectangle(gumUi, parent, outerBounds, border, radius + 2);

        var innerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius),
            (int)MathF.Round(center.Y - radius),
            radius * 2,
            radius * 2);
        AddRoundedRectangle(gumUi, parent, innerBounds, fill, radius);
    }

    private static void DrawNodeOutline(GumUiRenderer gumUi, Vector2 center, int radius, Color border, int padding, int thickness)
    {
        DrawNodeOutline(gumUi, parent: null, center, radius, border, padding, thickness);
    }

    private static void DrawNodeOutline(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 center,
        int radius,
        Color border,
        int padding,
        int thickness)
    {
        var bounds = new Rectangle(
            (int)MathF.Round(center.X - radius - padding),
            (int)MathF.Round(center.Y - radius - padding),
            (radius + padding) * 2,
            (radius + padding) * 2);
        AddRoundedFrame(gumUi, parent, bounds, new Color(255, 255, 255, 1), border, thickness, radius + padding);
    }

    private static void DrawCrispLine(GumUiRenderer gumUi, Vector2 start, Vector2 end, Color color, int thickness)
    {
        gumUi.AddLine(PixelSnap(start), PixelSnap(end), color, thickness);
    }

    private static void DrawCrispLine(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness)
    {
        if (parent is null)
        {
            DrawCrispLine(gumUi, start, end, color, thickness);
            return;
        }

        gumUi.AddLine(parent, PixelSnap(start), PixelSnap(end), color, thickness);
    }

    private static void AddRoundedRectangle(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        Color color,
        int radius)
    {
        if (parent is null)
        {
            gumUi.AddRoundedRectangle(bounds, color, radius);
            return;
        }

        gumUi.AddRoundedRectangle(parent, bounds, color, radius);
    }

    private static void AddRoundedOutline(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        Color color,
        int thickness,
        int radius)
    {
        if (parent is null)
        {
            gumUi.AddRoundedOutline(bounds, color, thickness, radius);
            return;
        }

        gumUi.AddRoundedOutline(parent, bounds, color, thickness, radius);
    }

    private static void AddRoundedFrame(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        Color fill,
        Color border,
        int thickness,
        int radius)
    {
        if (parent is null)
        {
            gumUi.AddRoundedFrame(bounds, fill, border, thickness, radius);
            return;
        }

        gumUi.AddRoundedFrame(parent, bounds, fill, border, thickness, radius);
    }

    private static Vector2 PixelSnap(Vector2 point)
    {
        return new Vector2(MathF.Round(point.X), MathF.Round(point.Y));
    }

    private static Rectangle Inset(Rectangle bounds, int inset)
    {
        return new Rectangle(
            bounds.X + inset,
            bounds.Y + inset,
            Math.Max(0, bounds.Width - (inset * 2)),
            Math.Max(0, bounds.Height - (inset * 2)));
    }
    private static void DrawCardFrame(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        GumUiFrameStyle style)
    {
        GumUiChrome.DrawFrame(gumUi, parent, bounds, style);
    }

    private static void DrawCardOutline(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        Color color,
        int thickness,
        int radius)
    {
        AddRoundedOutline(gumUi, parent, bounds, color, thickness, radius);
    }

    private static void DrawCardText(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style)
    {
        GumUiText.Add(gumUi, parent, bounds, text, color, style);
    }

    private static void DrawCardCenteredText(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style)
    {
        GumUiText.AddCentered(gumUi, parent, bounds, text, color, style);
    }

    private static void DrawCardConnector(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness,
        float startInset,
        float endInset)
    {
        DrawCrispConnector(gumUi, parent, start, end, color, thickness, startInset, endInset);
    }

    private sealed record ResearchTreeLayout(IReadOnlyList<ResearchTreeLayoutNode> Nodes, ResearchTreeBounds Bounds);

    private sealed record ResearchTreeLayoutNode(ResearchTreeViewNode Node, ResearchTreeLayoutNode? Parent, Vector2 LocalPosition);

    private sealed record ResearchTreeDetailNode(ResearchTreeViewNode Node, ResearchTreeDetailNode? Parent, Vector2 Position);

    private readonly record struct ResearchTreeRenderPayload(ResearchTreeViewNode Node, bool IsSynthetic);
}

internal readonly record struct ResearchTreeRenderConfig(
    bool ShowBackButton = false,
    bool ShowRootNode = true,
    bool EnableNodeSelection = true,
    bool EnableBranchDrafting = false,
    bool EnablePlacementPreview = false);

internal readonly record struct ResearchTreeCardData(
    string Title,
    string Subtitle,
    Rectangle Bounds,
    ResearchTreeViewNode? Root,
    bool IsHovered,
    bool IsSelected);

internal readonly record struct ResearchTreeViewLayout(
    IReadOnlyList<ResearchTreeViewLayoutNode> Nodes,
    int Radius,
    Rectangle Bounds);

internal sealed record ResearchTreeViewLayoutNode(
    ResearchTreeViewNode Node,
    ResearchTreeViewLayoutNode? Parent,
    Vector2 Position);

internal readonly record struct ResearchTreeDetailMetrics(
    Rectangle Bounds,
    Rectangle ContentBounds,
    Vector2 Origin,
    float EdgeLength,
    int NodeRadius,
    ResearchTreeBounds BaseBounds);

internal readonly record struct ResearchTreeBounds(float MinX, float MaxX, float MinY, float MaxY)
{
    public float Width => MaxX - MinX;

    public float Height => MaxY - MinY;

    public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

    public ResearchTreeBounds Offset(Vector2 offset)
    {
        return new ResearchTreeBounds(
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
