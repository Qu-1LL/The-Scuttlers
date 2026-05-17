using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchDraftControllerTests
{
    [Fact]
    public void BuildNodeHoverInfo_UsesFeatureTreeAndAffectedCategoriesWhenNoDescriptorsExist()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var node = new BinarySkillNode(new SkillNode("Tooltip Node", "Tooltip description."), new GridPoint(1, 0), "B1");

        var hoverInfo = ResearchDraftController.BuildNodeHoverInfo(session, node);

        Assert.Equal("Tooltip Node", hoverInfo.TitleText);
        Assert.Equal("B1", hoverInfo.FeatureTreeText);
        Assert.Equal("Building", hoverInfo.EffectText);
        Assert.Equal("Depends on placement.", hoverInfo.CostText);
    }

    [Fact]
    public void BuildNodeAffectText_FallsBackToDescriptionWhenNoFeatureTreeDataExists()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var node = new BinarySkillNode(new SkillNode("Core Anchor", "Root anchor."), GridPoint.Zero);

        var affectText = ResearchDraftController.BuildNodeAffectText(session, node);

        Assert.Equal("Root anchor.", affectText);
    }

    [Fact]
    public void ResolveHoverPlacement_UsesLeftRightAndBranchColumnTargetsAsExpected()
    {
        Assert.Equal(
            ResearchNodeHoverPlacement.LeftDock,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: true, hasSkillTreeHover: true, hasBranchHover: false));
        Assert.Equal(
            ResearchNodeHoverPlacement.RightDock,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: true, hasSkillTreeHover: true, hasBranchHover: true));
        Assert.Equal(
            ResearchNodeHoverPlacement.BranchColumn,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: false, hasSkillTreeHover: true, hasBranchHover: false));
        Assert.Null(
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: false, hasSkillTreeHover: false, hasBranchHover: true));
    }

    [Fact]
    public void GetSkillTreeConnectorColor_UsesWhiteForLockedNodesAndYellowForUnlockedNodes()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var root = new BinarySkillNode(new SkillNode("Root", "Root"));
        var child = new BinarySkillNode(new SkillNode("Child", "Child"));
        root.SetLeft(child);

        var lockedColor = ResearchDraftController.GetSkillTreeConnectorColor(child);
        Assert.Equal(new Color(246, 251, 253), lockedColor);

        Assert.True(root.TryUnlock(session));
        Assert.True(child.TryUnlock(session));

        var unlockedColor = ResearchDraftController.GetSkillTreeConnectorColor(child);
        Assert.Equal(new Color(247, 221, 92), unlockedColor);
    }

    [Fact]
    public void GetFeatureTreePrerequisiteSkillNames_UsesAuthoredFeatureTreeChainNotBinaryParents()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var featureTree = Assert.IsType<FeatureTree>(session.GetFeatureTree("B1"));
        var hovered = new BinarySkillNode(
            Assert.IsType<SkillNode>(featureTree.FindByName("B1-e")),
            new GridPoint(1, 0),
            featureTree.Name);
        var unrelatedAnchor = new BinarySkillNode(new SkillNode("Run Anchor", "Placed elsewhere."));
        unrelatedAnchor.SetLeft(hovered);

        var prerequisiteNames = ResearchDraftController.GetFeatureTreePrerequisiteSkillNames(hovered);

        Assert.Equal(["B1-d", "B1-c", "B1-b", "B1-a"], prerequisiteNames);
        Assert.DoesNotContain("Run Anchor", prerequisiteNames);
    }

    [Fact]
    public void CalculateBranchCardPreviewLayout_ScalesBranchesToUseMostOfThePreviewBounds()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new BinarySkillNode(new SkillNode("Root", "Root"), new GridPoint(1, 0), "B1"), new GridPoint(1, 0));
        var left = branch.AddLeftChild(root, new BinarySkillNode(new SkillNode("Left", "Left"), new GridPoint(2, 0), "B1"));
        var right = branch.AddRightChild(root, new BinarySkillNode(new SkillNode("Right", "Right"), new GridPoint(1, 1), "B1"));
        branch.AddRightChild(right, new BinarySkillNode(new SkillNode("Right Deep", "Right Deep"), new GridPoint(1, 2), "B1"));
        branch.AddLeftChild(left, new BinarySkillNode(new SkillNode("Left Deep", "Left Deep"), new GridPoint(3, 0), "B1"));

        var bounds = new Rectangle(0, 0, 240, 180);
        var layout = ResearchDraftController.CalculateBranchCardPreviewLayout(branch, bounds);
        var points = new List<Vector2> { layout.Origin };
        foreach (var node in branch.Nodes)
        {
            points.Add(ResearchDraftController.GetTreePoint(layout.Origin, layout.StepX, layout.StepY, node.Delta));
        }

        var leftEdge = float.MaxValue;
        var rightEdge = float.MinValue;
        var topEdge = float.MaxValue;
        var bottomEdge = float.MinValue;
        foreach (var point in points)
        {
            leftEdge = Math.Min(leftEdge, point.X - layout.Radius);
            rightEdge = Math.Max(rightEdge, point.X + layout.Radius);
            topEdge = Math.Min(topEdge, point.Y - layout.Radius);
            bottomEdge = Math.Max(bottomEdge, point.Y + layout.Radius);
        }

        Assert.InRange(leftEdge, bounds.Left, bounds.Right);
        Assert.InRange(rightEdge, bounds.Left, bounds.Right);
        Assert.InRange(topEdge, bounds.Top, bounds.Bottom);
        Assert.InRange(bottomEdge, bounds.Top, bounds.Bottom);
        Assert.True(rightEdge - leftEdge >= bounds.Width * 0.7f);
        Assert.True(bottomEdge - topEdge >= bounds.Height * 0.55f);
    }

    [Fact]
    public void GetTreePoint_ProjectsLeftAndRightChildrenWithExpectedScreenSlant()
    {
        var origin = new Vector2(320f, 480f);
        const float stepX = 40f;
        var stepY = stepX * ResearchDraftController.TreeStepYRatio;

        var leftChildPoint = ResearchDraftController.GetTreePoint(origin, stepX, stepY, new GridPoint(1, 0));
        var rightChildPoint = ResearchDraftController.GetTreePoint(origin, stepX, stepY, new GridPoint(0, 1));

        Assert.True(leftChildPoint.X > origin.X);
        Assert.True(rightChildPoint.X < origin.X);
        Assert.Equal(origin.Y - stepY, leftChildPoint.Y);
        Assert.Equal(origin.Y - stepY, rightChildPoint.Y);
    }

    [Fact]
    public void HandlePointerUp_ClickingOutsideThePanelRequestsClose()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        draftSystem.CreateDraft(session, round);
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);

        var outcome = controller.HandlePointerUp(Point.Zero, new Point(1280, 800), session, draftSystem);

        Assert.Equal(ResearchDraftInteractionOutcome.RequestedClose, outcome);
    }

    [Fact]
    public void HandleClosedButtonClick_ClickingSkillTreeButtonRequestsOpen()
    {
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);

        var outcome = controller.HandleClosedButtonClick(
            GetCenter(ResearchDraftLayout.GetSkillTreeButtonBounds(viewport)),
            viewport,
            canSkipGracePeriod: false);

        Assert.Equal(ResearchDraftInteractionOutcome.RequestedOpen, outcome);
    }

    [Fact]
    public void HandleClosedButtonClick_ClickingSkipButtonWhileGraceIsActiveRequestsGraceSkip()
    {
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);

        var outcome = controller.HandleClosedButtonClick(
            GetCenter(ResearchDraftLayout.GetSkipButtonBounds(viewport)),
            viewport,
            canSkipGracePeriod: true);

        Assert.Equal(ResearchDraftInteractionOutcome.RequestedSkipGracePeriod, outcome);
    }

    [Fact]
    public void HandleClosedButtonClick_ClickingSkipButtonDuringCombatIsConsumed()
    {
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);

        var outcome = controller.HandleClosedButtonClick(
            GetCenter(ResearchDraftLayout.GetSkipButtonBounds(viewport)),
            viewport,
            canSkipGracePeriod: false);

        Assert.Equal(ResearchDraftInteractionOutcome.Consumed, outcome);
    }

    [Fact]
    public void HandlePointerUp_SelectsABranchAndPlacesItOnASecondClick()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        var draft = Assert.IsType<ResearchDraftOffer>(draftSystem.CreateDraft(session, round));
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        var selectOutcome = controller.HandlePointerUp(GetCenter(layout.BranchCardBounds[0]), viewport, session, draftSystem);
        var placeOutcome = controller.HandlePointerUp(GetRootAnchorPoint(layout), viewport, session, draftSystem);

        Assert.Equal(ResearchDraftInteractionOutcome.Consumed, selectOutcome);
        Assert.Equal(ResearchDraftInteractionOutcome.BranchPlaced, placeOutcome);
        Assert.False(draftSystem.HasPendingDraft);
        Assert.Equal(1 + draft.Branches[0].Count, session.SkillTree.Count);
    }

    [Fact]
    public void HandlePointerUp_ClickingAnotherBranchSwitchesTheSelectedPlacement()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        var draft = Assert.IsType<ResearchDraftOffer>(draftSystem.CreateDraft(session, round));
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        controller.HandlePointerUp(GetCenter(layout.BranchCardBounds[0]), viewport, session, draftSystem);
        var switchOutcome = controller.HandlePointerUp(GetCenter(layout.BranchCardBounds[1]), viewport, session, draftSystem);
        var placeOutcome = controller.HandlePointerUp(GetRootAnchorPoint(layout), viewport, session, draftSystem);

        Assert.Equal(ResearchDraftInteractionOutcome.Consumed, switchOutcome);
        Assert.Equal(ResearchDraftInteractionOutcome.BranchPlaced, placeOutcome);
        Assert.False(draftSystem.HasPendingDraft);
        Assert.Equal(1 + draft.Branches[1].Count, session.SkillTree.Count);
    }

    [Fact]
    public void HandlePointerUp_SelectedPlacedNodeCanBeUnlockedFromTheBranchColumn()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        var root = Assert.IsType<BinarySkillNode>(session.SkillTree.Root);
        var featureTree = Assert.IsType<FeatureTree>(session.GetFeatureTree("B1"));
        var node = session.SkillTree.AddLeftChild(root, session.SkillTree.IntakeSkillNode(featureTree.Root!, GridPoint.Zero, featureTree.Name));
        session.SkillTree.SetNodeLocation(node, new GridPoint(1, 0));
        var miningPost = Assert.Single(Assert.IsType<TriloGame.Game.Core.World.Cave>(session.Cave).GetMiningPosts());
        Assert.Equal(100, miningPost.Deposit("Sandstone", 100));

        var selectOutcome = controller.HandlePointerUp(GetTreeNodePoint(layout, new GridPoint(1, 0)), viewport, session, draftSystem);
        var buttonLayout = ResearchDraftController.CalculateNodeInfoPanelLayout(
            layout.BranchColumnBounds,
            ResearchNodeHoverPlacement.BranchColumn,
            hasActionStatus: true,
            hasUnlockButton: true);
        var unlockOutcome = controller.HandlePointerUp(GetCenter(buttonLayout.UnlockButtonBounds!.Value), viewport, session, draftSystem);

        Assert.Equal(ResearchDraftInteractionOutcome.Consumed, selectOutcome);
        Assert.Equal(ResearchDraftInteractionOutcome.Consumed, unlockOutcome);
        Assert.True(node.IsUnlocked);
        Assert.Equal(0, session.GetStoredResourceTotal("Sandstone"));
        Assert.Equal(0, miningPost.GetInventory()["Sandstone"]);
    }

    private static Point GetCenter(Rectangle bounds)
    {
        return new Point(bounds.Center.X, bounds.Center.Y);
    }

    private static Point GetRootAnchorPoint(ResearchDraftLayoutInfo layout)
    {
        const int sidePadding = 12;
        const int topPadding = 8;
        const int bottomPadding = 12;
        const int scrollbarGap = 10;
        const int scrollbarWidth = 6;

        var contentBounds = new Rectangle(
            layout.TreeViewportBounds.X + sidePadding,
            layout.TreeViewportBounds.Y + topPadding,
            Math.Max(120, layout.TreeViewportBounds.Width - (sidePadding * 2) - scrollbarGap - scrollbarWidth),
            Math.Max(120, layout.TreeViewportBounds.Height - topPadding - bottomPadding));
        var stepX = Math.Clamp(
            (contentBounds.Width - 24f) / Math.Max(1f, TriloGame.Game.Core.Progression.SkillTree.MaxLateralDifference * 2f),
            18f,
            56f);
        var nodeRadius = Math.Clamp((int)MathF.Round(stepX * 0.22f), 7, 14);

        return new Point(
            contentBounds.Center.X,
            contentBounds.Bottom - nodeRadius - 4);
    }

    private static Point GetTreeNodePoint(ResearchDraftLayoutInfo layout, GridPoint location)
    {
        const int sidePadding = 12;
        const int topPadding = 8;
        const int bottomPadding = 12;
        const int scrollbarGap = 10;
        const int scrollbarWidth = 6;

        var contentBounds = new Rectangle(
            layout.TreeViewportBounds.X + sidePadding,
            layout.TreeViewportBounds.Y + topPadding,
            Math.Max(120, layout.TreeViewportBounds.Width - (sidePadding * 2) - scrollbarGap - scrollbarWidth),
            Math.Max(120, layout.TreeViewportBounds.Height - topPadding - bottomPadding));
        var stepX = Math.Clamp(
            (contentBounds.Width - 24f) / Math.Max(1f, TriloGame.Game.Core.Progression.SkillTree.MaxLateralDifference * 2f),
            18f,
            56f);
        var stepY = stepX * ResearchDraftController.TreeStepYRatio;
        var nodeRadius = Math.Clamp((int)MathF.Round(stepX * 0.22f), 7, 14);
        var origin = new Vector2(
            contentBounds.Center.X,
            contentBounds.Bottom - nodeRadius - 4f);
        var point = ResearchDraftController.GetTreePoint(origin, stepX, stepY, location);
        return new Point((int)MathF.Round(point.X), (int)MathF.Round(point.Y));
    }
}
