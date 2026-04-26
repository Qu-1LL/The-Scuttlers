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
    private static readonly Color BranchConnectorGhostColor = new(255, 255, 255, 232);
    private static readonly Color InvalidBranchConnectorColor = new(242, 126, 119);
    private static readonly Color LockedSkillTreeConnectorColor = new(246, 251, 253);
    private static readonly Color UnlockedSkillTreeConnectorColor = new(247, 221, 92);
    private static readonly Color BranchOriginFillColor = new(238, 207, 106);
    private static readonly Color BranchOriginBorderColor = new(255, 247, 222);
    private const float TreeDragThresholdPixels = 10f;
    private const float MinimumTreeEdgeLength = 80f;
    private const float MaximumTreeEdgeLength = 108f;
    private const float MinimumTreeZoom = 0.55f;
    private const float MaximumTreeZoom = 2.25f;

    private Point _pointerPoint;
    private Vector2 _treePanOffset;
    private Point _treePanStartPointer;
    private Vector2 _treePanStartOffset;
    private float _treeZoom = 1f;
    private bool _treePanCandidate;
    private bool _treePanning;
    private int? _selectedBranchIndex;
    private string _statusMessage = EmptyStatus;

    public bool IsOpen { get; private set; }

    public void Reset()
    {
        _pointerPoint = Point.Zero;
        _treePanOffset = Vector2.Zero;
        _treePanStartPointer = Point.Zero;
        _treePanStartOffset = Vector2.Zero;
        _treeZoom = 1f;
        _treePanCandidate = false;
        _treePanning = false;
        _selectedBranchIndex = null;
        _statusMessage = EmptyStatus;
        IsOpen = false;
    }

    public void Open(ResearchDraftSystem draftSystem)
    {
        IsOpen = true;
        _treePanOffset = Vector2.Zero;
        _treeZoom = 1f;
        _treePanCandidate = false;
        _treePanning = false;
        _statusMessage = BuildDefaultStatus(draftSystem);
    }

    public void Close(ResearchDraftSystem draftSystem)
    {
        _treePanOffset = Vector2.Zero;
        _treeZoom = 1f;
        _treePanCandidate = false;
        _treePanning = false;
        _selectedBranchIndex = null;
        _statusMessage = BuildDefaultStatus(draftSystem);
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

        if (!layout.TreeViewportBounds.Contains(point))
        {
            return true;
        }

        var previousZoom = _treeZoom;
        _treeZoom = Math.Clamp(_treeZoom + (-delta * 0.0015f), MinimumTreeZoom, MaximumTreeZoom);
        if (MathF.Abs(_treeZoom - previousZoom) <= float.Epsilon)
        {
            return true;
        }

        var metricsBefore = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree, previousZoom);
        var pointToOrigin = point.ToVector2() - metricsBefore.Origin - _treePanOffset;
        if (previousZoom > float.Epsilon)
        {
            _treePanOffset += pointToOrigin - (pointToOrigin * (_treeZoom / previousZoom));
        }

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
            _treePanCandidate = true;
            _treePanning = false;
            _treePanStartPointer = point;
            _treePanStartOffset = _treePanOffset;
        }

        return true;
    }

    public void HandlePointerDrag(Point point, Point viewport, GameSession session, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        if (!IsOpen || !_treePanCandidate)
        {
            return;
        }

        var layout = BuildLayout(viewport, draftSystem);
        var dragDelta = point - _treePanStartPointer;
        if (!_treePanning && dragDelta.ToVector2().Length() >= TreeDragThresholdPixels)
        {
            _treePanning = true;
        }

        if (!_treePanning)
        {
            return;
        }

        _treePanOffset = _treePanStartOffset + dragDelta.ToVector2();
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

        var wasPanning = _treePanning;
        _treePanCandidate = false;
        _treePanning = false;

        var layout = BuildLayout(viewport, draftSystem);
        if (wasPanning)
        {
            SnapTreeToContentBoundsIfNeeded(layout.TreeViewportBounds, session.SkillTree);
        }

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
        AddCenteredText(
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
        var pendingDraft = draftSystem.PendingDraft;
        var hoverDisplay = GetHoveredNodeDisplay(layout, session, draftSystem);

        gumUi.AddRoundedFrame(layout.PanelBounds, new Color(9, 18, 27, 248), new Color(83, 125, 145), 3, 20);
        gumUi.AddRoundedFrame(
            layout.CloseButtonBounds,
            layout.CloseButtonBounds.Contains(_pointerPoint) ? new Color(29, 55, 72) : new Color(20, 42, 58),
            layout.CloseButtonBounds.Contains(_pointerPoint) ? new Color(183, 223, 237) : new Color(114, 154, 172),
            2,
            12);
        AddCenteredText(gumUi, layout.CloseButtonBounds, "X", Color.White, GumTextStyle.Small);

        AddText(gumUi, layout.TitleBounds, "Skill Tree Research", Color.White, GumTextStyle.Ui);
        AddText(
            gumUi,
            layout.SubtitleBounds,
            pendingDraft is null
                ? "Review the colony's current run-specific skill tree."
                : BuildDraftSubtitle(pendingDraft),
            new Color(177, 203, 214),
            GumTextStyle.Small);

        if (pendingDraft is not null)
        {
            gumUi.AddRoundedFrame(layout.DraftAreaBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
            AddText(
                gumUi,
                layout.DraftHeaderBounds,
                "Draftable Branches",
                new Color(204, 228, 238),
                GumTextStyle.Small);
            DrawBranchCards(layout.BranchCardBounds, pendingDraft.Branches, session, gumUi, treeBackgroundTexture);
        }

        gumUi.AddRoundedFrame(layout.TreeBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        var treeMetrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree);
        DrawTiledTreeBackground(gumUi, layout.TreeViewportBounds, treeBackgroundTexture, 0, _treePanOffset, _treeZoom, treeMetrics.Origin);
        AddText(
            gumUi,
            layout.TreeHeaderBounds,
            "Global Skill Tree",
            new Color(204, 228, 238),
            GumTextStyle.Small);
        DrawSkillTreePanel(layout, session, draftSystem, gumUi);

        DrawInfoPanel(layout, hoverDisplay, pendingDraft is not null, gumUi);

        AddText(
            gumUi,
            layout.FooterBounds,
            _statusMessage,
            new Color(223, 233, 239),
            GumTextStyle.Compact);
    }

    private ResearchNodeHoverInfo? DrawSkillTreePanel(
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
        var hoverInfo = DrawPlacedTree(session, metrics, gumUi);

        if (_selectedBranchIndex is int activeBranchIndex &&
            draftSystem.PendingDraft is not null &&
            activeBranchIndex < draftSystem.PendingDraft.Branches.Count)
        {
            var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
            if (preview.AnchorNode is not null)
            {
                hoverInfo = DrawBranchPreview(metrics, session, branch, preview, gumUi) ?? hoverInfo;
            }
            else
            {
                hoverInfo = DrawCursorBoundBranchPreview(metrics, session, branch, gumUi) ?? hoverInfo;
            }
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
        ResearchNodeHoverInfo? branchHoverInfo = null;
        if (_selectedBranchIndex is int activeBranchIndex &&
            draftSystem.PendingDraft is not null &&
            activeBranchIndex < draftSystem.PendingDraft.Branches.Count)
        {
            var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
            if (preview.AnchorNode is TreeInstanceNode activeAnchor)
            {
                branchHoverInfo = GetAnchoredBranchHoverInfo(metrics, session, branch, activeAnchor) ?? branchHoverInfo;
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

    private void DrawInfoPanel(
        ResearchDraftLayoutInfo layout,
        ResearchNodeHoverDisplay? hoverDisplay,
        bool hasPendingDraft,
        GumUiRenderer gumUi)
    {
        if (hoverDisplay is ResearchNodeHoverDisplay dockedHoverDisplay)
        {
            DrawNodeInfoPanel(layout.InfoPanelBounds, dockedHoverDisplay.HoverInfo, gumUi, dockedHoverDisplay.Placement);
            return;
        }

        gumUi.AddRoundedFrame(layout.InfoPanelBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        AddText(
            gumUi,
            new Rectangle(layout.InfoPanelBounds.X + 14, layout.InfoPanelBounds.Y + 12, layout.InfoPanelBounds.Width - 28, 18),
            "Info",
            new Color(204, 228, 238),
            GumTextStyle.Compact);
        AddCenteredText(
            gumUi,
            new Rectangle(layout.InfoPanelBounds.X + 18, layout.InfoPanelBounds.Y + 46, layout.InfoPanelBounds.Width - 36, layout.InfoPanelBounds.Height - 64),
            hasPendingDraft
                ? "Hover a branch or tree node for details."
                : "Hover a tree node for details.",
            new Color(177, 203, 214),
            GumTextStyle.Small,
            maxLines: 3);
    }

    private ResearchNodeHoverInfo? DrawBranchCards(
        IReadOnlyList<Rectangle> cardBounds,
        IReadOnlyList<ResearchBranch> branches,
        GameSession session,
        GumUiRenderer gumUi,
        Texture2D? treeBackgroundTexture)
    {
        ResearchNodeHoverInfo? hoverInfo = null;
        for (var index = 0; index < cardBounds.Count; index++)
        {
            var bounds = cardBounds[index];
            var branch = index < branches.Count ? branches[index] : null;
            var hovered = bounds.Contains(_pointerPoint);
            var selected = _selectedBranchIndex == index;
        var fill = selected
            ? new Color(34, 70, 92)
            : hovered ? new Color(20, 45, 63) : new Color(13, 30, 44);
            var border = selected
                ? new Color(214, 236, 244)
                : hovered ? new Color(132, 181, 198) : new Color(66, 101, 118);

            gumUi.AddRoundedFrame(bounds, fill, border, 2, 14);
            var previewBounds = new Rectangle(bounds.X + 10, bounds.Y + 48, bounds.Width - 20, bounds.Height - 58);

            if (branch is null || branch.Count == 0)
            {
                AddText(
                    gumUi,
                    new Rectangle(bounds.X + 12, bounds.Y + 8, bounds.Width - 24, 18),
                    $"Branch {index + 1}",
                    Color.White,
                    GumTextStyle.Small);
                AddCenteredText(
                    gumUi,
                    previewBounds,
                    "Unavailable",
                    new Color(191, 204, 211),
                    GumTextStyle.Small);
                continue;
            }

            DrawTiledTreeBackground(gumUi, Inset(bounds, 2), treeBackgroundTexture, 12);
            AddText(
                gumUi,
                new Rectangle(bounds.X + 12, bounds.Y + 8, bounds.Width - 24, 18),
                $"Branch {index + 1}",
                Color.White,
                GumTextStyle.Small);
            AddText(
                gumUi,
                new Rectangle(bounds.X + 12, bounds.Y + 28, bounds.Width - 24, 16),
                $"{branch.Count} nodes",
                new Color(184, 206, 216),
                GumTextStyle.Compact);
            hoverInfo = DrawBranchCardPreview(
                branch,
                session,
                previewBounds,
                gumUi,
                treeBackgroundTexture) ?? hoverInfo;
        }

        return hoverInfo;
    }

    private ResearchNodeHoverInfo? DrawPlacedTree(GameSession session, TreeDisplayMetrics metrics, GumUiRenderer gumUi)
    {
        if (session.SkillTree.Root is null)
        {
            return null;
        }

        var treeLayout = BuildPlacedTreeLayout(metrics, session.SkillTree.Root);
        var hoveredNode = TryGetHoveredPlacedNode(metrics, treeLayout, out _);
        foreach (var node in treeLayout.Nodes)
        {
            if (node.Parent is null)
            {
                continue;
            }

            DrawClippedConnector(
                gumUi,
                metrics,
                node.Parent.Position,
                node.Position,
                GetSkillTreeConnectorColor(node.SkillNode),
                3,
                metrics.NodeRadius + 2f,
                metrics.NodeRadius + 2f);
        }

        foreach (var node in treeLayout.Nodes)
        {
            if (!IsNodeVisible(metrics, node.Position, metrics.NodeRadius))
            {
                continue;
            }

            DrawTreeNode(gumUi, node.Position, metrics.NodeRadius, GetNodeFillColor(session, node.SkillNode), GetNodeBorderColor(session, node.SkillNode));
        }

        if (hoveredNode is TreeInstanceNode hovered)
        {
            DrawPlacedPrerequisiteHighlights(gumUi, metrics, session, hovered, treeLayout);
            return BuildNodeHoverInfo(session, hovered);
        }

        return null;
    }

    private ResearchNodeHoverInfo? DrawBranchPreview(
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        ResearchDraftDragPreview preview,
        GumUiRenderer gumUi)
    {
        if (branch.Root is null)
        {
            return null;
        }

        var branchLayout = BuildBranchLayout(branch, metrics.EdgeLength);
        var anchorLayout = preview.AnchorNode is TreeInstanceNode anchorNode
            ? FindLayoutNodeBySkillNode(BuildPlacedTreeLayout(metrics, session.SkillTree.Root!), anchorNode)
            : null;
        var hoveredNode = anchorLayout is not null
            ? TryGetHoveredBranchNode(metrics, branchLayout, anchorLayout.Position, out _)
            : null;
        var lineColor = preview.CanPlace ? BranchConnectorColor : InvalidBranchConnectorColor;
        if (anchorLayout is not null)
        {
            var anchorPoint = anchorLayout.Position;
            var rootPoint = anchorPoint;
            foreach (var node in branchLayout.Nodes)
            {
                var point = anchorPoint + node.LocalPosition;
                if (node.Parent is null)
                {
                    rootPoint = point;
                    DrawClippedConnector(gumUi, metrics, anchorPoint, point, lineColor, 3, metrics.NodeRadius + 7f, metrics.NodeRadius + 2f);
                    continue;
                }

                DrawClippedConnector(
                    gumUi,
                    metrics,
                    anchorPoint + node.Parent.LocalPosition,
                    point,
                    lineColor,
                    3,
                    metrics.NodeRadius + 2f,
                    metrics.NodeRadius + 2f);
            }

            if (IsNodeVisible(metrics, anchorPoint, metrics.NodeRadius + 5))
            {
                DrawTreeNode(
                    gumUi,
                    anchorPoint,
                    metrics.NodeRadius + 5,
                    preview.CanPlace ? new Color(46, 92, 70, 120) : new Color(114, 41, 36, 120),
                    preview.CanPlace ? new Color(205, 240, 221) : new Color(255, 192, 188));
            }

            foreach (var node in branchLayout.Nodes)
            {
                var point = anchorPoint + node.LocalPosition;
                if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
                {
                    continue;
                }

                DrawTreeNode(
                    gumUi,
                    point,
                    metrics.NodeRadius,
                    preview.CanPlace ? GetBranchNodePreviewColor(session, node.BranchNode) : new Color(178, 70, 62),
                    preview.CanPlace ? new Color(246, 251, 253) : new Color(255, 220, 217));
            }
        }

        if (hoveredNode is TreeInstanceNode hoveredBranchNode && anchorLayout is not null)
        {
            DrawAnchoredBranchPrerequisiteHighlights(gumUi, metrics, session, branch, hoveredBranchNode, anchorLayout.Position, branchLayout);
            return BuildNodeHoverInfo(session, hoveredBranchNode);
        }

        return null;
    }

    private ResearchNodeHoverInfo? DrawCursorBoundBranchPreview(
        TreeDisplayMetrics metrics,
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
                GetBranchNodePreviewColor(session, node.BranchNode) * 0.72f,
                new Color(246, 251, 253, 210));
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

    private ResearchNodeHoverInfo? DrawBranchCardPreview(
        ResearchBranch branch,
        GameSession session,
        Rectangle bounds,
        GumUiRenderer gumUi,
        Texture2D? treeBackgroundTexture)
    {
        if (branch.Root is null)
        {
            return null;
        }

        gumUi.AddRoundedOutline(bounds, new Color(55, 87, 103), 1, 10);
        var layout = CalculateBranchCardPreviewLayout(branch, bounds);
        var hoveredNode = TryGetHoveredBranchNode(layout, out _);
        ResearchTreePreviewRenderer.DrawPreview(gumUi, session, bounds, ResearchTreeViewNode.FromResearchBranch(branch));

        if (hoveredNode is TreeInstanceNode hoveredBranchNode)
        {
            DrawFloatingBranchPrerequisiteHighlights(gumUi, layout, branch, hoveredBranchNode);
            return BuildNodeHoverInfo(session, hoveredBranchNode);
        }

        return null;
    }

    internal static BranchCardPreviewLayout CalculateBranchCardPreviewLayout(ResearchBranch branch, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (branch.Root is null)
        {
            throw new ArgumentException("Branch preview layout requires a visible root.", nameof(branch));
        }

        const float padding = 14f;
        var layout = BuildBranchLayout(branch, 1f);
        var availableWidth = Math.Max(60f, bounds.Width - (padding * 2f));
        var availableHeight = Math.Max(60f, bounds.Height - (padding * 2f));
        var scale = MathF.Min(
            availableWidth / MathF.Max(1f, layout.Bounds.Width),
            availableHeight / MathF.Max(1f, layout.Bounds.Height));
        scale = MathF.Max(18f, scale);
        var radius = Math.Clamp((int)MathF.Round(scale * 0.18f), 6, 14);
        var left = bounds.X + padding + radius;
        var top = bounds.Y + padding + radius;
        var layoutWidth = layout.Bounds.Width * scale;
        var layoutHeight = layout.Bounds.Height * scale;
        var offset = new Vector2(
            left + ((availableWidth - (radius * 2f) - layoutWidth) / 2f) - (layout.Bounds.MinX * scale),
            top + ((availableHeight - (radius * 2f) - layoutHeight) / 2f) - (layout.Bounds.MinY * scale));

        var nodes = new List<BranchCardPreviewNode>(layout.Nodes.Count);
        foreach (var node in layout.Nodes)
        {
            nodes.Add(new BranchCardPreviewNode(
                node.BranchNode,
                node.Parent is null ? null : nodes.First(existing => ReferenceEquals(existing.BranchNode, node.Parent.BranchNode)),
                (node.LocalPosition * scale) + offset));
        }

        return new BranchCardPreviewLayout(
            nodes,
            offset,
            offset + (layout.RootLocalPosition * scale),
            radius,
            bounds);
    }

    private TreeDisplayMetrics BuildTreeMetrics(Rectangle bounds, SkillTree skillTree)
    {
        return BuildTreeMetrics(bounds, skillTree, _treeZoom);
    }

    private static TreeDisplayMetrics BuildTreeMetrics(Rectangle bounds, SkillTree skillTree, float zoom)
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
            MaximumTreeEdgeLength) * Math.Clamp(zoom, MinimumTreeZoom, MaximumTreeZoom);
        var nodeRadius = Math.Clamp((int)MathF.Round(edgeLength * 0.18f), 9, 18);
        var origin = new Vector2(contentBounds.Center.X, contentBounds.Bottom - nodeRadius - 8f);
        var baseBounds = skillTree.Root is null
            ? new TreeBounds(0f, 0f, 0f, 0f)
            : BuildTreeBounds(origin, BuildPlacedTreeLayout(origin, edgeLength, Vector2.Zero, skillTree.Root));

        return new TreeDisplayMetrics(bounds, contentBounds, origin, edgeLength, nodeRadius, baseBounds);
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
            return new ResearchDraftDragPreview(null, false, false, "That branch is no longer available.");
        }

        var branch = draftSystem.PendingDraft.Branches[branchIndex];
        if (branch.Root is null)
        {
            return new ResearchDraftDragPreview(null, false, false, "That research branch is empty.");
        }

        if (!layout.TreeViewportBounds.Contains(point))
        {
            return new ResearchDraftDragPreview(null, false, false, SelectedBranchStatus);
        }

        if (TryGetAnchorLocation(point, layout.TreeViewportBounds, session.SkillTree, out var anchorNode))
        {
            var canPlace = session.SkillTree.CanPlaceResearchBranch(branch, anchorNode!, out var failureReason);
            return new ResearchDraftDragPreview(
                anchorNode,
                canPlace,
                true,
                canPlace ? "Click to graft this branch onto the tree." : failureReason ?? "That branch cannot be placed there.");
        }

        return new ResearchDraftDragPreview(null, false, true, "Drop the branch on the root anchor or an existing skill node.");
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
        TreeDisplayMetrics metrics,
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

    private static bool IsNodeVisible(TreeDisplayMetrics metrics, Vector2 center, int radius)
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

    private static float Clamp(float value, float minimum, float maximum)
    {
        return MathF.Min(MathF.Max(value, minimum), maximum);
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
        var scale = Math.Clamp(zoom, MinimumTreeZoom, MaximumTreeZoom);
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

    private void SnapTreeToContentBoundsIfNeeded(Rectangle treeBounds, SkillTree skillTree)
    {
        _treePanOffset = ResolveTreePanAfterRelease(treeBounds, skillTree, _treePanOffset, _treeZoom);
    }

    internal static Vector2 ResolveTreePanAfterRelease(
        Rectangle treeBounds,
        SkillTree skillTree,
        Vector2 panOffset,
        float zoom)
    {
        if (skillTree.Root is null)
        {
            return Vector2.Zero;
        }

        var metrics = BuildTreeMetrics(treeBounds, skillTree, zoom);
        var pannedBounds = OffsetTreeBounds(BuildVisibleTreeContentBounds(metrics), panOffset);
        if (TreeBoundsIntersects(metrics.ContentBounds, pannedBounds))
        {
            return panOffset;
        }

        return CalculateTreeCenteringPanOffset(metrics);
    }

    internal static TreeBounds BuildVisibleTreeContentBounds(Rectangle treeBounds, SkillTree skillTree, float zoom = 1f)
    {
        var metrics = BuildTreeMetrics(treeBounds, skillTree, zoom);
        return BuildVisibleTreeContentBounds(metrics);
    }

    internal static float ClampTreeZoom(float zoom)
    {
        return Math.Clamp(zoom, MinimumTreeZoom, MaximumTreeZoom);
    }

    private static TreeBounds BuildVisibleTreeContentBounds(TreeDisplayMetrics metrics)
    {
        return ExpandTreeBounds(metrics.BaseBounds, metrics.NodeRadius);
    }

    private static TreeBounds ExpandTreeBounds(TreeBounds bounds, float padding)
    {
        return new TreeBounds(
            bounds.MinX - padding,
            bounds.MaxX + padding,
            bounds.MinY - padding,
            bounds.MaxY + padding);
    }

    private static TreeBounds OffsetTreeBounds(TreeBounds bounds, Vector2 offset)
    {
        return new TreeBounds(
            bounds.MinX + offset.X,
            bounds.MaxX + offset.X,
            bounds.MinY + offset.Y,
            bounds.MaxY + offset.Y);
    }

    private static bool TreeBoundsIntersects(Rectangle rectangle, TreeBounds bounds)
    {
        return bounds.MaxX >= rectangle.Left &&
            bounds.MinX <= rectangle.Right &&
            bounds.MaxY >= rectangle.Top &&
            bounds.MinY <= rectangle.Bottom;
    }

    private static Vector2 CalculateTreeCenteringPanOffset(TreeDisplayMetrics metrics)
    {
        var treeCenter = new Vector2(
            (metrics.BaseBounds.MinX + metrics.BaseBounds.MaxX) * 0.5f,
            (metrics.BaseBounds.MinY + metrics.BaseBounds.MaxY) * 0.5f);
        return metrics.ContentBounds.Center.ToVector2() - treeCenter;
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

    private PlacedTreeRenderLayout BuildPlacedTreeLayout(TreeDisplayMetrics metrics, TreeInstanceNode root)
    {
        return BuildPlacedTreeLayout(metrics.Origin, metrics.EdgeLength, _treePanOffset, root);
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
            new TreeBounds(layout.MinX, layout.MaxX, layout.MinY, layout.MaxY));
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

    private static TreeBounds BuildTreeBounds(Vector2 origin, PlacedTreeRenderLayout layout)
    {
        if (layout.Nodes.Count == 0)
        {
            return new TreeBounds(origin.X, origin.X, origin.Y, origin.Y);
        }

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        foreach (var node in layout.Nodes)
        {
            minX = MathF.Min(minX, node.Position.X);
            maxX = MathF.Max(maxX, node.Position.X);
            minY = MathF.Min(minY, node.Position.Y);
            maxY = MathF.Max(maxY, node.Position.Y);
        }

        return new TreeBounds(minX, maxX, minY, maxY);
    }

    private static Vector2 ClampTreePanOffset(Vector2 desiredOffset, TreeDisplayMetrics metrics)
    {
        const float margin = 36f;
        if (metrics.BaseBounds.Width + (margin * 2f) > metrics.ContentBounds.Width)
        {
            var minX = metrics.ContentBounds.Right - margin - metrics.BaseBounds.MaxX;
            var maxX = metrics.ContentBounds.Left + margin - metrics.BaseBounds.MinX;
            desiredOffset.X = Clamp(desiredOffset.X, MathF.Min(minX, maxX), MathF.Max(minX, maxX));
        }
        else
        {
            desiredOffset.X = 0f;
        }

        if (metrics.BaseBounds.Height + (margin * 2f) > metrics.ContentBounds.Height)
        {
            var minY = metrics.ContentBounds.Bottom - margin - metrics.BaseBounds.MaxY;
            var maxY = metrics.ContentBounds.Top + margin - metrics.BaseBounds.MinY;
            desiredOffset.Y = Clamp(desiredOffset.Y, MathF.Min(minY, maxY), MathF.Max(minY, maxY));
        }
        else
        {
            desiredOffset.Y = 0f;
        }

        return desiredOffset;
    }

    private static PlacedTreeRenderNode? FindLayoutNode(PlacedTreeRenderLayout layout, TreeInstanceNode node)
    {
        return layout.Nodes.FirstOrDefault(existing => ReferenceEquals(existing.SkillNode, node));
    }

    private static BranchRenderNode? FindLayoutNode(BranchRenderLayout layout, TreeInstanceNode node)
    {
        return layout.Nodes.FirstOrDefault(existing => ReferenceEquals(existing.BranchNode, node));
    }

    private static PlacedTreeRenderNode? FindLayoutNodeBySkillNode(PlacedTreeRenderLayout layout, TreeInstanceNode node)
    {
        return layout.Nodes.FirstOrDefault(existing => ReferenceEquals(existing.SkillNode, node));
    }

    private void DrawNodeInfoPanel(
        Rectangle bounds,
        ResearchNodeHoverInfo hoverInfo,
        GumUiRenderer gumUi,
        ResearchNodeHoverPlacement placement)
    {
        gumUi.AddRoundedFrame(bounds, new Color(9, 18, 28, 248), new Color(204, 228, 238), 2, 16);

        var contentX = bounds.X + 14;
        var contentWidth = bounds.Width - 28;
        AddText(gumUi, new Rectangle(contentX, bounds.Y + 12, contentWidth, 18), "Node Details", new Color(204, 228, 238), GumTextStyle.Compact);
        DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 38, contentWidth, 44), "Node", hoverInfo.TitleText);
        DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 88, contentWidth, 40), "Feature Tree", hoverInfo.FeatureTreeText);
        DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 134, contentWidth, bounds.Height - 148), "Effect", hoverInfo.EffectText, maxLines: 10);
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

    private TreeInstanceNode? TryGetHoveredPlacedNode(TreeDisplayMetrics metrics, PlacedTreeRenderLayout layout, out Vector2 center)
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

    private ResearchNodeHoverInfo? GetPlacedTreeHoverInfo(GameSession session, TreeDisplayMetrics metrics)
    {
        if (session.SkillTree.Root is null)
        {
            return null;
        }

        var hoveredNode = TryGetHoveredPlacedNode(metrics, BuildPlacedTreeLayout(metrics, session.SkillTree.Root), out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
    }

    private ResearchNodeHoverInfo? GetAnchoredBranchHoverInfo(
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        TreeInstanceNode anchorNode)
    {
        if (session.SkillTree.Root is null)
        {
            return null;
        }

        var treeLayout = BuildPlacedTreeLayout(metrics, session.SkillTree.Root);
        var anchorLayoutNode = FindLayoutNodeBySkillNode(treeLayout, anchorNode);
        if (anchorLayoutNode is null)
        {
            return null;
        }

        var hoveredNode = TryGetHoveredBranchNode(metrics, BuildBranchLayout(branch, metrics.EdgeLength), anchorLayoutNode.Position, out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
    }

    private ResearchNodeHoverInfo? GetCursorBoundBranchHoverInfo(
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch)
    {
        var hoveredNode = TryGetHoveredBranchNode(metrics, BuildBranchLayout(branch, metrics.EdgeLength), _pointerPoint.ToVector2(), out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
    }

    private ResearchNodeHoverInfo? GetBranchCardHoverInfo(
        IReadOnlyList<Rectangle> cardBounds,
        IReadOnlyList<ResearchBranch> branches,
        GameSession session)
    {
        ResearchNodeHoverInfo? hoverInfo = null;
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

    private ResearchNodeHoverInfo? GetBranchCardHoverInfo(
        ResearchBranch branch,
        GameSession session,
        Rectangle bounds)
    {
        if (branch.Root is null)
        {
            return null;
        }

        var layout = CalculateBranchCardPreviewLayout(branch, bounds);
        var hoveredNode = TryGetHoveredBranchNode(layout, out _);

        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
    }

    private TreeInstanceNode? TryGetHoveredBranchNode(
        TreeDisplayMetrics metrics,
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

    private TreeInstanceNode? TryGetHoveredBranchNode(BranchCardPreviewLayout layout, out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = layout.Radius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        TreeInstanceNode? hovered = null;
        foreach (var node in layout.Nodes)
        {
            var point = node.Position;
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
        TreeDisplayMetrics metrics,
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

    private void DrawAnchoredBranchPrerequisiteHighlights(
        GumUiRenderer gumUi,
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        TreeInstanceNode hoveredNode,
        Vector2 anchorPosition,
        BranchRenderLayout layout)
    {
        DrawFloatingBranchPrerequisiteHighlights(
            gumUi,
            metrics.NodeRadius,
            layout,
            anchorPosition,
            branch,
            hoveredNode,
            point => IsNodeVisible(metrics, point, metrics.NodeRadius));
        DrawPlacedFeatureTreePrerequisiteHighlights(gumUi, metrics, session, hoveredNode, branch, BuildPlacedTreeLayout(metrics, session.SkillTree.Root!));
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
        TreeDisplayMetrics metrics,
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

    private static void DrawFloatingBranchPrerequisiteHighlights(
        GumUiRenderer gumUi,
        BranchCardPreviewLayout layout,
        ResearchBranch branch,
        TreeInstanceNode hoveredNode)
    {
        var hoveredLayoutNode = layout.Nodes.FirstOrDefault(node => ReferenceEquals(node.BranchNode, hoveredNode));
        if (hoveredLayoutNode is null)
        {
            return;
        }

        DrawNodeOutline(gumUi, hoveredLayoutNode.Position, layout.Radius, new Color(255, 255, 255, 240), 6, 2);
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

            var prerequisiteLayoutNode = layout.Nodes.FirstOrDefault(node => ReferenceEquals(node.BranchNode, prerequisiteNode));
            if (prerequisiteLayoutNode is null)
            {
                continue;
            }

            DrawNodeOutline(gumUi, prerequisiteLayoutNode.Position, layout.Radius, new Color(255, 255, 255, 216), 4, 2);
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

    internal static ResearchNodeHoverInfo BuildNodeHoverInfo(GameSession session, TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        return new ResearchNodeHoverInfo(
            node.Name,
            string.IsNullOrWhiteSpace(node.SourceFeatureTreeName) ? "Core" : node.SourceFeatureTreeName,
            BuildNodeAffectText(session, node));
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

    private static void AddText(GumUiRenderer gumUi, Rectangle bounds, string text, Color color, GumTextStyle style)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(bounds, text, color, fontSize: metrics.FontSize, verticalAlignment: VerticalAlignment.Center);
    }

    private static void AddCenteredText(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 0)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(
            bounds,
            text,
            color,
            HorizontalAlignment.Center,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines);
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

        var baseColor = GetBaseFeatureColor(session, node.SourceFeatureTreeName);
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

        var fill = GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return Color.Lerp(fill, Color.White, 0.38f);
    }

    private static Color GetBranchNodePreviewColor(GameSession session, TreeInstanceNode node)
    {
        return GetBaseFeatureColor(session, node.SourceFeatureTreeName);
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

    private readonly record struct TreeDisplayMetrics(
        Rectangle Bounds,
        Rectangle ContentBounds,
        Vector2 Origin,
        float EdgeLength,
        int NodeRadius,
        TreeBounds BaseBounds);

    internal readonly record struct TreeBounds(
        float MinX,
        float MaxX,
        float MinY,
        float MaxY)
    {
        public float Width => MaxX - MinX;

        public float Height => MaxY - MinY;
    }

    private readonly record struct ResearchDraftDragPreview(
        TreeInstanceNode? AnchorNode,
        bool CanPlace,
        bool IsHoveringTree,
        string StatusMessage)
    {
        public static ResearchDraftDragPreview Empty => new(null, false, false, string.Empty);
    }

    internal readonly record struct BranchCardPreviewLayout(
        IReadOnlyList<BranchCardPreviewNode> Nodes,
        Vector2 OriginPoint,
        Vector2 RootPoint,
        int Radius,
        Rectangle Bounds);

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
        TreeBounds Bounds);

    internal sealed record BranchCardPreviewNode(
        TreeInstanceNode BranchNode,
        BranchCardPreviewNode? Parent,
        Vector2 Position);

    private readonly record struct ResearchNodeHoverDisplay(
        ResearchNodeHoverInfo HoverInfo,
        ResearchNodeHoverPlacement Placement);

    internal readonly record struct ResearchNodeHoverInfo(
        string TitleText,
        string FeatureTreeText,
        string EffectText);
}
