using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Progression;

public sealed class SkillTreeTests
{
    [Fact]
    public void IntakeSkillNode_CreatesDetachedBinaryCopy()
    {
        var descriptor = new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddPercent, 0.1);
        var template = new SkillNode("Dig Sprint", "Move faster while mining.", [descriptor]);
        var skillTree = new SkillTree();

        var binaryNode = skillTree.IntakeSkillNode(template, "Mobility");

        Assert.Same(template, binaryNode.SourceSkillNode);
        Assert.Equal("Dig Sprint", binaryNode.Name);
        Assert.Equal("Move faster while mining.", binaryNode.Description);
        Assert.Equal("Mobility", binaryNode.SourceFeatureTreeName);
        Assert.Equal(GridPoint.Zero, binaryNode.NodeDelta);
        Assert.False(binaryNode.HasNodeLocation);
        Assert.Null(binaryNode.NodeLocation);
        Assert.True(binaryNode.IsLocked);
        Assert.False(binaryNode.IsUnlocked);
        Assert.Null(binaryNode.Parent);
        Assert.Null(binaryNode.Left);
        Assert.Null(binaryNode.Right);
        Assert.Single(binaryNode.EffectDescriptors);
        Assert.Equal(descriptor, binaryNode.EffectDescriptors[0]);
    }

    [Fact]
    public void IntakeSkillNode_WithDelta_CapturesRelativeGridOffset()
    {
        var template = new SkillNode("Dig Sprint", "Move faster while mining.");
        var skillTree = new SkillTree();

        var binaryNode = skillTree.IntakeSkillNode(template, new GridPoint(2, 1), "Mobility");

        Assert.Equal(new GridPoint(2, 1), binaryNode.NodeDelta);
        Assert.False(binaryNode.HasNodeLocation);
        Assert.Null(binaryNode.NodeLocation);
    }

    [Fact]
    public void ImportingSkills_CreatesBinaryChildrenFromDifferentFeatureTrees()
    {
        var mobilityTemplate = new SkillNode(
            "Dig Sprint",
            "Move faster while mining.",
            [new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddPercent, 0.1)]);
        var economyTemplate = new SkillNode(
            "Packed Haul",
            "Carry more resources.",
            [new ResearchEffectDescriptor("Trilobite.InventoryCapacity", ResearchOperation.AddFlat, 2)]);
        var surveyTemplate = new SkillNode(
            "Stone Sense",
            "Reveal useful stone seams.",
            [new ResearchEffectDescriptor("Cave.RevealRadius", ResearchOperation.AddFlat, 1)]);

        var dex = new TriloDex(
        [
            new FeatureTree("Mobility", "Movement upgrades.", ["movement"], 1, [], mobilityTemplate),
            new FeatureTree("Economy", "Resource upgrades.", ["resources"], 1, [], economyTemplate),
            new FeatureTree("Survey", "Reveal upgrades.", ["reveal"], 1, [], surveyTemplate)
        ]);

        var skillTree = new SkillTree(dex);

        var rootNode = skillTree.ImportRoot("Mobility", "Dig Sprint");
        var leftNode = skillTree.ImportLeftChild(rootNode!, "Economy", "Packed Haul");
        var rightNode = skillTree.ImportRightChild(rootNode!, "Survey", "Stone Sense");

        Assert.NotNull(rootNode);
        Assert.NotNull(leftNode);
        Assert.NotNull(rightNode);
        Assert.NotSame(mobilityTemplate, rootNode);
        Assert.NotSame(economyTemplate, leftNode);
        Assert.NotSame(surveyTemplate, rightNode);
        Assert.Same(rootNode, skillTree.Root);
        Assert.Same(leftNode, rootNode!.Left);
        Assert.Same(rightNode, rootNode.Right);
        Assert.Same(rootNode, leftNode!.Prerequisite);
        Assert.Same(rootNode, rightNode!.Prerequisite);
        Assert.Equal("Mobility", skillTree.GetSourceFeatureTreeName(rootNode));
        Assert.Equal("Economy", skillTree.GetSourceFeatureTreeName(leftNode));
        Assert.Equal("Survey", skillTree.GetSourceFeatureTreeName(rightNode));
        Assert.Equal(3, skillTree.Count);
    }

    [Fact]
    public void SkillTree_TracksGridLocationsForPlacedNodes()
    {
        var rootTemplate = new SkillNode("Root", "Root node.");
        var childTemplate = new SkillNode("Child", "Child node.");
        var alternateTemplate = new SkillNode("Alternate", "Alternate node.");
        var skillTree = new SkillTree();

        var rootNode = skillTree.IntakeSkillNode(rootTemplate, GridPoint.Zero, "Mobility");
        var childNode = skillTree.IntakeSkillNode(childTemplate, new GridPoint(1, 0), "Mobility");
        var alternateNode = skillTree.IntakeSkillNode(alternateTemplate, new GridPoint(0, 1), "Mobility");

        skillTree.SetRoot(rootNode);
        skillTree.AddLeftChild(rootNode, childNode);
        skillTree.AddRightChild(rootNode, alternateNode);

        skillTree.SetNodeLocation(childNode, new GridPoint(1, 0));
        skillTree.SetNodeLocation(alternateNode, new GridPoint(0, 1));

        Assert.Equal(GridPoint.Zero, rootNode.NodeLocation);
        Assert.Equal(new GridPoint(1, 0), childNode.NodeLocation);
        Assert.Equal(new GridPoint(0, 1), alternateNode.NodeLocation);
        Assert.True(skillTree.IsLocationOccupied(GridPoint.Zero));
        Assert.True(skillTree.IsLocationOccupied(new GridPoint(1, 0)));
        Assert.True(skillTree.IsLocationOccupied(new GridPoint(0, 1)));
        Assert.Same(rootNode, skillTree.FindByLocation(GridPoint.Zero));
        Assert.Same(childNode, skillTree.FindByLocation(new GridPoint(1, 0)));
        Assert.Same(alternateNode, skillTree.FindByLocation(new GridPoint(0, 1)));
        Assert.Equal(new GridPoint(1, 0), skillTree.GetLeftChildLocation(rootNode));
        Assert.Equal(new GridPoint(0, 1), skillTree.GetRightChildLocation(rootNode));
    }

    [Fact]
    public void SkillTree_RejectsDuplicateGridLocations()
    {
        var rootTemplate = new SkillNode("Root", "Root node.");
        var firstTemplate = new SkillNode("First", "First node.");
        var secondTemplate = new SkillNode("Second", "Second node.");
        var skillTree = new SkillTree();

        var rootNode = skillTree.IntakeSkillNode(rootTemplate, GridPoint.Zero, "Mobility");
        var firstNode = skillTree.IntakeSkillNode(firstTemplate, new GridPoint(1, 0), "Mobility");
        var secondNode = skillTree.IntakeSkillNode(secondTemplate, new GridPoint(0, 1), "Mobility");

        skillTree.SetRoot(rootNode);
        skillTree.AddLeftChild(rootNode, firstNode);
        skillTree.SetNodeLocation(firstNode, new GridPoint(1, 0));
        skillTree.AddRightChild(rootNode, secondNode);

        Assert.Throws<InvalidOperationException>(() =>
            skillTree.SetNodeLocation(secondNode, new GridPoint(1, 0)));
    }

    [Fact]
    public void ImportedSkillAcquisition_DoesNotMutateTheFeatureTreeTemplate()
    {
        var template = new SkillNode(
            "Stone Sense",
            "Reveal useful stone seams.",
            [new ResearchEffectDescriptor("Cave.RevealRadius", ResearchOperation.AddFlat, 1)]);
        var dex = new TriloDex(
        [
            new FeatureTree("Survey", "Reveal upgrades.", ["reveal"], 1, [], template)
        ]);
        var skillTree = new SkillTree(dex);
        var session = new GameSession();

        var localNode = skillTree.ImportRoot("Survey", "Stone Sense");

        Assert.NotNull(localNode);
        Assert.True(localNode!.TryUnlock(session));
        Assert.True(localNode.IsUnlocked);
        Assert.False(localNode.IsLocked);
        Assert.True(localNode.IsAcquired);
        Assert.False(template.IsAcquired);
        Assert.Equal(1, skillTree.UnlockedCount);
        Assert.Single(session.GlobalResearch.Descriptors);
        Assert.Equal("Cave.RevealRadius", session.GlobalResearch.Descriptors[0].StatKey);
    }

    [Fact]
    public void GameSessions_HaveDistinctLocalSkillTrees()
    {
        var firstSession = new GameSession();
        var secondSession = new GameSession();

        Assert.NotSame(firstSession.SkillTree, secondSession.SkillTree);
        Assert.Same(firstSession.ProgressionDex, firstSession.SkillTree.SourceDex);
        Assert.Same(secondSession.ProgressionDex, secondSession.SkillTree.SourceDex);
        Assert.True(firstSession.SkillTree.IsEmpty);
        Assert.True(secondSession.SkillTree.IsEmpty);
        Assert.Null(firstSession.SkillTree.Root);
        Assert.Null(secondSession.SkillTree.Root);
    }

    [Fact]
    public void TryPlaceResearchBranch_AttachesBranchToTheSelectedAnchor()
    {
        var rootTemplate = new SkillNode("Hive Core", "Root anchor.");
        var branchRootTemplate = new SkillNode("Branch Root", "Research root.");
        var branchChildTemplate = new SkillNode("Branch Child", "Research child.");
        var skillTree = new SkillTree();

        var root = skillTree.SetRoot(skillTree.IntakeSkillNode(rootTemplate, GridPoint.Zero));
        var branch = new ResearchBranch();
        var branchRoot = branch.SetRoot(new BinarySkillNode(branchRootTemplate, new GridPoint(1, 0), "B1"), new GridPoint(1, 0));
        branch.AddRightChild(branchRoot, new BinarySkillNode(branchChildTemplate, new GridPoint(1, 1), "B1"));

        var placed = skillTree.TryPlaceResearchBranch(branch, GridPoint.Zero, out var failureReason);

        Assert.True(placed);
        Assert.Null(failureReason);
        Assert.Same(branchRoot.Node, root.Left);
        Assert.Equal(new GridPoint(1, 0), branchRoot.Node.NodeLocation);
        Assert.Equal(new GridPoint(1, 1), branchRoot.Right!.Node.NodeLocation);
        Assert.Same(branchRoot.Node, branchRoot.Right.Node.Parent);
        Assert.Same(branchRoot.Right.Node, skillTree.FindByLocation(new GridPoint(1, 1)));
    }

    [Fact]
    public void CanPlaceResearchBranch_RejectsAnchorsThatAlreadyUseTheRequiredEntrySlot()
    {
        var rootTemplate = new SkillNode("Hive Core", "Root anchor.");
        var existingTemplate = new SkillNode("Existing", "Existing child.");
        var branchRootTemplate = new SkillNode("Branch Root", "Research root.");
        var skillTree = new SkillTree();

        var root = skillTree.SetRoot(skillTree.IntakeSkillNode(rootTemplate, GridPoint.Zero));
        var existing = skillTree.AddLeftChild(root, skillTree.IntakeSkillNode(existingTemplate, new GridPoint(1, 0), "B1"));
        skillTree.SetNodeLocation(existing, new GridPoint(1, 0));

        var branch = new ResearchBranch();
        branch.SetRoot(new BinarySkillNode(branchRootTemplate, new GridPoint(1, 0), "C1"), new GridPoint(1, 0));

        var canPlace = skillTree.CanPlaceResearchBranch(branch, GridPoint.Zero, out var failureReason);

        Assert.False(canPlace);
        Assert.Equal("That placement overlaps an existing skill node.", failureReason);
    }

    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(8, 1, true)]
    [InlineData(9, 1, true)]
    [InlineData(9, 0, false)]
    [InlineData(0, 9, false)]
    public void IsValidGridLocation_UsesEightStepLateralBoundary(int x, int y, bool expected)
    {
        Assert.Equal(expected, SkillTree.IsValidGridLocation(new GridPoint(x, y)));
    }

    [Fact]
    public void CanPlaceResearchBranch_RejectsPlacementsOutsideTheUpwardGrowthBoundary()
    {
        var skillTree = new SkillTree();
        var current = skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Hive Core", "Root anchor."), GridPoint.Zero, "Core"));
        for (var x = 1; x <= SkillTree.MaxLateralDifference; x++)
        {
            var next = skillTree.AddLeftChild(
                current,
                skillTree.IntakeSkillNode(new SkillNode($"Left {x}", "Boundary node."), GridPoint.Zero, "Core"));
            skillTree.SetNodeLocation(next, new GridPoint(x, 0));
            current = next;
        }

        var branch = new ResearchBranch();
        branch.SetRoot(new BinarySkillNode(new SkillNode("Branch Root", "Research root."), new GridPoint(1, 0), "B1"), new GridPoint(1, 0));

        var canPlace = skillTree.CanPlaceResearchBranch(branch, new GridPoint(SkillTree.MaxLateralDifference, 0), out var failureReason);

        Assert.False(canPlace);
        Assert.Equal("That placement would move part of the branch outside the skill grid.", failureReason);
    }

    [Fact]
    public void GetNodeUnlockCost_UsesPlacedCoordinatesSumTimesOneHundred()
    {
        var (session, _) = TestWorldFactory.CreateRectangularSession(24, 12);
        var root = InitializeUnlockedRoot(session);
        var node = AddPlacedFeatureNode(session, root, "B1", "B1-a", new GridPoint(3, 5));

        var cost = session.SkillTree.GetNodeUnlockCost(node);

        Assert.Equal(800, cost);
        Assert.Equal("800 sandstone", TriloGame.Game.UI.Research.ResearchDraftController.BuildNodeCostText(node));
    }

    [Fact]
    public void TryPurchaseNode_SpendsSandstoneEvenlyAcrossMiningPosts()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(40, 12);
        var root = InitializeUnlockedRoot(session);
        var node = AddPlacedFeatureNode(session, root, "B1", "B1-a", new GridPoint(1, 0));
        var posts = BuildMiningPosts(cave, session, new[]
        {
            new GridPoint(0, 0),
            new GridPoint(6, 0),
            new GridPoint(12, 0),
            new GridPoint(18, 0)
        });
        foreach (var post in posts)
        {
            Assert.Equal(40, post.Deposit("Sandstone", 40));
        }

        var purchased = session.SkillTree.TryPurchaseNode(session, node, out var failureReason);

        Assert.True(purchased);
        Assert.Null(failureReason);
        Assert.True(node.IsUnlocked);
        Assert.Equal(60, session.GetStoredResourceTotal("Sandstone"));
        Assert.All(posts, post => Assert.Equal(15, post.GetInventory()["Sandstone"]));
    }

    [Fact]
    public void TryPurchaseNode_RedistributesSandstoneShortfallAcrossRemainingMiningPosts()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(72, 12);
        var root = InitializeUnlockedRoot(session);
        var node = AddPlacedFeatureNode(session, root, "B1", "B1-a", new GridPoint(5, 0));
        var postLocations = new[]
        {
            new GridPoint(0, 0),
            new GridPoint(6, 0),
            new GridPoint(12, 0),
            new GridPoint(18, 0),
            new GridPoint(24, 0),
            new GridPoint(30, 0),
            new GridPoint(36, 0),
            new GridPoint(42, 0),
            new GridPoint(48, 0),
            new GridPoint(54, 0)
        };
        var posts = BuildMiningPosts(cave, session, postLocations);
        Assert.Equal(5, posts[0].Deposit("Sandstone", 5));
        for (var index = 1; index < posts.Count; index++)
        {
            Assert.Equal(55, posts[index].Deposit("Sandstone", 55));
        }

        var purchased = session.SkillTree.TryPurchaseNode(session, node, out var failureReason);

        Assert.True(purchased);
        Assert.Null(failureReason);
        Assert.True(node.IsUnlocked);
        Assert.Equal(0, session.GetStoredResourceTotal("Sandstone"));
        Assert.All(posts, post => Assert.Equal(0, post.GetInventory()["Sandstone"]));
    }

    [Fact]
    public void TryPurchaseNode_IgnoresMiningPostsThatDoNotStoreTheRequestedResource()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(24, 12);
        var root = InitializeUnlockedRoot(session);
        var node = AddPlacedFeatureNode(session, root, "B1", "B1-a", new GridPoint(1, 0));
        var filledPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 0));
        var emptyPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 0));
        Assert.Equal(100, filledPost.Deposit("Sandstone", 100));
        Assert.Equal(0, emptyPost.GetInventory()["Sandstone"]);

        var purchased = session.SkillTree.TryPurchaseNode(session, node, out var failureReason);

        Assert.True(purchased);
        Assert.Null(failureReason);
        Assert.True(node.IsUnlocked);
        Assert.Equal(0, session.GetStoredResourceTotal("Sandstone"));
        Assert.Equal(0, filledPost.GetInventory()["Sandstone"]);
        Assert.Equal(0, emptyPost.GetInventory()["Sandstone"]);
    }

    [Fact]
    public void CanPurchaseNode_RequiresAuthoredFeatureTreePrerequisitesToBeUnlocked()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(24, 12);
        var root = InitializeUnlockedRoot(session);
        var node = AddPlacedFeatureNode(session, root, "B1", "B1-c", new GridPoint(1, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 0));
        Assert.Equal(100, post.Deposit("Sandstone", 100));

        var canPurchase = session.SkillTree.CanPurchaseNode(session, node, out var failureReason);

        Assert.False(canPurchase);
        Assert.Equal("Unlock prerequisite skills B1-a, B1-b first.", failureReason);
        Assert.False(node.IsUnlocked);
    }

    private static BinarySkillNode InitializeUnlockedRoot(GameSession session)
    {
        var root = session.SkillTree.SetRoot(
            session.SkillTree.IntakeSkillNode(
                new SkillNode("Hive Core", "The colony's structural research anchor."),
                GridPoint.Zero));
        Assert.True(root.TryUnlock(session));
        return root;
    }

    private static BinarySkillNode AddPlacedFeatureNode(
        GameSession session,
        BinarySkillNode parent,
        string featureTreeName,
        string skillName,
        GridPoint location)
    {
        var featureTree = Assert.IsType<FeatureTree>(session.GetFeatureTree(featureTreeName));
        var template = Assert.IsType<SkillNode>(featureTree.FindByName(skillName));
        var node = session.SkillTree.AddLeftChild(parent, session.SkillTree.IntakeSkillNode(template, GridPoint.Zero, featureTreeName));
        session.SkillTree.SetNodeLocation(node, location);
        return node;
    }

    private static IReadOnlyList<MiningPost> BuildMiningPosts(
        TriloGame.Game.Core.World.Cave cave,
        GameSession session,
        IReadOnlyList<GridPoint> locations)
    {
        var posts = new List<MiningPost>(locations.Count);
        foreach (var location in locations)
        {
            posts.Add(TestWorldFactory.BuildMiningPost(cave, session, location));
        }

        return posts;
    }
}
