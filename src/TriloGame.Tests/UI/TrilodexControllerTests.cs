using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class TrilodexControllerTests
{
    [Fact]
    public void BuildCatalogMenuModel_LabelsMenuAsTrilodex()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);

        var model = controller.BuildCatalogMenuModel(layout);

        Assert.Equal("Trilodex", model.Title);
        Assert.Equal(ResearchTreeMenuMode.TrilodexCatalog, model.Mode);
    }

    [Fact]
    public void BuildCatalogCardModels_UsesReadableTreeDisplayName()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);

        var cards = controller.BuildCatalogCardModels(layout);

        Assert.Equal("Shellwright Basics", cards[0].Title);
        Assert.Equal(string.Empty, cards[0].Subtitle);
    }

    [Fact]
    public void BuildCatalogCardModels_GivesEveryCardAVisibleTreeName()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);

        var cards = controller.BuildCatalogCardModels(layout);

        Assert.All(cards, card => Assert.False(string.IsNullOrWhiteSpace(card.Title)));
    }

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

    [Fact]
    public void HandleWheel_InfoPanelScrollsWhenDetailEffectTextOverflows()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);

        var handled = controller.HandleWheel(layout.DetailInfoPanelBounds.Center, 90, viewport);
        var infoPanelScroll = (float)typeof(TrilodexController)
            .GetField("_infoPanelScroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(controller)!;

        Assert.True(handled);
        Assert.True(infoPanelScroll > 0f);
    }

    [Fact]
    public void HandlePointerDrag_DoesNotPanDetailTree()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);

        controller.HandlePointerDown(layout.DetailTreeViewportBounds.Center, viewport);
        controller.HandlePointerDrag(layout.DetailTreeViewportBounds.Center + new Point(80, -40), viewport);

        Assert.Equal(Vector2.Zero, controller.TreePanOffset);
    }

    [Fact]
    public void HandlePanPointerDrag_PansDetailTreeThroughSharedViewportState()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);

        Assert.True(controller.HandlePanPointerDown(layout.DetailTreeViewportBounds.Center, viewport));
        controller.HandlePanPointerDrag(layout.DetailTreeViewportBounds.Center + new Point(80, -40));

        Assert.Equal(new Vector2(80f, -40f), controller.TreePanOffset);
    }

    [Fact]
    public void HandleWheel_ZoomsDetailTreeThroughSharedViewportState()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);

        Assert.True(controller.HandleWheel(layout.DetailTreeViewportBounds.Center, -120, viewport));

        Assert.True(controller.TreeZoom > 1f);
    }
}
