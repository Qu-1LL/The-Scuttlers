using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

public enum TrilodexInteractionOutcome
{
    None,
    Consumed,
    RequestedOpen,
    RequestedClose
}

public sealed class TrilodexController
{
    private const float TreeDragThresholdPixels = 10f;

    private Point _pointerPoint;
    private float _gridScroll;
    private FeatureTree? _selectedTree;
    private ResearchTreeViewNode? _selectedTreeRoot;
    private Vector2 _treePanOffset;
    private Vector2 _treePanStartOffset;
    private Point _treePanStartPointer;
    private float _treeZoom = 1f;
    private bool _treePanCandidate;
    private bool _treePanning;

    public bool IsOpen { get; private set; }

    internal bool IsDetailOpen => _selectedTree is not null;

    public void Reset()
    {
        _pointerPoint = Point.Zero;
        _gridScroll = 0f;
        ClearDetail();
        IsOpen = false;
    }

    public void Open()
    {
        IsOpen = true;
        _gridScroll = 0f;
        ClearDetail();
    }

    public void Close()
    {
        ClearDetail();
        IsOpen = false;
    }

    public void UpdatePointer(Point point)
    {
        _pointerPoint = point;
    }

    public bool CoversScreenPoint(Point point, Point viewport)
    {
        var layout = TrilodexLayout.Build(viewport, TriloDex.Global.Count, _gridScroll);
        return IsOpen && layout.PanelBounds.Contains(point);
    }

    public TrilodexInteractionOutcome HandleClosedButtonClick(Point point, Point viewport)
    {
        return TrilodexInteractionOutcome.None;
    }

    public TrilodexInteractionOutcome HandleEscape()
    {
        if (!IsOpen)
        {
            return TrilodexInteractionOutcome.None;
        }

        if (_selectedTree is not null)
        {
            ClearDetail();
            return TrilodexInteractionOutcome.Consumed;
        }

        return TrilodexInteractionOutcome.RequestedClose;
    }

    public bool HandleWheel(Point point, int delta, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = TrilodexLayout.Build(viewport, TriloDex.Global.Count, _gridScroll);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        if (_selectedTreeRoot is not null)
        {
            if (!layout.DetailTreeViewportBounds.Contains(point))
            {
                return true;
            }

            var previousZoom = _treeZoom;
            _treeZoom = ResearchTreePreviewRenderer.ClampZoom(_treeZoom + (-delta * 0.0015f));
            if (MathF.Abs(_treeZoom - previousZoom) <= float.Epsilon)
            {
                return true;
            }

            var metricsBefore = ResearchTreePreviewRenderer.CalculateDetailMetrics(
                layout.DetailTreeViewportBounds,
                _selectedTreeRoot,
                previousZoom);
            var pointToOrigin = point.ToVector2() - metricsBefore.Origin - _treePanOffset;
            if (previousZoom > float.Epsilon)
            {
                _treePanOffset += pointToOrigin - (pointToOrigin * (_treeZoom / previousZoom));
            }

            return true;
        }

        if (layout.GridFrameBounds.Contains(point))
        {
            _gridScroll = Math.Clamp(_gridScroll + delta, 0f, layout.MaxScroll);
        }

        return true;
    }

    public bool HandlePointerDown(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = TrilodexLayout.Build(viewport, TriloDex.Global.Count, _gridScroll);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        if (_selectedTreeRoot is not null && layout.DetailTreeViewportBounds.Contains(point))
        {
            _treePanCandidate = true;
            _treePanning = false;
            _treePanStartPointer = point;
            _treePanStartOffset = _treePanOffset;
        }

        return true;
    }

    public void HandlePointerDrag(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen || !_treePanCandidate || _selectedTreeRoot is null)
        {
            return;
        }

        var dragDelta = point - _treePanStartPointer;
        if (!_treePanning && dragDelta.ToVector2().Length() >= TreeDragThresholdPixels)
        {
            _treePanning = true;
        }

