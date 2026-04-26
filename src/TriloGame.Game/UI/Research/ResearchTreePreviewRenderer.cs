using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreePreviewRenderer
{
    public const float MinimumZoom = 0.55f;
    public const float MaximumZoom = 2.25f;
    private const float BaseDetailEdgeLength = 92f;
    private static readonly Color ConnectorColor = new(246, 251, 253);
    private static readonly Color UnlockedConnectorColor = new(247, 221, 92);

    public static ResearchTreePreviewLayout CalculatePreviewLayout(ResearchTreeViewNode root, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(root);

        const float padding = 14f;
        var layout = BuildLayout(root, edgeLength: 1f);
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

        var nodes = new List<ResearchTreePreviewNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new ResearchTreePreviewNode(
                node.Node,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Node)),
                (node.LocalPosition * scale) + offset));
        }

        return new ResearchTreePreviewLayout(nodes, radius, bounds);
    }

    public static void DrawPreview(
        GumUiRenderer gumUi,
        GameSession session,
        Rectangle bounds,
        ResearchTreeViewNode root)
    {
        DrawPreviewCore(gumUi, parent: null, session, bounds, root);
    }

    public static void DrawPreview(
        GumUiRenderer gumUi,
        ContainerRuntime parent,
        GameSession session,
        Rectangle bounds,
        ResearchTreeViewNode root)
    {
        DrawPreviewCore(gumUi, parent, session, bounds, root);
    }

    private static void DrawPreviewCore(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        GameSession session,
        Rectangle bounds,
        ResearchTreeViewNode root)
    {
        AddRoundedOutline(gumUi, parent, bounds, new Color(55, 87, 103), 1, 10);
        var preview = CalculatePreviewLayout(root, bounds);
        foreach (var node in preview.Nodes)
        {
            if (node.Parent is null)
            {
                continue;
            }

            DrawCrispConnector(gumUi, parent, node.Parent.Position, node.Position, ConnectorColor, 2, preview.Radius + 2f, preview.Radius + 2f);
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

    public static ResearchTreeDetailMetrics CalculateDetailMetrics(Rectangle bounds, ResearchTreeViewNode root, float zoom)
    {
        ArgumentNullException.ThrowIfNull(root);
        var safeZoom = ClampZoom(zoom);
        var contentBounds = Inset(bounds, 14);
        var edgeLength = BaseDetailEdgeLength * safeZoom;
        var nodeRadius = Math.Clamp((int)MathF.Round(edgeLength * 0.18f), 9, 18);
        var origin = new Vector2(contentBounds.Center.X, contentBounds.Bottom - nodeRadius - 8f);
        var layout = BuildLayout(root, edgeLength);
        var baseBounds = new ResearchTreeBounds(
            origin.X + layout.Bounds.MinX - nodeRadius,
            origin.X + layout.Bounds.MaxX + nodeRadius,
            origin.Y + layout.Bounds.MinY - nodeRadius,
            origin.Y + layout.Bounds.MaxY + nodeRadius);

        return new ResearchTreeDetailMetrics(bounds, contentBounds, origin, edgeLength, nodeRadius, baseBounds);
    }

    public static Vector2 ResolvePanAfterRelease(Rectangle bounds, ResearchTreeViewNode root, Vector2 panOffset, float zoom)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom);
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
        Point pointerPoint)
    {
        var metrics = CalculateDetailMetrics(bounds, root, zoom);
        DrawTiledBackground(gumUi, metrics.ContentBounds, backgroundTexture, panOffset, zoom, metrics.Origin);
        gumUi.AddRoundedOutline(metrics.ContentBounds, new Color(74, 115, 134), 1, 12);

        var layout = BuildLayout(root, metrics.EdgeLength);
        var nodes = new List<ResearchTreeDetailNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new ResearchTreeDetailNode(
                node.Node,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Node)),
                metrics.Origin + panOffset + node.LocalPosition));
        }

        var hoveredNode = TryGetHoveredDetailNode(metrics, nodes, pointerPoint, out var hoveredPosition);
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

    public static ResearchTreeNodeInfo BuildNodeInfo(GameSession session, ResearchTreeViewNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        return new ResearchTreeNodeInfo(
            node.Name,
            string.IsNullOrWhiteSpace(node.SourceFeatureTreeName) ? "Core" : node.SourceFeatureTreeName,
            BuildNodeAffectText(session, node));
    }

    private static string BuildNodeAffectText(GameSession session, ResearchTreeViewNode node)
    {
        if (node.EffectDescriptors.Count > 0)
        {
            var parts = new List<string>(node.EffectDescriptors.Count);
            foreach (var descriptor in node.EffectDescriptors)
            {
                parts.Add(FormatEffectDescriptor(descriptor));
            }

            return string.Join(", ", parts);
        }

        if (!string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
        {
            var featureTree = session.GetFeatureTree(node.SourceFeatureTreeName);
            if (featureTree is not null && featureTree.FeaturesAffected.Count > 0)
            {
                return BuildFeatureAffectLabel(featureTree);
            }
        }

        return node.Description;
    }

    private static ResearchTreeLayout BuildLayout(ResearchTreeViewNode root, float edgeLength)
    {
        var layout = UniversalTreeLayout.Layout(BuildRenderNode(root), new UniversalTreeLayoutSettings(edgeLength));
        var nodes = new List<ResearchTreeLayoutNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new ResearchTreeLayoutNode(
                node.Payload,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.Node, node.Parent.Payload)),
                node.LocalPosition));
        }

        return new ResearchTreeLayout(nodes, new ResearchTreeBounds(layout.MinX, layout.MaxX, layout.MinY, layout.MaxY));
    }

    private static TreeRenderNode<ResearchTreeViewNode> BuildRenderNode(ResearchTreeViewNode source)
    {
        var node = new TreeRenderNode<ResearchTreeViewNode>(source);
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
        var baseColor = GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return node.IsUnlocked
            ? baseColor
            : Color.Lerp(new Color(32, 38, 43), baseColor, 0.82f);
    }

    private static Color GetNodeBorderColor(GameSession session, ResearchTreeViewNode node)
    {
        var fill = GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return Color.Lerp(fill, Color.White, 0.38f);
    }

    private static Color GetBaseFeatureColor(GameSession session, string? sourceFeatureTreeName)
    {
        if (string.IsNullOrWhiteSpace(sourceFeatureTreeName))
        {
            return new Color(180, 191, 199);
        }

        var featureTree = session.GetFeatureTree(sourceFeatureTreeName);
        if (featureTree is null || featureTree.FeaturesAffected.Count == 0)
        {
            return GetFeatureColorFromTreeName(sourceFeatureTreeName);
        }

        var red = 0f;
        var green = 0f;
        var blue = 0f;
        foreach (var featureName in featureTree.FeaturesAffected)
        {
            var featureColor = GetFeatureColor(featureName);
            red += featureColor.R;
            green += featureColor.G;
            blue += featureColor.B;
        }

        var divisor = featureTree.FeaturesAffected.Count;
        return new Color(
            (int)MathF.Round(red / divisor),
            (int)MathF.Round(green / divisor),
            (int)MathF.Round(blue / divisor));
    }

    private static Color GetFeatureColorFromTreeName(string featureTreeName)
    {
        return featureTreeName switch
        {
            var name when name.StartsWith("B", StringComparison.Ordinal) => GetFeatureColor("building"),
            var name when name.StartsWith("C", StringComparison.Ordinal) => GetFeatureColor("combat"),
            var name when name.StartsWith("F", StringComparison.Ordinal) => GetFeatureColor("farming"),
            var name when name.StartsWith("M", StringComparison.Ordinal) => GetFeatureColor("mining"),
            _ => new Color(180, 191, 199)
        };
    }

    private static Color GetFeatureColor(string featureName)
    {
        return featureName switch
        {
            "building" => new Color(240, 88, 80),
            "combat" => new Color(78, 164, 233),
            "farming" => new Color(239, 214, 86),
            "mining" => new Color(189, 138, 94),
            _ => new Color(180, 191, 199)
        };
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
            !TryClipLineToBounds(bounds, ref start, ref end))
        {
            return;
        }

        DrawCrispLine(gumUi, start, end, color, thickness);
    }

    private static void DrawCrispConnector(
        GumUiRenderer gumUi,
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

    private static bool TryClipLineToBounds(Rectangle bounds, ref Vector2 start, ref Vector2 end)
    {
        var left = (float)bounds.Left;
        var right = bounds.Right;
        var top = bounds.Top;
        var bottom = bounds.Bottom;
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var t0 = 0f;
        var t1 = 1f;

        if (!ClipTest(-deltaX, start.X - left, ref t0, ref t1) ||
            !ClipTest(deltaX, right - start.X, ref t0, ref t1) ||
            !ClipTest(-deltaY, start.Y - top, ref t0, ref t1) ||
            !ClipTest(deltaY, bottom - start.Y, ref t0, ref t1))
        {
            return false;
        }

        var originalStart = start;
        start = new Vector2(originalStart.X + (t0 * deltaX), originalStart.Y + (t0 * deltaY));
        end = new Vector2(originalStart.X + (t1 * deltaX), originalStart.Y + (t1 * deltaY));
        return true;
    }

    private static bool ClipTest(float direction, float distance, ref float lower, ref float upper)
    {
        if (MathF.Abs(direction) <= float.Epsilon)
        {
            return distance >= 0f;
        }

        var ratio = distance / direction;
        if (direction < 0f)
        {
            if (ratio > upper)
            {
                return false;
            }

            if (ratio > lower)
            {
                lower = ratio;
            }

            return true;
        }

        if (ratio < lower)
        {
            return false;
        }

        if (ratio < upper)
        {
            upper = ratio;
        }

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
        var bounds = new Rectangle(
            (int)MathF.Round(center.X - radius - padding),
            (int)MathF.Round(center.Y - radius - padding),
            (radius + padding) * 2,
            (radius + padding) * 2);
        gumUi.AddRoundedFrame(bounds, new Color(255, 255, 255, 1), border, thickness, radius + padding);
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

    private static string BuildFeatureAffectLabel(FeatureTree featureTree)
    {
        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < featureTree.FeaturesAffected.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(FormatFeatureName(featureTree.FeaturesAffected[index]));
        }

        return builder.ToString();
    }

    private static string FormatFeatureName(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return "Unknown";
        }

        var trimmed = featureName.Trim();
        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string FormatEffectDescriptor(ResearchEffectDescriptor descriptor)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(descriptor.Operation switch
        {
            ResearchOperation.AddFlat => $"+{descriptor.Value:0.##} ",
            ResearchOperation.AddPercent => $"+{descriptor.Value * 100d:0.##}% ",
            ResearchOperation.Multiply => $"x{descriptor.Value:0.##} ",
            ResearchOperation.Set => $"Set to {descriptor.Value:0.##} ",
            _ => string.Empty
        });
        builder.Append(descriptor.StatKey);

        if (descriptor.TargetKind != ResearchTargetKind.Global)
        {
            builder.Append(" (");
            builder.Append(descriptor.TargetKind);
            if (!string.IsNullOrWhiteSpace(descriptor.TargetKey))
            {
                builder.Append(": ");
                builder.Append(descriptor.TargetKey);
            }

            builder.Append(')');
        }

        return builder.ToString();
    }

    private sealed record ResearchTreeLayout(IReadOnlyList<ResearchTreeLayoutNode> Nodes, ResearchTreeBounds Bounds);

    private sealed record ResearchTreeLayoutNode(ResearchTreeViewNode Node, ResearchTreeLayoutNode? Parent, Vector2 LocalPosition);

    private sealed record ResearchTreeDetailNode(ResearchTreeViewNode Node, ResearchTreeDetailNode? Parent, Vector2 Position);
}

internal readonly record struct ResearchTreePreviewLayout(
    IReadOnlyList<ResearchTreePreviewNode> Nodes,
    int Radius,
    Rectangle Bounds);

internal sealed record ResearchTreePreviewNode(
    ResearchTreeViewNode Node,
    ResearchTreePreviewNode? Parent,
    Vector2 Position);

internal readonly record struct ResearchTreeDetailMetrics(
    Rectangle Bounds,
    Rectangle ContentBounds,
    Vector2 Origin,
    float EdgeLength,
    int NodeRadius,
    ResearchTreeBounds BaseBounds);

internal readonly record struct ResearchTreeNodeInfo(
    string TitleText,
    string FeatureTreeText,
    string EffectText);

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
