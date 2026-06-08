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
        var buttonBounds = ResearchDraftLayout.GetSkillTreeButtonBounds(viewport);

        Assert.Equal(settingsBounds.X, buttonBounds.X);
        Assert.True(buttonBounds.Width > settingsBounds.Width);
        Assert.True(buttonBounds.Top > settingsBounds.Bottom);
    }

    [Fact]
    public void GetSkipButtonBounds_PlacesSkipButtonToTheRightOfTheSkillTreeButton()
    {
        var viewport = new Point(1440, 900);

        var skillTreeButtonBounds = ResearchDraftLayout.GetSkillTreeButtonBounds(viewport);
        var skipButtonBounds = ResearchDraftLayout.GetSkipButtonBounds(viewport);

        Assert.Equal(skillTreeButtonBounds.Y, skipButtonBounds.Y);
        Assert.Equal(skillTreeButtonBounds.Height, skipButtonBounds.Height);
        Assert.True(skipButtonBounds.Left > skillTreeButtonBounds.Right);
    }

    [Fact]
    public void Build_KeepsPanelAndBranchCardsInsideViewport()
    {
        var viewport = new Point(960, 640);
        var layout = ResearchDraftLayout.Build(viewport);
        var viewportBounds = new Rectangle(0, 0, viewport.X, viewport.Y);

        Assert.True(viewportBounds.Contains(layout.PanelBounds));
        Assert.True(viewportBounds.Contains(layout.DraftAreaBounds));
        Assert.True(viewportBounds.Contains(layout.TreeBounds));
        Assert.True(viewportBounds.Contains(layout.TreeViewportBounds));
        Assert.True(viewportBounds.Contains(layout.InfoPanelBounds));
        Assert.True(layout.DraftAreaBounds.Contains(layout.DraftHeaderBounds));
        Assert.True(layout.TreeBounds.Contains(layout.TreeViewportBounds));
        Assert.True(layout.TreeViewportBounds.Top > layout.TreeHeaderBounds.Bottom);
        Assert.All(layout.BranchCardBounds, bounds => Assert.True(viewportBounds.Contains(bounds)));
        Assert.All(layout.BranchCardBounds, bounds => Assert.True(layout.DraftAreaBounds.Contains(bounds)));
    }

    [Fact]
    public void Build_PlacesDraftCardsAcrossTheTopArea()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        Assert.True(layout.DraftAreaBounds.Top < layout.TreeBounds.Top);
        Assert.Equal(layout.TreeBounds.Left, layout.DraftAreaBounds.Left);
        Assert.Equal(layout.InfoPanelBounds.Right, layout.DraftAreaBounds.Right);
        Assert.All(layout.BranchCardBounds, bounds =>
        {
            Assert.True(bounds.Top > layout.DraftHeaderBounds.Bottom);
            Assert.True(bounds.Bottom <= layout.DraftAreaBounds.Bottom);
        });
        Assert.True(layout.BranchCardBounds[0].Right < layout.BranchCardBounds[1].Left);
        Assert.True(layout.BranchCardBounds[1].Right < layout.BranchCardBounds[2].Left);
    }

    [Fact]
    public void Build_HidesDraftAreaAndExpandsTreeWhenNoBranchesAreAvailable()
    {
        var viewport = new Point(1280, 800);
        var withDrafts = ResearchDraftLayout.Build(viewport);
        var withoutDrafts = ResearchDraftLayout.Build(viewport, branchCardCount: 0);

        Assert.Equal(Rectangle.Empty, withoutDrafts.DraftAreaBounds);
        Assert.Equal(Rectangle.Empty, withoutDrafts.DraftHeaderBounds);
        Assert.Empty(withoutDrafts.BranchCardBounds);
        Assert.True(withoutDrafts.TreeBounds.Top < withDrafts.TreeBounds.Top);
        Assert.True(withoutDrafts.TreeBounds.Height > withDrafts.TreeBounds.Height);
        Assert.Equal(withoutDrafts.PanelBounds.Y + 84, withoutDrafts.TreeBounds.Top);
        Assert.Equal(withoutDrafts.TreeBounds.Top, withoutDrafts.InfoPanelBounds.Top);
        Assert.Equal(withoutDrafts.TreeBounds.Bottom, withoutDrafts.InfoPanelBounds.Bottom);
    }

    [Fact]
    public void Build_ReservesRightSideForInfoPanel()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        Assert.True(layout.InfoPanelBounds.Left > layout.TreeBounds.Right);
        Assert.True(layout.InfoPanelBounds.Top > layout.DraftAreaBounds.Bottom);
        Assert.Equal(layout.TreeBounds.Top, layout.InfoPanelBounds.Top);
        Assert.Equal(layout.TreeBounds.Bottom, layout.InfoPanelBounds.Bottom);
    }
}
