using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
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
    private Point _pointerPoint;
    private float _gridScroll;
    private float _infoPanelScroll;
    private int? _selectedCardIndex;
    private FeatureTree? _selectedTree;
    private ResearchTreeViewNode? _selectedTreeRoot;
    private ResearchTreeViewNode? _selectedNode;
    private string _detailTitle = string.Empty;
    private string _detailSubtitle = string.Empty;
    private bool _isTransientDetail;
    private readonly ResearchTreeViewerController _treeViewer = new();

    public bool IsOpen { get; private set; }

    internal bool IsDetailOpen => _selectedTreeRoot is not null;

    internal Vector2 TreePanOffset => _treeViewer.PanOffset;

    internal float TreeZoom => _treeViewer.Zoom;

    internal bool IsTransientDetail => _isTransientDetail;

    public void Reset()
    {
        _pointerPoint = Point.Zero;
        _gridScroll = 0f;
        _infoPanelScroll = 0f;
        _selectedCardIndex = null;
        ClearDetail();
        IsOpen = false;
    }

    public void Open()
    {
        IsOpen = true;
        _gridScroll = 0f;
        _infoPanelScroll = 0f;
        _selectedCardIndex = null;
        ClearDetail();
    }

    public void OpenBranchPreview(ResearchBranch branch, string title)
    {
        ArgumentNullException.ThrowIfNull(branch);
        IsOpen = true;
        _gridScroll = 0f;
        _infoPanelScroll = 0f;
        _selectedCardIndex = null;
        ClearDetail();
        _selectedTreeRoot = ResearchTreeViewNode.FromResearchBranch(branch);
        _detailTitle = string.IsNullOrWhiteSpace(title) ? "Research Branch" : title;
        _detailSubtitle = "Read-only draft branch preview.";
        _isTransientDetail = true;
    }

    public void Close()
    {
        _infoPanelScroll = 0f;
        _selectedCardIndex = null;
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

        if (_selectedTreeRoot is not null)
        {
            if (_isTransientDetail)
            {
                return TrilodexInteractionOutcome.RequestedClose;
            }

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

            _treeViewer.HandleWheel(
                point,
                delta,
                layout.DetailTreeViewportBounds,
                _selectedTreeRoot,
                ResearchTreeUiRenderer.ReadOnlyDetailConfig);
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

        return true;
    }

    public void HandlePointerDrag(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return;
        }
    }

    public bool HandlePanPointerDown(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
        if (_selectedTreeRoot is null || !layout.DetailTreeViewportBounds.Contains(point))
        {
            return false;
        }

        return _treeViewer.HandlePanPointerDown(point, layout.DetailTreeViewportBounds, _selectedTreeRoot);
    }

    public void HandlePanPointerDrag(Point point)
    {
        _pointerPoint = point;
        if (!IsOpen || _selectedTreeRoot is null)
        {
            return;
        }

        _treeViewer.HandlePanPointerDrag(point);
    }

    public bool HandlePanPointerUp(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen || _selectedTreeRoot is null)
        {
            return false;
        }

        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
        return _treeViewer.HandlePanPointerUp(
            layout.DetailTreeViewportBounds,
            _selectedTreeRoot,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);
    }

    public TrilodexInteractionOutcome HandlePointerUp(Point point, Point viewport)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return TrilodexInteractionOutcome.None;
        }

        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count, _gridScroll);
        if (!layout.PanelBounds.Contains(point))
        {
            return TrilodexInteractionOutcome.RequestedClose;
        }

        if (layout.CloseButtonBounds.Contains(point))
        {
            return TrilodexInteractionOutcome.RequestedClose;
        }

        if (_selectedTreeRoot is not null)
        {
            if (layout.BackButtonBounds.Contains(point))
            {
                if (_isTransientDetail)
                {
                    return TrilodexInteractionOutcome.RequestedClose;
                }

                ClearDetail();
                return TrilodexInteractionOutcome.Consumed;
            }

            if (_selectedTreeRoot is not null &&
                layout.DetailTreeViewportBounds.Contains(point) &&
                ResearchTreeUiRenderer.TryGetHoveredDetailNode(
                    layout.DetailTreeViewportBounds,
                    _selectedTreeRoot,
                    _treeViewer.PanOffset,
                    _treeViewer.Zoom,
                    point,
                    ResearchTreeUiRenderer.ReadOnlyDetailConfig,
                    out _) is ResearchTreeViewNode selectedNode)
            {
                _selectedNode = selectedNode;
                _infoPanelScroll = 0f;
            }

            return TrilodexInteractionOutcome.Consumed;
        }

        if (TryGetCardIndex(point, layout, out var index))
        {
            _selectedCardIndex = index;
            OpenDetail(TriloDex.Global.FeatureTrees[index]);
        }

        return TrilodexInteractionOutcome.Consumed;
    }

    public void Draw(
        Point viewport,
        GameSession session,
        GumUiRenderer gumUi,
        GumRenderTargetViewport? renderTargetViewport = null,
        Texture2D? treeBackgroundTexture = null,
        double visualTimeMs = 0d)
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
            BuildMenuModel(layout, session, treeBackgroundTexture, visualTimeMs),
            _pointerPoint,
            renderTargetViewport);
    }

    private ResearchTreeMenuModel BuildMenuModel(
        ResearchDraftTreeCatalogLayoutInfo layout,
        GameSession session,
        Texture2D? treeBackgroundTexture,
        double visualTimeMs)
    {
        var detailOpen = _selectedTreeRoot is not null;
        return detailOpen
            ? BuildDetailMenuModel(layout, session, treeBackgroundTexture, visualTimeMs)
            : BuildCatalogMenuModel(layout, visualTimeMs);
    }

    internal ResearchTreeMenuModel BuildCatalogMenuModel(
        ResearchDraftTreeCatalogLayoutInfo layout,
        double visualTimeMs = 0d)
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
            string.Empty,
            CardHeaderText: string.Empty,
            TreeHeaderText: string.Empty,
            BuildCatalogCardModels(layout),
            new ResearchTreeViewportModel(
                Root: null,
                Vector2.Zero,
                Zoom: 1f,
                BackgroundTexture: null,
                VisualTimeMs: visualTimeMs),
            new ResearchTreeInfoPanelModel(NodeInfo: null, "Info", "Hover a tree node for details.", _infoPanelScroll),
            FooterText: string.Empty);
    }

    internal ResearchTreeMenuModel BuildDetailMenuModel(
        ResearchDraftTreeCatalogLayoutInfo layout,
        GameSession session,
        Texture2D? treeBackgroundTexture,
        double visualTimeMs)
    {
        ResearchNodeInfo? selectedNodeInfo = _selectedNode is null
            ? null
            : ResearchTreeUiRenderer.BuildNodeInfo(session, _selectedNode);
        var selectionOverlay = new Func<ResearchTreeViewportOverlayContext, ResearchNodeInfo?>(context =>
        {
            DrawSelectedNodeHighlight(context, visualTimeMs);
            return null;
        });

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
            string.IsNullOrWhiteSpace(_detailTitle) ? "Trilodex" : _detailTitle,
            string.IsNullOrWhiteSpace(_detailSubtitle)
                ? "Curated research tree. Read-only preview."
                : _detailSubtitle,
            CardHeaderText: string.Empty,
            TreeHeaderText: string.Empty,
            Cards: [],
            new ResearchTreeViewportModel(
                _selectedTreeRoot,
                _treeViewer.PanOffset,
                _treeViewer.Zoom,
                treeBackgroundTexture,
                selectionOverlay,
                VisualTimeMs: visualTimeMs),
            new ResearchTreeInfoPanelModel(selectedNodeInfo, "Info", "Hover a tree node for details.", _infoPanelScroll),
            FooterText: string.Empty);
    }

    internal IReadOnlyList<ResearchTreeCardModel> BuildCatalogCardModels(ResearchDraftTreeCatalogLayoutInfo layout)
    {
        var trees = TriloDex.Global.FeatureTrees;
        var cards = new List<ResearchTreeCardModel>(trees.Count);
        for (var index = 0; index < trees.Count; index++)
        {
            var tree = trees[index];
            var hovered = index < layout.CardBounds.Count && layout.CardBounds[index].Contains(_pointerPoint);
            cards.Add(new ResearchTreeCardModel(
                tree.DisplayName,
                string.Empty,
                tree.Root is null ? null : ResearchTreeViewNode.FromFeatureTree(tree),
                hovered,
                IsSelected: _selectedCardIndex == index));
        }

        return cards;
    }

    private void OpenDetail(FeatureTree featureTree)
    {
        _selectedTree = featureTree;
        _selectedTreeRoot = ResearchTreeViewNode.FromFeatureTree(featureTree);
        _detailTitle = featureTree.DisplayName;
        _detailSubtitle = $"{featureTree.Name} - Tier {featureTree.Tier} curated tree. Read-only preview.";
        _isTransientDetail = false;
        _selectedNode = null;
        _treeViewer.Reset();
        _infoPanelScroll = 0f;
    }

    private void ClearDetail()
    {
        _selectedTree = null;
        _selectedTreeRoot = null;
        _selectedNode = null;
        _detailTitle = string.Empty;
        _detailSubtitle = string.Empty;
        _isTransientDetail = false;
        _treeViewer.Reset();
        _infoPanelScroll = 0f;
    }

    private void DrawSelectedNodeHighlight(
        ResearchTreeViewportOverlayContext context,
        double visualTimeMs)
    {
        if (_selectedTreeRoot is null ||
            _selectedNode is null ||
            !ResearchTreeUiRenderer.TryGetDetailNodeCenter(
                context.ViewportBounds,
                _selectedTreeRoot,
                _treeViewer.PanOffset,
                _treeViewer.Zoom,
                _selectedNode,
                ResearchTreeUiRenderer.ReadOnlyDetailConfig,
                out var center))
        {
            return;
        }

        var hoveredNode = ResearchTreeUiRenderer.TryGetHoveredDetailNode(
            context.ViewportBounds,
            _selectedTreeRoot,
            _treeViewer.PanOffset,
            _treeViewer.Zoom,
            context.PointerPoint,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig,
            out _);
        if (ReferenceEquals(hoveredNode, _selectedNode))
        {
            return;
        }

        ResearchTreeUiRenderer.DrawSelectedNodeHalo(
            context.GumUi,
            center,
            context.Metrics.NodeRadius,
            visualTimeMs);
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
