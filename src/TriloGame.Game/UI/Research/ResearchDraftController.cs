using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Research;
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
    RequestedSkipGracePeriod,
    RequestedClose,
    BranchPlaced
}

internal enum ResearchNodeHoverPlacement
{
    LeftDock,
    RightDock,
    BranchColumn
}

public sealed class ResearchDraftController
{
    private const string EmptyStatus = "No research branches are waiting. Finish a wave to generate a new set of branches.";
    private const string PendingStatus = "Click a research branch, then click a valid spot on the skill tree to graft it.";
    private const string SelectedBranchStatus = "Move the selected branch over the skill tree and click to place it.";
    internal const float TreeStepYRatio = 0.58f;
    private static readonly Color BranchConnectorColor = new(255, 255, 255);
    private static readonly Color BranchConnectorGhostColor = new(255, 255, 255, 232);
    private static readonly Color InvalidBranchConnectorColor = new(242, 126, 119);
    private static readonly Color LockedSkillTreeConnectorColor = new(246, 251, 253);
    private static readonly Color UnlockedSkillTreeConnectorColor = new(247, 221, 92);
    private static readonly Color BranchOriginFillColor = new(238, 207, 106);
    private static readonly Color BranchOriginBorderColor = new(255, 247, 222);

    private Point _pointerPoint;
    private float _treeScroll;
    private int? _selectedBranchIndex;
    private string _statusMessage = EmptyStatus;

    public bool IsOpen { get; private set; }

    public void Reset()
    {
        _pointerPoint = Point.Zero;
        _treeScroll = 0f;
        _selectedBranchIndex = null;
        _statusMessage = EmptyStatus;
        IsOpen = false;
    }

    public void Open(ResearchDraftSystem draftSystem)
    {
        IsOpen = true;
        _treeScroll = 0f;
        _statusMessage = BuildDefaultStatus(draftSystem);
    }

    public void Close(ResearchDraftSystem draftSystem)
    {
        _treeScroll = 0f;
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

        var layout = ResearchDraftLayout.Build(viewport);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        if (!layout.TreeViewportBounds.Contains(point))
        {
            return true;
        }

        var metrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree, draftSystem);
        if (metrics.MaxScroll <= 0f)
        {
            return true;
        }

