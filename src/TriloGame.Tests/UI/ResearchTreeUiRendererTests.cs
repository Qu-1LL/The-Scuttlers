using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeUiRendererTests
{
    [Fact]
    public void CalculateCardTreeLayout_FitsTheWholeCuratedTreeInsideThePreviewBounds()
    {
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var bounds = new Rectangle(20, 30, 240, 150);

        var layout = ResearchTreeUiRenderer.CalculateCardTreeLayout(
            ResearchTreeViewNode.FromFeatureTree(featureTree),
            bounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);

        Assert.NotEmpty(layout.Nodes);
        foreach (var node in layout.Nodes)
        {
            Assert.InRange(node.Position.X - layout.Radius, bounds.Left, bounds.Right);
            Assert.InRange(node.Position.X + layout.Radius, bounds.Left, bounds.Right);
            Assert.InRange(node.Position.Y - layout.Radius, bounds.Top, bounds.Bottom);
            Assert.InRange(node.Position.Y + layout.Radius, bounds.Top, bounds.Bottom);
        }
    }

    [Fact]
    public void CalculateCardTreeLayout_SupportsDraftBranchesThroughTheSameTreeEntryPath()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root"), "B1"));
        branch.AddChild(root, new TreeInstanceNode(new SkillNode("Child", "Child"), "B1"));
        var bounds = new Rectangle(0, 0, 180, 120);

        var layout = ResearchTreeUiRenderer.CalculateCardTreeLayout(
            ResearchTreeViewNode.FromResearchBranch(branch),
            bounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);

        Assert.Equal(2, layout.Nodes.Count);
        Assert.All(layout.Nodes, node => Assert.True(bounds.Contains(node.Position)));
    }

    [Fact]
    public void ClampZoom_StaysWithinReadOnlyTreeViewerLimits()
    {
        Assert.Equal(0.55f, ResearchTreeUiRenderer.ClampZoom(0.1f));
        Assert.Equal(1.2f, ResearchTreeUiRenderer.ClampZoom(1.2f));
        Assert.Equal(2.25f, ResearchTreeUiRenderer.ClampZoom(9f));
    }

    [Fact]
    public void TreeEntryCardConfig_ExposesDataDrivenFeatureFlags()
    {
        var config = ResearchTreeUiRenderer.TreeEntryCardConfig;

        Assert.False(config.ShowBackButton);
        Assert.True(config.ShowRootNode);
        Assert.True(config.EnableNodeSelection);
        Assert.False(config.EnableBranchDrafting);
        Assert.False(config.EnablePlacementPreview);
    }
}
