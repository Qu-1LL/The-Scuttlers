using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private float _infoPanelScroll;
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
        _infoPanelScroll = 0f;
        ClearDetail();
        IsOpen = false;
    }

    public void Open()
    {
        IsOpen = true;
        _gridScroll = 0f;
        _infoPanelScroll = 0f;
        ClearDetail();
    }

    public void Close()
    {
        _infoPanelScroll = 0f;
        ClearDetail();
        IsOpen = false;
    }

    public void UpdatePointer(Point point)
    {
        _pointerPoint = point;
    }

    public bool CoversScreenPoint(Point point, Point viewport)
    {
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
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

        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        if (_selectedTreeRoot is not null)
        {
            if (layout.DetailInfoPanelBounds.Contains(point))
            {
                _infoPanelScroll += delta;
                return true;
            }

            if (!layout.DetailTreeViewportBounds.Contains(point))
            {
                return true;
            }

            var previousZoom = _treeZoom;
            _treeZoom = ResearchTreeUiRenderer.ClampZoom(_treeZoom + (-delta * 0.0015f));
            if (MathF.Abs(_treeZoom - previousZoom) <= float.Epsilon)
            {
                return true;
            }

            var metricsBefore = ResearchTreeUiRenderer.CalculateDetailMetrics(
                layout.DetailTreeViewportBounds,
                _selectedTreeRoot,
                previousZoom,
                ResearchTreeUiRenderer.ReadOnlyDetailConfig);
            var pointToOrigin = point.ToVector2() - metricsBefore.Origin - _treePanOffset;
            if (previousZoom > float.Epsilon)
            {
                _treePanOffset += pointToOrigin - (pointToOrigin * (_treeZoom / previousZoom));
            }

            return true;
        }

        if (layout.CatalogFrameBounds.Contains(point))
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

        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
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

        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
        if (wasPanning && _selectedTreeRoot is not null)
        {
            _treePanOffset = ResearchTreeUiRenderer.ResolvePanAfterRelease(
                layout.DetailTreeViewportBounds,
                _selectedTreeRoot,
                _treePanOffset,
                _treeZoom,
                ResearchTreeUiRenderer.ReadOnlyDetailConfig);
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
        GumRenderTargetViewport? renderTargetViewport = null,
        Texture2D? treeBackgroundTexture = null)
    {
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
        if (!IsOpen)
        {
            return;
        }

        gumUi.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(5, 11, 17, 164));
        ResearchTreeMenuRenderer.Draw(
            gumUi,
            session,
            BuildMenuModel(layout, session, treeBackgroundTexture),
            _pointerPoint,
            renderTargetViewport);
    }

    private ResearchTreeMenuModel BuildMenuModel(
        ResearchDraftTreeCatalogLayoutInfo layout,
        GameSession session,
        Texture2D? treeBackgroundTexture)
    {
        var detailOpen = _selectedTree is not null && _selectedTreeRoot is not null;
        return detailOpen
            ? BuildDetailMenuModel(layout, treeBackgroundTexture)
            : BuildCatalogMenuModel(layout);
    }

    private ResearchTreeMenuModel BuildCatalogMenuModel(ResearchDraftTreeCatalogLayoutInfo layout)
    {
        return new ResearchTreeMenuModel(
            ResearchTreeMenuMode.TrilodexCatalog,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: true,
                CardAreaMode: ResearchTreeCardAreaMode.CatalogGrid,
                ShowTreeViewport: false,
                ShowInfoPanel: false,
                ShowFooter: false,
                EnablePanZoom: false,
                EnableNodeHover: true,
                EnableNodeSelection: true,
                EnableBranchDrafting: false,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: true,
                ShowRootNode: true,
                CanPlaceBranches: false),
            ResearchTreeMenuRenderer.FromCatalogLayout(layout, detailOpen: false),
            "Trilodex",
            "Curated research trees discovered by the colony.",
            CardHeaderText: string.Empty,
            TreeHeaderText: string.Empty,
            BuildCatalogCardModels(layout),
            new ResearchTreeViewportModel(Root: null, Vector2.Zero, Zoom: 1f, BackgroundTexture: null),
            new ResearchTreeInfoPanelModel(NodeInfo: null, "Info", "Hover a tree node for details.", _infoPanelScroll),
            FooterText: string.Empty);
    }

    private ResearchTreeMenuModel BuildDetailMenuModel(
        ResearchDraftTreeCatalogLayoutInfo layout,
        Texture2D? treeBackgroundTexture)
    {
        return new ResearchTreeMenuModel(
            ResearchTreeMenuMode.ReadOnlyDetail,
            new ResearchTreeMenuConfig(
                ShowBackButton: true,
                ShowCloseButton: true,
                CardAreaMode: ResearchTreeCardAreaMode.None,
                ShowTreeViewport: true,
                ShowInfoPanel: true,
                ShowFooter: false,
                EnablePanZoom: true,
                EnableNodeHover: true,
                EnableNodeSelection: true,
                EnableBranchDrafting: false,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: true,
                ShowRootNode: true,
                CanPlaceBranches: false),
            ResearchTreeMenuRenderer.FromCatalogLayout(layout, detailOpen: true),
            _selectedTree?.Name ?? "Trilodex",
            _selectedTree is null
                ? "Curated research trees discovered by the colony."
                : $"Tier {_selectedTree.Tier} curated tree. Read-only preview.",
            CardHeaderText: string.Empty,
            TreeHeaderText: string.Empty,
            Cards: [],
            new ResearchTreeViewportModel(_selectedTreeRoot, _treePanOffset, _treeZoom, treeBackgroundTexture),
            new ResearchTreeInfoPanelModel(NodeInfo: null, "Info", "Hover a tree node for details.", _infoPanelScroll),
            FooterText: string.Empty);
    }

    private IReadOnlyList<ResearchTreeCardModel> BuildCatalogCardModels(ResearchDraftTreeCatalogLayoutInfo layout)
    {
        var trees = TriloDex.Global.FeatureTrees;
        var cards = new List<ResearchTreeCardModel>(trees.Count);
        for (var index = 0; index < trees.Count; index++)
        {
            var tree = trees[index];
            var hovered = index < layout.CardBounds.Count && layout.CardBounds[index].Contains(_pointerPoint);
            cards.Add(new ResearchTreeCardModel(
                tree.Name,
                $"Tier {tree.Tier}",
                tree.Root is null ? null : ResearchTreeViewNode.FromFeatureTree(tree),
                hovered,
                IsSelected: false));
        }

        return cards;
    }

    private void OpenDetail(FeatureTree featureTree)
    {
        _selectedTree = featureTree;
        _selectedTreeRoot = ResearchTreeViewNode.FromFeatureTree(featureTree);
        _treePanOffset = Vector2.Zero;
        _treePanStartOffset = Vector2.Zero;
        _treeZoom = 1f;
        _infoPanelScroll = 0f;
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
        _infoPanelScroll = 0f;
        _treePanCandidate = false;
        _treePanning = false;
    }

    private static bool TryGetCardIndex(Point point, ResearchDraftTreeCatalogLayoutInfo layout, out int index)
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
}
