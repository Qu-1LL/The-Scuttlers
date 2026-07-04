using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

public enum ResearchDraftInteractionOutcome
{
    None,
    Consumed,
    RequestedOpen,
    RequestedClose,
    BranchPlaced,
    NodeSelected,
    NodeUnlocked
}

internal enum ResearchNodeHoverPlacement
{
    InfoPanel
}

public sealed class ResearchDraftController
{
    private const string EmptyStatus = "No research branches are waiting right now.";
    private const string PendingStatus = "Click a research branch, then click a valid spot on the skill tree to graft it.";
    private const string SelectedBranchStatus = "Move the selected branch over the skill tree and click to place it.";
    private static readonly Color BranchConnectorColor = new(255, 255, 255);
    private static readonly Color BranchConnectorGhostColor = new(255, 255, 255, 140);
    private static readonly Color InvalidBranchConnectorColor = new(242, 126, 119);
    private static readonly Color BranchCollisionColor = new(242, 72, 68);
    private static readonly Color BranchCollisionBorderColor = new(255, 220, 217);
    private static readonly Color BoundaryColor = new(126, 149, 159, 184);
    private static readonly Color BoundaryCollisionColor = new(242, 72, 68, 235);
    private const byte BranchPreviewFillAlpha = 150;
    private const byte BranchPreviewBorderAlpha = 190;
    private static readonly Color BranchOriginFillColor = new(238, 207, 106);
    private static readonly Color BranchOriginBorderColor = new(255, 247, 222);
    private static readonly ResearchTreeRenderConfig DraftTreeRenderConfig = new(
        ShowBackButton: false,
        ShowRootNode: true,
        EnableNodeSelection: true,
        EnableBranchDrafting: true,
        EnablePlacementPreview: true);
    private readonly ResearchTreeViewerController _treeViewer = new();
    private Point _pointerPoint;
    private int? _selectedBranchIndex;
    private TreeInstanceNode? _selectedSkillTreeNode;
    private string _statusMessage = EmptyStatus;
    private float _infoPanelScroll;

    public bool IsOpen { get; private set; }

    internal Vector2 TreePanOffset => _treeViewer.PanOffset;

    public void Reset()
    {
        _pointerPoint = Point.Zero;
        _treeViewer.Reset();
        _selectedBranchIndex = null;
        _selectedSkillTreeNode = null;
        _statusMessage = EmptyStatus;
        _infoPanelScroll = 0f;
        IsOpen = false;
    }

    public void Open(ResearchDraftSystem draftSystem)
    {
        IsOpen = true;
        _treeViewer.Reset();
        _selectedSkillTreeNode = null;
        _statusMessage = BuildDefaultStatus(draftSystem);
        _infoPanelScroll = 0f;
    }

    public void Close(ResearchDraftSystem draftSystem)
    {
        _treeViewer.Reset();
        _selectedBranchIndex = null;
        _selectedSkillTreeNode = null;
        _statusMessage = BuildDefaultStatus(draftSystem);
        _infoPanelScroll = 0f;
        IsOpen = false;
    }

    public void UpdatePointer(Point point)
    {
        _pointerPoint = point;
    }

    public bool HandleWheel(Point point, int delta, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = BuildLayout(viewport, draftSystem);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        if (layout.InfoPanelBounds.Contains(point))
        {
            _infoPanelScroll += delta;
            return true;
        }

        if (!layout.TreeViewportBounds.Contains(point))
        {
            return true;
        }

        _treeViewer.HandleWheel(
            point,
            delta,
            layout.TreeViewportBounds,
            BuildSkillTreeRoot(session),
            DraftTreeRenderConfig);
        return true;
    }

    public bool CoversScreenPoint(Point point, Point viewport)
    {
        var layout = ResearchDraftLayout.Build(viewport);
        return IsOpen
            ? layout.PanelBounds.Contains(point)
            : layout.ButtonBounds.Contains(point);
    }

    public ResearchDraftInteractionOutcome HandleClosedButtonClick(Point point, Point viewport)
    {
        if (IsOpen || !ResearchDraftLayout.GetButtonBounds(viewport).Contains(point))
        {
            return ResearchDraftInteractionOutcome.None;
        }

        return ResearchDraftInteractionOutcome.RequestedOpen;
    }

    public bool HandlePointerDown(Point point, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = BuildLayout(viewport, draftSystem);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        return true;
    }

    public void HandlePointerDrag(Point point, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
    }

    public bool HandlePanPointerDown(Point point, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = BuildLayout(viewport, draftSystem);
        if (!layout.TreeViewportBounds.Contains(point))
        {
            return false;
        }

        return _treeViewer.HandlePanPointerDown(point, layout.TreeViewportBounds, BuildSkillTreeRoot(session));
    }

