using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class TrilodexLayoutTests
{
    [Fact]
    public void Build_UsesTheSamePanelSizeAsTheResearchDraftMenu()
    {
        var viewport = new Point(1440, 900);

        var trilodex = TrilodexLayout.Build(viewport, treeCount: 18);
        var drafter = ResearchDraftLayout.Build(viewport);

        Assert.Equal(drafter.PanelBounds, trilodex.PanelBounds);
    }

    [Fact]
    public void Build_PlacesCardsInFourColumns()
    {
        var viewport = new Point(1440, 900);
        var layout = TrilodexLayout.Build(viewport, treeCount: 18);

        Assert.Equal(18, layout.CardBounds.Count);
        Assert.Equal(layout.CardBounds[0].Y, layout.CardBounds[1].Y);
        Assert.Equal(layout.CardBounds[1].Y, layout.CardBounds[2].Y);
        Assert.Equal(layout.CardBounds[2].Y, layout.CardBounds[3].Y);
        Assert.Equal(layout.CardBounds[0].X, layout.CardBounds[4].X);
        Assert.True(layout.CardBounds[0].Right < layout.CardBounds[1].Left);
        Assert.True(layout.CardBounds[3].Right <= layout.GridViewportBounds.Right);
    }
}
