using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchDraftTreeCatalogLayoutTests
{
    [Fact]
    public void BuildTreeCatalog_UsesTheResearchDraftPanelSize()
    {
        var viewport = new Point(1440, 900);

        var catalog = ResearchDraftLayout.BuildTreeCatalog(viewport, treeCount: 18);
        var drafter = ResearchDraftLayout.Build(viewport);
        var displayStyle = GumTextStyleCatalog.Get(GumTextStyle.Display);

        Assert.Equal(drafter.PanelBounds, catalog.PanelBounds);
        Assert.Equal(catalog.PanelBounds.Center.X, catalog.TitleBounds.Center.X);
        Assert.Equal(48, GumTextLayout.GetMetrics(GumTextStyle.Display).FontSize);
        Assert.Equal(1f, displayStyle.FontScale);
        Assert.Equal(GumTextStyleCatalog.DisplayFontFile, displayStyle.CustomFontFile);
        Assert.True(catalog.TitleBounds.Height >= GumTextLayout.GetMetrics(GumTextStyle.Display).LineHeight);
        Assert.Equal(Rectangle.Empty, catalog.SubtitleBounds);
        Assert.True(catalog.CatalogFrameBounds.Y > catalog.TitleBounds.Bottom);
        Assert.Equal(12, catalog.CatalogFrameBounds.Y - catalog.TitleBounds.Bottom);
    }

    [Fact]
    public void BuildTreeCatalog_PlacesCardsInFourColumns()
    {
        var viewport = new Point(1440, 900);
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, treeCount: 18);

        Assert.Equal(18, layout.CardBounds.Count);
        Assert.Equal(layout.CardBounds[0].Y, layout.CardBounds[1].Y);
        Assert.Equal(layout.CardBounds[1].Y, layout.CardBounds[2].Y);
        Assert.Equal(layout.CardBounds[2].Y, layout.CardBounds[3].Y);
        Assert.Equal(layout.CardBounds[0].X, layout.CardBounds[4].X);
        Assert.True(layout.CardBounds[0].Right < layout.CardBounds[1].Left);
        Assert.True(layout.CardBounds[3].Right <= layout.CatalogViewportBounds.Right);
    }
}
