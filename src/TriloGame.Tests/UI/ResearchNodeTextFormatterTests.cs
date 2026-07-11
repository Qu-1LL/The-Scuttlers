using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchNodeTextFormatterTests
{
    [Fact]
    public void BuildNodeInfo_UsesTheSameSharedShapeForTreeInstanceAndTreeViewNodes()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var root = new TreeInstanceNode(
            new SkillNode(
                "Faster Legs",
                "Move quicker through the colony.",
                [new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddPercent, 0.15)]),
            "B1");
        var branch = new ResearchBranch();
        branch.SetRoot(root);

        var treeNodeInfo = ResearchNodeTextFormatter.BuildNodeInfo(session, root);
        var viewNodeInfo = ResearchNodeTextFormatter.BuildNodeInfo(session, ResearchTreeViewNode.FromResearchBranch(branch));

        Assert.Equal(treeNodeInfo, viewNodeInfo);
        Assert.Equal("B1", treeNodeInfo.FeatureTreeText);
        Assert.Equal("+15% Trilobite.MoveSpeed", treeNodeInfo.EffectText);
    }

    [Fact]
    public void BuildNodeInfo_UsesCoreFallbackAndFormatsTargetedEffects()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var node = new TreeInstanceNode(
            new SkillNode(
                "Harvest Boost",
                "Boost algae output.",
                [new ResearchEffectDescriptor("Building.HarvestYield", ResearchOperation.AddFlat, 1, ResearchTargetKind.BuildingType, "Algae Farm")]));

        var info = ResearchNodeTextFormatter.BuildNodeInfo(session, node);

        Assert.Equal("Core", info.FeatureTreeText);
        Assert.Equal("+1 Building.HarvestYield (BuildingType: Algae Farm)", info.EffectText);
    }
}
