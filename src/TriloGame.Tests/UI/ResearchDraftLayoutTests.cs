using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Research;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class ResearchDraftLayoutTests
{
    [Fact]
    public void GetButtonBounds_PlacesResearchButtonInTopHudRow()
    {
        var viewport = new Point(1440, 900);

        var settingsBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var buttonBounds = ResearchDraftLayout.GetButtonBounds(viewport);

        Assert.Equal(settingsBounds.Y, buttonBounds.Y);
        Assert.Equal(settingsBounds.Size, buttonBounds.Size);
        Assert.True(buttonBounds.Left > settingsBounds.Right);
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
        Assert.True(viewportBounds.Contains(layout.InfoPanelBounds));
        Assert.Equal(Rectangle.Empty, layout.DraftHeaderBounds);
        Assert.Equal(InsetByRim(layout.TreeBounds), layout.TreeViewportBounds);
        Assert.All(layout.BranchCardBounds, bounds => Assert.True(viewportBounds.Contains(bounds)));
        Assert.All(layout.BranchCardBounds, bounds => Assert.True(layout.DraftAreaBounds.Contains(bounds)));
    }

    [Fact]
    public void Build_PlacesDraftCardsAcrossTheTopArea()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        Assert.True(layout.DraftAreaBounds.Top < layout.TreeBounds.Top);
        Assert.Equal(Rectangle.Empty, layout.DraftHeaderBounds);
        Assert.All(layout.BranchCardBounds, bounds =>
        {
            Assert.Equal(layout.InfoPanelBounds.Top, bounds.Top);
            Assert.True(bounds.Bottom <= layout.DraftAreaBounds.Bottom);
            Assert.Equal(ResearchTreeCardRenderer.PreferredCardHeight, bounds.Height);
        });
        Assert.True(layout.TreeBounds.Top - layout.BranchCardBounds[0].Bottom <= 24);
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
        Assert.Equal(withoutDrafts.TreeBounds.Top, withoutDrafts.InfoPanelBounds.Top);
    }

    [Fact]
    public void BuildTreeCatalog_DetailTreeViewportUsesSharedTreeViewportSizeRule()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, treeCount: 4);

        Assert.Equal(InsetByRim(layout.DetailTreeFrameBounds), layout.DetailTreeViewportBounds);
    }

    [Fact]
    public void Build_ReservesRightSideForInfoPanel()
    {
        var viewport = new Point(1280, 800);
        var layout = ResearchDraftLayout.Build(viewport);

        Assert.True(layout.InfoPanelBounds.Left > layout.TreeBounds.Right);
        Assert.True(layout.InfoPanelBounds.Left > layout.DraftAreaBounds.Right);
        Assert.Equal(layout.DraftAreaBounds.Top, layout.InfoPanelBounds.Top);
        Assert.Equal(layout.TreeBounds.Bottom, layout.InfoPanelBounds.Bottom);
        Assert.Equal(layout.FooterBounds.Top, layout.InfoPanelBounds.Bottom);
    }

    private static Rectangle InsetByRim(Rectangle bounds)
    {
        return new Rectangle(
            bounds.X + ResearchDraftLayout.TreeViewportRimThickness,
            bounds.Y + ResearchDraftLayout.TreeViewportRimThickness,
            bounds.Width - (ResearchDraftLayout.TreeViewportRimThickness * 2),
            bounds.Height - (ResearchDraftLayout.TreeViewportRimThickness * 2));
    }
}
