using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using TriloGame.Game.Core.Research;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
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
    BranchPlaced
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
    private const byte BranchPreviewFillAlpha = 150;
    private const byte BranchPreviewBorderAlpha = 190;
    private static readonly Color LockedSkillTreeConnectorColor = new(246, 251, 253);
    private static readonly Color UnlockedSkillTreeConnectorColor = new(247, 221, 92);
    private static readonly Color BranchOriginFillColor = new(238, 207, 106);
    private static readonly Color BranchOriginBorderColor = new(255, 247, 222);
    private readonly ResearchTreeViewportState _treeViewport = new();
    private Point _pointerPoint;
    private int? _selectedBranchIndex;
    private string _statusMessage = EmptyStatus;
    private float _infoPanelScroll;

    public bool IsOpen { get; private set; }

    public void Reset()
    {
        _pointerPoint = Point.Zero;
        _treeViewport.Reset();
        _selectedBranchIndex = null;
        _statusMessage = EmptyStatus;
        _infoPanelScroll = 0f;
        IsOpen = false;
    }

    public void Open(ResearchDraftSystem draftSystem)
    {
        IsOpen = true;
        _treeViewport.Reset();
        _statusMessage = BuildDefaultStatus(draftSystem);
        _infoPanelScroll = 0f;
    }

    public void Close(ResearchDraftSystem draftSystem)
    {
        _treeViewport.Reset();
        _selectedBranchIndex = null;
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

        _treeViewport.ZoomAt(point, delta, layout.TreeViewportBounds, session.SkillTree);
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

        if (layout.TreeViewportBounds.Contains(point))
        {
            _treeViewport.BeginPan(point);
        }

        return true;
    }

    public void HandlePointerDrag(Point point, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen)
        {
            return;
        }

        _treeViewport.DragPan(point);
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
        var wasPanning = _treeViewport.EndPan(layout.TreeViewportBounds, session.SkillTree);

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

        if (draftSystem.PendingDraft is null)
        {
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (wasPanning)
        {
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (TryGetBranchCardSelection(point, layout, draftSystem.PendingDraft, out var selectedBranchIndex))
        {
            _selectedBranchIndex = selectedBranchIndex;
            _statusMessage = SelectedBranchStatus;
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (_selectedBranchIndex is not int branchIndex)
        {
            return ResearchDraftInteractionOutcome.Consumed;
        }

        return TryPlaceSelectedBranch(point, layout, session, draftSystem, branchIndex);
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
        Texture2D? treeBackgroundTexture = null)
    {
        var layout = BuildLayout(viewport, draftSystem);
        DrawButton(layout.ButtonBounds, draftSystem.HasPendingDraft, gumUi);

        if (!IsOpen)
        {
            return;
        }

        gumUi.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(5, 11, 17, 164));
        DrawPanel(layout, session, draftSystem, gumUi, treeBackgroundTexture);
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
        Texture2D? treeBackgroundTexture)
    {
        ResearchTreeMenuRenderer.Draw(
            gumUi,
            session,
            BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture),
            _pointerPoint);
    }

    private ResearchTreeMenuModel BuildDraftMenuModel(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        Texture2D? treeBackgroundTexture)
    {
        var pendingDraft = draftSystem.PendingDraft;
        var hoverDisplay = GetHoveredNodeDisplay(layout, session, draftSystem);
        var treeMetrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree);

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
                Root: null,
                _treeViewport.PanOffset,
                _treeViewport.Zoom,
                treeBackgroundTexture,
                DrawCustomContent: ui =>
                {
                    DrawTiledTreeBackground(ui, layout.TreeViewportBounds, treeBackgroundTexture, 0, _treeViewport.PanOffset, _treeViewport.Zoom, treeMetrics.Origin);
                    DrawSkillTreePanel(layout, session, draftSystem, ui);
                }),
            new ResearchTreeInfoPanelModel(
                hoverDisplay is ResearchNodeHoverDisplay display ? display.HoverInfo : null,
                "Info",
                pendingDraft is not null
                    ? "Hover a branch or tree node for details."
                    : "Hover a tree node for details.",
                _infoPanelScroll),
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

    private ResearchNodeInfo? DrawSkillTreePanel(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        GumUiRenderer gumUi)
    {
        var preview = _selectedBranchIndex is int selectedBranchIndex
            ? BuildDragPreview(_pointerPoint, layout, session, draftSystem, selectedBranchIndex)
            : ResearchDraftDragPreview.Empty;
        if (_selectedBranchIndex is not null && !string.IsNullOrWhiteSpace(preview.StatusMessage))
        {
            _statusMessage = preview.StatusMessage;
        }

        var metrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree);
        ResearchNodeInfo? hoverInfo = null;
        if (_selectedBranchIndex is int activeBranchIndex &&
            draftSystem.PendingDraft is not null &&
            activeBranchIndex < draftSystem.PendingDraft.Branches.Count)
        {
            var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
            if (preview.AnchorNode is not null)
            {
                hoverInfo = DrawProjectedPlacementPreview(metrics, session, branch, preview, gumUi);
            }
            else
            {
                hoverInfo = DrawPlacedTree(session, metrics, gumUi);
                hoverInfo = DrawCursorBoundBranchPreview(metrics, session, branch, gumUi) ?? hoverInfo;
            }
        }
        else
        {
            hoverInfo = DrawPlacedTree(session, metrics, gumUi);
        }

        return hoverInfo;
    }

    private ResearchNodeHoverDisplay? GetHoveredNodeDisplay(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem)
    {
        var preview = _selectedBranchIndex is int selectedBranchIndex
            ? BuildDragPreview(_pointerPoint, layout, session, draftSystem, selectedBranchIndex)
            : ResearchDraftDragPreview.Empty;
        var metrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree);

        var skillTreeHoverInfo = GetPlacedTreeHoverInfo(session, metrics);
        ResearchNodeInfo? branchHoverInfo = null;
        if (_selectedBranchIndex is int activeBranchIndex &&
            draftSystem.PendingDraft is not null &&
            activeBranchIndex < draftSystem.PendingDraft.Branches.Count)
        {
            var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
            if (preview.AnchorNode is TreeInstanceNode activeAnchor)
            {
                branchHoverInfo = GetProjectedPlacementHoverInfo(metrics, session, branch, activeAnchor) ?? branchHoverInfo;
            }
            else
            {
                branchHoverInfo = GetCursorBoundBranchHoverInfo(metrics, session, branch) ?? branchHoverInfo;
            }
        }

        if (draftSystem.PendingDraft is not null)
        {
            branchHoverInfo = GetBranchCardHoverInfo(layout.BranchCardBounds, draftSystem.PendingDraft.Branches, session) ?? branchHoverInfo;
        }

        var placement = ResolveHoverPlacement(
            draftSystem.PendingDraft is not null,
            skillTreeHoverInfo is not null,
            branchHoverInfo is not null);
        if (placement is not ResearchNodeHoverPlacement resolvedPlacement)
        {
            return null;
        }

        return branchHoverInfo is not null
            ? new ResearchNodeHoverDisplay(branchHoverInfo.Value, resolvedPlacement)
            : new ResearchNodeHoverDisplay(skillTreeHoverInfo!.Value, resolvedPlacement);
    }

    private ResearchNodeInfo? DrawPlacedTree(
        GameSession session,
        ResearchTreeViewportMetrics metrics,
        GumUiRenderer gumUi,
        ResearchTreeCollisionResult? collision = null)
    {
        if (session.SkillTree.Root is null)
        {
            return null;
        }

        var treeLayout = BuildPlacedTreeLayout(metrics, session.SkillTree.Root);
        var hoveredNode = TryGetHoveredPlacedNode(metrics, treeLayout, out _);
        for (var nodeIndex = 0; nodeIndex < treeLayout.Nodes.Count; nodeIndex++)
        {
            var node = treeLayout.Nodes[nodeIndex];
            if (node.Parent is null)
            {
                continue;
            }

            DrawClippedConnector(
                gumUi,
                metrics,
                node.Parent.Position,
                node.Position,
                collision?.ContainsFixedLine(nodeIndex) == true
                    ? WithAlpha(BranchCollisionColor, 240)
                    : GetSkillTreeConnectorColor(node.SkillNode),
                3,
                metrics.NodeRadius + 2f,
                metrics.NodeRadius + 2f);
        }

        for (var nodeIndex = 0; nodeIndex < treeLayout.Nodes.Count; nodeIndex++)
        {
            var node = treeLayout.Nodes[nodeIndex];
            if (!IsNodeVisible(metrics, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            var hasCollision = collision?.ContainsFixedNode(nodeIndex) == true;
            DrawTreeNode(
                gumUi,
                node.Position,
                metrics.NodeRadius,
                hasCollision ? WithAlpha(BranchCollisionColor, 235) : GetNodeFillColor(session, node.SkillNode),
                hasCollision ? WithAlpha(BranchCollisionBorderColor, 255) : GetNodeBorderColor(session, node.SkillNode));
        }

        if (hoveredNode is TreeInstanceNode hovered)
        {
            DrawPlacedPrerequisiteHighlights(gumUi, metrics, session, hovered, treeLayout);
            return BuildNodeHoverInfo(session, hovered);
        }

        return null;
    }

    private ResearchNodeInfo? DrawProjectedPlacementPreview(
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        ResearchDraftDragPreview preview,
        GumUiRenderer gumUi)
    {
        if (session.SkillTree.Root is null || branch.Root is null || preview.AnchorNode is null)
        {
            return DrawPlacedTree(session, metrics, gumUi);
        }

        var layout = BuildProjectedPlacementLayout(
            metrics.Origin,
            metrics.EdgeLength,
            _treeViewport.PanOffset,
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
            var lineColor = isMovingLine
                ? GetBranchPreviewLineColor(preview, node.BranchNodeId)
                : preview.Collision.ContainsFixedLine(node.FixedNodeId)
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
                3,
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
            DrawTreeNode(
                gumUi,
                node.Position,
                metrics.NodeRadius,
                hasCollision ? WithAlpha(BranchCollisionColor, 235) : GetNodeFillColor(session, node.SkillNode),
                hasCollision ? WithAlpha(BranchCollisionBorderColor, 255) : GetNodeBorderColor(session, node.SkillNode));
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

        DrawProjectedPlacementHighlights(gumUi, metrics, branch, hoveredNode, layout);
        return BuildNodeHoverInfo(session, hoveredNode.SkillNode);
    }

    private ResearchNodeInfo? DrawCursorBoundBranchPreview(
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        GumUiRenderer gumUi)
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
                DrawClippedConnector(gumUi, metrics, origin, point, lineColor, 3, 7f, metrics.NodeRadius + 2f);
                continue;
            }

            DrawClippedConnector(gumUi, metrics, origin + node.Parent.LocalPosition, point, lineColor, 3, metrics.NodeRadius + 2f, metrics.NodeRadius + 2f);
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
                hoveredBranchNode);
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
        return _treeViewport.BuildMetrics(bounds, skillTree);
    }

    private ResearchDraftDragPreview BuildDragPreview(
        Point point,
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        int branchIndex)
    {
        if (draftSystem.PendingDraft is null || branchIndex < 0 || branchIndex >= draftSystem.PendingDraft.Branches.Count)
        {
            return new ResearchDraftDragPreview(null, false, false, false, "That branch is no longer available.", ResearchTreeCollisionResult.Empty);
        }

        var branch = draftSystem.PendingDraft.Branches[branchIndex];
        if (branch.Root is null)
        {
            return new ResearchDraftDragPreview(null, false, false, false, "That research branch is empty.", ResearchTreeCollisionResult.Empty);
        }

        if (!layout.TreeViewportBounds.Contains(point))
        {
            return new ResearchDraftDragPreview(null, false, false, false, SelectedBranchStatus, ResearchTreeCollisionResult.Empty);
        }

        if (TryGetAnchorLocation(point, layout.TreeViewportBounds, session.SkillTree, out var anchorNode))
        {
            var structurallyValid = session.SkillTree.CanPlaceResearchBranch(branch, anchorNode!, out var failureReason);
            var collision = BuildPlacementCollision(layout.TreeViewportBounds, session.SkillTree, branch, anchorNode!);
            var canPlace = structurallyValid && !collision.HasCollision;
            return new ResearchDraftDragPreview(
                anchorNode,
                canPlace,
                structurallyValid,
                true,
                canPlace
                    ? "Click to graft this branch onto the tree."
                    : collision.HasCollision
                        ? "That branch collides with the existing tree."
                        : failureReason ?? "That branch cannot be placed there.",
                collision);
        }

        return new ResearchDraftDragPreview(null, false, false, true, "Drop the branch on the root anchor or an existing skill node.", ResearchTreeCollisionResult.Empty);
    }

    private ResearchTreeCollisionResult BuildPlacementCollision(
        Rectangle treeBounds,
        SkillTree skillTree,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        if (skillTree.Root is null || branch.Root is null)
        {
            return ResearchTreeCollisionResult.Empty;
        }

        var metrics = BuildTreeMetrics(treeBounds, skillTree);
        var projectedLayout = BuildProjectedPlacementLayout(
            metrics.Origin,
            metrics.EdgeLength,
            _treeViewport.PanOffset,
            skillTree.Root,
            branch,
            anchorNode);
        return DetectPlacementCollisions(projectedLayout, metrics.NodeRadius);
    }

    private static ResearchTreeCollisionResult DetectPlacementCollisions(
        ProjectedTreeRenderLayout projectedLayout,
        int nodeRadius)
    {
        var hitboxes = new List<ResearchTreeHitbox>(projectedLayout.Nodes.Count * 2);

        for (var nodeIndex = 0; nodeIndex < projectedLayout.Nodes.Count; nodeIndex++)
        {
            var node = projectedLayout.Nodes[nodeIndex];
            hitboxes.Add(ResearchTreeHitbox.Node(
                GetProjectedNodeHitboxId(node),
                GetProjectedNodeHitboxOwner(node),
                node.Position,
                nodeRadius));

            if (node.Parent is null)
            {
                continue;
            }

            var isMovingLine = node.IsBranchNode || node.Parent.IsBranchNode;
            var startInset = isMovingLine && !node.Parent.IsBranchNode
                ? nodeRadius + 7f
                : nodeRadius + 2f;
            var start = node.Parent.Position;
            var end = node.Position;
            if (!TryInsetConnector(ref start, ref end, startInset, nodeRadius + 2f))
            {
                continue;
            }

            hitboxes.Add(ResearchTreeHitbox.Connector(
                isMovingLine ? node.BranchNodeId : node.FixedNodeId,
                isMovingLine ? ResearchTreeHitboxOwner.Moving : ResearchTreeHitboxOwner.Fixed,
                start,
                end,
                thickness: 3,
                GetProjectedNodeEndpoint(node.Parent),
                GetProjectedNodeEndpoint(node)));
        }

        return ResearchTreeCollisionDetector.DetectHitboxes(
            hitboxes,
            includeFixedFixedPairs: true,
            includeMovingMovingPairs: true,
            padding: 2f);
    }

    private static int GetProjectedNodeHitboxId(ProjectedTreeRenderNode node)
    {
        return node.IsBranchNode ? node.BranchNodeId : node.FixedNodeId;
    }

    private static ResearchTreeHitboxOwner GetProjectedNodeHitboxOwner(ProjectedTreeRenderNode node)
    {
        return node.IsBranchNode ? ResearchTreeHitboxOwner.Moving : ResearchTreeHitboxOwner.Fixed;
    }

    private static ResearchTreeHitboxEndpoint GetProjectedNodeEndpoint(ProjectedTreeRenderNode node)
    {
        return new ResearchTreeHitboxEndpoint(GetProjectedNodeHitboxOwner(node), GetProjectedNodeHitboxId(node));
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
        var outerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius - 2),
            (int)MathF.Round(center.Y - radius - 2),
            (radius + 2) * 2,
            (radius + 2) * 2);
        gumUi.AddRoundedRectangle(outerBounds, border, radius + 2);

        var innerBounds = new Rectangle(
            (int)MathF.Round(center.X - radius),
            (int)MathF.Round(center.Y - radius),
            radius * 2,
            radius * 2);
        gumUi.AddRoundedRectangle(innerBounds, fill, radius);
    }

    private static void DrawTiledTreeBackground(
        GumUiRenderer gumUi,
        Rectangle bounds,
        Texture2D? texture,
        int cornerRadius,
        Vector2 panOffset = default,
        float zoom = 1f,
        Vector2? surfaceOrigin = null)
    {
        if (texture is null || bounds.Width <= 0 || bounds.Height <= 0 || texture.Width <= 0 || texture.Height <= 0)
        {
            return;
        }

        var clipLayer = gumUi.AddClippingContainer(bounds);
        var tileSize = CalculateTreeBackgroundTileSize(texture, zoom);
        var columns = Math.Max(1, (int)MathF.Ceiling(bounds.Width / (float)tileSize.X) + 2);
        var rows = Math.Max(1, (int)MathF.Ceiling(bounds.Height / (float)tileSize.Y) + 2);
        var anchoredOrigin = (surfaceOrigin ?? bounds.Location.ToVector2()) + panOffset;
        var startX = CalculateTreeBackgroundStartCoordinate(bounds.Left, anchoredOrigin.X, tileSize.X);
        var startY = CalculateTreeBackgroundStartCoordinate(bounds.Top, anchoredOrigin.Y, tileSize.Y);

        if (cornerRadius <= 0)
        {
            DrawFullBackgroundTiles(gumUi, clipLayer, bounds, texture, tileSize, columns, rows, startX, startY);
            return;
        }

        foreach (var maskBand in EnumerateRoundedClipBands(bounds, cornerRadius))
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var tileBounds = new Rectangle(
                        startX + (column * tileSize.X),
                        startY + (row * tileSize.Y),
                        tileSize.X,
                        tileSize.Y);
                    var visibleBounds = Rectangle.Intersect(maskBand, tileBounds);
                    if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
                    {
                        continue;
                    }

                    gumUi.AddSprite(
                        clipLayer,
                        new Rectangle(
                            visibleBounds.X - bounds.X,
                            visibleBounds.Y - bounds.Y,
                            visibleBounds.Width,
                            visibleBounds.Height),
                        texture,
                        CalculateTreeBackgroundSourceRectangle(texture, tileBounds, visibleBounds),
                        Color.White);
                }
            }
        }
    }

    private static void DrawFullBackgroundTiles(
        GumUiRenderer gumUi,
        ContainerRuntime clipLayer,
        Rectangle bounds,
        Texture2D texture,
        Point tileSize,
        int columns,
        int rows,
        int startX,
        int startY)
    {
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

    private static IEnumerable<Rectangle> EnumerateRoundedClipBands(Rectangle bounds, int cornerRadius)
    {
        var radius = Math.Clamp(cornerRadius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        if (radius <= 0)
        {
            yield return bounds;
            yield break;
        }

        var bandStart = bounds.Y;
        var previousInset = CalculateRoundedClipInset(0, bounds.Height, radius);
        for (var row = 1; row < bounds.Height; row++)
        {
            var inset = CalculateRoundedClipInset(row, bounds.Height, radius);
            if (inset == previousInset)
            {
                continue;
            }

            var band = BuildRoundedClipBand(bounds, bandStart, row, previousInset);
            if (band.Width > 0 && band.Height > 0)
            {
                yield return band;
            }

            bandStart = bounds.Y + row;
            previousInset = inset;
        }

        var finalBand = BuildRoundedClipBand(bounds, bandStart, bounds.Height, previousInset);
        if (finalBand.Width > 0 && finalBand.Height > 0)
        {
            yield return finalBand;
        }
    }

    private static Rectangle BuildRoundedClipBand(Rectangle bounds, int bandStartY, int endRow, int inset)
    {
        return new Rectangle(
            bounds.X + inset,
            bandStartY,
            Math.Max(0, bounds.Width - (inset * 2)),
            bounds.Y + endRow - bandStartY);
    }

    private static int CalculateRoundedClipInset(int row, int height, int radius)
    {
        var distanceFromTop = row + 0.5f;
        var distanceFromBottom = height - row - 0.5f;
        var distanceIntoCorner = MathF.Min(distanceFromTop, distanceFromBottom);
        if (distanceIntoCorner >= radius)
        {
            return 0;
        }

        var yFromCornerCenter = radius - distanceIntoCorner;
        var xFromCornerCenter = MathF.Sqrt(MathF.Max(0f, (radius * radius) - (yFromCornerCenter * yFromCornerCenter)));
        return (int)MathF.Ceiling(radius - xFromCornerCenter);
    }

    private static Point CalculateTreeBackgroundTileSize(Texture2D texture)
    {
        return CalculateTreeBackgroundTileSize(texture, 1f);
    }

    private static Point CalculateTreeBackgroundTileSize(Texture2D texture, float zoom)
    {
        var scale = ResearchTreeViewportState.ClampZoom(zoom);
        return new Point(
            Math.Max(1, (int)MathF.Round(texture.Width * scale)),
            Math.Max(1, (int)MathF.Round(texture.Height * scale)));
    }

    internal static int CalculateTreeBackgroundStartCoordinate(int viewportMinimum, float surfaceOrigin, int tileLength)
    {
        if (tileLength <= 0)
        {
            return viewportMinimum;
        }

        var tileOffset = MathF.Floor((viewportMinimum - surfaceOrigin) / tileLength) * tileLength;
        return (int)MathF.Round(surfaceOrigin + tileOffset);
    }

    private static Rectangle CalculateTreeBackgroundSourceRectangle(Texture2D texture, Rectangle tileBounds, Rectangle visibleBounds)
    {
        var sourceLeft = MapTileCoordinateToSource(visibleBounds.Left - tileBounds.Left, tileBounds.Width, texture.Width, roundUp: false);
        var sourceTop = MapTileCoordinateToSource(visibleBounds.Top - tileBounds.Top, tileBounds.Height, texture.Height, roundUp: false);
        var sourceRight = MapTileCoordinateToSource(visibleBounds.Right - tileBounds.Left, tileBounds.Width, texture.Width, roundUp: true);
        var sourceBottom = MapTileCoordinateToSource(visibleBounds.Bottom - tileBounds.Top, tileBounds.Height, texture.Height, roundUp: true);
        sourceLeft = Math.Clamp(sourceLeft, 0, texture.Width - 1);
        sourceTop = Math.Clamp(sourceTop, 0, texture.Height - 1);
        sourceRight = Math.Clamp(sourceRight, sourceLeft + 1, texture.Width);
        sourceBottom = Math.Clamp(sourceBottom, sourceTop + 1, texture.Height);

        return new Rectangle(sourceLeft, sourceTop, sourceRight - sourceLeft, sourceBottom - sourceTop);
    }

    private static int MapTileCoordinateToSource(int tileCoordinate, int tileLength, int textureLength, bool roundUp)
    {
        if (tileLength <= 0)
        {
            return 0;
        }

        var sourceCoordinate = tileCoordinate * (textureLength / (float)tileLength);
        return roundUp
            ? (int)MathF.Ceiling(sourceCoordinate)
            : (int)MathF.Floor(sourceCoordinate);
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
        return BuildPlacedTreeLayout(metrics.Origin, metrics.EdgeLength, _treeViewport.PanOffset, root);
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

    private static PlacedTreeRenderNode? FindLayoutNodeBySkillNode(PlacedTreeRenderLayout layout, TreeInstanceNode node)
    {
        return layout.Nodes.FirstOrDefault(existing => ReferenceEquals(existing.SkillNode, node));
    }

    private TreeInstanceNode? TryGetHoveredPlacedNode(ResearchTreeViewportMetrics metrics, PlacedTreeRenderLayout layout, out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = metrics.NodeRadius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        TreeInstanceNode? hovered = null;
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
            hovered = node.SkillNode;
            center = point;
        }

        return hovered;
    }

    private ResearchNodeInfo? GetPlacedTreeHoverInfo(GameSession session, ResearchTreeViewportMetrics metrics)
    {
        if (session.SkillTree.Root is null)
        {
            return null;
        }

        var hoveredNode = TryGetHoveredPlacedNode(metrics, BuildPlacedTreeLayout(metrics, session.SkillTree.Root), out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
    }

    private ResearchNodeInfo? GetProjectedPlacementHoverInfo(
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        if (session.SkillTree.Root is null || branch.Root is null)
        {
            return null;
        }

        var projectedLayout = BuildProjectedPlacementLayout(
            metrics.Origin,
            metrics.EdgeLength,
            _treeViewport.PanOffset,
            session.SkillTree.Root,
            branch,
            anchorNode);
        var hoveredNode = TryGetHoveredProjectedNode(metrics, projectedLayout, out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode.SkillNode);
    }

    private ResearchNodeInfo? GetCursorBoundBranchHoverInfo(
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        ResearchBranch branch)
    {
        var hoveredNode = TryGetHoveredBranchNode(metrics, BuildBranchLayout(branch, metrics.EdgeLength), _pointerPoint.ToVector2(), out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
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

    private void DrawPlacedPrerequisiteHighlights(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        GameSession session,
        TreeInstanceNode hoveredNode,
        PlacedTreeRenderLayout layout)
    {
        var hoveredLayoutNode = FindLayoutNode(layout, hoveredNode);
        if (hoveredLayoutNode is not null)
        {
            DrawNodeOutline(gumUi, hoveredLayoutNode.Position, metrics.NodeRadius, new Color(255, 255, 255, 240), 6, 2);
        }

        DrawPlacedFeatureTreePrerequisiteHighlights(gumUi, metrics, session, hoveredNode, branch: null, layout);
    }

    private void DrawProjectedPlacementHighlights(
        GumUiRenderer gumUi,
        ResearchTreeViewportMetrics metrics,
        ResearchBranch branch,
        ProjectedTreeRenderNode hoveredNode,
        ProjectedTreeRenderLayout layout)
    {
        DrawNodeOutline(gumUi, hoveredNode.Position, metrics.NodeRadius, new Color(255, 255, 255, 240), 6, 2);
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
        Predicate<Vector2>? isVisible = null)
    {
        DrawFloatingBranchPrerequisiteHighlights(gumUi, branch, hoveredNode, radius, layout, origin, isVisible);
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
            DrawNodeOutline(gumUi, hoveredPoint, radius, new Color(255, 255, 255, 240), 6, 2);
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
        return child.IsUnlocked ? UnlockedSkillTreeConnectorColor : LockedSkillTreeConnectorColor;
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

    private static Rectangle Inset(Rectangle bounds, int inset)
    {
        return new Rectangle(
            bounds.X + inset,
            bounds.Y + inset,
            Math.Max(0, bounds.Width - (inset * 2)),
            Math.Max(0, bounds.Height - (inset * 2)));
    }

    private static string BuildDefaultStatus(ResearchDraftSystem draftSystem)
    {
        return draftSystem.HasPendingDraft ? PendingStatus : EmptyStatus;
    }

    private static string BuildDraftSubtitle(ResearchDraftOffer pendingDraft)
    {
        return pendingDraft.Source == ResearchDraftSource.InfiniteDraft
            ? "Infinite draft is active. Choose one branch and click a valid graft point on the tree."
            : $"Round {pendingDraft.SourceRoundNumber} reward. Choose one branch and click a valid graft point on the tree.";
    }

    private static string BuildBranchCardTitle(ResearchBranch branch, int branchIndex)
    {
        var sourceTreeName = branch.Root?.SourceFeatureTreeName;
        return string.IsNullOrWhiteSpace(sourceTreeName)
            ? $"Branch {branchIndex + 1}"
            : sourceTreeName;
    }

    private static string BuildBranchCardSubtitle(GameSession session, ResearchBranch branch)
    {
        var sourceTreeName = branch.Root?.SourceFeatureTreeName;
        if (!string.IsNullOrWhiteSpace(sourceTreeName) &&
            session.GetFeatureTree(sourceTreeName) is FeatureTree featureTree)
        {
            return $"Tier {featureTree.Tier}";
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
        if (node.IsRoot && string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
        {
            return new Color(18, 22, 26);
        }

        var baseColor = ResearchTreeColorResolver.GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return node.IsUnlocked
            ? baseColor
            : Color.Lerp(new Color(32, 38, 43), baseColor, 0.8f);
    }

    private static Color GetNodeBorderColor(GameSession session, TreeInstanceNode node)
    {
        if (node.IsRoot && string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
        {
            return new Color(230, 238, 244);
        }

        var fill = ResearchTreeColorResolver.GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return Color.Lerp(fill, Color.White, 0.38f);
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
        ResearchTreeCollisionResult Collision)
    {
        public static ResearchDraftDragPreview Empty => new(
            null,
            false,
            false,
            false,
            string.Empty,
            ResearchTreeCollisionResult.Empty);
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
