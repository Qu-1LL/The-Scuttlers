using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeCardRendererTests
{
    [Fact]
    public void BuildLayout_KeepsPreviewInsideReusableCardBounds()
    {
        var bounds = new Rectangle(120, 80, 260, 190);

        var layout = ResearchTreeCardRenderer.BuildLayout(bounds);

        Assert.Equal(bounds, layout.Bounds);
        Assert.True(bounds.Contains(layout.TitleBounds));
        Assert.True(bounds.Contains(layout.SubtitleBounds));
        Assert.True(bounds.Contains(layout.PreviewBounds));
        Assert.True(layout.PreviewBounds.Width > layout.PreviewBounds.Height);
    }

    [Fact]
    public void CalculateTreeLayout_CentersAndScalesCuratedTreeInsidePreview()
    {
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var cardBounds = new Rectangle(20, 30, 260, 190);

        var layout = ResearchTreeCardRenderer.CalculateTreeLayout(
            ResearchTreeViewNode.FromFeatureTree(featureTree),
            cardBounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);
        var previewBounds = ResearchTreeCardRenderer.BuildLayout(cardBounds).PreviewBounds;

        AssertTreeFits(layout, previewBounds);
    }

    [Fact]
    public void CalculateTreeLayout_CentersAndScalesDraftBranchInsidePreview()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root"), "B1"));
        var left = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Left", "Left"), "B1"));
        var right = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Right", "Right"), "B1"));
        branch.AddChild(left, new TreeInstanceNode(new SkillNode("Left Deep", "Left Deep"), "B1"));
        branch.AddChild(right, new TreeInstanceNode(new SkillNode("Right Deep", "Right Deep"), "B1"));
        var cardBounds = new Rectangle(0, 0, 240, 180);

        var layout = ResearchTreeCardRenderer.CalculateTreeLayout(
            ResearchTreeViewNode.FromResearchBranch(branch),
            cardBounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);
        var previewBounds = ResearchTreeCardRenderer.BuildLayout(cardBounds).PreviewBounds;

        AssertTreeFits(layout, previewBounds);
        Assert.True(GetContentWidth(layout) >= previewBounds.Width * 0.35f);
        Assert.True(GetContentHeight(layout) >= previewBounds.Height * 0.35f);
    }

    private static void AssertTreeFits(ResearchTreeViewLayout layout, Rectangle bounds)
    {
        Assert.NotEmpty(layout.Nodes);
        foreach (var node in layout.Nodes)
        {
            Assert.InRange(node.Position.X - layout.Radius, bounds.Left, bounds.Right);
            Assert.InRange(node.Position.X + layout.Radius, bounds.Left, bounds.Right);
            Assert.InRange(node.Position.Y - layout.Radius, bounds.Top, bounds.Bottom);
            Assert.InRange(node.Position.Y + layout.Radius, bounds.Top, bounds.Bottom);
        }
    }

    private static float GetContentWidth(ResearchTreeViewLayout layout)
    {
        var left = float.MaxValue;
        var right = float.MinValue;
        foreach (var node in layout.Nodes)
        {
            left = Math.Min(left, node.Position.X - layout.Radius);
            right = Math.Max(right, node.Position.X + layout.Radius);
        }

        return right - left;
    }

    private static float GetContentHeight(ResearchTreeViewLayout layout)
    {
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var node in layout.Nodes)
        {
            top = Math.Min(top, node.Position.Y - layout.Radius);
            bottom = Math.Max(bottom, node.Position.Y + layout.Radius);
        }

        return bottom - top;
    }
}
