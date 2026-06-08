using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Progression;

public sealed class ResearchBranchGeneratorTests
{
    [Fact]
    public void Generate_ScoresAvailableNodesAndGatesHigherTierRoots()
    {
        var dex = CreateProgressionDex();
        var skillTree = new SkillTree(dex);
        var session = new GameSession();

        var bRoot = skillTree.SetRoot(skillTree.IntakeSkillNode(dex.FindFeatureTree("B1")!.Root!, GridPoint.Zero, "B1"));
        var bChild = skillTree.AddLeftChild(bRoot, skillTree.IntakeSkillNode(dex.FindFeatureTree("B1")!.Root!.Children[0], GridPoint.Zero, "B1"));
        var mRoot = skillTree.AddRightChild(bRoot, skillTree.IntakeSkillNode(dex.FindFeatureTree("M1")!.Root!, GridPoint.Zero, "M1"));

        Assert.True(bRoot.TryUnlock(session));
        Assert.True(bChild.TryUnlock(session));
        Assert.True(mRoot.TryUnlock(session));

        var result = new ResearchBranchGenerator(new Random(7)).Generate(skillTree);

        Assert.Equal(4, result.AvailableNodeCount);
        Assert.Contains(result.CandidateScores, candidate => candidate.FeatureTreeName == "BM1" && candidate.SkillName == "BM-root");
        Assert.DoesNotContain(result.CandidateScores, candidate => candidate.FeatureTreeName == "BCM1" && candidate.SkillName == "BCM-root");
        Assert.Equal(1, GetPoints(result, "C1", "C-root"));
        Assert.Equal(1, GetPoints(result, "F1", "F-root"));
        Assert.Equal(4, GetPoints(result, "M1", "M-child"));
        Assert.Equal(1, GetPoints(result, "BM1", "BM-root"));
    }

    [Fact]
    public void Generate_BuildsThreeResearchBranchesWithFourUniqueNodesEach()
    {
        var dex = CreateProgressionDex();
        var skillTree = CreateUnlockedStarterTree(dex);

        var result = new ResearchBranchGenerator(new Random(19)).Generate(skillTree);

        Assert.Equal(3, result.Branches.Count);
        foreach (var branch in result.Branches)
        {
            Assert.Equal(4, branch.Count);
            Assert.NotNull(branch.Root);
            Assert.True(
                branch.Root!.Delta == new GridPoint(1, 0) ||
                branch.Root.Delta == new GridPoint(0, 1));
            Assert.Equal(branch.Count, branch.Nodes.Select(node => (node.Node.SourceFeatureTreeName, node.Node.Name)).Distinct().Count());
            Assert.Equal(branch.Count, branch.NodesByDelta.Count);
            Assert.All(branch.Nodes, node =>
            {
                Assert.Equal(node.Delta, node.Node.NodeDelta);
                Assert.True(node.Node.IsLocked);
                Assert.False(node.Node.IsUnlocked);
            });
        }
    }

    [Fact]
    public void Generate_UnlockingACompleteTierTwoTreeMakesTierThreeRootsAvailable()
    {
        var dex = CreateProgressionDex();
        var skillTree = CreateUnlockedStarterTree(dex);
        var session = new GameSession();

        var mRoot = skillTree.FindBySourceSkill("M1", "M-root");
        Assert.NotNull(mRoot);
        Assert.True(mRoot!.IsUnlocked);

        var bmRoot = skillTree.AddLeftChild(mRoot, skillTree.IntakeSkillNode(dex.FindFeatureTree("BM1")!.Root!, GridPoint.Zero, "BM1"));
        var bmChild = skillTree.AddRightChild(bmRoot, skillTree.IntakeSkillNode(dex.FindFeatureTree("BM1")!.Root!.Children[0], GridPoint.Zero, "BM1"));

        Assert.True(bmRoot.TryUnlock(session));
        Assert.True(bmChild.TryUnlock(session));

        var result = new ResearchBranchGenerator(new Random(31)).Generate(skillTree);

        Assert.Contains(result.CandidateScores, candidate => candidate.FeatureTreeName == "BCM1" && candidate.SkillName == "BCM-root");
    }

