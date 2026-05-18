using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeMenuRendererTests
{
    [Fact]
    public void FromDraftLayout_UsesSharedShellSectionsForDraftingMode()
    {
        var draftLayout = ResearchDraftLayout.Build(new Point(1280, 800), branchCardCount: 3);

        var shellLayout = ResearchTreeMenuRenderer.FromDraftLayout(draftLayout);

        Assert.Equal(draftLayout.PanelBounds, shellLayout.PanelBounds);
        Assert.Equal(draftLayout.DraftAreaBounds, shellLayout.CardFrameBounds);
        Assert.Equal(draftLayout.TreeViewportBounds, shellLayout.TreeViewportBounds);
        Assert.Equal(draftLayout.InfoPanelBounds, shellLayout.InfoPanelBounds);
        Assert.Equal(draftLayout.BranchCardBounds, shellLayout.CardBounds);
    }

    [Fact]
    public void FromCatalogLayout_ReadOnlyDetailUsesTreeAndInfoButNoCards()
    {
        var catalogLayout = ResearchDraftLayout.BuildTreeCatalog(new Point(1280, 800), treeCount: 18);

        var shellLayout = ResearchTreeMenuRenderer.FromCatalogLayout(catalogLayout, detailOpen: true);

        Assert.Equal(Rectangle.Empty, shellLayout.CardFrameBounds);
        Assert.Empty(shellLayout.CardBounds);
        Assert.Equal(catalogLayout.DetailTreeViewportBounds, shellLayout.TreeViewportBounds);
        Assert.Equal(catalogLayout.DetailInfoPanelBounds, shellLayout.InfoPanelBounds);
    }

    [Fact]
    public void ReadOnlyConfig_DisablesDraftingAndPlacement()
    {
        var config = new ResearchTreeMenuConfig(
            ShowBackButton: true,
            ShowCloseButton: true,
            CardAreaMode: ResearchTreeCardAreaMode.None,
            ShowTreeViewport: true,
            ShowInfoPanel: true,
            ShowFooter: false,
            EnablePanZoom: true,
            EnableNodeHover: true,
            EnableNodeSelection: true,
            EnableBranchDrafting: false,
            EnablePlacementPreview: false,
            EnableReadOnlyPreview: true,
            ShowRootNode: true,
            CanPlaceBranches: false);

        Assert.True(config.EnableReadOnlyPreview);
        Assert.False(config.EnableBranchDrafting);
        Assert.False(config.EnablePlacementPreview);
        Assert.False(config.CanPlaceBranches);
    }
}
