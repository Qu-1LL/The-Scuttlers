using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Progression;

public sealed class SkillTreeTests
{
    [Fact]
    public void IntakeSkillNode_CreatesDetachedBinaryCopy()
    {
        var template = new SkillNode("Dig Sprint", "Move faster while mining.", _ => { });
        var skillTree = new SkillTree();

        var binaryNode = skillTree.IntakeSkillNode(template, "Mobility");

        Assert.Same(template, binaryNode.SourceSkillNode);
        Assert.Equal("Dig Sprint", binaryNode.Name);
        Assert.Equal("Move faster while mining.", binaryNode.Description);
        Assert.Equal("Mobility", binaryNode.SourceFeatureTreeName);
        Assert.Null(binaryNode.Parent);
        Assert.Null(binaryNode.Left);
        Assert.Null(binaryNode.Right);
    }

    [Fact]
    public void ImportingSkills_CreatesBinaryChildrenFromDifferentFeatureTrees()
    {
        var mobilityTemplate = new SkillNode("Dig Sprint", "Move faster while mining.", _ => { });
        var economyTemplate = new SkillNode("Packed Haul", "Carry more resources.", _ => { });
        var surveyTemplate = new SkillNode("Stone Sense", "Reveal useful stone seams.", _ => { });

        var dex = new TriloDex(
        [
            new FeatureTree("Mobility", "Movement upgrades.", ["movement"], 1, mobilityTemplate),
            new FeatureTree("Economy", "Resource upgrades.", ["resources"], 1, economyTemplate),
            new FeatureTree("Survey", "Reveal upgrades.", ["reveal"], 1, surveyTemplate)
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
    public void ImportedSkillAcquisition_DoesNotMutateTheFeatureTreeTemplate()
    {
        var template = new SkillNode("Stone Sense", "Reveal useful stone seams.", _ => { });
        var dex = new TriloDex(
        [
            new FeatureTree("Survey", "Reveal upgrades.", ["reveal"], 1, template)
        ]);
        var skillTree = new SkillTree(dex);
        var session = new GameSession();

        var localNode = skillTree.ImportRoot("Survey", "Stone Sense");

        Assert.NotNull(localNode);
        Assert.True(localNode!.TryAcquire(session));
        Assert.True(localNode.IsAcquired);
        Assert.False(template.IsAcquired);
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
}