        _treeScroll = Clamp(_treeScroll - delta, 0f, metrics.MaxScroll);
        return true;
    }

    public bool CoversScreenPoint(Point point, Point viewport)
    {
        var layout = ResearchDraftLayout.Build(viewport);
        return IsOpen
            ? layout.PanelBounds.Contains(point)
            : layout.SkillTreeButtonBounds.Contains(point) || layout.SkipButtonBounds.Contains(point);
    }

    public ResearchDraftInteractionOutcome HandleClosedButtonClick(Point point, Point viewport, bool canSkipGracePeriod)
    {
        if (IsOpen)
        {
            return ResearchDraftInteractionOutcome.None;
        }

        var layout = ResearchDraftLayout.Build(viewport);
        if (layout.SkillTreeButtonBounds.Contains(point))
        {
            return ResearchDraftInteractionOutcome.RequestedOpen;
        }

        if (!layout.SkipButtonBounds.Contains(point))
        {
            return ResearchDraftInteractionOutcome.None;
        }

        return canSkipGracePeriod
            ? ResearchDraftInteractionOutcome.RequestedSkipGracePeriod
            : ResearchDraftInteractionOutcome.Consumed;
    }

    public bool HandlePointerDown(Point point, Point viewport, ResearchDraftSystem draftSystem)
    {
        _pointerPoint = point;
        return IsOpen && ResearchDraftLayout.Build(viewport).PanelBounds.Contains(point);
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

        var layout = ResearchDraftLayout.Build(viewport);
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
        bool canSkipGracePeriod,
        GumUiRenderer gumUi)
    {
        var layout = ResearchDraftLayout.Build(viewport);
        DrawSkillTreeButton(layout.SkillTreeButtonBounds, draftSystem.HasPendingDraft, gumUi);
        DrawSkipButton(layout.SkipButtonBounds, canSkipGracePeriod, gumUi);

        if (!IsOpen)
        {
            return;
        }

        gumUi.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(5, 11, 17, 164));
        DrawPanel(layout, session, draftSystem, gumUi);
    }

    private ResearchDraftInteractionOutcome TryPlaceSelectedBranch(
        Point point,
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        int branchIndex)
    {
        var preview = BuildDragPreview(point, layout, session, draftSystem, branchIndex);
        if (!preview.CanPlace || preview.AnchorLocation is not GridPoint anchorLocation)
        {
            _statusMessage = preview.StatusMessage;
            return ResearchDraftInteractionOutcome.Consumed;
        }

        if (!draftSystem.TryPlaceBranch(session, branchIndex, anchorLocation, out var failureReason))
        {
            _statusMessage = failureReason ?? "That branch could not be placed there.";
            return ResearchDraftInteractionOutcome.Consumed;
        }

        _selectedBranchIndex = null;
        _statusMessage = "Research branch added to the colony skill tree.";
        return ResearchDraftInteractionOutcome.BranchPlaced;
    }

    private void DrawSkillTreeButton(Rectangle bounds, bool hasPendingDraft, GumUiRenderer gumUi)
    {
        var hovered = bounds.Contains(_pointerPoint);
        var fill = hasPendingDraft
            ? hovered ? new Color(176, 147, 92) : new Color(152, 125, 74)
            : hovered ? new Color(24, 55, 76) : new Color(16, 38, 54);
        var border = hasPendingDraft
            ? hovered ? new Color(255, 229, 170) : new Color(233, 201, 143)
            : hovered ? new Color(161, 210, 228) : new Color(93, 136, 154);
        var text = hasPendingDraft ? new Color(18, 26, 34) : Color.White;

        gumUi.AddRoundedFrame(bounds, fill, border, 2, 14);
        AddCenteredText(
            gumUi,
            new Rectangle(bounds.X + 12, bounds.Y, bounds.Width - 24, bounds.Height),
            "Skill Tree",
            text,
            GumTextStyle.Small);
    }

    private void DrawSkipButton(Rectangle bounds, bool canSkipGracePeriod, GumUiRenderer gumUi)
    {
        var hovered = canSkipGracePeriod && bounds.Contains(_pointerPoint);
        var fill = !canSkipGracePeriod
            ? new Color(33, 40, 44)
            : hovered ? new Color(110, 84, 33) : new Color(84, 60, 20);
        var border = !canSkipGracePeriod
            ? new Color(92, 104, 112)
            : hovered ? new Color(245, 223, 173) : new Color(214, 188, 128);
        var text = canSkipGracePeriod ? Color.White : new Color(183, 191, 196);

        gumUi.AddRoundedFrame(bounds, fill, border, 2, 14);
        AddCenteredText(
            gumUi,
            new Rectangle(bounds.X + 10, bounds.Y, bounds.Width - 20, bounds.Height),
            "Skip Wait",
            text,
            GumTextStyle.Small);
    }

    private void DrawPanel(
        ResearchDraftLayoutInfo layout,
        GameSession session,
        ResearchDraftSystem draftSystem,
        GumUiRenderer gumUi)
    {
        var pendingDraft = draftSystem.PendingDraft;
        var hoverDisplay = GetHoveredNodeDisplay(layout, session, draftSystem);
        DrawDockedHoverInfoPanel(layout, hoverDisplay, gumUi);

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
                : $"Round {pendingDraft.SourceRoundNumber} reward. Choose one branch and click a valid graft point on the tree.",
            new Color(177, 203, 214),
            GumTextStyle.Small);

        gumUi.AddRoundedFrame(layout.TreeBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        AddText(
            gumUi,
            layout.TreeHeaderBounds,
            "Global Skill Tree",
            new Color(204, 228, 238),
            GumTextStyle.Small);
        DrawSkillTreePanel(layout, session, draftSystem, gumUi);

        if (pendingDraft is null)
        {
            if (hoverDisplay is { Placement: ResearchNodeHoverPlacement.BranchColumn } branchColumnHoverDisplay)
            {
                DrawNodeInfoPanel(
                    layout.BranchColumnBounds,
                    branchColumnHoverDisplay.HoverInfo,
                    gumUi,
                    ResearchNodeHoverPlacement.BranchColumn);
            }
            else
            {
                gumUi.AddRoundedFrame(layout.BranchColumnBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
                AddCenteredText(
                    gumUi,
                    Inset(layout.BranchColumnBounds, 18),
                    "No pending research branches.\nDefeat another wave to generate three new options.",
                    new Color(210, 228, 236),
                    GumTextStyle.Small,
                    maxLines: 3);
            }
        }
        else
        {
            DrawBranchCards(layout.BranchCardBounds, pendingDraft.Branches, session, gumUi);
        }

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

        var metrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree, draftSystem);
        _treeScroll = Clamp(_treeScroll, 0f, metrics.MaxScroll);
        metrics = metrics with { Scroll = _treeScroll };
        DrawGuideGrid(metrics, gumUi);
        DrawGrowthBoundary(metrics, gumUi);
        var hoverInfo = DrawPlacedTree(session, metrics, gumUi);
        DrawTreeScrollbar(metrics, gumUi);

        if (_selectedBranchIndex is int activeBranchIndex &&
            draftSystem.PendingDraft is not null &&
            activeBranchIndex < draftSystem.PendingDraft.Branches.Count)
        {
            var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
            if (preview.AnchorLocation is GridPoint)
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
        var metrics = BuildTreeMetrics(layout.TreeViewportBounds, session.SkillTree, draftSystem);
        metrics = metrics with { Scroll = Clamp(_treeScroll, 0f, metrics.MaxScroll) };

        var skillTreeHoverInfo = GetPlacedTreeHoverInfo(session, metrics);
        ResearchNodeHoverInfo? branchHoverInfo = null;
        if (_selectedBranchIndex is int activeBranchIndex &&
            draftSystem.PendingDraft is not null &&
            activeBranchIndex < draftSystem.PendingDraft.Branches.Count)
        {
            var branch = draftSystem.PendingDraft.Branches[activeBranchIndex];
            if (preview.AnchorLocation is GridPoint activeAnchor)
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

        return resolvedPlacement == ResearchNodeHoverPlacement.RightDock
            ? new ResearchNodeHoverDisplay(branchHoverInfo!.Value, resolvedPlacement)
            : new ResearchNodeHoverDisplay(skillTreeHoverInfo!.Value, resolvedPlacement);
    }

    private void DrawDockedHoverInfoPanel(
        ResearchDraftLayoutInfo layout,
        ResearchNodeHoverDisplay? hoverDisplay,
        GumUiRenderer gumUi)
    {
        if (hoverDisplay is not ResearchNodeHoverDisplay dockedHoverDisplay)
        {
            return;
        }

        if (dockedHoverDisplay.Placement == ResearchNodeHoverPlacement.LeftDock)
        {
            DrawNodeInfoPanel(layout.HoverInfoBounds, dockedHoverDisplay.HoverInfo, gumUi, dockedHoverDisplay.Placement);
        }
        else if (dockedHoverDisplay.Placement == ResearchNodeHoverPlacement.RightDock)
        {
            DrawNodeInfoPanel(layout.RightHoverInfoBounds, dockedHoverDisplay.HoverInfo, gumUi, dockedHoverDisplay.Placement);
        }
    }

    private ResearchNodeHoverInfo? DrawBranchCards(
        IReadOnlyList<Rectangle> cardBounds,
        IReadOnlyList<ResearchBranch> branches,
        GameSession session,
        GumUiRenderer gumUi)
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
            AddText(
                gumUi,
                new Rectangle(bounds.X + 12, bounds.Y + 8, bounds.Width - 24, 18),
                $"Branch {index + 1}",
                Color.White,
                GumTextStyle.Small);

            if (branch is null || branch.Count == 0)
            {
                AddCenteredText(
                    gumUi,
                    new Rectangle(bounds.X + 12, bounds.Y + 36, bounds.Width - 24, bounds.Height - 48),
                    "Unavailable",
                    new Color(191, 204, 211),
                    GumTextStyle.Small);
                continue;
            }

            AddText(
                gumUi,
                new Rectangle(bounds.X + 12, bounds.Y + 28, bounds.Width - 24, 16),
                $"{branch.Count} nodes",
                new Color(184, 206, 216),
                GumTextStyle.Compact);
            hoverInfo = DrawBranchCardPreview(
                branch,
                session,
                new Rectangle(bounds.X + 10, bounds.Y + 48, bounds.Width - 20, bounds.Height - 58),
                gumUi) ?? hoverInfo;
        }

        return hoverInfo;
    }

    private void DrawGuideGrid(TreeDisplayMetrics metrics, GumUiRenderer gumUi)
    {
        for (var depth = 0; depth <= metrics.MaxContentDepth; depth++)
        {
            for (var x = 0; x <= depth; x++)
            {
                var y = depth - x;
                var location = new GridPoint(x, y);
                if (!SkillTree.IsValidGridLocation(location))
                {
                    continue;
                }

                var point = GetTreePoint(metrics, location);
                var leftChildLocation = new GridPoint(x + 1, y);
                if (depth + 1 <= metrics.MaxContentDepth && SkillTree.IsValidGridLocation(leftChildLocation))
                {
                    DrawClippedLine(gumUi, metrics, point, GetTreePoint(metrics, leftChildLocation), new Color(71, 88, 97, 112), 1);
                }

                var rightChildLocation = new GridPoint(x, y + 1);
                if (depth + 1 <= metrics.MaxContentDepth && SkillTree.IsValidGridLocation(rightChildLocation))
                {
                    DrawClippedLine(gumUi, metrics, point, GetTreePoint(metrics, rightChildLocation), new Color(71, 88, 97, 112), 1);
                }
            }
        }
    }

    private void DrawGrowthBoundary(TreeDisplayMetrics metrics, GumUiRenderer gumUi)
    {
        var boundaryColor = new Color(126, 149, 159, 184);
        for (var level = 0; level <= metrics.MaxContentDepth + 1; level++)
        {
            var leftStart = new GridPoint(SkillTree.MaxLateralDifference + level, level);
            var leftMid = new GridPoint(SkillTree.MaxLateralDifference + level, level + 1);
            var leftEnd = new GridPoint(SkillTree.MaxLateralDifference + level + 1, level + 1);
            DrawClippedLine(gumUi, metrics, GetTreePoint(metrics, leftStart), GetTreePoint(metrics, leftMid), boundaryColor, 2);
            DrawClippedLine(gumUi, metrics, GetTreePoint(metrics, leftMid), GetTreePoint(metrics, leftEnd), boundaryColor, 2);

            var rightStart = new GridPoint(level, SkillTree.MaxLateralDifference + level);
            var rightMid = new GridPoint(level + 1, SkillTree.MaxLateralDifference + level);
            var rightEnd = new GridPoint(level + 1, SkillTree.MaxLateralDifference + level + 1);
            DrawClippedLine(gumUi, metrics, GetTreePoint(metrics, rightStart), GetTreePoint(metrics, rightMid), boundaryColor, 2);
            DrawClippedLine(gumUi, metrics, GetTreePoint(metrics, rightMid), GetTreePoint(metrics, rightEnd), boundaryColor, 2);
        }
    }

    private ResearchNodeHoverInfo? DrawPlacedTree(GameSession session, TreeDisplayMetrics metrics, GumUiRenderer gumUi)
    {
        var hoveredNode = TryGetHoveredPlacedNode(metrics, session.SkillTree, out _);
        foreach (var node in session.SkillTree.TraverseDepthFirst())
        {
            if (node.NodeLocation is not GridPoint location)
            {
                continue;
            }

            if (node.Left is BinarySkillNode leftChild && leftChild.NodeLocation is GridPoint leftLocation)
            {
                DrawClippedLine(
                    gumUi,
                    metrics,
                    GetTreePoint(metrics, location),
                    GetTreePoint(metrics, leftLocation),
                    GetSkillTreeConnectorColor(leftChild),
                    3);
            }

            if (node.Right is BinarySkillNode rightChild && rightChild.NodeLocation is GridPoint rightLocation)
            {
                DrawClippedLine(
                    gumUi,
                    metrics,
                    GetTreePoint(metrics, location),
                    GetTreePoint(metrics, rightLocation),
                    GetSkillTreeConnectorColor(rightChild),
                    3);
            }
        }

        foreach (var node in session.SkillTree.TraverseDepthFirst())
        {
            if (node.NodeLocation is not GridPoint location)
            {
                continue;
            }

            var point = GetTreePoint(metrics, location);
            if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
            {
                continue;
            }

            DrawTreeNode(gumUi, point, metrics.NodeRadius, GetNodeFillColor(session, node), GetNodeBorderColor(session, node));
        }

        if (hoveredNode is BinarySkillNode hovered)
        {
            DrawPlacedPrerequisiteHighlights(gumUi, metrics, session, hovered);
            return BuildNodeHoverInfo(session, hovered);
        }

        return null;
    }

    private void DrawTreeScrollbar(TreeDisplayMetrics metrics, GumUiRenderer gumUi)
    {
        if (metrics.ScrollbarTrackBounds is not Rectangle trackBounds ||
            metrics.ScrollbarThumbBounds is not Rectangle thumbBounds)
        {
            return;
        }

        gumUi.AddRoundedFrame(trackBounds, new Color(10, 22, 31), new Color(48, 73, 87), 1, 8);
        gumUi.AddRoundedFrame(thumbBounds, new Color(122, 171, 191), new Color(208, 233, 242), 1, 8);
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

        var hoveredNode = preview.AnchorLocation is GridPoint anchorLocationForHover
            ? TryGetHoveredBranchNode(
                branch,
                metrics.NodeRadius,
                node => GetTreePoint(metrics, GetAbsoluteLocation(anchorLocationForHover, node.Delta)),
                node => IsNodeVisible(metrics, GetTreePoint(metrics, GetAbsoluteLocation(anchorLocationForHover, node.Delta)), metrics.NodeRadius),
                out _)
            : null;
        var lineColor = preview.CanPlace ? BranchConnectorColor : InvalidBranchConnectorColor;
        foreach (var branchNode in branch.Nodes)
        {
            if (preview.AnchorLocation is not GridPoint anchorLocation)
            {
                break;
            }

            var point = GetTreePoint(metrics, GetAbsoluteLocation(anchorLocation, branchNode.Delta));
            if (branchNode.Parent is not null)
            {
                var parentPoint = GetTreePoint(metrics, GetAbsoluteLocation(anchorLocation, branchNode.Parent.Delta));
                DrawClippedLine(gumUi, metrics, parentPoint, point, lineColor, 3);
            }
        }

        if (preview.AnchorLocation is GridPoint activeAnchor)
        {
            var anchorPoint = GetTreePoint(metrics, activeAnchor);
            var rootPoint = GetTreePoint(metrics, GetAbsoluteLocation(activeAnchor, branch.Root.Delta));
            DrawClippedLine(gumUi, metrics, anchorPoint, rootPoint, lineColor, 3);
            if (IsNodeVisible(metrics, anchorPoint, metrics.NodeRadius + 5))
            {
                DrawTreeNode(
                    gumUi,
                    anchorPoint,
                    metrics.NodeRadius + 5,
                    preview.CanPlace ? new Color(46, 92, 70, 120) : new Color(114, 41, 36, 120),
                    preview.CanPlace ? new Color(205, 240, 221) : new Color(255, 192, 188));
            }

            foreach (var branchNode in branch.Nodes)
            {
                var point = GetTreePoint(metrics, GetAbsoluteLocation(activeAnchor, branchNode.Delta));
                if (!IsNodeVisible(metrics, point, metrics.NodeRadius))
                {
                    continue;
                }

                DrawTreeNode(
                    gumUi,
                    point,
                    metrics.NodeRadius,
                    preview.CanPlace ? GetBranchNodePreviewColor(session, branchNode) : new Color(178, 70, 62),
                    preview.CanPlace ? new Color(246, 251, 253) : new Color(255, 220, 217));
            }
        }

        if (hoveredNode is ResearchBranchNode hoveredBranchNode &&
            preview.AnchorLocation is GridPoint activeAnchorLocation)
        {
            DrawAnchoredBranchPrerequisiteHighlights(gumUi, metrics, session, branch, hoveredBranchNode, activeAnchorLocation);
            return BuildNodeHoverInfo(session, hoveredBranchNode.Node);
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
        var rootPoint = GetTreePoint(origin, metrics.StepX, metrics.StepY, branch.Root.Delta);
        gumUi.AddLine(origin, rootPoint, lineColor, 3);
        DrawBranchOriginMarker(gumUi, origin, ghosted: true);
        var hoveredNode = TryGetHoveredBranchNode(
            branch,
            metrics.NodeRadius,
            node => GetTreePoint(origin, metrics.StepX, metrics.StepY, node.Delta),
            static _ => true,
            out _);
        foreach (var branchNode in branch.Nodes)
        {
            var point = GetTreePoint(origin, metrics.StepX, metrics.StepY, branchNode.Delta);
            if (branchNode.Parent is not null)
            {
                var parentPoint = GetTreePoint(origin, metrics.StepX, metrics.StepY, branchNode.Parent.Delta);
                gumUi.AddLine(parentPoint, point, lineColor, 3);
            }

            DrawTreeNode(
                gumUi,
                point,
                metrics.NodeRadius,
                GetBranchNodePreviewColor(session, branchNode) * 0.72f,
                new Color(246, 251, 253, 210));
        }

        if (hoveredNode is ResearchBranchNode hoveredBranchNode)
        {
            DrawFloatingBranchPrerequisiteHighlights(
                gumUi,
                origin,
                metrics.StepX,
                metrics.StepY,
                metrics.NodeRadius,
                branch,
                hoveredBranchNode);
            DrawPlacedFeatureTreePrerequisiteHighlights(gumUi, metrics, session, hoveredBranchNode.Node, branch);
            return BuildNodeHoverInfo(session, hoveredBranchNode.Node);
        }

        return null;
    }

    private ResearchNodeHoverInfo? DrawBranchCardPreview(ResearchBranch branch, GameSession session, Rectangle bounds, GumUiRenderer gumUi)
    {
        if (branch.Root is null)
        {
            return null;
        }

        var layout = CalculateBranchCardPreviewLayout(branch, bounds);
        var stepX = layout.StepX;
        var stepY = layout.StepY;
        var root = layout.Origin;
        var radius = layout.Radius;
        var hoveredNode = TryGetHoveredBranchNode(
            branch,
            radius,
            node => GetTreePoint(root, stepX, stepY, node.Delta),
            node =>
            {
                var point = GetTreePoint(root, stepX, stepY, node.Delta);
                return point.X + radius >= bounds.Left &&
                       point.X - radius <= bounds.Right &&
                       point.Y + radius >= bounds.Top &&
                       point.Y - radius <= bounds.Bottom;
            },
            out _);
        var originPoint = root;
        var branchRootPoint = GetTreePoint(root, stepX, stepY, branch.Root.Delta);

        gumUi.AddLine(originPoint, branchRootPoint, BranchConnectorColor, 2);
        DrawBranchOriginMarker(gumUi, originPoint, ghosted: false);

        foreach (var node in branch.Nodes)
        {
            var point = GetTreePoint(root, stepX, stepY, node.Delta);
            if (node.Parent is not null)
            {
                gumUi.AddLine(GetTreePoint(root, stepX, stepY, node.Parent.Delta), point, BranchConnectorColor, 2);
            }

            DrawTreeNode(gumUi, point, radius, GetBranchNodePreviewColor(session, node), new Color(246, 251, 253));
        }

        if (hoveredNode is ResearchBranchNode hoveredBranchNode)
        {
            DrawFloatingBranchPrerequisiteHighlights(
                gumUi,
                root,
                stepX,
                stepY,
                radius,
                branch,
                hoveredBranchNode,
                point => point.X + radius >= bounds.Left &&
                         point.X - radius <= bounds.Right &&
                         point.Y + radius >= bounds.Top &&
                         point.Y - radius <= bounds.Bottom);
            return BuildNodeHoverInfo(session, hoveredBranchNode.Node);
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

        const float horizontalPadding = 14f;
        const float topPadding = 12f;
        const float bottomPadding = 14f;

        var minHorizontalUnits = 0;
        var maxHorizontalUnits = 0;
        var maxDepthUnits = 0;
        foreach (var node in branch.Nodes)
        {
            var horizontalUnits = node.Delta.X - node.Delta.Y;
            minHorizontalUnits = Math.Min(minHorizontalUnits, horizontalUnits);
            maxHorizontalUnits = Math.Max(maxHorizontalUnits, horizontalUnits);
            maxDepthUnits = Math.Max(maxDepthUnits, node.Delta.X + node.Delta.Y);
        }

        var horizontalSpanUnits = Math.Max(1, maxHorizontalUnits - minHorizontalUnits);
        var verticalSpanUnits = Math.Max(1, maxDepthUnits);
        var availableWidth = Math.Max(72f, bounds.Width - (horizontalPadding * 2f));
        var availableHeight = Math.Max(72f, bounds.Height - topPadding - bottomPadding);
        var stepX = MathF.Min(
            availableWidth / horizontalSpanUnits,
            availableHeight / (verticalSpanUnits * TreeStepYRatio));
        var radius = Math.Clamp((int)MathF.Round(stepX * 0.22f), 6, 14);

        availableWidth = Math.Max(48f, bounds.Width - (horizontalPadding * 2f) - (radius * 2f));
        availableHeight = Math.Max(48f, bounds.Height - topPadding - bottomPadding - (radius * 2f));
        stepX = MathF.Max(
            12f,
            MathF.Min(
                availableWidth / horizontalSpanUnits,
                availableHeight / (verticalSpanUnits * TreeStepYRatio)));
        var stepY = stepX * TreeStepYRatio;

        var left = minHorizontalUnits * stepX;
        var right = maxHorizontalUnits * stepX;
        var span = right - left;
        var originX = bounds.X + horizontalPadding + radius + ((availableWidth - span) / 2f) - left;
        var originY = bounds.Bottom - bottomPadding - radius;
        return new BranchCardPreviewLayout(new Vector2(originX, originY), stepX, stepY, radius);
    }

    private TreeDisplayMetrics BuildTreeMetrics(Rectangle bounds, SkillTree skillTree, ResearchDraftSystem draftSystem)
    {
        const int sidePadding = 12;
        const int topPadding = 8;
        const int bottomPadding = 12;
        const int scrollbarGap = 10;
        const int scrollbarWidth = 6;

        var contentBounds = new Rectangle(
            bounds.X + sidePadding,
            bounds.Y + topPadding,
            Math.Max(120, bounds.Width - (sidePadding * 2) - scrollbarGap - scrollbarWidth),
            Math.Max(120, bounds.Height - topPadding - bottomPadding));
        var stepX = Math.Clamp(
            (contentBounds.Width - 24f) / Math.Max(1f, SkillTree.MaxLateralDifference * 2f),
            18f,
            56f);
        var stepY = stepX * TreeStepYRatio;
        var nodeRadius = Math.Clamp((int)MathF.Round(stepX * 0.22f), 7, 14);

        var deepestPlacedDepth = 0;
        foreach (var node in skillTree.TraverseDepthFirst())
        {
            if (node.NodeLocation is not GridPoint location)
            {
                continue;
            }

            deepestPlacedDepth = Math.Max(deepestPlacedDepth, location.X + location.Y);
        }

        var maxBranchDepth = 0;
        if (draftSystem.PendingDraft is not null)
        {
            foreach (var branch in draftSystem.PendingDraft.Branches)
            {
                foreach (var node in branch.Nodes)
                {
                    maxBranchDepth = Math.Max(maxBranchDepth, node.Delta.X + node.Delta.Y);
                }
            }
        }

        var depthToFillViewport = Math.Max(
            6,
            (int)MathF.Ceiling((contentBounds.Height - (nodeRadius * 2f) - 16f) / stepY));
        var maxContentDepth = Math.Max(depthToFillViewport, deepestPlacedDepth + maxBranchDepth + 2);
        var contentHeight = (maxContentDepth * stepY) + (nodeRadius * 2f) + 16f;
        var maxScroll = Math.Max(0f, contentHeight - contentBounds.Height);

        Rectangle? scrollbarTrackBounds = null;
        Rectangle? scrollbarThumbBounds = null;
        if (maxScroll > 0f)
        {
            scrollbarTrackBounds = new Rectangle(
                bounds.Right - scrollbarWidth - 2,
                contentBounds.Y,
                scrollbarWidth,
                contentBounds.Height);
            var thumbHeight = Math.Max(28f, (contentBounds.Height / contentHeight) * contentBounds.Height);
            var travel = Math.Max(0f, contentBounds.Height - thumbHeight);
            var ratio = maxScroll <= 0f ? 0f : Clamp(_treeScroll, 0f, maxScroll) / maxScroll;
            scrollbarThumbBounds = new Rectangle(
                scrollbarTrackBounds.Value.X,
                contentBounds.Y + (int)MathF.Round(ratio * travel),
                scrollbarWidth,
                (int)MathF.Round(thumbHeight));
        }

        var origin = new Vector2(
            contentBounds.Center.X,
            contentBounds.Bottom - nodeRadius - 4f);

        return new TreeDisplayMetrics(
            bounds,
            contentBounds,
            origin,
            stepX,
            stepY,
            nodeRadius,
            maxContentDepth,
            0f,
            maxScroll,
            scrollbarTrackBounds,
            scrollbarThumbBounds);
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

        if (TryGetAnchorLocation(point, layout.TreeViewportBounds, session.SkillTree, draftSystem, out var anchorLocation))
        {
            var canPlace = session.SkillTree.CanPlaceResearchBranch(branch, anchorLocation, out var failureReason);
            return new ResearchDraftDragPreview(
                anchorLocation,
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
        ResearchDraftSystem draftSystem,
        out GridPoint anchorLocation)
    {
        anchorLocation = GridPoint.Zero;
        var metrics = BuildTreeMetrics(treeBounds, skillTree, draftSystem);
        _treeScroll = Clamp(_treeScroll, 0f, metrics.MaxScroll);
        metrics = metrics with { Scroll = _treeScroll };
        if (!metrics.ContentBounds.Contains(point))
        {
            return false;
        }

        var bestDistanceSquared = float.MaxValue;
        foreach (var node in skillTree.TraverseDepthFirst())
        {
            if (node.NodeLocation is not GridPoint location)
            {
                continue;
            }

            var nodePoint = GetTreePoint(metrics, location);
            var distanceSquared = Vector2.DistanceSquared(nodePoint, point.ToVector2());
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            anchorLocation = location;
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

    private static Vector2 GetTreePoint(TreeDisplayMetrics metrics, GridPoint location)
    {
        var point = GetTreePoint(metrics.Origin, metrics.StepX, metrics.StepY, location);
        return new Vector2(point.X, point.Y + metrics.Scroll);
    }

    internal static Vector2 GetTreePoint(Vector2 origin, float stepX, float stepY, GridPoint location)
    {
        return new Vector2(
            origin.X + ((location.X - location.Y) * stepX),
            origin.Y - ((location.X + location.Y) * stepY));
    }

    private static GridPoint GetAbsoluteLocation(GridPoint anchorLocation, GridPoint branchDelta)
    {
        return new GridPoint(anchorLocation.X + branchDelta.X, anchorLocation.Y + branchDelta.Y);
    }

    private static void DrawClippedLine(
        GumUiRenderer gumUi,
        TreeDisplayMetrics metrics,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness)
    {
        if (!TryClipLineToBounds(metrics.ContentBounds, ref start, ref end))
        {
            return;
        }

        gumUi.AddLine(start, end, color, thickness);
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

    private static void DrawBranchOriginMarker(GumUiRenderer gumUi, Vector2 center, bool ghosted)
    {
        var fill = ghosted ? new Color(BranchOriginFillColor.R, BranchOriginFillColor.G, BranchOriginFillColor.B, (byte)224) : BranchOriginFillColor;
        var border = ghosted ? new Color(BranchOriginBorderColor.R, BranchOriginBorderColor.G, BranchOriginBorderColor.B, (byte)236) : BranchOriginBorderColor;
        DrawTreeNode(gumUi, center, 5, fill, border);
    }

    private void DrawNodeInfoPanel(
        Rectangle bounds,
        ResearchNodeHoverInfo hoverInfo,
        GumUiRenderer gumUi,
        ResearchNodeHoverPlacement placement)
    {
        const int hiddenInset = 30;
        var hiddenLeftInset = placement == ResearchNodeHoverPlacement.RightDock ? hiddenInset : 0;
        var hiddenRightInset = placement == ResearchNodeHoverPlacement.LeftDock ? hiddenInset : 0;
        if (placement == ResearchNodeHoverPlacement.LeftDock)
        {
            var bridgeBounds = new Rectangle(bounds.Right - hiddenRightInset, bounds.Y + 28, hiddenRightInset + 10, Math.Max(56, bounds.Height - 56));
            gumUi.AddRoundedFrame(bridgeBounds, new Color(9, 18, 28, 248), new Color(116, 156, 174), 2, 10);
        }
        else if (placement == ResearchNodeHoverPlacement.RightDock)
        {
            var bridgeBounds = new Rectangle(bounds.X - 10, bounds.Y + 28, hiddenLeftInset + 10, Math.Max(56, bounds.Height - 56));
            gumUi.AddRoundedFrame(bridgeBounds, new Color(9, 18, 28, 248), new Color(116, 156, 174), 2, 10);
        }

        gumUi.AddRoundedFrame(bounds, new Color(9, 18, 28, 248), new Color(204, 228, 238), 2, 16);

        var contentX = bounds.X + 14 + hiddenLeftInset;
        var contentWidth = bounds.Width - 28 - hiddenLeftInset - hiddenRightInset;
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

    private BinarySkillNode? TryGetHoveredPlacedNode(TreeDisplayMetrics metrics, SkillTree skillTree, out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = metrics.NodeRadius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        BinarySkillNode? hovered = null;
        foreach (var node in skillTree.TraverseDepthFirst())
        {
            if (node.NodeLocation is not GridPoint location)
            {
                continue;
            }

            var point = GetTreePoint(metrics, location);
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

    private ResearchNodeHoverInfo? GetPlacedTreeHoverInfo(GameSession session, TreeDisplayMetrics metrics)
    {
        var hoveredNode = TryGetHoveredPlacedNode(metrics, session.SkillTree, out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode);
    }

    private ResearchNodeHoverInfo? GetAnchoredBranchHoverInfo(
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        GridPoint anchorLocation)
    {
        var hoveredNode = TryGetHoveredBranchNode(
            branch,
            metrics.NodeRadius,
            node => GetTreePoint(metrics, GetAbsoluteLocation(anchorLocation, node.Delta)),
            node => IsNodeVisible(metrics, GetTreePoint(metrics, GetAbsoluteLocation(anchorLocation, node.Delta)), metrics.NodeRadius),
            out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode.Node);
    }

    private ResearchNodeHoverInfo? GetCursorBoundBranchHoverInfo(
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch)
    {
        var origin = _pointerPoint.ToVector2();
        var hoveredNode = TryGetHoveredBranchNode(
            branch,
            metrics.NodeRadius,
            node => GetTreePoint(origin, metrics.StepX, metrics.StepY, node.Delta),
            static _ => true,
            out _);
        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode.Node);
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
        var hoveredNode = TryGetHoveredBranchNode(
            branch,
            layout.Radius,
            node => GetTreePoint(layout.Origin, layout.StepX, layout.StepY, node.Delta),
            node =>
            {
                var point = GetTreePoint(layout.Origin, layout.StepX, layout.StepY, node.Delta);
                return point.X + layout.Radius >= bounds.Left &&
                       point.X - layout.Radius <= bounds.Right &&
                       point.Y + layout.Radius >= bounds.Top &&
                       point.Y - layout.Radius <= bounds.Bottom;
            },
            out _);

        return hoveredNode is null ? null : BuildNodeHoverInfo(session, hoveredNode.Node);
    }

    private ResearchBranchNode? TryGetHoveredBranchNode(
        ResearchBranch branch,
        int radius,
        Func<ResearchBranchNode, Vector2> pointResolver,
        Predicate<ResearchBranchNode> isVisible,
        out Vector2 center)
    {
        center = Vector2.Zero;
        var hitRadius = radius + 6;
        var hitRadiusSquared = hitRadius * hitRadius;
        var bestDistanceSquared = float.MaxValue;
        ResearchBranchNode? hovered = null;
        foreach (var node in branch.Nodes)
        {
            if (!isVisible(node))
            {
                continue;
            }

            var point = pointResolver(node);
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

    private static void DrawPlacedPrerequisiteHighlights(
        GumUiRenderer gumUi,
        TreeDisplayMetrics metrics,
        GameSession session,
        BinarySkillNode hoveredNode)
    {
        if (hoveredNode.NodeLocation is GridPoint hoveredLocation)
        {
            DrawNodeOutline(gumUi, GetTreePoint(metrics, hoveredLocation), metrics.NodeRadius, new Color(255, 255, 255, 240), 6, 2);
        }

        DrawPlacedFeatureTreePrerequisiteHighlights(gumUi, metrics, session, hoveredNode, branch: null);
    }

    private static void DrawAnchoredBranchPrerequisiteHighlights(
        GumUiRenderer gumUi,
        TreeDisplayMetrics metrics,
        GameSession session,
        ResearchBranch branch,
        ResearchBranchNode hoveredNode,
        GridPoint anchorLocation)
    {
        DrawFloatingBranchPrerequisiteHighlights(
            gumUi,
            branch,
            hoveredNode,
            metrics.NodeRadius,
            node => GetTreePoint(metrics, GetAbsoluteLocation(anchorLocation, node.Delta)),
            point => IsNodeVisible(metrics, point, metrics.NodeRadius));
        DrawPlacedFeatureTreePrerequisiteHighlights(gumUi, metrics, session, hoveredNode.Node, branch);
    }

    private static void DrawFloatingBranchPrerequisiteHighlights(
        GumUiRenderer gumUi,
        Vector2 origin,
        float stepX,
        float stepY,
        int radius,
        ResearchBranch branch,
        ResearchBranchNode hoveredNode,
        Predicate<Vector2>? isVisible = null)
    {
        DrawFloatingBranchPrerequisiteHighlights(
            gumUi,
            branch,
            hoveredNode,
            radius,
            node => GetTreePoint(origin, stepX, stepY, node.Delta),
            isVisible);
    }

    private static void DrawPlacedFeatureTreePrerequisiteHighlights(
        GumUiRenderer gumUi,
        TreeDisplayMetrics metrics,
        GameSession session,
        BinarySkillNode hoveredNode,
        ResearchBranch? branch)
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
            if (prerequisiteNode?.NodeLocation is not GridPoint prerequisiteLocation)
            {
                continue;
            }

            var point = GetTreePoint(metrics, prerequisiteLocation);
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
        ResearchBranchNode hoveredNode,
        int radius,
        Func<ResearchBranchNode, Vector2> pointResolver,
        Predicate<Vector2>? isVisible = null)
    {
        var hoveredPoint = pointResolver(hoveredNode);
        if (isVisible is null || isVisible(hoveredPoint))
        {
            DrawNodeOutline(gumUi, hoveredPoint, radius, new Color(255, 255, 255, 240), 6, 2);
        }

        if (string.IsNullOrWhiteSpace(hoveredNode.Node.SourceFeatureTreeName))
        {
            return;
        }

        for (var current = hoveredNode.Node.SourceSkillNode.Parent; current is not null; current = current.Parent)
        {
            var prerequisiteNode = FindBranchNodeBySourceSkill(branch, hoveredNode.Node.SourceFeatureTreeName, current.Name);
            if (prerequisiteNode is null)
            {
                continue;
            }

            var point = pointResolver(prerequisiteNode);
            if (isVisible is not null && !isVisible(point))
            {
                continue;
            }

            DrawNodeOutline(gumUi, point, radius, new Color(255, 255, 255, 216), 4, 2);
        }
    }

    private static ResearchBranchNode? FindBranchNodeBySourceSkill(
        ResearchBranch branch,
        string featureTreeName,
        string skillName)
    {
        foreach (var node in branch.Nodes)
        {
            if (string.Equals(node.Node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) &&
                string.Equals(node.Node.Name, skillName, StringComparison.Ordinal))
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

    internal static ResearchNodeHoverInfo BuildNodeHoverInfo(GameSession session, BinarySkillNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        return new ResearchNodeHoverInfo(
            node.Name,
            string.IsNullOrWhiteSpace(node.SourceFeatureTreeName) ? "Core" : node.SourceFeatureTreeName,
            BuildNodeAffectText(session, node));
    }

    internal static Color GetSkillTreeConnectorColor(BinarySkillNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return child.IsUnlocked ? UnlockedSkillTreeConnectorColor : LockedSkillTreeConnectorColor;
    }

    internal static ResearchNodeHoverPlacement? ResolveHoverPlacement(
        bool hasPendingDraft,
        bool hasSkillTreeHover,
        bool hasBranchHover)
    {
        if (!hasPendingDraft)
        {
            return hasSkillTreeHover ? ResearchNodeHoverPlacement.BranchColumn : null;
        }

        if (hasBranchHover)
        {
            return ResearchNodeHoverPlacement.RightDock;
        }

        return hasSkillTreeHover ? ResearchNodeHoverPlacement.LeftDock : null;
    }

    internal static string BuildNodeAffectText(GameSession session, BinarySkillNode node)
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

    internal static IReadOnlyList<string> GetFeatureTreePrerequisiteSkillNames(BinarySkillNode node)
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

    private static Color GetNodeFillColor(GameSession session, BinarySkillNode node)
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

    private static Color GetNodeBorderColor(GameSession session, BinarySkillNode node)
    {
        if (node.IsRoot && string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
        {
            return new Color(230, 238, 244);
        }

        var fill = GetBaseFeatureColor(session, node.SourceFeatureTreeName);
        return Color.Lerp(fill, Color.White, 0.38f);
    }

    private static Color GetBranchNodePreviewColor(GameSession session, ResearchBranchNode node)
    {
        return GetBaseFeatureColor(session, node.Node.SourceFeatureTreeName);
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
        float StepX,
        float StepY,
        int NodeRadius,
        int MaxContentDepth,
        float Scroll,
        float MaxScroll,
        Rectangle? ScrollbarTrackBounds,
        Rectangle? ScrollbarThumbBounds);

    private readonly record struct ResearchDraftDragPreview(
        GridPoint? AnchorLocation,
        bool CanPlace,
        bool IsHoveringTree,
        string StatusMessage)
    {
        public static ResearchDraftDragPreview Empty => new(null, false, false, string.Empty);
    }

    internal readonly record struct BranchCardPreviewLayout(
        Vector2 Origin,
        float StepX,
        float StepY,
        int Radius);

    private readonly record struct ResearchNodeHoverDisplay(
        ResearchNodeHoverInfo HoverInfo,
        ResearchNodeHoverPlacement Placement);

    internal readonly record struct ResearchNodeHoverInfo(
        string TitleText,
        string FeatureTreeText,
        string EffectText);
}
