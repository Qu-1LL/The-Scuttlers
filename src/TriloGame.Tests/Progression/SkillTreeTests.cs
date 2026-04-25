using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Progression;

public sealed class SkillTreeTests
{
    [Fact]
    public void IntakeSkillNode_CreatesDetachedInstanceCopy()
    {
        var descriptor = new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddPercent, 0.1);
        var template = new SkillNode("Dig Sprint", "Move faster while mining.", [descriptor]);
        var skillTree = new SkillTree();

        var instanceNode = skillTree.IntakeSkillNode(template, "Mobility");

        Assert.Same(template, instanceNode.SourceSkillNode);
        Assert.Equal("Dig Sprint", instanceNode.Name);
        Assert.Equal("Move faster while mining.", instanceNode.Description);
        Assert.Equal("Mobility", instanceNode.SourceFeatureTreeName);
        Assert.False(instanceNode.IsUnlocked);
        Assert.True(instanceNode.IsLocked);
        Assert.Null(instanceNode.Parent);
        Assert.Empty(instanceNode.Children);
        Assert.Single(instanceNode.EffectDescriptors);
        Assert.Equal(descriptor, instanceNode.EffectDescriptors[0]);
    }

    [Fact]
    public void ImportingSkills_CreatesCollectionBasedChildrenFromDifferentFeatureTrees()
    {
        var mobilityTemplate = new SkillNode("Dig Sprint", "Move faster while mining.");
        var economyTemplate = new SkillNode("Packed Haul", "Carry more resources.");
        var surveyTemplate = new SkillNode("Stone Sense", "Reveal useful stone seams.");

        var dex = new TriloDex(
        [
            new FeatureTree("Mobility", "Movement upgrades.", ["movement"], 1, [], mobilityTemplate),
            new FeatureTree("Economy", "Resource upgrades.", ["resources"], 1, [], economyTemplate),
            new FeatureTree("Survey", "Reveal upgrades.", ["reveal"], 1, [], surveyTemplate)
        ]);

        var skillTree = new SkillTree(dex);

        var rootNode = skillTree.ImportRoot("Mobility", "Dig Sprint");
        var firstChild = skillTree.ImportChild(rootNode!, "Economy", "Packed Haul", childIndex: 0);
        var secondChild = skillTree.ImportChild(rootNode!, "Survey", "Stone Sense");

        Assert.NotNull(rootNode);
        Assert.NotNull(firstChild);
        Assert.NotNull(secondChild);
        Assert.NotSame(mobilityTemplate, rootNode);
        Assert.NotSame(economyTemplate, firstChild);
        Assert.NotSame(surveyTemplate, secondChild);
        Assert.Same(rootNode, skillTree.Root);
        Assert.Equal(2, rootNode!.ChildCount);
        Assert.Same(firstChild, rootNode.Children[0]);
        Assert.Same(secondChild, rootNode.Children[1]);
        Assert.Same(rootNode, firstChild!.Prerequisite);
        Assert.Same(rootNode, secondChild!.Prerequisite);
        Assert.Equal("Mobility", skillTree.GetSourceFeatureTreeName(rootNode));
        Assert.Equal("Economy", skillTree.GetSourceFeatureTreeName(firstChild));
        Assert.Equal("Survey", skillTree.GetSourceFeatureTreeName(secondChild));
        Assert.Equal(3, skillTree.Count);
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
        var skillTree = new SkillTree();
        var root = skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Hive Core", "Root anchor."), "Core"));
        var branch = new ResearchBranch();
        var branchRoot = branch.SetRoot(new TreeInstanceNode(new SkillNode("Branch Root", "Research root."), "B1"));
        var branchChild = branch.AddChild(branchRoot, new TreeInstanceNode(new SkillNode("Branch Child", "Research child."), "B1"));

        var placed = skillTree.TryPlaceResearchBranch(branch, root, out var failureReason);

        Assert.True(placed);
        Assert.Null(failureReason);
        Assert.Single(root.Children);
        Assert.Same(branchRoot, root.Children[0]);
        Assert.Same(branchRoot, branchChild.Parent);
        Assert.Single(branchRoot.Children);
        Assert.Same(branchChild, branchRoot.Children[0]);
        Assert.Equal(3, skillTree.Count);
    }

    [Fact]
    public void CanPlaceResearchBranch_RejectsBranchesThatDuplicateAnExistingSourceSkill()
    {
        var skillTree = new SkillTree();
        var root = skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Hive Core", "Root anchor."), "Core"));
        skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Existing", "Existing child."), "B1"));

        var branch = new ResearchBranch();
        branch.SetRoot(new TreeInstanceNode(new SkillNode("Existing", "Duplicate child."), "B1"));

        var canPlace = skillTree.CanPlaceResearchBranch(branch, root, out var failureReason);

        Assert.False(canPlace);
        Assert.Equal("That research branch overlaps skills already in the colony tree.", failureReason);
    }

    [Fact]
    public void RemoveSubtree_DetachesDescendantsFromTheirParent()
    {
        var skillTree = new SkillTree();
        var root = skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Hive Core", "Root anchor."), "Core"));
        var child = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Child", "Child node."), "B1"));
        var grandchild = skillTree.AddChild(child, skillTree.IntakeSkillNode(new SkillNode("Grandchild", "Grandchild node."), "B1"));

        var removed = skillTree.RemoveSubtree(child);

        Assert.True(removed);
        Assert.Empty(root.Children);
        Assert.Null(child.Parent);
        Assert.Same(child, grandchild.Parent);
        Assert.Equal(1, skillTree.Count);
    }
}