        if (_treePanning)
        {
            _treePanOffset = _treePanStartOffset + dragDelta.ToVector2();
        }
    }

    public TrilodexInteractionOutcome HandlePointerUp(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return TrilodexInteractionOutcome.None;
        }

        var wasPanning = _treePanning;
        _treePanCandidate = false;
        _treePanning = false;

        var layout = TrilodexLayout.Build(viewport, TriloDex.Global.Count, _gridScroll);
        if (wasPanning && _selectedTreeRoot is not null)
        {
            _treePanOffset = ResearchTreePreviewRenderer.ResolvePanAfterRelease(
                layout.DetailTreeViewportBounds,
                _selectedTreeRoot,
                _treePanOffset,
                _treeZoom);
            return TrilodexInteractionOutcome.Consumed;
        }

        if (!layout.PanelBounds.Contains(point))
        {
            return TrilodexInteractionOutcome.RequestedClose;
        }

        if (layout.CloseButtonBounds.Contains(point))
        {
            return TrilodexInteractionOutcome.RequestedClose;
        }

        if (_selectedTree is not null)
        {
            if (layout.BackButtonBounds.Contains(point))
            {
                ClearDetail();
            }

            return TrilodexInteractionOutcome.Consumed;
        }

        if (TryGetCardIndex(point, layout, out var index))
        {
            OpenDetail(TriloDex.Global.FeatureTrees[index]);
        }

        return TrilodexInteractionOutcome.Consumed;
    }

    public void Draw(
        Point viewport,
        GameSession session,
        GumUiRenderer gumUi,
        Texture2D? treeBackgroundTexture = null)
    {
        var layout = TrilodexLayout.Build(viewport, TriloDex.Global.Count, _gridScroll);
        if (!IsOpen)
        {
            return;
        }

        gumUi.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(5, 11, 17, 164));
        gumUi.AddRoundedFrame(layout.PanelBounds, new Color(9, 18, 27, 248), new Color(83, 125, 145), 3, 20);
        DrawChrome(layout, gumUi);

        if (_selectedTree is not null && _selectedTreeRoot is not null)
        {
            DrawDetail(layout, session, gumUi, treeBackgroundTexture);
            return;
        }

        DrawGrid(layout, session, gumUi);
    }

    private void OpenDetail(FeatureTree featureTree)
    {
        _selectedTree = featureTree;
        _selectedTreeRoot = ResearchTreeViewNode.FromFeatureTree(featureTree);
        _treePanOffset = Vector2.Zero;
        _treePanStartOffset = Vector2.Zero;
        _treeZoom = 1f;
        _treePanCandidate = false;
        _treePanning = false;
    }

    private void ClearDetail()
    {
        _selectedTree = null;
        _selectedTreeRoot = null;
        _treePanOffset = Vector2.Zero;
        _treePanStartOffset = Vector2.Zero;
        _treePanStartPointer = Point.Zero;
        _treeZoom = 1f;
        _treePanCandidate = false;
        _treePanning = false;
    }

    private void DrawChrome(TrilodexLayoutInfo layout, GumUiRenderer gumUi)
    {
        gumUi.AddRoundedFrame(
            layout.CloseButtonBounds,
            layout.CloseButtonBounds.Contains(_pointerPoint) ? new Color(29, 55, 72) : new Color(20, 42, 58),
            layout.CloseButtonBounds.Contains(_pointerPoint) ? new Color(183, 223, 237) : new Color(114, 154, 172),
            2,
            12);
        AddCenteredText(gumUi, layout.CloseButtonBounds, "X", Color.White, GumTextStyle.Small);

        if (_selectedTree is not null)
        {
            gumUi.AddRoundedFrame(
                layout.BackButtonBounds,
                layout.BackButtonBounds.Contains(_pointerPoint) ? new Color(29, 55, 72) : new Color(20, 42, 58),
                layout.BackButtonBounds.Contains(_pointerPoint) ? new Color(183, 223, 237) : new Color(114, 154, 172),
                2,
                12);
            AddCenteredText(gumUi, layout.BackButtonBounds, "<", Color.White, GumTextStyle.Ui);
        }

        AddCenteredText(gumUi, layout.TitleBounds, _selectedTree?.Name ?? "Trilodex", Color.White, GumTextStyle.UiLarge);
        AddCenteredText(
            gumUi,
            layout.SubtitleBounds,
            _selectedTree is null
                ? "Curated research trees discovered by the colony."
                : $"Tier {_selectedTree.Tier} curated tree. Read-only preview.",
            new Color(177, 203, 214),
            GumTextStyle.Small);
    }

    private void DrawGrid(TrilodexLayoutInfo layout, GameSession session, GumUiRenderer gumUi)
    {
        gumUi.AddRoundedFrame(layout.GridFrameBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        var clipLayer = gumUi.AddClippingContainer(layout.GridViewportBounds);
        var trees = TriloDex.Global.FeatureTrees;
        for (var index = 0; index < Math.Min(layout.CardBounds.Count, trees.Count); index++)
        {
            var bounds = layout.CardBounds[index];
            if (bounds.Bottom < layout.GridViewportBounds.Top || bounds.Top > layout.GridViewportBounds.Bottom)
            {
                continue;
            }

            DrawCard(clipLayer, layout.GridViewportBounds, bounds, trees[index], session, gumUi);
        }

        if (layout.MaxScroll > 0f)
        {
            gumUi.AddRoundedRectangle(layout.ScrollbarTrackBounds, new Color(10, 22, 32, 210), 3);
            gumUi.AddRoundedRectangle(layout.ScrollbarThumbBounds, new Color(92, 137, 154), 3);
        }
    }

    private void DrawCard(
        ContainerRuntime clipLayer,
        Rectangle viewportBounds,
        Rectangle bounds,
        FeatureTree featureTree,
        GameSession session,
        GumUiRenderer gumUi)
    {
        var localBounds = new Rectangle(bounds.X - viewportBounds.X, bounds.Y - viewportBounds.Y, bounds.Width, bounds.Height);
        var hovered = bounds.Contains(_pointerPoint);
        gumUi.AddRoundedFrame(
            clipLayer,
            localBounds,
            hovered ? new Color(20, 45, 63) : new Color(13, 30, 44),
            hovered ? new Color(132, 181, 198) : new Color(66, 101, 118),
            2,
            14);

        var titleBounds = new Rectangle(localBounds.X + 12, localBounds.Y + 8, localBounds.Width - 24, 20);
        var tierBounds = new Rectangle(localBounds.X + 12, localBounds.Y + 30, localBounds.Width - 24, 18);
        var previewBounds = new Rectangle(localBounds.X + 10, localBounds.Y + 54, localBounds.Width - 20, localBounds.Height - 64);
        AddText(clipLayer, gumUi, titleBounds, featureTree.Name, Color.White, GumTextStyle.Small);
        AddText(clipLayer, gumUi, tierBounds, $"Tier {featureTree.Tier}", new Color(184, 206, 216), GumTextStyle.Compact);
        if (featureTree.Root is not null)
        {
            ResearchTreePreviewRenderer.DrawPreview(gumUi, clipLayer, session, previewBounds, ResearchTreeViewNode.FromFeatureTree(featureTree));
        }
    }

    private void DrawDetail(
        TrilodexLayoutInfo layout,
        GameSession session,
        GumUiRenderer gumUi,
        Texture2D? treeBackgroundTexture)
    {
        gumUi.AddRoundedFrame(layout.DetailTreeFrameBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        var hoveredNode = ResearchTreePreviewRenderer.DrawDetail(
            gumUi,
            session,
            layout.DetailTreeViewportBounds,
            _selectedTreeRoot!,
            _treePanOffset,
            _treeZoom,
            treeBackgroundTexture,
            _pointerPoint);
        DrawInfoPanel(layout.DetailInfoPanelBounds, hoveredNode, session, gumUi);
    }

    private void DrawInfoPanel(
        Rectangle bounds,
        ResearchTreeViewNode? hoveredNode,
        GameSession session,
        GumUiRenderer gumUi)
    {
        if (hoveredNode is null)
        {
            gumUi.AddRoundedFrame(bounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
            AddText(gumUi, new Rectangle(bounds.X + 14, bounds.Y + 12, bounds.Width - 28, 18), "Info", new Color(204, 228, 238), GumTextStyle.Compact);
            AddCenteredText(
                gumUi,
                new Rectangle(bounds.X + 18, bounds.Y + 46, bounds.Width - 36, bounds.Height - 64),
                "Hover a tree node for details.",
                new Color(177, 203, 214),
                GumTextStyle.Small);
            return;
        }

        var info = ResearchTreePreviewRenderer.BuildNodeInfo(session, hoveredNode);
        gumUi.AddRoundedFrame(bounds, new Color(9, 18, 28, 248), new Color(204, 228, 238), 2, 16);
        var contentX = bounds.X + 14;
        var contentWidth = bounds.Width - 28;
        AddText(gumUi, new Rectangle(contentX, bounds.Y + 12, contentWidth, 18), "Node Details", new Color(204, 228, 238), GumTextStyle.Compact);
        DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 38, contentWidth, 44), "Node", info.TitleText);
        DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 88, contentWidth, 40), "Feature Tree", info.FeatureTreeText);
        DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 134, contentWidth, bounds.Height - 148), "Effect", info.EffectText, maxLines: 10);
    }

    private static void DrawInfoSection(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string label,
        string value,
        int maxLines = 2)
    {
        gumUi.AddText(
            new Rectangle(bounds.X, bounds.Y, bounds.Width, 14),
            label,
            new Color(153, 194, 211),
            fontSize: GumTextLayout.GetMetrics(GumTextStyle.Compact).FontSize,
            verticalAlignment: VerticalAlignment.Top);
        gumUi.AddText(
            new Rectangle(bounds.X, bounds.Y + 16, bounds.Width, Math.Max(20, bounds.Height - 16)),
            value,
            Color.White,
            fontSize: GumTextLayout.GetMetrics(GumTextStyle.Small).FontSize,
            verticalAlignment: VerticalAlignment.Top,
            maxLines: maxLines);
    }

    private static bool TryGetCardIndex(Point point, TrilodexLayoutInfo layout, out int index)
    {
        for (var i = 0; i < layout.CardBounds.Count; i++)
        {
            if (!layout.CardBounds[i].Contains(point))
            {
                continue;
            }

            index = i;
            return true;
        }

        index = -1;
        return false;
    }

    private static void AddText(GumUiRenderer gumUi, Rectangle bounds, string text, Color color, GumTextStyle style)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(bounds, text, color, fontSize: metrics.FontSize, verticalAlignment: VerticalAlignment.Center);
    }

    private static void AddText(
        ContainerRuntime parent,
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(parent, bounds, text, color, fontSize: metrics.FontSize, verticalAlignment: VerticalAlignment.Center);
    }

    private static void AddCenteredText(GumUiRenderer gumUi, Rectangle bounds, string text, Color color, GumTextStyle style)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(
            bounds,
            text,
            color,
            HorizontalAlignment.Center,
            VerticalAlignment.Center,
            metrics.FontSize);
    }
}
