using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
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
    public void GetSkillTreeConnectorColor_UsesWhiteForLockedNodesAndYellowForUnlockedNodes()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var root = new TreeInstanceNode(new SkillNode("Root", "Root"));
        var child = new TreeInstanceNode(new SkillNode("Child", "Child"));
        root.AddChild(child);

        var lockedColor = ResearchDraftController.GetSkillTreeConnectorColor(child);
        Assert.Equal(new Color(246, 251, 253), lockedColor);

        Assert.True(root.TryUnlock(session));
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
    public void CalculateBranchCardPreviewLayout_ScalesBranchesToUseMostOfThePreviewBounds()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root"), "B1"));
        var left = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Left", "Left"), "B1"), childIndex: 0);
        var right = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Right", "Right"), "B1"));
        branch.AddChild(right, new TreeInstanceNode(new SkillNode("Right Deep", "Right Deep"), "B1"));
        branch.AddChild(left, new TreeInstanceNode(new SkillNode("Left Deep", "Left Deep"), "B1"));

        var bounds = new Rectangle(0, 0, 240, 180);
        var layout = ResearchDraftController.CalculateBranchCardPreviewLayout(branch, bounds);
        var points = new List<Vector2> { layout.OriginPoint };
        points.AddRange(layout.Nodes.Select(node => node.Position));

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
    public void BuildVisibleTreeContentBounds_UsesThePlacedTreeExtentsInsteadOfOnlyTheCoreNode()
    {
        var viewport = new Rectangle(100, 80, 620, 420);
        var rootOnlyTree = CreateRootOnlySkillTree();
        var expandedTree = CreateWideSkillTree();

        var rootOnlyBounds = ResearchDraftController.BuildVisibleTreeContentBounds(viewport, rootOnlyTree);
        var expandedBounds = ResearchDraftController.BuildVisibleTreeContentBounds(viewport, expandedTree);

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

        var resolved = ResearchDraftController.ResolveTreePanAfterRelease(viewport, skillTree, panOffset, zoom: 1f);

        Assert.Equal(panOffset, resolved);
    }

    [Fact]
    public void ResolveTreePanAfterRelease_SnapsBackUsingTheFullTreeContentBounds()
    {
        var viewport = new Rectangle(100, 80, 620, 420);
        var skillTree = CreateWideSkillTree();
        var farOutsidePan = new Vector2(-10000f, 7200f);

        var resolved = ResearchDraftController.ResolveTreePanAfterRelease(viewport, skillTree, farOutsidePan, zoom: 1f);
        var baseBounds = ResearchDraftController.BuildVisibleTreeContentBounds(viewport, skillTree);
        var pannedCenter = new Vector2(
            ((baseBounds.MinX + baseBounds.MaxX) * 0.5f) + resolved.X,
            ((baseBounds.MinY + baseBounds.MaxY) * 0.5f) + resolved.Y);

        Assert.NotEqual(farOutsidePan, resolved);
        Assert.InRange(pannedCenter.X, viewport.Center.X - 40f, viewport.Center.X + 40f);
        Assert.InRange(pannedCenter.Y, viewport.Center.Y - 40f, viewport.Center.Y + 40f);
    }

    [Fact]
    public void ClampTreeZoom_StaysWithinSensibleLimits()
    {
        Assert.Equal(0.55f, ResearchDraftController.ClampTreeZoom(0.1f));
        Assert.Equal(1.25f, ResearchDraftController.ClampTreeZoom(1.25f));
        Assert.Equal(2.25f, ResearchDraftController.ClampTreeZoom(9f));
    }

    [Fact]
    public void CalculateTreeBackgroundStartCoordinate_AnchorsTilesToTheTreeSurfaceOrigin()
    {
        const int viewportLeft = 100;
        const float treeSurfaceOrigin = 421f;

        var normalZoomStart = ResearchDraftController.CalculateTreeBackgroundStartCoordinate(viewportLeft, treeSurfaceOrigin, tileLength: 64);
        var zoomedInStart = ResearchDraftController.CalculateTreeBackgroundStartCoordinate(viewportLeft, treeSurfaceOrigin, tileLength: 96);

        Assert.True(normalZoomStart <= viewportLeft);
        Assert.True(zoomedInStart <= viewportLeft);
        Assert.True(viewportLeft - normalZoomStart < 64);
        Assert.True(viewportLeft - zoomedInStart < 96);
        Assert.Equal(0, ((int)treeSurfaceOrigin - normalZoomStart) % 64);
        Assert.Equal(0, ((int)treeSurfaceOrigin - zoomedInStart) % 96);
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

    private static Point GetCenter(Rectangle bounds)
    {
        return new Point(bounds.Center.X, bounds.Center.Y);
    }

    private static Point GetRootAnchorPoint(ResearchDraftLayoutInfo layout)
    {
        const int sidePadding = 12;
        const int topPadding = 8;
        const int bottomPadding = 12;

        var contentBounds = new Rectangle(
            layout.TreeViewportBounds.X + sidePadding,
            layout.TreeViewportBounds.Y + topPadding,
            Math.Max(120, layout.TreeViewportBounds.Width - (sidePadding * 2)),
            Math.Max(120, layout.TreeViewportBounds.Height - topPadding - bottomPadding));
        var nodeRadius = Math.Clamp((int)MathF.Round(76f * 0.18f), 9, 18);

        return new Point(
            contentBounds.Center.X,
            contentBounds.Bottom - nodeRadius - 8);
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
