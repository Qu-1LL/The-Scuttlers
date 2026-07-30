using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchDraftControllerTests
{
    [Fact]
    public void BuildNodeHoverInfo_UsesFeatureTreeAndAffectedCategoriesWhenNoDescriptorsExist()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var node = new TreeInstanceNode(new SkillNode("Tooltip Node", "Tooltip description."), "B1");

        var hoverInfo = ResearchDraftController.BuildNodeHoverInfo(session, node);

        Assert.Equal("Tooltip Node", hoverInfo.TitleText);
        Assert.Equal("B1", hoverInfo.FeatureTreeText);
        Assert.Equal("Building", hoverInfo.EffectText);
    }

    [Fact]
    public void BuildNodeAffectText_FallsBackToDescriptionWhenNoFeatureTreeDataExists()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var node = new TreeInstanceNode(new SkillNode("Core Anchor", "Root anchor."));

        var affectText = ResearchDraftController.BuildNodeAffectText(session, node);

        Assert.Equal("Root anchor.", affectText);
    }

    [Fact]
    public void ResolveHoverPlacement_UsesSingleInfoPanelForTreeAndBranchHoverTargets()
    {
        Assert.Equal(
            ResearchNodeHoverPlacement.InfoPanel,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: true, hasSkillTreeHover: true, hasBranchHover: false));
        Assert.Equal(
            ResearchNodeHoverPlacement.InfoPanel,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: true, hasSkillTreeHover: true, hasBranchHover: true));
        Assert.Equal(
            ResearchNodeHoverPlacement.InfoPanel,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: false, hasSkillTreeHover: true, hasBranchHover: false));
        Assert.Equal(
            ResearchNodeHoverPlacement.InfoPanel,
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: false, hasSkillTreeHover: false, hasBranchHover: true));
        Assert.Null(
            ResearchDraftController.ResolveHoverPlacement(hasPendingDraft: false, hasSkillTreeHover: false, hasBranchHover: false));
    }

    [Fact]
    public void GetSkillTreeConnectorColor_DistinguishesLockedAvailableAndUnlockedNodes()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var root = new TreeInstanceNode(new SkillNode("Root", "Root"));
        var child = new TreeInstanceNode(new SkillNode("Child", "Child"));
        root.AddChild(child);

        var lockedColor = ResearchDraftController.GetSkillTreeConnectorColor(child);
        Assert.Equal(new Color(126, 141, 150, 64), lockedColor);

        Assert.True(root.TryUnlock(session));
        var availableColor = ResearchDraftController.GetSkillTreeConnectorColor(child);
        Assert.Equal(new Color(194, 225, 235, 210), availableColor);

        Assert.True(child.TryUnlock(session));

        var unlockedColor = ResearchDraftController.GetSkillTreeConnectorColor(child);
        Assert.Equal(new Color(247, 221, 92), unlockedColor);
    }

    [Fact]
    public void GetFeatureTreePrerequisiteSkillNames_UsesAuthoredFeatureTreeChainNotInstanceParents()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var featureTree = Assert.IsType<FeatureTree>(session.GetFeatureTree("B1"));
        var hovered = new TreeInstanceNode(
            Assert.IsType<SkillNode>(featureTree.FindByName("B1-e")),
            featureTree.Name);
        var unrelatedAnchor = new TreeInstanceNode(new SkillNode("Run Anchor", "Placed elsewhere."));
        unrelatedAnchor.AddChild(hovered);

        var prerequisiteNames = ResearchDraftController.GetFeatureTreePrerequisiteSkillNames(hovered);

        Assert.Equal(["B1-d", "B1-c", "B1-b", "B1-a"], prerequisiteNames);
        Assert.DoesNotContain("Run Anchor", prerequisiteNames);
    }

    [Fact]
    public void CalculateCardTreeLayout_ScalesDraftBranchesToUseMostOfTheCardBounds()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root"), "B1"));
        var left = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Left", "Left"), "B1"), childIndex: 0);
        var right = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Right", "Right"), "B1"));
        branch.AddChild(right, new TreeInstanceNode(new SkillNode("Right Deep", "Right Deep"), "B1"));
        branch.AddChild(left, new TreeInstanceNode(new SkillNode("Left Deep", "Left Deep"), "B1"));

        var bounds = new Rectangle(0, 0, 240, 180);
        var layout = ResearchTreeUiRenderer.CalculateCardTreeLayout(
            ResearchTreeViewNode.FromResearchBranch(branch),
            bounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);
        var points = layout.Nodes.Select(node => node.Position).ToList();

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
        Assert.True(rightEdge - leftEdge >= bounds.Width * 0.35f);
        Assert.True(bottomEdge - topEdge >= bounds.Height * 0.35f);
    }

    [Fact]
    public void UniversalTreeLayout_SpacesChildrenEvenlyInsideThePlusMinusNinetyDegreeRange()
    {
        Assert.Equal(0f, UniversalTreeLayout.GetChildAngleDegrees(0, 1));
        Assert.Equal(-30f, UniversalTreeLayout.GetChildAngleDegrees(0, 2));
        Assert.Equal(30f, UniversalTreeLayout.GetChildAngleDegrees(1, 2));
        Assert.Equal(-45f, UniversalTreeLayout.GetChildAngleDegrees(0, 3));
        Assert.Equal(0f, UniversalTreeLayout.GetChildAngleDegrees(1, 3));
        Assert.Equal(45f, UniversalTreeLayout.GetChildAngleDegrees(2, 3));
    }

    [Fact]
    public void BuildProjectedPlacementLayout_UsesPostPlacementOrientationWithoutMutatingTheTrees()
    {
        const float edgeLength = 80f;
        var skillTree = CreateRootOnlySkillTree();
        var root = skillTree.Root!;
        skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Left", "Left branch."), "B1"));
        skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Right", "Right branch."), "C1"));

        var branch = new ResearchBranch();
        var branchRoot = branch.SetRoot(new TreeInstanceNode(new SkillNode("Preview Root", "Preview root."), "F1"));
        branch.AddChild(branchRoot, new TreeInstanceNode(new SkillNode("Preview Child", "Preview child."), "F1"));

        var projected = ResearchDraftController.BuildProjectedPlacementLayout(
            Vector2.Zero,
            edgeLength,
            Vector2.Zero,
            root,
            branch,
            root);

        Assert.Equal(2, root.ChildCount);
        Assert.Null(branchRoot.Parent);

        var projectedRoot = projected.Nodes.Single(node => ReferenceEquals(node.SkillNode, root) && !node.IsBranchNode);
        var projectedBranchRoot = projected.Nodes.Single(node => ReferenceEquals(node.SkillNode, branchRoot) && node.IsBranchNode);
        var expectedDirection = UniversalTreeLayout.DegreesToUnitVector(-45f);
        var expectedPosition = projectedRoot.Position + (expectedDirection * edgeLength);

        Assert.Same(projectedRoot, projectedBranchRoot.Parent);
        Assert.InRange(Vector2.Distance(projectedBranchRoot.Position, expectedPosition), 0f, 0.001f);
    }

    [Fact]
    public void InterpolateProjectedPlacementLayout_MovesMatchingNodesAndKeepsTargetParentage()
    {
        const float edgeLength = 80f;
        var skillTree = CreateRootOnlySkillTree();
        var root = skillTree.Root!;
        var child = skillTree.AddChild(
            root,
            skillTree.IntakeSkillNode(new SkillNode("Placed Child", "Placed child."), "B1"));
        var branch = new ResearchBranch();
        var branchRoot = branch.SetRoot(
            new TreeInstanceNode(new SkillNode("Preview Root", "Preview root."), "F1"));

        var start = ResearchDraftController.BuildProjectedPlacementLayout(
            Vector2.Zero,
            edgeLength,
            Vector2.Zero,
            root,
            branch,
            root);
        var target = ResearchDraftController.BuildProjectedPlacementLayout(
            Vector2.Zero,
            edgeLength,
            Vector2.Zero,
            root,
            branch,
            child);

        var interpolated = ResearchDraftController.InterpolateProjectedPlacementLayout(start, target, 0.5f);
        var startBranchNode = start.Nodes.Single(node => node.IsBranchNode && ReferenceEquals(node.SkillNode, branchRoot));
        var targetBranchNode = target.Nodes.Single(node => node.IsBranchNode && ReferenceEquals(node.SkillNode, branchRoot));
        var interpolatedBranchNode = interpolated.Nodes.Single(node => node.IsBranchNode && ReferenceEquals(node.SkillNode, branchRoot));

        Assert.InRange(
            Vector2.Distance(
                interpolatedBranchNode.Position,
                Vector2.Lerp(startBranchNode.Position, targetBranchNode.Position, 0.5f)),
            0f,
            0.001f);
        Assert.NotNull(interpolatedBranchNode.Parent);
        Assert.Same(child, interpolatedBranchNode.Parent!.SkillNode);
        Assert.False(interpolatedBranchNode.Parent.IsBranchNode);
    }

    [Fact]
    public void BuildVisibleTreeContentBounds_UsesThePlacedTreeExtentsInsteadOfOnlyTheCoreNode()
    {
        var viewport = new Rectangle(100, 80, 620, 420);
        var rootOnlyTree = CreateRootOnlySkillTree();
        var expandedTree = CreateWideSkillTree();

        var rootOnlyBounds = ResearchTreeViewportState.BuildVisibleContentBounds(viewport, rootOnlyTree);
        var expandedBounds = ResearchTreeViewportState.BuildVisibleContentBounds(viewport, expandedTree);

        Assert.True(expandedBounds.MinX < rootOnlyBounds.MinX - 20f);
        Assert.True(expandedBounds.MaxX > rootOnlyBounds.MaxX + 20f);
        Assert.True(expandedBounds.MinY < rootOnlyBounds.MinY - 20f);
        Assert.True(expandedBounds.Width > rootOnlyBounds.Width);
        Assert.True(expandedBounds.Height > rootOnlyBounds.Height);
    }

    [Fact]
    public void ResolveTreePanAfterRelease_KeepsPanWhenTheTreeContentBoundsAreStillVisible()
    {
        var viewport = new Rectangle(100, 80, 620, 420);
        var skillTree = CreateWideSkillTree();
        var panOffset = new Vector2(36f, -24f);

        var resolved = ResearchTreeViewportState.ResolvePanAfterRelease(viewport, skillTree, panOffset, zoom: 1f);

        Assert.Equal(panOffset, resolved);
    }

    [Fact]
    public void ResolveTreePanAfterRelease_SnapsBackUsingTheFullTreeContentBounds()
    {
        var viewport = new Rectangle(100, 80, 620, 420);
        var skillTree = CreateWideSkillTree();
        var farOutsidePan = new Vector2(-10000f, 7200f);

        var resolved = ResearchTreeViewportState.ResolvePanAfterRelease(viewport, skillTree, farOutsidePan, zoom: 1f);
        var baseBounds = ResearchTreeViewportState.BuildVisibleContentBounds(viewport, skillTree);
        var pannedCenter = new Vector2(
            ((baseBounds.MinX + baseBounds.MaxX) * 0.5f) + resolved.X,
            ((baseBounds.MinY + baseBounds.MaxY) * 0.5f) + resolved.Y);

        Assert.NotEqual(farOutsidePan, resolved);
        Assert.InRange(pannedCenter.X, viewport.Center.X - 40f, viewport.Center.X + 40f);
        Assert.InRange(pannedCenter.Y, viewport.Center.Y - 40f, viewport.Center.Y + 40f);
    }

    [Fact]
    public void HandlePointerDrag_DoesNotPanAdaptationTree()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        controller.Open(draftSystem);

        controller.HandlePointerDown(layout.TreeViewportBounds.Center, viewport, session, draftSystem);
        controller.HandlePointerDrag(layout.TreeViewportBounds.Center + new Point(80, -40), viewport, session, draftSystem);

        Assert.Equal(Vector2.Zero, controller.TreePanOffset);
    }

    [Fact]
    public void HandlePanPointerDrag_PansAdaptationTree()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        controller.Open(draftSystem);

        Assert.True(controller.HandlePanPointerDown(layout.TreeViewportBounds.Center, viewport, session, draftSystem));
        controller.HandlePanPointerDrag(layout.TreeViewportBounds.Center + new Point(80, -40));

        Assert.Equal(new Vector2(80f, -40f), controller.TreePanOffset);
    }

    [Fact]
    public void BuildDraftMenuModel_UsesSharedTreeViewportRootAndBoundaryOverlayForAdaptationTree()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        controller.Open(draftSystem);

        var model = controller.BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture: null);

        Assert.NotNull(model.TreeViewport.Root);
        Assert.NotNull(model.TreeViewport.DrawOverlay);
        Assert.False(model.TreeViewport.OverlayReplacesTreeContent);
        Assert.False(model.Config.EnablePlacementPreview);
    }

    [Fact]
    public void BuildDraftMenuModel_UsesPlacementOverlayInsteadOfReplacingTreeViewport()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        draftSystem.CreateDraft(session, round);
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        controller.Open(draftSystem);

        controller.HandlePointerUp(GetCenter(layout.BranchCardBounds[0]), viewport, session, draftSystem);
        var model = controller.BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture: null);

        Assert.NotNull(model.TreeViewport.Root);
        Assert.NotNull(model.TreeViewport.DrawOverlay);
        Assert.False(model.TreeViewport.OverlayReplacesTreeContent);
        Assert.True(model.Config.EnablePlacementPreview);
    }

    [Fact]
    public void BuildDraftMenuModel_ReplacesTreeContentWhenPlacementPreviewIsAnchored()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        draftSystem.CreateDraft(session, round);
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        controller.Open(draftSystem);

        controller.HandlePointerUp(GetCenter(layout.BranchCardBounds[0]), viewport, session, draftSystem);
        controller.UpdatePointer(layout.TreeViewportBounds.Center);
        var model = controller.BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture: null);

        Assert.NotNull(model.TreeViewport.Root);
        Assert.NotNull(model.TreeViewport.DrawOverlay);
        Assert.True(model.TreeViewport.OverlayReplacesTreeContent);
    }

    [Fact]
    public void ClampTreeZoom_AllowsArbitrarilySmallPositiveValuesAndCapsZoomIn()
    {
        Assert.Equal(0.000001f, ResearchTreeViewportState.ClampZoom(0.000001f));
        Assert.Equal(1.25f, ResearchTreeViewportState.ClampZoom(1.25f));
        Assert.Equal(2.25f, ResearchTreeViewportState.ClampZoom(9f));
    }

    [Fact]
    public void CalculateZoomAfterWheel_AllowsRepeatedZoomOutWithoutAConfiguredMinimum()
    {
        var zoom = 1f;
        for (var index = 0; index < 40; index++)
        {
            zoom = ResearchTreeViewportState.CalculateZoomAfterWheel(zoom, wheelDelta: 90);
        }

        Assert.InRange(zoom, float.Epsilon, 0.01f);
    }

    [Fact]
    public void BuildTreeViewportMetrics_ScalesNodeRadiusAcrossZoomLevels()
    {
        var bounds = new Rectangle(100, 80, 620, 420);
        var skillTree = CreateWideSkillTree();

        var zoomedOut = ResearchTreeViewportState.BuildMetrics(bounds, skillTree, zoom: 0.05f);
        var normal = ResearchTreeViewportState.BuildMetrics(bounds, skillTree, zoom: 1f);
        var zoomedIn = ResearchTreeViewportState.BuildMetrics(bounds, skillTree, zoom: 2.25f);

        Assert.True(zoomedOut.NodeRadius < normal.NodeRadius);
        Assert.True(normal.NodeRadius < zoomedIn.NodeRadius);
        Assert.True(zoomedOut.EdgeLength < normal.EdgeLength);
        Assert.True(zoomedIn.EdgeLength > normal.EdgeLength);
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
    public void HandlePointerUp_ClickingBranchNameRequestsFullTreePreview()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        var draft = Assert.IsType<ResearchDraftOffer>(draftSystem.CreateDraft(session, round));
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        var cardLayout = ResearchTreeCardRenderer.BuildLayout(layout.BranchCardBounds[0]);

        var outcome = controller.HandlePointerUp(cardLayout.TitleBounds.Center, viewport, session, draftSystem);
        var hasRequest = controller.TryTakeBranchPreviewRequest(out var branch, out var title);

        Assert.Equal(ResearchDraftInteractionOutcome.RequestedBranchPreview, outcome);
        Assert.True(hasRequest);
        Assert.Same(draft.Branches[0], branch);
        Assert.Equal(draft.Branches[0].Name, title);
        Assert.True(draftSystem.HasPendingDraft);
    }

    [Fact]
    public void HandlePointerUp_ClickingInsideCardTreeKeepsDraftSelectionBehavior()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        draftSystem.CreateDraft(session, round);
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        var cardLayout = ResearchTreeCardRenderer.BuildLayout(layout.BranchCardBounds[0]);

        var outcome = controller.HandlePointerUp(cardLayout.PreviewBounds.Center, viewport, session, draftSystem);
        var hasRequest = controller.TryTakeBranchPreviewRequest(out _, out _);

        Assert.Equal(ResearchDraftInteractionOutcome.Consumed, outcome);
        Assert.False(hasRequest);
        Assert.True(controller.BuildDraftMenuModel(layout, session, draftSystem, null).Cards[0].IsSelected);
    }

    [Fact]
    public void HandleSecondaryClick_DeselectsSelectedBranch()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var draftSystem = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        draftSystem.CreateDraft(session, round);
        var controller = new ResearchDraftController();
        controller.Open(draftSystem);
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);
        var previewBounds = ResearchTreeCardRenderer.BuildLayout(layout.BranchCardBounds[0]).PreviewBounds;
        controller.HandlePointerUp(previewBounds.Center, viewport, session, draftSystem);

        var handled = controller.HandleSecondaryClick(draftSystem);
        var model = controller.BuildDraftMenuModel(layout, session, draftSystem, null);

        Assert.True(handled);
        Assert.All(model.Cards, card => Assert.False(card.IsSelected));
    }

    [Fact]
    public void HandleSecondaryClick_WithoutSelectedBranchDoesNothing()
    {
        var controller = new ResearchDraftController();
        var draftSystem = new ResearchDraftSystem();
        controller.Open(draftSystem);

        Assert.False(controller.HandleSecondaryClick(draftSystem));
    }

    [Fact]
    public void ObstructedBranchPlacement_UsesDedicatedInteractionOutcome()
    {
        Assert.Equal(
            ResearchDraftInteractionOutcome.BranchPlacementObstructed,
            ResearchDraftController.GetRejectedPlacementOutcome(hasCollision: true));
        Assert.Equal(
            ResearchDraftInteractionOutcome.Consumed,
            ResearchDraftController.GetRejectedPlacementOutcome(hasCollision: false));
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
    public void HandlePointerUp_ClickingSkillTreeNodePinsInfoPanelAndUnlockAction()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var child = session.SkillTree.AddChild(
            session.SkillTree.Root!,
            session.SkillTree.IntakeSkillNode(new SkillNode("Pinned Node", "Pinned description."), "B1"));
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport, branchCardCount: 0);
        controller.Open(draftSystem);

        var outcome = controller.HandlePointerUp(GetFirstChildPoint(layout), viewport, session, draftSystem);
        var model = controller.BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture: null);

        Assert.Equal(ResearchDraftInteractionOutcome.NodeSelected, outcome);
        Assert.Equal(child.Name, model.InfoPanel.NodeInfo!.Value.TitleText);
        Assert.NotNull(model.InfoPanel.UnlockAction);
        Assert.Equal(40, model.InfoPanel.UnlockAction!.Value.Cost);
        Assert.False(model.InfoPanel.UnlockAction.Value.CanUnlock);
        Assert.Equal(SkillTreeUnlockBlockReason.NotEnoughResources, model.InfoPanel.UnlockAction.Value.BlockReason);
    }

    [Fact]
    public void BuildDraftMenuModel_ShowsRockCategoryTotalAcrossMixedStorageResources()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var cave = session.Cave!;
        session.SkillTree.AddChild(
            session.SkillTree.Root!,
            session.SkillTree.IntakeSkillNode(new SkillNode("Unlockable Node", "Unlockable description."), "B1"));

        var post = new MiningPost(session);
        var postLocation = TestWorldFactory.FindBuildLocation(cave, post);
        Assert.True(cave.Build(post, postLocation));
        var storage = new Storage(session);
        var storageLocation = TestWorldFactory.FindBuildLocation(cave, storage);
        Assert.True(cave.Build(storage, storageLocation));
        Assert.Equal(7, post.Deposit(ResourceName.Sandstone, 7));
        Assert.Equal(3, storage.Deposit(ResourceName.Malachite, 3));

        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport, branchCardCount: 0);
        controller.Open(draftSystem);
        controller.HandlePointerUp(GetFirstChildPoint(layout), viewport, session, draftSystem);

        var model = controller.BuildDraftMenuModel(layout, session, draftSystem, treeBackgroundTexture: null);

        Assert.NotNull(model.InfoPanel.UnlockAction);
        Assert.Equal(10, model.InfoPanel.UnlockAction!.Value.Available);
        Assert.Equal("Rock", model.InfoPanel.UnlockAction.Value.ResourceType);
    }

    [Fact]
    public void HandlePointerUp_ClickingEnabledUnlockButtonUnlocksSelectedNode()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var child = session.SkillTree.AddChild(
            session.SkillTree.Root!,
            session.SkillTree.IntakeSkillNode(new SkillNode("Unlockable Node", "Unlockable description."), "B1"));
        DepositRock(session, 40);
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport, branchCardCount: 0);
        controller.Open(draftSystem);
        controller.HandlePointerUp(GetFirstChildPoint(layout), viewport, session, draftSystem);

        var outcome = controller.HandlePointerUp(
            ResearchTreeInfoPanelLayout.GetUnlockButtonBounds(layout.InfoPanelBounds).Center,
            viewport,
            session,
            draftSystem);

        Assert.Equal(ResearchDraftInteractionOutcome.NodeUnlocked, outcome);
        Assert.True(child.IsUnlocked);
        Assert.Equal(0, ResourceStockpileSystem.GetStoredAmount(session, ResourceCategory.Rock));
    }

    [Fact]
    public void Draw_SelectedSkillTreeNodeUsesDoubleCyanHalo()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        session.SkillTree.AddChild(
            session.SkillTree.Root!,
            session.SkillTree.IntakeSkillNode(new SkillNode("Selected Node", "Selected description."), "B1"));
        var draftSystem = new ResearchDraftSystem();
        var controller = new ResearchDraftController();
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport, branchCardCount: 0);
        controller.Open(draftSystem);
        controller.HandlePointerUp(GetFirstChildPoint(layout), viewport, session, draftSystem);
        controller.UpdatePointer(layout.InfoPanelBounds.Center);
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(viewport);

        controller.Draw(viewport, session, draftSystem, gumUi, treeBackgroundTexture: null);

        var cyanOutlines = gumUi.Root.Children
            .OfType<RectangleRuntime>()
            .Where(shape =>
                !shape.IsFilled &&
                shape.StrokeColor.R == 105 &&
                shape.StrokeColor.G == 226 &&
                shape.StrokeColor.B == 239)
            .ToArray();
        Assert.Equal(2, cyanOutlines.Length);
    }

    private static Point GetCenter(Rectangle bounds)
    {
        return new Point(bounds.Center.X, bounds.Center.Y);
    }

    private static Point GetRootAnchorPoint(ResearchDraftLayoutInfo layout)
    {
        var contentBounds = layout.TreeViewportBounds;
        var nodeRadius = Math.Clamp((int)MathF.Round(92f * 0.18f), 9, 18);

        return new Point(
            contentBounds.Center.X,
            contentBounds.Bottom - nodeRadius - 8);
    }

    private static Point GetFirstChildPoint(ResearchDraftLayoutInfo layout)
    {
        var root = GetRootAnchorPoint(layout);
        return new Point(root.X, root.Y - 92);
    }

    private static void DepositRock(TriloGame.Game.Core.Simulation.GameSession session, int amount)
    {
        var cave = session.Cave ?? new TriloGame.Game.Core.World.Cave(session);
        TestWorldFactory.ResetToRectangularMap(cave, 8, 8);
        var post = new MiningPost(session);
        Assert.True(cave.Build(post, new GridPoint(0, 0)));
        Assert.Equal(amount, post.Deposit(ResourceName.Sandstone, amount));
    }

    private static SkillTree CreateRootOnlySkillTree()
    {
        var skillTree = new SkillTree();
        skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Hive Core", "Root anchor.")));
        return skillTree;
    }

    private static SkillTree CreateWideSkillTree()
    {
        var skillTree = CreateRootOnlySkillTree();
        var root = skillTree.Root!;
        var left = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Left", "Left branch."), "B1"), childIndex: 0);
        var upperLeft = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Upper Left", "Upper left branch."), "C1"), childIndex: 1);
        var upperRight = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Upper Right", "Upper right branch."), "F1"), childIndex: 2);
        var right = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Right", "Right branch."), "M1"), childIndex: 3);

        skillTree.AddChild(left, skillTree.IntakeSkillNode(new SkillNode("Left Deep", "Left deep branch."), "B2"));
        skillTree.AddChild(upperLeft, skillTree.IntakeSkillNode(new SkillNode("Upper Deep", "Upper deep branch."), "C2"));
        skillTree.AddChild(upperRight, skillTree.IntakeSkillNode(new SkillNode("Upper Right Deep", "Upper right deep branch."), "F2"));
        skillTree.AddChild(right, skillTree.IntakeSkillNode(new SkillNode("Right Deep", "Right deep branch."), "M2"));

        return skillTree;
    }
}