    public void HandlePanPointerDrag(Point point)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return;
        }

        _treeViewer.HandlePanPointerDrag(point);
    }

    public bool HandlePanPointerUp(Point point, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return false;
        }

        var layout = BuildLayout(viewport, draftSystem);
        return _treeViewer.HandlePanPointerUp(layout.TreeViewportBounds, BuildSkillTreeRoot(session), DraftTreeRenderConfig);
    }

    public ResearchDraftInteractionOutcome HandlePointerUp(
        Point point,
        Point viewport,
        GameSession session,
        ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return ResearchDraftInteractionOutcome.None;
        }

        var layout = BuildLayout(viewport, draftSystem);

        if (!layout.PanelBounds.Contains(point))
        {
            _selectedBranchIndex = null;
            _statusMessage = BuildDefaultStatus(draftSystem);
            return ResearchDraftInteractionOutcome.RequestedClose;
        }

        if (layout.CloseButtonBounds.Contains(point))
        {
            _selectedBranchIndex = null;
            _statusMessage = BuildDefaultStatus(draftSystem);
            return ResearchDraftInteractionOutcome.RequestedClose;
        }

        var unlockOutcome = TryHandleUnlockClick(point, layout, session);
        if (unlockOutcome != ResearchDraftInteractionOutcome.None)
        {
            return unlockOutcome;
        }

        if (draftSystem.PendingDraft is not null &&
            TryGetBranchCardSelection(point, layout, draftSystem.PendingDraft, out var selectedBranchIndex))
        {
            _selectedBranchIndex = selectedBranchIndex;
            _selectedSkillTreeNode = null;
            _statusMessage = SelectedBranchStatus;
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (_selectedBranchIndex is int branchIndex)
        {
            return TryPlaceSelectedBranch(point, layout, session, draftSystem, branchIndex);
        }

        if (TrySelectSkillTreeNode(point, layout, session))
        {
            return ResearchDraftInteractionOutcome.NodeSelected;
        }

        return ResearchDraftInteractionOutcome.Consumed;
    }

    public ResearchDraftInteractionOutcome HandleEscape(ResearchDraftSystem draftSystem)
    {
        if (!IsOpen)
        {
            return ResearchDraftInteractionOutcome.None;
        }

        _selectedBranchIndex = null;
        _statusMessage = BuildDefaultStatus(draftSystem);
        return ResearchDraftInteractionOutcome.RequestedClose;
    }

    public void Draw(
        Point viewport,
        GameSession session,
        ResearchDraftSystem draftSystem,
        GumUiRenderer gumUi,
        Texture2D? treeBackgroundTexture = null,
        double visualTimeMs = 0d)
    {
        var layout = BuildLayout(viewport, draftSystem);
        DrawButton(layout.ButtonBounds, draftSystem.HasPendingDraft, gumUi);

        if (!IsOpen)
        {
            return;
        }

        gumUi.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(5, 11, 17, 164));
        DrawPanel(layout, session, draftSystem, gumUi, treeBackgroundTexture, visualTimeMs);
    }

    private ResearchDraftInteractionOutcome TryPlaceSelectedBranch(
        Point point,
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        int branchIndex)
    {
        var preview = BuildDragPreview(point, layout, session, draftSystem, branchIndex);
        if (!preview.CanPlace || preview.AnchorNode is not TreeInstanceNode anchorNode)
        {
            _statusMessage = preview.StatusMessage;
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (!draftSystem.TryPlaceBranch(session, branchIndex, anchorNode, out var failureReason))
        {
            _statusMessage = failureReason ?? "That branch could not be placed there.";
            return ResearchDraftInteractionOutcome.Consumed;
        }

        _selectedBranchIndex = null;
        _statusMessage = "Research branch added to the colony skill tree.";
        return ResearchDraftInteractionOutcome.BranchPlaced;
    }

    private ResearchDraftInteractionOutcome TryHandleUnlockClick(
        Point point,
        ResearchDraftLayoutInfo layout,
        GameSession session)
    {
        if (_selectedSkillTreeNode is not TreeInstanceNode selectedNode)
        {
            return ResearchDraftInteractionOutcome.None;
        }

        var buttonBounds = ResearchTreeInfoPanelLayout.GetUnlockButtonBounds(layout.InfoPanelBounds);
        if (!buttonBounds.Contains(point))
        {
            return ResearchDraftInteractionOutcome.None;
        }

        var quote = SkillTreeUnlockSystem.GetUnlockQuote(session, selectedNode);
        if (!quote.CanUnlock)
        {
            _statusMessage = BuildUnlockFailureStatus(quote.BlockReason);
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (!SkillTreeUnlockSystem.TryUnlock(session, selectedNode, out var result))
        {
            _statusMessage = BuildUnlockFailureStatus(result.BlockReason);
            return ResearchDraftInteractionOutcome.Consumed;
        }

        _statusMessage = "Skill node unlocked.";
        return ResearchDraftInteractionOutcome.NodeUnlocked;
    }

    private bool TrySelectSkillTreeNode(
        Point point,
        ResearchDraftLayoutInfo layout,
        GameSession session)
    {
        if (!layout.TreeViewportBounds.Contains(point) ||
            session.SkillTree.Root is null ||
            !TryGetPlacedSkillTreeNodeAtPoint(point, layout.TreeViewportBounds, session.SkillTree, out var node))
        {
            return false;
        }

        _selectedSkillTreeNode = node;
        _selectedBranchIndex = null;
        _infoPanelScroll = 0f;
        _statusMessage = node!.IsUnlocked ? "Skill node selected." : "Locked skill node selected.";
        return true;
    }

    private void DrawButton(Rectangle bounds, bool hasPendingDraft, GumUiRenderer gumUi)
    {
        var hovered = bounds.Contains(_pointerPoint);
        var fill = hasPendingDraft
            ? hovered ? new Color(176, 147, 92) : new Color(152, 125, 74)
            : hovered ? new Color(22, 50, 71) : new Color(16, 38, 54);
        var border = hasPendingDraft
            ? hovered ? new Color(255, 229, 170) : new Color(233, 201, 143)
            : hovered ? new Color(125, 179, 196) : new Color(54, 88, 107);
        var text = hasPendingDraft ? new Color(18, 26, 34) : Color.White;

        gumUi.AddRoundedFrame(bounds, fill, border, 2, 14);
        GumUiText.AddCentered(
            gumUi,
            new Rectangle(bounds.X + 12, bounds.Y, bounds.Width - 24, bounds.Height),
            "Adaptation\nTree",
            text,
            GumTextStyle.Compact,
            maxLines: 2);
    }

    private void DrawPanel(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        GumUiRenderer gumUi,
        Texture2D? treeBackgroundTexture,
        double visualTimeMs)
    {
        ResearchTreeMenuRenderer.Draw(
            gumUi,
            session,
            BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture, visualTimeMs),
            _pointerPoint);
    }

    internal ResearchTreeMenuModel BuildDraftMenuModel(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        Texture2D? treeBackgroundTexture,
        double visualTimeMs = 0d)
    {
        var pendingDraft = draftSystem.PendingDraft;
        UpdateSelectedBranchStatus(layout, session, draftSystem);
        var branchCardHoverInfo = pendingDraft is null
            ? null
            : GetBranchCardHoverInfo(layout.BranchCardBounds, pendingDraft.Branches, session);
        var selectedNodeInfo = BuildSelectedSkillTreeNodeInfo(session);
        var infoPanelNodeInfo = branchCardHoverInfo ?? selectedNodeInfo;
        var unlockAction = branchCardHoverInfo is null
            ? BuildSelectedSkillTreeUnlockAction(session)
            : null;
        var skillTreeRoot = BuildSkillTreeRoot(session);
        var placementOverlay = new Func<ResearchTreeViewportOverlayContext, ResearchNodeInfo?>(context =>
            DrawDraftTreeOverlay(
                context.ViewportBounds,
                ConvertToPlacementMetrics(context.Metrics),
                context.Session,
                draftSystem,
                context.GumUi,
                visualTimeMs));
        var overlayReplacesTreeContent = ShouldPlacementOverlayReplaceTreeContent(layout, session, draftSystem);

        return new ResearchTreeMenuModel(
            ResearchTreeMenuMode.Drafting,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: true,
                CardAreaMode: pendingDraft is null ? ResearchTreeCardAreaMode.None : ResearchTreeCardAreaMode.DraftRow,
                ShowTreeViewport: true,
                ShowInfoPanel: true,
                ShowFooter: true,
                EnablePanZoom: true,
                EnableNodeHover: true,
                EnableNodeSelection: true,
                EnableBranchDrafting: pendingDraft is not null,
                EnablePlacementPreview: _selectedBranchIndex is not null,
                EnableReadOnlyPreview: false,
                ShowRootNode: true,
                CanPlaceBranches: pendingDraft is not null),
            ResearchTreeMenuRenderer.FromDraftLayout(layout),
            "Skill Tree Research",
            pendingDraft is null
                ? "Review the colony's current run-specific skill tree."
                : BuildDraftSubtitle(pendingDraft),
            "Draftable Branches",
            "Global Skill Tree",
            pendingDraft is null ? [] : BuildDraftCardModels(layout.BranchCardBounds, pendingDraft.Branches, session),
            new ResearchTreeViewportModel(
                skillTreeRoot,
                _treeViewer.PanOffset,
                _treeViewer.Zoom,
                treeBackgroundTexture,
                placementOverlay,
                overlayReplacesTreeContent,
                visualTimeMs),
            new ResearchTreeInfoPanelModel(
                infoPanelNodeInfo,
                "Info",
                pendingDraft is not null
                    ? "Hover a branch or tree node for details."
                    : "Hover a tree node for details.",
                _infoPanelScroll,
                unlockAction),
            _statusMessage);
    }

    private IReadOnlyList<ResearchTreeCardModel> BuildDraftCardModels(
        IReadOnlyList<Rectangle> cardBounds,
        IReadOnlyList<ResearchBranch> branches,
        GameSession session)
    {
        var cards = new List<ResearchTreeCardModel>(cardBounds.Count);
        for (var index = 0; index < cardBounds.Count; index++)
        {
            var branch = index < branches.Count ? branches[index] : null;
            var hovered = cardBounds[index].Contains(_pointerPoint);
            var selected = _selectedBranchIndex == index;
            cards.Add(branch is null || branch.Count == 0
                ? new ResearchTreeCardModel($"Branch {index + 1}", "Unavailable", Root: null, hovered, selected)
                : new ResearchTreeCardModel(
                    BuildBranchCardTitle(branch, index),
                    BuildBranchCardSubtitle(session, branch),
                    ResearchTreeViewNode.FromResearchBranch(branch),
                    hovered,
                    selected));
        }

        return cards;
    }

    private void UpdateSelectedBranchStatus(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem)
    {
        if (_selectedBranchIndex is not int selectedBranchIndex)
        {
            return;
        }

        var preview = BuildDragPreview(_pointerPoint, layout.TreeViewportBounds, session, draftSystem, selectedBranchIndex);
        if (!string.IsNullOrWhiteSpace(preview.StatusMessage))
        {
            _statusMessage = preview.StatusMessage;
        }
    }

    private bool ShouldPlacementOverlayReplaceTreeContent(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem)
    {
        if (_selectedBranchIndex is not int selectedBranchIndex)
        {
            return false;
        }

        var preview = BuildDragPreview(_pointerPoint, layout.TreeViewportBounds, session, draftSystem, selectedBranchIndex);
        return preview.AnchorNode is not null;
    }

    private ResearchNodeInfo? DrawDraftTreeOverlay(
        Rectangle treeViewportBounds,
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchDraftSystem draftSystem,
        GumUiRenderer gumUi,
        double visualTimeMs)
    {
        ResearchDraftDragPreview preview = ResearchDraftDragPreview.Empty;
        if (_selectedBranchIndex is not int activeBranchIndex ||
            draftSystem.PendingDraft is null ||
            activeBranchIndex >= draftSystem.PendingDraft.Branches.Count)
        {
            DrawBoundary(gumUi, metrics, _treeViewer.PanOffset, preview.Collision);
            DrawSelectedSkillTreeNodeHighlight(gumUi, metrics, session, visualTimeMs);
            return null;
        }

        var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
        preview = BuildDragPreview(_pointerPoint, treeViewportBounds, session, draftSystem, activeBranchIndex);
        if (!string.IsNullOrWhiteSpace(preview.StatusMessage))
        {
            _statusMessage = preview.StatusMessage;
        }

        DrawBoundary(gumUi, metrics, _treeViewer.PanOffset, preview.Collision);
        return preview.AnchorNode is not null
            ? DrawProjectedPlacementPreview(metrics, session, branch, preview, gumUi, visualTimeMs)
            : DrawCursorBoundBranchPreview(metrics, session, branch, gumUi, visualTimeMs);
    }

    private static void DrawBoundary(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        Vector2 panOffset,
        ResearchDraftPlacementCollision collision)
    {
        var offset = metrics.Origin + panOffset;
        foreach (var segment in ResearchDraftBoundaryProfile.Default.CreateSegments(metrics.EdgeLength, offset))
        {
            DrawClippedLine(
                gumUi,
                metrics,
                segment.Start,
                segment.End,
                collision.ContainsBoundaryLine(segment.Id) ? BoundaryCollisionColor : BoundaryColor,
                3);
        }
    }

    private void DrawSelectedSkillTreeNodeHighlight(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        double visualTimeMs)
    {
        if (_selectedSkillTreeNode is not TreeInstanceNode selectedNode ||
            session.SkillTree.Root is null ||
            !session.SkillTree.Contains(selectedNode))
        {
            return;
        }

        var layout = BuildPlacedTreeLayout(metrics, session.SkillTree.Root);
        var selectedLayoutNode = FindLayoutNode(layout, selectedNode);
        if (selectedLayoutNode is null ||
            !IsNodeVisible(metrics, selectedLayoutNode.Position, metrics.NodeRadius + 12))
        {
            return;
        }

        if (TryGetHoveredPlacedNode(metrics, layout, _pointerPoint, out var hoveredNode, out _) &&
            ReferenceEquals(hoveredNode, selectedNode))
        {
            return;
        }

        var drawPosition = selectedLayoutNode.Position;
        if (selectedNode.CanUnlock())
        {
            drawPosition += ResearchTreeUiRenderer.CalculateAvailableNodeShakeOffset(
                $"{selectedNode.SourceFeatureTreeName}:{selectedNode.Name}",
                metrics.NodeRadius,
                visualTimeMs);
        }

        ResearchTreeUiRenderer.DrawSelectedNodeHalo(
            gumUi,
            drawPosition,
            metrics.NodeRadius,
            visualTimeMs);
    }

    private ResearchNodeInfo? DrawProjectedPlacementPreview(
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        ResearchDraftDragPreview preview,
        GumUiRenderer gumUi,
        double visualTimeMs)
    {
        if (session.SkillTree.Root is null || branch.Root is null || preview.AnchorNode is null)
        {
            return null;
        }

        var layout = BuildProjectedPlacementLayout(
            metrics.Origin,
            metrics.EdgeLength,
            _treeViewer.PanOffset,
            session.SkillTree.Root,
            branch,
            preview.AnchorNode);
        var hoveredNode = TryGetHoveredProjectedNode(metrics, layout, out _);

        for (var nodeIndex = 0; nodeIndex < layout.Nodes.Count; nodeIndex++)
        {
            var node = layout.Nodes[nodeIndex];
            if (node.Parent is null)
            {
                continue;
            }

            var isMovingLine = node.IsBranchNode || node.Parent.IsBranchNode;
            var isFixedCollisionLine = !isMovingLine && preview.Collision.ContainsFixedLine(node.FixedNodeId);
            var lineColor = isMovingLine
                ? GetBranchPreviewLineColor(preview, node.BranchNodeId)
                : isFixedCollisionLine
                    ? WithAlpha(BranchCollisionColor, 240)
                    : GetSkillTreeConnectorColor(node.SkillNode);
            var startInset = isMovingLine && !node.Parent.IsBranchNode
                ? metrics.NodeRadius + 7f
                : metrics.NodeRadius + 2f;

            DrawClippedConnector(
                gumUi,
                metrics,
                node.Parent.Position,
                node.Position,
                lineColor,
                ResearchTreeUiRenderer.DetailConnectorThickness,
                startInset,
                metrics.NodeRadius + 2f);
        }

        ProjectedTreeRenderNode? anchorLayout = null;
        for (var nodeIndex = 0; nodeIndex < layout.Nodes.Count; nodeIndex++)
        {
            var node = layout.Nodes[nodeIndex];
            if (node.IsBranchNode)
            {
                continue;
            }

            if (ReferenceEquals(node.SkillNode, preview.AnchorNode))
            {
                anchorLayout = node;
            }

            if (!IsNodeVisible(metrics, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            var hasCollision = preview.Collision.ContainsFixedNode(node.FixedNodeId);
            var isAvailable = !hasCollision && node.SkillNode.CanUnlock();
            var drawPosition = node.Position;
            if (isAvailable)
            {
                drawPosition += ResearchTreeUiRenderer.CalculateAvailableNodeShakeOffset(
                    $"{node.SkillNode.SourceFeatureTreeName}:{node.SkillNode.Name}",
                    metrics.NodeRadius,
                    visualTimeMs);
            }

            DrawTreeNode(
                gumUi,
                drawPosition,
                metrics.NodeRadius,
                hasCollision ? WithAlpha(BranchCollisionColor, 235) : GetNodeFillColor(session, node.SkillNode),
                hasCollision ? WithAlpha(BranchCollisionBorderColor, 255) : GetNodeBorderColor(session, node.SkillNode));
            if (!hasCollision && node.SkillNode.IsLocked && !node.SkillNode.CanUnlock() && metrics.NodeRadius >= 4)
            {
                ResearchTreeUiRenderer.DrawLockedNodeMarker(gumUi, drawPosition, metrics.NodeRadius);
            }
            else if (isAvailable)
            {
                ResearchTreeUiRenderer.DrawAvailableNodeMarker(gumUi, drawPosition, metrics.NodeRadius);
            }
        }

        if (anchorLayout is not null && IsNodeVisible(metrics, anchorLayout.Position, metrics.NodeRadius + 5))
        {
            DrawTreeNode(
                gumUi,
                anchorLayout.Position,
                metrics.NodeRadius + 5,
                preview.CanPlace ? new Color(46, 92, 70, 120) : new Color(114, 41, 36, 120),
                preview.CanPlace ? new Color(205, 240, 221) : new Color(255, 192, 188));
        }

        for (var nodeIndex = 0; nodeIndex < layout.Nodes.Count; nodeIndex++)
        {
            var node = layout.Nodes[nodeIndex];
            if (!node.IsBranchNode || !IsNodeVisible(metrics, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            DrawTreeNode(
                gumUi,
                node.Position,
                metrics.NodeRadius,
                GetBranchPreviewNodeFillColor(session, node.SkillNode, preview, node.BranchNodeId),
                GetBranchPreviewNodeBorderColor(preview, node.BranchNodeId));
        }

        if (hoveredNode is null)
        {
            return null;
        }

        DrawProjectedPlacementHighlights(gumUi, metrics, branch, hoveredNode, layout, visualTimeMs);
        return BuildNodeHoverInfo(session, hoveredNode.SkillNode);
    }

    private ResearchNodeInfo? DrawCursorBoundBranchPreview(
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        GumUiRenderer gumUi,
        double visualTimeMs)
    {
        if (branch.Root is null)
        {
            return null;
        }

        var lineColor = BranchConnectorGhostColor;
        var origin = _pointerPoint.ToVector2();
        var branchLayout = BuildBranchLayout(branch, metrics.EdgeLength);
        var hoveredNode = TryGetHoveredBranchNode(metrics, branchLayout, origin, out _);

        foreach (var node in branchLayout.Nodes)
        {
            var point = origin + node.LocalPosition;
            if (node.Parent is null)
            {
                DrawClippedConnector(
                    gumUi,
                    metrics,
                    origin,
                    point,
                    lineColor,
                    ResearchTreeUiRenderer.DetailConnectorThickness,
                    7f,
                    metrics.NodeRadius + 2f);
                continue;
            }

            DrawClippedConnector(
                gumUi,
                metrics,
                origin + node.Parent.LocalPosition,
                point,
                lineColor,
                ResearchTreeUiRenderer.DetailConnectorThickness,
                metrics.NodeRadius + 2f,
                metrics.NodeRadius + 2f);
        }

        if (IsNodeVisible(metrics, origin, 5))
        {
            DrawBranchOriginMarker(gumUi, origin, ghosted: true);
        }

        foreach (var node in branchLayout.Nodes)
        {
            var point = origin + node.LocalPosition;
            if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
            {
                continue;
            }

            DrawTreeNode(
                gumUi,
                point,
                metrics.NodeRadius,
                WithAlpha(GetBranchNodePreviewColor(session, node.BranchNode), BranchPreviewFillAlpha),
                WithAlpha(new Color(246, 251, 253), BranchPreviewBorderAlpha));
        }

        if (hoveredNode is TreeInstanceNode hoveredBranchNode)
        {
            DrawFloatingBranchPrerequisiteHighlights(
                gumUi,
                metrics.NodeRadius,
                branchLayout,
                origin,
                branch,
                hoveredBranchNode,
                visualTimeMs);
            if (session.SkillTree.Root is not null)
            {
                DrawPlacedFeatureTreePrerequisiteHighlights(
                    gumUi,
                    metrics,
                    session,
                    hoveredBranchNode,
                    branch,
                    BuildPlacedTreeLayout(metrics, session.SkillTree.Root));
            }
            return BuildNodeHoverInfo(session, hoveredBranchNode);
        }

        return null;
    }

    private ResearchTreeViewportMetrics BuildTreeMetrics(Rectangle bounds, SkillTree skillTree)
    {
        if (skillTree.Root is null)
        {
            return new ResearchTreeViewportMetrics(
                bounds,
                bounds,
                bounds.Center.ToVector2(),
                0f,
                0,
                new ResearchTreeViewportBounds(0f, 0f, 0f, 0f));
        }

        var detailMetrics = ResearchTreeUiRenderer.CalculateDetailMetrics(
            bounds,
            ResearchTreeViewNode.FromSkillTree(skillTree),
            _treeViewer.Zoom,
            DraftTreeRenderConfig);
        return ConvertToPlacementMetrics(detailMetrics);
    }

    private static ResearchTreeViewNode? BuildSkillTreeRoot(GameSession session)
    {
        return session.SkillTree.Root is null
            ? null
            : ResearchTreeViewNode.FromSkillTree(session.SkillTree);
    }

    private ResearchNodeInfo? BuildSelectedSkillTreeNodeInfo(GameSession session)
    {
        if (_selectedSkillTreeNode is not TreeInstanceNode selectedNode)
        {
            return null;
        }

        if (!session.SkillTree.Contains(selectedNode))
        {
            _selectedSkillTreeNode = null;
            return null;
        }

        return BuildNodeHoverInfo(session, selectedNode);
    }

    private ResearchNodeUnlockActionModel? BuildSelectedSkillTreeUnlockAction(GameSession session)
    {
        if (_selectedSkillTreeNode is not TreeInstanceNode selectedNode)
        {
            return null;
        }

        if (!session.SkillTree.Contains(selectedNode))
        {
            _selectedSkillTreeNode = null;
            return null;
        }

        var quote = SkillTreeUnlockSystem.GetUnlockQuote(session, selectedNode);
        return new ResearchNodeUnlockActionModel(
            quote.ResourceType,
            quote.Available,
            quote.Cost,
            quote.CanUnlock,
            selectedNode.IsUnlocked,
            quote.BlockReason);
    }

    private static ResearchTreeViewportMetrics ConvertToPlacementMetrics(ResearchTreeDetailMetrics metrics)
    {
        return new ResearchTreeViewportMetrics(
            metrics.Bounds,
            metrics.ContentBounds,
            metrics.Origin,
            metrics.EdgeLength,
            metrics.NodeRadius,
            new ResearchTreeViewportBounds(
                metrics.BaseBounds.MinX,
                metrics.BaseBounds.MaxX,
                metrics.BaseBounds.MinY,
                metrics.BaseBounds.MaxY));
    }

    private ResearchDraftDragPreview BuildDragPreview(
        Point point,
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        int branchIndex)
    {
        return BuildDragPreview(point, layout.TreeViewportBounds, session, draftSystem, branchIndex);
    }

    private ResearchDraftDragPreview BuildDragPreview(
        Point point,
        Rectangle treeViewportBounds,
        GameSession session,
        ResearchDraftSystem draftSystem,
        int branchIndex)
    {
        if (draftSystem.PendingDraft is null || branchIndex < 0 || branchIndex >= draftSystem.PendingDraft.Branches.Count)
        {
            return new ResearchDraftDragPreview(null, false, false, false, "That branch is no longer available.", ResearchDraftPlacementCollision.Empty);
        }

        var branch = draftSystem.PendingDraft.Branches[branchIndex];
        if (branch.Root is null)
        {
            return new ResearchDraftDragPreview(null, false, false, false, "That research branch is empty.", ResearchDraftPlacementCollision.Empty);
        }

        if (!treeViewportBounds.Contains(point))
        {
            return new ResearchDraftDragPreview(null, false, false, false, SelectedBranchStatus, ResearchDraftPlacementCollision.Empty);
        }

        if (TryGetAnchorLocation(point, treeViewportBounds, session.SkillTree, out var anchorNode))
        {
            var validation = ResearchDraftPlacementValidator.Validate(session.SkillTree, branch, anchorNode!);
            var canPlace = validation.CanPlace;
            return new ResearchDraftDragPreview(
                anchorNode,
                canPlace,
                validation.IsStructurallyValid,
                true,
                canPlace
                    ? "Click to graft this branch onto the tree."
                    : validation.FailureReason ?? "That branch cannot be placed there.",
                validation.Collision);
        }

        return new ResearchDraftDragPreview(null, false, false, true, "Drop the branch on the root anchor or an existing skill node.", ResearchDraftPlacementCollision.Empty);
    }

    private bool TryGetAnchorLocation(
        Point point,
        Rectangle treeBounds,
        SkillTree skillTree,
        out TreeInstanceNode? anchorNode)
    {
        anchorNode = null;
        var metrics = BuildTreeMetrics(treeBounds, skillTree);
        if (!metrics.ContentBounds.Contains(point))
        {
            return false;
        }

        if (skillTree.Root is null)
        {
            return false;
        }

        var treeLayout = BuildPlacedTreeLayout(metrics, skillTree.Root);
        var bestDistanceSquared = float.MaxValue;
        foreach (var node in treeLayout.Nodes)
        {
            var nodePoint = node.Position;
            var distanceSquared = Vector2.DistanceSquared(nodePoint, point.ToVector2());
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            anchorNode = node.SkillNode;
        }

        return bestDistanceSquared < float.MaxValue;
    }

    private bool TryGetPlacedSkillTreeNodeAtPoint(
        Point point,
        Rectangle treeBounds,
        SkillTree skillTree,
        out TreeInstanceNode? selectedNode)
    {
        selectedNode = null;
        var metrics = BuildTreeMetrics(treeBounds, skillTree);
        if (!metrics.ContentBounds.Contains(point) || skillTree.Root is null)
        {
            return false;
        }

        var treeLayout = BuildPlacedTreeLayout(metrics, skillTree.Root);
        return TryGetHoveredPlacedNode(metrics, treeLayout, point, out selectedNode, out _);
    }

    private static bool TryGetHoveredPlacedNode(
        ResearchTreeViewportMetrics metrics,
        PlacedTreeRenderLayout layout,
        Point point,
        out TreeInstanceNode? hoveredNode,
        out Vector2 center)
    {
        hoveredNode = null;
        center = Vector2.Zero;
        var hitRadius = metrics.NodeRadius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        foreach (var node in layout.Nodes)
        {
            if (!IsNodeVisible(metrics, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(node.Position, point.ToVector2());
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            hoveredNode = node.SkillNode;
            center = node.Position;
        }

        return hoveredNode is not null;
    }

    private static bool TryGetBranchCardSelection(
        Point point,
        ResearchDraftLayoutInfo layout,
        ResearchDraftOffer pendingDraft,
        out int branchIndex)
    {
        for (var index = 0; index < Math.Min(layout.BranchCardBounds.Count, pendingDraft.Branches.Count); index++)
        {
            if (!layout.BranchCardBounds[index].Contains(point) || pendingDraft.Branches[index].Count == 0)
            {
                continue;
            }

            branchIndex = index;
            return true;
        }

        branchIndex = -1;
        return false;
    }

    private static void DrawClippedConnector(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness,
        float startInset = 0f,
        float endInset = 0f)
    {
        if (!TryInsetConnector(ref start, ref end, startInset, endInset))
        {
            return;
        }

        if (!TryClipLineToBounds(metrics.ContentBounds, ref start, ref end))
        {
            return;
        }

        DrawCrispLine(gumUi, start, end, color, thickness);
    }

    private static bool IsNodeVisible(ResearchTreeViewportMetrics metrics, Vector2 center, int radius)
    {
        return center.X + radius >= metrics.ContentBounds.Left &&
               center.X - radius <= metrics.ContentBounds.Right &&
               center.Y + radius >= metrics.ContentBounds.Top &&
               center.Y - radius <= metrics.ContentBounds.Bottom;
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

        var clippedStart = new Vector2(start.X + (t0 * deltaX), start.Y + (t0 * deltaY));
        var clippedEnd = new Vector2(start.X + (t1 * deltaX), start.Y + (t1 * deltaY));
        start = clippedStart;
        end = clippedEnd;
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

    private static void DrawTreeNode(GumUiRenderer gumUi, Vector2 center, int radius, Color fill, Color border)
    {
        var borderThickness = ResearchTreeUiRenderer.CalculateDetailNodeBorderThickness(radius);
        var outerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius - borderThickness),
            (int)MathF.Round(center.Y - radius - borderThickness),
            (radius + borderThickness) * 2,
            (radius + borderThickness) * 2);
        gumUi.AddRoundedRectangle(outerBounds, border, radius + borderThickness);

        var innerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius),
            (int)MathF.Round(center.Y - radius),
            radius * 2,
            radius * 2);
        gumUi.AddRoundedRectangle(innerBounds, fill, radius);
    }

    private static void DrawCrispConnector(
        GumUiRenderer gumUi,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness,
        float startInset = 0f,
        float endInset = 0f)
    {
        if (!TryInsetConnector(ref start, ref end, startInset, endInset))
        {
            return;
        }

        DrawCrispLine(gumUi, start, end, color, thickness);
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

    private static void DrawCrispLine(
        GumUiRenderer gumUi,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness)
    {
        gumUi.AddLine(PixelSnap(start), PixelSnap(end), color, thickness);
    }

    private static void DrawClippedLine(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness)
    {
        if (!TryClipLineToBounds(metrics.ContentBounds, ref start, ref end))
        {
            return;
        }

        DrawCrispLine(gumUi, start, end, color, thickness);
    }

    private static Vector2 PixelSnap(Vector2 point)
    {
        return new Vector2(MathF.Round(point.X), MathF.Round(point.Y));
    }

    private static void DrawBranchOriginMarker(GumUiRenderer gumUi, Vector2 center, bool ghosted)
    {
        var fill = ghosted ? new Color(BranchOriginFillColor.R, BranchOriginFillColor.G, BranchOriginFillColor.B, (byte)224) : BranchOriginFillColor;
        var border = ghosted ? new Color(BranchOriginBorderColor.R, BranchOriginBorderColor.G, BranchOriginBorderColor.B, (byte)236) : BranchOriginBorderColor;
        DrawTreeNode(gumUi, center, 5, fill, border);
    }

    private PlacedTreeRenderLayout BuildPlacedTreeLayout(ResearchTreeViewportMetrics metrics, TreeInstanceNode root)
    {
        return BuildPlacedTreeLayout(metrics.Origin, metrics.EdgeLength, _treeViewer.PanOffset, root);
    }

    private static PlacedTreeRenderLayout BuildPlacedTreeLayout(Vector2 origin, float edgeLength, Vector2 panOffset, TreeInstanceNode root)
    {
        var layout = UniversalTreeLayout.Layout(BuildPlacedTreeRenderNode(root), new UniversalTreeLayoutSettings(edgeLength));
        var nodes = new List<PlacedTreeRenderNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new PlacedTreeRenderNode(
                node.Payload,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.SkillNode, node.Parent.Payload)),
                origin + panOffset + node.LocalPosition,
                node.MedialDegrees));
        }

        return new PlacedTreeRenderLayout(nodes);
    }

    internal static ProjectedTreeRenderLayout BuildProjectedPlacementLayout(
        Vector2 origin,
        float edgeLength,
        Vector2 panOffset,
        TreeInstanceNode root,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(anchorNode);

        if (branch.Root is null)
        {
            return new ProjectedTreeRenderLayout([]);
        }

        var renderRoot = BuildProjectedTreeRenderNode(root, branch.Root, anchorNode);
        var layout = UniversalTreeLayout.Layout(renderRoot, new UniversalTreeLayoutSettings(edgeLength));
        var nodes = new List<ProjectedTreeRenderNode>(layout.Nodes.Count);
        var fixedNodeId = 0;
        var branchNodeId = 0;
        foreach (var node in layout.Nodes)
        {
            var parent = node.Parent is null
                ? null
                : nodes.First(existing =>
                    ReferenceEquals(existing.SkillNode, node.Parent.Payload.Node) &&
                    existing.IsBranchNode == node.Parent.Payload.IsBranchNode);
            var isBranchNode = node.Payload.IsBranchNode;
            nodes.Add(new ProjectedTreeRenderNode(
                node.Payload.Node,
                parent,
                origin + panOffset + node.LocalPosition,
                node.MedialDegrees,
                isBranchNode,
                isBranchNode ? -1 : fixedNodeId++,
                isBranchNode ? branchNodeId++ : -1));
        }

        return new ProjectedTreeRenderLayout(nodes);
    }

    private static BranchRenderLayout BuildBranchLayout(ResearchBranch branch, float edgeLength)
    {
        var origin = new TreeRenderNode<TreeInstanceNode?>(null);
        origin.AddChild(BuildBranchRenderNode(branch.Root!));
        var layout = UniversalTreeLayout.Layout(origin, new UniversalTreeLayoutSettings(edgeLength));
        var nodes = new List<BranchRenderNode>();
        foreach (var node in layout.Nodes)
        {
            if (node.Payload is null)
            {
                continue;
            }

            nodes.Add(new BranchRenderNode(
                node.Payload,
                node.Parent?.Payload is null ? null : nodes.First(existing => ReferenceEquals(existing.BranchNode, node.Parent.Payload)),
                node.LocalPosition,
                node.MedialDegrees));
        }

        return new BranchRenderLayout(
            nodes,
            layout.Root.Children[0].LocalPosition,
            new ResearchTreeBounds(layout.MinX, layout.MaxX, layout.MinY, layout.MaxY));
    }

    private static TreeRenderNode<ProjectedTreeRenderPayload> BuildProjectedTreeRenderNode(
        TreeInstanceNode node,
        TreeInstanceNode branchRoot,
        TreeInstanceNode anchorNode)
    {
        var renderNode = new TreeRenderNode<ProjectedTreeRenderPayload>(new ProjectedTreeRenderPayload(node, IsBranchNode: false));
        foreach (var child in node.Children)
        {
            renderNode.AddChild(BuildProjectedTreeRenderNode(child, branchRoot, anchorNode));
        }

        if (ReferenceEquals(node, anchorNode))
        {
            renderNode.AddChild(BuildProjectedBranchRenderNode(branchRoot));
        }

        return renderNode;
    }

    private static TreeRenderNode<ProjectedTreeRenderPayload> BuildProjectedBranchRenderNode(TreeInstanceNode node)
    {
        var renderNode = new TreeRenderNode<ProjectedTreeRenderPayload>(new ProjectedTreeRenderPayload(node, IsBranchNode: true));
        foreach (var child in node.Children)
        {
            renderNode.AddChild(BuildProjectedBranchRenderNode(child));
        }

        return renderNode;
    }

    private static TreeRenderNode<TreeInstanceNode> BuildPlacedTreeRenderNode(TreeInstanceNode node)
    {
        var renderNode = new TreeRenderNode<TreeInstanceNode>(node);
        foreach (var child in node.Children)
        {
            renderNode.AddChild(BuildPlacedTreeRenderNode(child));
        }

        return renderNode;
    }

    private static TreeRenderNode<TreeInstanceNode?> BuildBranchRenderNode(TreeInstanceNode node)
    {
        var renderNode = new TreeRenderNode<TreeInstanceNode?>(node);
        foreach (var child in node.Children)
        {
            renderNode.AddChild(BuildBranchRenderNode(child));
        }

        return renderNode;
    }

    private static PlacedTreeRenderNode? FindLayoutNode(PlacedTreeRenderLayout layout, TreeInstanceNode node)
    {
        return layout.Nodes.FirstOrDefault(existing => ReferenceEquals(existing.SkillNode, node));
    }

    private static BranchRenderNode? FindLayoutNode(BranchRenderLayout layout, TreeInstanceNode node)
    {
        return layout.Nodes.FirstOrDefault(existing => ReferenceEquals(existing.BranchNode, node));
    }

    private static ProjectedTreeRenderNode? FindProjectedLayoutNodeBySourceSkill(
        ProjectedTreeRenderLayout layout,
        string featureTreeName,
        string skillName,
        bool isBranchNode)
    {
        foreach (var node in layout.Nodes)
        {
            if (node.IsBranchNode != isBranchNode ||
                !string.Equals(node.SkillNode.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) ||
                !string.Equals(node.SkillNode.Name, skillName, StringComparison.Ordinal))
            {
                continue;
            }

            return node;
        }

        return null;
    }

    private ResearchNodeInfo? GetBranchCardHoverInfo(
        IReadOnlyList<Rectangle> cardBounds,
        IReadOnlyList<ResearchBranch> branches,
        GameSession session)
    {
        ResearchNodeInfo? hoverInfo = null;
        for (var index = 0; index < cardBounds.Count; index++)
        {
            if (index >= branches.Count || branches[index].Count == 0)
            {
                continue;
            }

            hoverInfo = GetBranchCardHoverInfo(
                branches[index],
                session,
                new Rectangle(cardBounds[index].X + 10, cardBounds[index].Y + 48, cardBounds[index].Width - 20, cardBounds[index].Height - 58))
                ?? hoverInfo;
        }

        return hoverInfo;
    }

    private ResearchNodeInfo? GetBranchCardHoverInfo(
        ResearchBranch branch,
        GameSession session,
        Rectangle bounds)
    {
        if (branch.Root is null)
        {
            return null;
        }

        var hoveredNode = ResearchTreeUiRenderer.TryGetHoveredCardNode(
            ResearchTreeViewNode.FromResearchBranch(branch),
            bounds,
            _pointerPoint,
            ResearchTreeUiRenderer.TreeEntryCardConfig,
            out _);

        return hoveredNode is null ? null : ResearchTreeUiRenderer.BuildNodeInfo(session, hoveredNode);
    }

    private ProjectedTreeRenderNode? TryGetHoveredProjectedNode(
        ResearchTreeViewportMetrics metrics,
        ProjectedTreeRenderLayout layout,
        out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = metrics.NodeRadius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        ProjectedTreeRenderNode? hovered = null;
        foreach (var node in layout.Nodes)
        {
            var point = node.Position;
            if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(point, _pointerPoint.ToVector2());
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            hovered = node;
            center = point;
        }

        return hovered;
    }

    private TreeInstanceNode? TryGetHoveredBranchNode(
        ResearchTreeViewportMetrics metrics,
        BranchRenderLayout layout,
        Vector2 origin,
        out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = metrics.NodeRadius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        TreeInstanceNode? hovered = null;
        foreach (var node in layout.Nodes)
        {
            var point = origin + node.LocalPosition;
            if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(point, _pointerPoint.ToVector2());
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            hovered = node.BranchNode;
            center = point;
        }

        return hovered;
    }

    private void DrawProjectedPlacementHighlights(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        ResearchBranch branch,
        ProjectedTreeRenderNode hoveredNode,
        ProjectedTreeRenderLayout layout,
        double visualTimeMs)
    {
        var hoveredPosition = hoveredNode.Position;
        if (!hoveredNode.IsBranchNode && hoveredNode.SkillNode.CanUnlock())
        {
            hoveredPosition += ResearchTreeUiRenderer.CalculateAvailableNodeShakeOffset(
                $"{hoveredNode.SkillNode.SourceFeatureTreeName}:{hoveredNode.SkillNode.Name}",
                metrics.NodeRadius,
                visualTimeMs);
        }

        ResearchTreeUiRenderer.DrawHoveredNodeHalo(gumUi, hoveredPosition, metrics.NodeRadius, visualTimeMs);
        if (string.IsNullOrWhiteSpace(hoveredNode.SkillNode.SourceFeatureTreeName))
        {
            return;
        }

        for (var current = hoveredNode.SkillNode.SourceSkillNode.Parent; current is not null; current = current.Parent)
        {
            var prerequisiteNode = hoveredNode.IsBranchNode &&
                branch.ContainsSourceSkill(hoveredNode.SkillNode.SourceFeatureTreeName, current.Name)
                ? FindProjectedLayoutNodeBySourceSkill(layout, hoveredNode.SkillNode.SourceFeatureTreeName, current.Name, isBranchNode: true)
                : FindProjectedLayoutNodeBySourceSkill(layout, hoveredNode.SkillNode.SourceFeatureTreeName, current.Name, isBranchNode: false);
            if (prerequisiteNode is null)
            {
                continue;
            }

            if (!IsNodeVisible(metrics, prerequisiteNode.Position, metrics.NodeRadius))
            {
                continue;
            }

            DrawNodeOutline(gumUi, prerequisiteNode.Position, metrics.NodeRadius, new Color(255, 255, 255, 216), 4, 2);
        }
    }

    private static void DrawFloatingBranchPrerequisiteHighlights(
        GumUiRenderer gumUi,
        int radius,
        BranchRenderLayout layout,
        Vector2 origin,
        ResearchBranch branch,
        TreeInstanceNode hoveredNode,
        double visualTimeMs,
        Predicate<Vector2>? isVisible = null)
    {
        DrawFloatingBranchPrerequisiteHighlights(gumUi, branch, hoveredNode, radius, layout, origin, visualTimeMs, isVisible);
    }

    private void DrawPlacedFeatureTreePrerequisiteHighlights(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        TreeInstanceNode hoveredNode,
        ResearchBranch? branch,
        PlacedTreeRenderLayout layout)
    {
        if (string.IsNullOrWhiteSpace(hoveredNode.SourceFeatureTreeName))
        {
            return;
        }

        for (var current = hoveredNode.SourceSkillNode.Parent; current is not null; current = current.Parent)
        {
            if (branch is not null &&
                branch.ContainsSourceSkill(hoveredNode.SourceFeatureTreeName, current.Name))
            {
                continue;
            }

            var prerequisiteNode = session.SkillTree.FindBySourceSkill(hoveredNode.SourceFeatureTreeName, current.Name);
            if (prerequisiteNode is null)
            {
                continue;
            }

            var layoutNode = FindLayoutNode(layout, prerequisiteNode);
            if (layoutNode is null)
            {
                continue;
            }

            var point = layoutNode.Position;
            if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
            {
                continue;
            }

            DrawNodeOutline(gumUi, point, metrics.NodeRadius, new Color(255, 255, 255, 216), 4, 2);
        }
    }

    private static void DrawFloatingBranchPrerequisiteHighlights(
        GumUiRenderer gumUi,
        ResearchBranch branch,
        TreeInstanceNode hoveredNode,
        int radius,
        BranchRenderLayout layout,
        Vector2 origin,
        double visualTimeMs,
        Predicate<Vector2>? isVisible = null)
    {
        var hoveredLayoutNode = FindLayoutNode(layout, hoveredNode);
        if (hoveredLayoutNode is null)
        {
            return;
        }

        var hoveredPoint = origin + hoveredLayoutNode.LocalPosition;
        if (isVisible is null || isVisible(hoveredPoint))
        {
            ResearchTreeUiRenderer.DrawHoveredNodeHalo(gumUi, hoveredPoint, radius, visualTimeMs);
        }

        if (string.IsNullOrWhiteSpace(hoveredNode.SourceFeatureTreeName))
        {
            return;
        }

        for (var current = hoveredNode.SourceSkillNode.Parent; current is not null; current = current.Parent)
        {
            var prerequisiteNode = FindBranchNodeBySourceSkill(branch, hoveredNode.SourceFeatureTreeName, current.Name);
            if (prerequisiteNode is null)
            {
                continue;
            }

            var prerequisiteLayoutNode = FindLayoutNode(layout, prerequisiteNode);
            if (prerequisiteLayoutNode is null)
            {
                continue;
            }

            var point = origin + prerequisiteLayoutNode.LocalPosition;
            if (isVisible is not null && !isVisible(point))
            {
                continue;
            }

            DrawNodeOutline(gumUi, point, radius, new Color(255, 255, 255, 216), 4, 2);
        }
    }

    private static TreeInstanceNode? FindBranchNodeBySourceSkill(
        ResearchBranch branch,
        string featureTreeName,
        string skillName)
    {
        foreach (var node in branch.Nodes)
        {
            if (string.Equals(node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) &&
                string.Equals(node.Name, skillName, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
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

    internal static ResearchNodeInfo BuildNodeHoverInfo(GameSession session, TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        return ResearchNodeTextFormatter.BuildNodeInfo(session, node);
    }

    internal static Color GetSkillTreeConnectorColor(TreeInstanceNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return ResearchTreeUiRenderer.ResolveConnectorColor(
            child.IsUnlocked,
            child.CanUnlock(),
            showsProgressState: true);
    }

    internal static ResearchNodeHoverPlacement? ResolveHoverPlacement(
        bool hasPendingDraft,
        bool hasSkillTreeHover,
        bool hasBranchHover)
    {
        if (hasBranchHover || hasSkillTreeHover)
        {
            return ResearchNodeHoverPlacement.InfoPanel;
        }

        return null;
    }

    internal static string BuildNodeAffectText(GameSession session, TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);
        return ResearchNodeTextFormatter.BuildNodeAffectText(session, node);
    }

    internal static IReadOnlyList<string> GetFeatureTreePrerequisiteSkillNames(TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var prerequisites = new List<string>();
        for (var current = node.SourceSkillNode.Parent; current is not null; current = current.Parent)
        {
            prerequisites.Add(current.Name);
        }

        return prerequisites;
    }

    private static string BuildDefaultStatus(ResearchDraftSystem draftSystem)
    {
        return draftSystem.HasPendingDraft ? PendingStatus : EmptyStatus;
    }

    private static string BuildUnlockFailureStatus(SkillTreeUnlockBlockReason reason)
    {
        return reason switch
        {
            SkillTreeUnlockBlockReason.AlreadyUnlocked => "That node is already unlocked.",
            SkillTreeUnlockBlockReason.NoPathToNode => "No path to node.",
            SkillTreeUnlockBlockReason.NotEnoughResources => "Not enough resources to unlock.",
            SkillTreeUnlockBlockReason.NotInTree => "That node is not in the skill tree.",
            _ => "That node cannot be unlocked."
        };
    }

    private static string BuildDraftSubtitle(ResearchDraftOffer pendingDraft)
    {
        return pendingDraft.Source == ResearchDraftSource.InfiniteDraft
            ? "Infinite draft is active. Choose one branch and click a valid graft point on the tree."
            : $"Round {pendingDraft.SourceRoundNumber} reward. Choose one branch and click a valid graft point on the tree.";
    }

    private static string BuildBranchCardTitle(ResearchBranch branch, int branchIndex)
    {
        return string.IsNullOrWhiteSpace(branch.Name)
            ? $"Branch {branchIndex + 1}"
            : branch.Name;
    }

    private static string BuildBranchCardSubtitle(GameSession session, ResearchBranch branch)
    {
        var sourceTreeName = branch.Root?.SourceFeatureTreeName;
        if (!string.IsNullOrWhiteSpace(sourceTreeName) &&
            session.GetFeatureTree(sourceTreeName) is FeatureTree featureTree)
        {
            return $"{featureTree.DisplayName} - Tier {featureTree.Tier}";
        }

        return $"{branch.Count} nodes";
    }

    private static ResearchDraftLayoutInfo BuildLayout(Point viewport, ResearchDraftSystem draftSystem)
    {
        var branchCardCount = draftSystem.PendingDraft?.Branches.Count ?? 0;
        return ResearchDraftLayout.Build(viewport, branchCardCount);
    }

    private static Color GetNodeFillColor(GameSession session, TreeInstanceNode node)
    {
        return ResearchTreeUiRenderer.ResolveNodeFillColor(
            session,
            node.SourceFeatureTreeName,
            node.IsUnlocked,
            node.CanUnlock(),
            showsProgressState: true);
    }

    private static Color GetNodeBorderColor(GameSession session, TreeInstanceNode node)
    {
        return ResearchTreeUiRenderer.ResolveNodeBorderColor(
            session,
            node.SourceFeatureTreeName,
            node.IsUnlocked,
            node.CanUnlock(),
            showsProgressState: true);
    }

    private static Color GetBranchNodePreviewColor(GameSession session, TreeInstanceNode node)
    {
        return ResearchTreeColorResolver.GetBaseFeatureColor(session, node.SourceFeatureTreeName);
    }

    private static Color GetBranchPreviewLineColor(ResearchDraftDragPreview preview, int lineId)
    {
        if (preview.Collision.ContainsMovingLine(lineId))
        {
            return WithAlpha(BranchCollisionColor, 235);
        }

        if (!preview.IsStructurallyValid)
        {
            return WithAlpha(InvalidBranchConnectorColor, BranchPreviewBorderAlpha);
        }

        return WithAlpha(BranchConnectorColor, BranchPreviewBorderAlpha);
    }

    private static Color GetBranchPreviewNodeFillColor(
        GameSession session,
        TreeInstanceNode node,
        ResearchDraftDragPreview preview,
        int nodeId)
    {
        if (preview.Collision.ContainsMovingNode(nodeId))
        {
            return WithAlpha(BranchCollisionColor, 235);
        }

        if (!preview.IsStructurallyValid)
        {
            return WithAlpha(new Color(178, 70, 62), BranchPreviewFillAlpha);
        }

        return WithAlpha(GetBranchNodePreviewColor(session, node), BranchPreviewFillAlpha);
    }

    private static Color GetBranchPreviewNodeBorderColor(ResearchDraftDragPreview preview, int nodeId)
    {
        if (preview.Collision.ContainsMovingNode(nodeId))
        {
            return WithAlpha(BranchCollisionBorderColor, 255);
        }

        if (!preview.IsStructurallyValid)
        {
            return WithAlpha(BranchCollisionBorderColor, BranchPreviewBorderAlpha);
        }

        return WithAlpha(new Color(246, 251, 253), BranchPreviewBorderAlpha);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    private readonly record struct ResearchDraftDragPreview(
        TreeInstanceNode? AnchorNode,
        bool CanPlace,
        bool IsStructurallyValid,
        bool IsHoveringTree,
        string StatusMessage,
        ResearchDraftPlacementCollision Collision)
    {
        public static ResearchDraftDragPreview Empty => new(
            null,
            false,
            false,
            false,
            string.Empty,
            ResearchDraftPlacementCollision.Empty);
    }

    private sealed record PlacedTreeRenderNode(
        TreeInstanceNode SkillNode,
        PlacedTreeRenderNode? Parent,
        Vector2 Position,
        float MedialDegrees);

    private sealed record PlacedTreeRenderLayout(
        IReadOnlyList<PlacedTreeRenderNode> Nodes);

    private sealed record BranchRenderNode(
        TreeInstanceNode BranchNode,
        BranchRenderNode? Parent,
        Vector2 LocalPosition,
        float MedialDegrees);

    private sealed record BranchRenderLayout(
        IReadOnlyList<BranchRenderNode> Nodes,
        Vector2 RootLocalPosition,
        ResearchTreeBounds Bounds);

    internal sealed record ProjectedTreeRenderNode(
        TreeInstanceNode SkillNode,
        ProjectedTreeRenderNode? Parent,
        Vector2 Position,
        float MedialDegrees,
        bool IsBranchNode,
        int FixedNodeId,
        int BranchNodeId);

    internal sealed record ProjectedTreeRenderLayout(
        IReadOnlyList<ProjectedTreeRenderNode> Nodes);

    private readonly record struct ProjectedTreeRenderPayload(
        TreeInstanceNode Node,
        bool IsBranchNode);

    private readonly record struct ResearchNodeHoverDisplay(
        ResearchNodeInfo HoverInfo,
        ResearchNodeHoverPlacement Placement);
}
