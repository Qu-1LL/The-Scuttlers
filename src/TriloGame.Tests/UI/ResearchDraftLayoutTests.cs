using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Research;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class ResearchDraftLayoutTests
{
    [Fact]
    public void GetButtonBounds_PlacesResearchButtonBelowSettings()
    {
        var viewport = new Point(1440, 900);

        var settingsBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var buttonBounds = ResearchDraftLayout.GetButtonBounds(viewport);

        Assert.Equal(settingsBounds.X, buttonBounds.X);
        Assert.True(buttonBounds.Width > settingsBounds.Width);
        Assert.True(buttonBounds.Top > settingsBounds.Bottom);
    }

    [Fact]
    public void Build_KeepsPanelAndBranchCardsInsideViewport()
    {
        var viewport = new Point(960, 640);
        var layout = ResearchDraftLayout.Build(viewport);
        var viewportBounds = new Rectangle(0, 0, viewport.X, viewport.Y);

        Assert.True(viewportBounds.Contains(layout.PanelBounds));
        Assert.True(viewportBounds.Contains(layout.TreeBounds));
        Assert.True(viewportBounds.Contains(layout.TreeViewportBounds));
        Assert.True(viewportBounds.Contains(layout.HoverInfoBounds));
        Assert.True(viewportBounds.Contains(layout.RightHoverInfoBounds));
        Assert.True(layout.TreeBounds.Contains(layout.TreeViewportBounds));
        Assert.True(layout.TreeViewportBounds.Top > layout.TreeHeaderBounds.Bottom);
        Assert.All(layout.BranchCardBounds, bounds => Assert.True(viewportBounds.Contains(bounds)));
    }

    [Fact]
    public void Build_AttachesHoverInfoPanelAlongTheLeftEdgeOfTheResearchMenu()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        Assert.True(layout.HoverInfoBounds.Left < layout.PanelBounds.Left);
        Assert.True(layout.HoverInfoBounds.Right > layout.PanelBounds.Left);
        Assert.Equal(layout.PanelBounds.Top, layout.HoverInfoBounds.Top);
        Assert.Equal(layout.PanelBounds.Height, layout.HoverInfoBounds.Height);
    }

    [Fact]
    public void Build_AttachesRightHoverInfoPanelAlongTheRightEdgeOfTheResearchMenu()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        Assert.True(layout.RightHoverInfoBounds.Left < layout.PanelBounds.Right);
        Assert.True(layout.RightHoverInfoBounds.Right > layout.PanelBounds.Right);
        Assert.Equal(layout.PanelBounds.Top, layout.RightHoverInfoBounds.Top);
        Assert.Equal(layout.PanelBounds.Height, layout.RightHoverInfoBounds.Height);
    }
}
