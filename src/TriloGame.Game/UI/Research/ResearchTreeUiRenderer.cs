using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeUiRenderer
{
    public const int DetailNodeRadius = 17;
    public const int DetailConnectorThickness = 3;
    public const float MaximumZoom = 2.25f;
    private const int MinimumRenderedBackgroundTileLength = 8;
    private const float BaseDetailEdgeLength = 92f;
    private static readonly Color PreviewConnectorColor = new(246, 251, 253, 128);
    private static readonly Color LockedConnectorColor = new(126, 141, 150, 64);
    private static readonly Color AvailableConnectorColor = new(194, 225, 235, 210);
    private static readonly Color UnlockedConnectorColor = new(247, 221, 92);
    private static readonly Color LockedNodeBaseColor = new(27, 32, 36);
    private static readonly Color LockedNodeBorderColor = new(94, 108, 116, 210);
    private static readonly Color AvailableNodeBorderColor = new(205, 238, 246);
    private static readonly Color LockedNodeMarkerColor = new(10, 15, 19, 230);
    private static readonly Color CyanHaloColor = new(105, 226, 239);
    private static readonly Color AvailableNodeMarkerColor = new(9, 35, 43);

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

    public static ResearchTreeViewNode? TryGetHoveredDetailNode(
        Rectangle bounds,
        ResearchTreeViewNode root,
        Vector2 panOffset,
        float zoom,
        Point pointerPoint,
        ResearchTreeRenderConfig config,
        out Vector2 center)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom, config);
        var layout = BuildLayout(root, metrics.EdgeLength, config);
        var nodes = new List<ResearchTreeDetailNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new ResearchTreeDetailNode(
                node.Node,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Node)),
                metrics.Origin + panOffset + node.LocalPosition));
        }

        return TryGetHoveredDetailNode(metrics, nodes, pointerPoint, out center);
    }

    public static bool TryGetDetailNodeCenter(
        Rectangle bounds,
        ResearchTreeViewNode root,
        Vector2 panOffset,
        float zoom,
        ResearchTreeViewNode target,
        ResearchTreeRenderConfig config,
        out Vector2 center)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom, config);
        var layout = BuildLayout(root, metrics.EdgeLength, config);
        foreach (var node in layout.Nodes)
        {
            if (!ReferenceEquals(node.Node, target))
            {
                continue;
            }

            center = metrics.Origin + panOffset + node.LocalPosition;
            return IsNodeVisible(metrics.ContentBounds, center, metrics.NodeRadius + 12);
        }

        center = Vector2.Zero;
        return false;
    }

    public static ResearchTreeDetailMetrics CalculateDetailMetrics(
        Rectangle bounds,
        ResearchTreeViewNode root,
        float zoom,
        ResearchTreeRenderConfig config = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var safeZoom = ClampZoom(zoom);
        var contentBounds = bounds;
        var edgeLength = BaseDetailEdgeLength * safeZoom;
        var nodeRadius = CalculateDetailNodeRadius(safeZoom);
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
        return MathF.Min(zoom, MaximumZoom);
    }

    internal static int CalculateDetailNodeRadius(float zoom)
    {
        return Math.Max(1, (int)MathF.Round(DetailNodeRadius * ClampZoom(zoom)));
    }

    internal static int CalculateDetailNodeBorderThickness(int radius)
    {
        return Math.Max(1, (int)MathF.Round(radius * (2f / DetailNodeRadius)));
    }

    public static ResearchTreeDetailDrawResult DrawDetail(
        GumUiRenderer gumUi,
        GameSession session,
        Rectangle bounds,
        ResearchTreeViewNode root,
        Vector2 panOffset,
        float zoom,
        Texture2D? backgroundTexture,
        Point pointerPoint,
        ResearchTreeRenderConfig config,
        double visualTimeMs = 0d)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom, config);
        DrawDetailBackground(gumUi, metrics, backgroundTexture, panOffset, zoom);
        var hoveredNode = DrawDetailContent(gumUi, session, metrics, root, panOffset, pointerPoint, config, visualTimeMs);
        return new ResearchTreeDetailDrawResult(metrics, hoveredNode);
    }

    internal static void DrawDetailBackground(
        GumUiRenderer gumUi,
        ResearchTreeDetailMetrics metrics,
        Texture2D? backgroundTexture,
        Vector2 panOffset,
        float zoom)
    {
        DrawTiledBackground(gumUi, metrics.ContentBounds, backgroundTexture, panOffset, zoom, metrics.Origin);
    }

    public static ResearchTreeViewNode? DrawDetailContent(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeDetailMetrics metrics,
        ResearchTreeViewNode root,
        Vector2 panOffset,
        Point pointerPoint,
        ResearchTreeRenderConfig config,
        double visualTimeMs = 0d)
    {
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
                GetConnectorColor(node.Node),
                DetailConnectorThickness,
                metrics.NodeRadius + 2f,
                metrics.NodeRadius + 2f);
        }

        foreach (var node in nodes)
        {
            var drawPosition = node.Position + GetAvailableNodeShakeOffset(node.Node, metrics.NodeRadius, visualTimeMs);
            if (!IsNodeVisible(metrics.ContentBounds, drawPosition, metrics.NodeRadius + 2))
            {
                continue;
            }

            DrawTreeNode(
                gumUi,
                drawPosition,
                metrics.NodeRadius,
                GetNodeFillColor(session, node.Node),
                GetNodeBorderColor(session, node.Node),
                ShouldDrawLockedMarker(node.Node));
            if (ShouldDrawAvailableAdornment(node.Node))
            {
                DrawAvailableNodeMarker(gumUi, drawPosition, metrics.NodeRadius);
            }
        }

        if (hoveredNode is not null)
        {
            var drawPosition = hoveredPosition + GetAvailableNodeShakeOffset(hoveredNode, metrics.NodeRadius, visualTimeMs);
            DrawHoveredNodeHalo(gumUi, drawPosition, metrics.NodeRadius, visualTimeMs);
        }

        return hoveredNode;
    }

    public static ResearchNodeInfo BuildNodeInfo(GameSession session, ResearchTreeViewNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        return ResearchNodeTextFormatter.BuildNodeInfo(session, node);
    }

    internal static void DrawCardPreview(
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
                GetConnectorColor(node.Node),
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

    internal static void DrawCardNodeHover(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 center,
        int radius)
    {
        DrawNodeOutline(gumUi, parent, center, radius, new Color(255, 255, 255, 240), 6, 2);
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

        var tileSize = CalculateBackgroundTileSize(texture.Width, texture.Height, zoom);
        if (tileSize.X < MinimumRenderedBackgroundTileLength ||
            tileSize.Y < MinimumRenderedBackgroundTileLength)
        {
            gumUi.AddRoundedRectangle(bounds, new Color(8, 19, 29), 12);
            return;
        }

        var clipLayer = gumUi.AddClippingContainer(bounds);
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

    internal static Point CalculateBackgroundTileSize(int textureWidth, int textureHeight, float zoom)
    {
        var safeZoom = ClampZoom(zoom);
        return new Point(
            Math.Max(1, (int)MathF.Round(textureWidth * safeZoom)),
            Math.Max(1, (int)MathF.Round(textureHeight * safeZoom)));
    }

    internal static Color GetNodeFillColor(GameSession session, ResearchTreeViewNode node)
    {
        return ResolveNodeFillColor(
            session,
            node.SourceFeatureTreeName,
            node.IsUnlocked,
            node.CanUnlock,
            node.ShowsProgressState);
    }

    internal static Color ResolveNodeFillColor(
        GameSession session,
        string? sourceFeatureTreeName,
        bool isUnlocked,
        bool canUnlock,
        bool showsProgressState)
    {
        var baseColor = ResearchTreeColorResolver.GetBaseFeatureColor(session, sourceFeatureTreeName);
        if (!showsProgressState || isUnlocked)
        {
            return baseColor;
        }

        return canUnlock
            ? Color.Lerp(baseColor, Color.White, 0.08f)
            : Color.Lerp(LockedNodeBaseColor, baseColor, 0.30f);
    }

    internal static Color GetNodeBorderColor(GameSession session, ResearchTreeViewNode node)
    {
        return ResolveNodeBorderColor(
            session,
            node.SourceFeatureTreeName,
            node.IsUnlocked,
            node.CanUnlock,
            node.ShowsProgressState);
    }

    internal static Color ResolveNodeBorderColor(
        GameSession session,
        string? sourceFeatureTreeName,
        bool isUnlocked,
        bool canUnlock,
        bool showsProgressState)
    {
        var fill = ResearchTreeColorResolver.GetBaseFeatureColor(session, sourceFeatureTreeName);
        if (!showsProgressState)
        {
            return WithAlpha(Color.Lerp(fill, Color.White, 0.38f), 210);
        }

        if (isUnlocked)
        {
            return Color.Lerp(fill, Color.White, 0.55f);
        }

        return canUnlock ? AvailableNodeBorderColor : LockedNodeBorderColor;
    }

    internal static Color GetConnectorColor(ResearchTreeViewNode node)
    {
        return ResolveConnectorColor(node.IsUnlocked, node.CanUnlock, node.ShowsProgressState);
    }

    internal static Color ResolveConnectorColor(bool isUnlocked, bool canUnlock, bool showsProgressState)
    {
        if (!showsProgressState)
        {
            return PreviewConnectorColor;
        }

        if (isUnlocked)
        {
            return UnlockedConnectorColor;
        }

        return canUnlock ? AvailableConnectorColor : LockedConnectorColor;
    }

    internal static bool ShouldDrawLockedMarker(ResearchTreeViewNode node)
    {
        return node.ShowsProgressState && !node.IsUnlocked && !node.CanUnlock;
    }

    internal static bool ShouldDrawAvailableAdornment(ResearchTreeViewNode node)
    {
        return node.ShowsProgressState && !node.IsUnlocked && node.CanUnlock;
    }

    internal static Vector2 GetAvailableNodeShakeOffset(
        ResearchTreeViewNode node,
        int radius,
        double visualTimeMs)
    {
        return ShouldDrawAvailableAdornment(node)
            ? CalculateAvailableNodeShakeOffset($"{node.SourceFeatureTreeName}:{node.Name}", radius, visualTimeMs)
            : Vector2.Zero;
    }

    internal static Vector2 CalculateAvailableNodeShakeOffset(string stableKey, int radius, double visualTimeMs)
    {
        var hash = 2166136261u;
        for (var index = 0; index < stableKey.Length; index++)
        {
            hash ^= stableKey[index];
            hash *= 16777619u;
        }

        var phase = (hash % 6283u) / 1000f;
        var time = (float)visualTimeMs;
        var amplitude = Math.Clamp(radius / (float)DetailNodeRadius, 0.35f, 1.25f) * 1.25f;
        return new Vector2(
            MathF.Sin((time * 0.032f) + phase) * amplitude,
            MathF.Sin((time * 0.043f) + (phase * 1.7f)) * amplitude * 0.65f);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
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

    private static void DrawTreeNode(
        GumUiRenderer gumUi,
        Vector2 center,
        int radius,
        Color fill,
        Color border,
        bool showLockedMarker = false)
    {
        DrawTreeNode(gumUi, parent: null, center, radius, fill, border, showLockedMarker);
    }

    private static void DrawTreeNode(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 center,
        int radius,
        Color fill,
        Color border,
        bool showLockedMarker = false)
    {
        var borderThickness = CalculateDetailNodeBorderThickness(radius);
        var outerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius - borderThickness),
            (int)MathF.Round(center.Y - radius - borderThickness),
            (radius + borderThickness) * 2,
            (radius + borderThickness) * 2);
        AddRoundedRectangle(gumUi, parent, outerBounds, border, radius + borderThickness);

        var innerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius),
            (int)MathF.Round(center.Y - radius),
            radius * 2,
            radius * 2);
        AddRoundedRectangle(gumUi, parent, innerBounds, fill, radius);

        if (showLockedMarker && radius >= 4)
        {
            DrawLockedNodeMarker(gumUi, parent, center, radius);
        }
    }

    internal static void DrawLockedNodeMarker(GumUiRenderer gumUi, Vector2 center, int radius)
    {
        DrawLockedNodeMarker(gumUi, parent: null, center, radius);
    }

    private static void DrawLockedNodeMarker(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Vector2 center,
        int radius)
    {
        var markerRadius = Math.Max(2, (int)MathF.Round(radius * 0.24f));
        var markerBounds = new Rectangle(
            (int)MathF.Round(center.X) - markerRadius,
            (int)MathF.Round(center.Y) - markerRadius,
            markerRadius * 2,
            markerRadius * 2);
        AddRoundedRectangle(gumUi, parent, markerBounds, LockedNodeMarkerColor, markerRadius);
    }

    internal static void DrawHoveredNodeHalo(
        GumUiRenderer gumUi,
        Vector2 center,
        int radius,
        double visualTimeMs)
    {
        var phase = (float)((visualTimeMs % 1200d) / 1200d * MathHelper.TwoPi);
        var pulse = (MathF.Sin(phase) + 1f) * 0.5f;
        var padding = Math.Max(5, (int)MathF.Round(radius * 0.30f)) + (int)MathF.Round(pulse * 2f);
        DrawNodeOutline(
            gumUi,
            center,
            radius,
            WithAlpha(CyanHaloColor, (byte)MathF.Round(170f + (pulse * 85f))),
            padding,
            thickness: 2);
    }

    internal static void DrawSelectedNodeHalo(
        GumUiRenderer gumUi,
        Vector2 center,
        int radius,
        double visualTimeMs)
    {
        var phase = (float)((visualTimeMs % 1200d) / 1200d * MathHelper.TwoPi);
        var pulse = (MathF.Sin(phase) + 1f) * 0.5f;
        var innerPadding = Math.Max(5, (int)MathF.Round(radius * 0.30f)) + (int)MathF.Round(pulse);
        var outerPadding = innerPadding + Math.Max(4, (int)MathF.Round(radius * 0.24f)) + (int)MathF.Round(pulse);
        DrawNodeOutline(
            gumUi,
            center,
            radius,
            WithAlpha(CyanHaloColor, (byte)MathF.Round(195f + (pulse * 60f))),
            innerPadding,
            thickness: 2);
        DrawNodeOutline(
            gumUi,
            center,
            radius,
            WithAlpha(CyanHaloColor, (byte)MathF.Round(85f + (pulse * 70f))),
            outerPadding,
            thickness: 1);
    }

    internal static void DrawAvailableNodeMarker(GumUiRenderer gumUi, Vector2 center, int radius)
    {
        if (radius < 5)
        {
            return;
        }

        var halfLength = Math.Max(2, (int)MathF.Round(radius * 0.28f));
        var thickness = Math.Max(1, (int)MathF.Round(radius * 0.12f));
        var firstStart = new Vector2(center.X - halfLength, center.Y - halfLength);
        var firstEnd = new Vector2(center.X + halfLength, center.Y + halfLength);
        var secondStart = new Vector2(center.X + halfLength, center.Y - halfLength);
        var secondEnd = new Vector2(center.X - halfLength, center.Y + halfLength);
        gumUi.AddLine(firstStart, firstEnd, AvailableNodeMarkerColor, thickness);
        gumUi.AddLine(secondStart, secondEnd, AvailableNodeMarkerColor, thickness);
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

internal readonly record struct ResearchTreeDetailDrawResult(
    ResearchTreeDetailMetrics Metrics,
    ResearchTreeViewNode? HoveredNode);

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
