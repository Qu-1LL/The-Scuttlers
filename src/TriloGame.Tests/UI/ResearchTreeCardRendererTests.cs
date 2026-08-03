using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;
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
        Assert.Equal(Rectangle.Empty, layout.SubtitleBounds);
        Assert.True(bounds.Contains(layout.PreviewBounds));
        Assert.True(layout.PreviewBounds.Y > layout.TitleBounds.Bottom);
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

    [Fact]
    public void Draw_PlacesTheTitleTextAboveTheTreePreview()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(640, 480));
        var session = new GameSession();
        var bounds = new Rectangle(20, 24, 260, 190);
        var layout = ResearchTreeCardRenderer.BuildLayout(bounds);
        var card = new ResearchTreeCardData(
            "Shellwright Basics",
            string.Empty,
            bounds,
            ResearchTreeViewNode.FromFeatureTree(TriloDex.Global.FindFeatureTree("B1")!),
            IsHovered: false,
            IsSelected: false);

        ResearchTreeCardRenderer.Draw(
            gumUi,
            session,
            card,
            ResearchTreeUiRenderer.TreeEntryCardConfig,
            Point.Zero);

        var title = gumUi.Root.Children.OfType<TextRuntime>().Single();
        Assert.Equal(1f, title.FontScale);
        Assert.Equal(GumTextLayout.GetMetrics(GumTextStyle.Small).FontSize, title.FontSize);
        Assert.True(title.Y >= layout.TitleBounds.Y);
        Assert.True(title.Y + title.Height <= layout.PreviewBounds.Y);
    }

    [Fact]
    public void Draw_DoesNotAddATitleBackgroundBand()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(640, 480));
        var card = new ResearchTreeCardData(
            "Shellwright Basics",
            string.Empty,
            new Rectangle(20, 24, 260, 190),
            Root: null,
            IsHovered: false,
            IsSelected: false);
        var titleBounds = ResearchTreeCardRenderer.BuildLayout(card.Bounds).TitleBounds;

        ResearchTreeCardRenderer.Draw(
            gumUi,
            session: null!,
            card,
            ResearchTreeUiRenderer.TreeEntryCardConfig,
            Point.Zero);

        Assert.DoesNotContain(
            gumUi.Root.Children,
            child => child.GetType().Name.Contains("RectangleRuntime", StringComparison.Ordinal) &&
                     (int)MathF.Round(child.X) == titleBounds.X &&
                     (int)MathF.Round(child.Y) == titleBounds.Y &&
                     (int)MathF.Round(child.Width) == titleBounds.Width &&
                     (int)MathF.Round(child.Height) == titleBounds.Height);
    }

    [Fact]
    public void Draw_SelectedCardUsesDoublePulsatingCyanOutline()
    {
        var bounds = new Rectangle(20, 24, 260, 190);
        var card = new ResearchTreeCardData(
            "Shellwright Basics",
            string.Empty,
            bounds,
            ResearchTreeViewNode.FromFeatureTree(TriloDex.Global.FindFeatureTree("B1")!),
            IsHovered: false,
            IsSelected: true);
        var firstUi = new GumUiRenderer(addToManagers: false);
        firstUi.BeginFrame(new Point(640, 480));
        var secondUi = new GumUiRenderer(addToManagers: false);
        secondUi.BeginFrame(new Point(640, 480));

        ResearchTreeCardRenderer.Draw(
            firstUi,
            new GameSession(),
            card,
            ResearchTreeUiRenderer.TreeEntryCardConfig,
            Point.Zero,
            visualTimeMs: 0d);
        ResearchTreeCardRenderer.Draw(
            secondUi,
            new GameSession(),
            card,
            ResearchTreeUiRenderer.TreeEntryCardConfig,
            Point.Zero,
            visualTimeMs: 300d);

        var firstCyan = GetCyanOutlines(firstUi);
        var secondCyan = GetCyanOutlines(secondUi);
        Assert.Equal(2, firstCyan.Length);
        Assert.Equal(2, secondCyan.Length);
        Assert.NotEqual(firstCyan[0].StrokeColor.A, secondCyan[0].StrokeColor.A);
        Assert.NotEqual(firstCyan[0].Width, secondCyan[0].Width);
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

    private static RectangleRuntime[] GetCyanOutlines(GumUiRenderer gumUi)
    {
        return gumUi.Root.Children
            .OfType<RectangleRuntime>()
            .Where(shape =>
                !shape.IsFilled &&
                shape.StrokeColor.R == 105 &&
                shape.StrokeColor.G == 226 &&
                shape.StrokeColor.B == 239)
            .ToArray();
    }
}