    [Fact]
    public void Generate_TierThreeFallbackRequiresFortyUnlockedNodes_NotJustPlacedNodes()
    {
        var root = new SkillNode("Chain-0", "Chain root.");
        var current = root;
        for (var index = 1; index < 40; index++)
        {
            var next = new SkillNode($"Chain-{index}", $"Chain node {index}.");
            current.AddChild(next);
            current = next;
        }

        var tierThreeRoot = new SkillNode("T3-root", "Tier three root.");
        var dex = new TriloDex(
        [
            new FeatureTree("Chain", "Large tier one chain.", ["building"], 1, [], root),
            new FeatureTree("T3", "Tier three root.", ["combat", "mining", "building"], 3, ["MissingT2"], tierThreeRoot)
        ]);

        var skillTree = new SkillTree(dex);
        var session = new GameSession();

        var localRoot = skillTree.SetRoot(skillTree.IntakeSkillNode(root, GridPoint.Zero, "Chain"));
        Assert.True(localRoot.TryUnlock(session));

        var parent = localRoot;
        var placedNodes = new List<BinarySkillNode> { localRoot };
        foreach (var templateNode in dex.FindFeatureTree("Chain")!.TraverseDepthFirst().Skip(1))
        {
            var child = skillTree.AddLeftChild(parent, skillTree.IntakeSkillNode(templateNode, GridPoint.Zero, "Chain"));
            placedNodes.Add(child);
            parent = child;
        }

        foreach (var node in placedNodes.Take(39).Skip(1))
        {
            Assert.True(node.TryUnlock(session));
        }

        Assert.Equal(40, skillTree.Count);
        Assert.Equal(39, skillTree.UnlockedCount);

        var result = new ResearchBranchGenerator(new Random(5)).Generate(skillTree);

        Assert.DoesNotContain(result.CandidateScores, candidate => candidate.FeatureTreeName == "T3" && candidate.SkillName == "T3-root");
    }

    private static int GetPoints(ResearchBranchGenerationResult result, string featureTreeName, string skillName)
    {
        return Assert.Single(result.CandidateScores, candidate =>
            candidate.FeatureTreeName == featureTreeName &&
            candidate.SkillName == skillName).Points;
    }

    private static SkillTree CreateUnlockedStarterTree(TriloDex dex)
    {
        var skillTree = new SkillTree(dex);
        var session = new GameSession();

        var bRoot = skillTree.SetRoot(skillTree.IntakeSkillNode(dex.FindFeatureTree("B1")!.Root!, GridPoint.Zero, "B1"));
        var bChild = skillTree.AddLeftChild(bRoot, skillTree.IntakeSkillNode(dex.FindFeatureTree("B1")!.Root!.Children[0], GridPoint.Zero, "B1"));
        var mRoot = skillTree.AddRightChild(bRoot, skillTree.IntakeSkillNode(dex.FindFeatureTree("M1")!.Root!, GridPoint.Zero, "M1"));

        Assert.True(bRoot.TryUnlock(session));
        Assert.True(bChild.TryUnlock(session));
        Assert.True(mRoot.TryUnlock(session));

        return skillTree;
    }

    private static TriloDex CreateProgressionDex()
    {
        var bRoot = new SkillNode("B-root", "Building root.");
        var bChild = new SkillNode("B-child", "Building child.");
        bRoot.AddChild(bChild);

        var mRoot = new SkillNode("M-root", "Mining root.");
        var mChild = new SkillNode("M-child", "Mining child.");
        mRoot.AddChild(mChild);

        var cRoot = new SkillNode("C-root", "Combat root.");
        var cChild = new SkillNode("C-child", "Combat child.");
        cRoot.AddChild(cChild);

        var fRoot = new SkillNode("F-root", "Farming root.");
        var fChild = new SkillNode("F-child", "Farming child.");
        fRoot.AddChild(fChild);

        var bmRoot = new SkillNode("BM-root", "Building and mining root.");
        var bmChild = new SkillNode("BM-child", "Building and mining child.");
        bmRoot.AddChild(bmChild);

        var bcmRoot = new SkillNode("BCM-root", "Tier three root.");
        var bcmChild = new SkillNode("BCM-child", "Tier three child.");
        bcmRoot.AddChild(bcmChild);

        return new TriloDex(
        [
            new FeatureTree("B1", "Building tier one.", ["building"], 1, [], bRoot),
            new FeatureTree("M1", "Mining tier one.", ["mining"], 1, [], mRoot),
            new FeatureTree("C1", "Combat tier one.", ["combat"], 1, [], cRoot),
            new FeatureTree("F1", "Farming tier one.", ["farming"], 1, [], fRoot),
            new FeatureTree("BM1", "Building and mining tier two.", ["building", "mining"], 2, ["B1", "M1"], bmRoot),
            new FeatureTree("BCM1", "Tier three tree.", ["building", "combat", "mining"], 3, ["BM1"], bcmRoot)
        ]);
    }
}
