using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class TrilodexControllerTests
{
    [Fact]
    public void HandlePointerUp_ClickingACardOpensReadOnlyTreeDetail()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);

        var outcome = controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);

        Assert.Equal(TrilodexInteractionOutcome.Consumed, outcome);
        Assert.True(controller.IsDetailOpen);
    }

    [Fact]
    public void HandlePointerUp_BackButtonReturnsFromDetailToTheGrid()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);

        var backOutcome = controller.HandlePointerUp(layout.BackButtonBounds.Center, viewport);

        Assert.Equal(TrilodexInteractionOutcome.Consumed, backOutcome);
        Assert.False(controller.IsDetailOpen);
    }
}
