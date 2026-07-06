using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;
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
    public void OpenBranchPreview_ShowsAllBranchNodesAndBackClosesTransientDetail()
    {
        var branch = new ResearchBranch("Moonlit Sprig");
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root."), "B1"));
        branch.AddChild(root, new TreeInstanceNode(new SkillNode("Child", "Child."), "B1"));
        var controller = new TrilodexController();
        var session = new GameSession();
        var viewport = new Point(1440, 900);
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);

        controller.OpenBranchPreview(branch, branch.Name);
        var model = controller.BuildDetailMenuModel(layout, session, treeBackgroundTexture: null, visualTimeMs: 0d);
        var backOutcome = controller.HandlePointerUp(layout.BackButtonBounds.Center, viewport);

        Assert.True(controller.IsDetailOpen);
        Assert.True(controller.IsTransientDetail);
        Assert.Equal("Moonlit Sprig", model.Title);
        Assert.Equal(2, CountNodes(model.TreeViewport.Root!));
        Assert.Null(model.InfoPanel.UnlockAction);
        Assert.Equal(TrilodexInteractionOutcome.RequestedClose, backOutcome);
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
    public void HandlePointerUp_ReturningFromDetailKeepsCatalogCardSelected()
    {
        var controller = new TrilodexController();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);
        controller.HandlePointerUp(layout.BackButtonBounds.Center, viewport);

        var cards = controller.BuildCatalogCardModels(layout);

        Assert.True(cards[0].IsSelected);
        Assert.All(cards.Skip(1), card => Assert.False(card.IsSelected));
    }

    [Fact]
    public void HandlePointerUp_ClickingDetailNodePinsInfoWithoutUnlockAction()
    {
        var controller = new TrilodexController();
        var session = new GameSession();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);
        var rootPoint = GetDetailRootPoint(layout);

        var outcome = controller.HandlePointerUp(rootPoint, viewport);
        var model = controller.BuildDetailMenuModel(layout, session, treeBackgroundTexture: null, visualTimeMs: 300d);

        Assert.Equal(TrilodexInteractionOutcome.Consumed, outcome);
        Assert.NotNull(model.InfoPanel.NodeInfo);
        Assert.Equal(TriloDex.Global.FeatureTrees[0].Root!.Name, model.InfoPanel.NodeInfo!.Value.TitleText);
        Assert.Null(model.InfoPanel.UnlockAction);
    }

    [Fact]
    public void Draw_SelectedDetailNodeUsesDoublePulsatingCyanHalo()
    {
        var controller = new TrilodexController();
        var session = new GameSession();
        var viewport = new Point(1440, 900);
        controller.Open();
        var layout = ResearchDraftLayout.BuildTreeCatalog(viewport, TriloDex.Global.Count);
        controller.HandlePointerUp(layout.CardBounds[0].Center, viewport);
        controller.HandlePointerUp(GetDetailRootPoint(layout), viewport);
        controller.UpdatePointer(layout.DetailInfoPanelBounds.Center);
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(viewport);

        controller.Draw(viewport, session, gumUi, treeBackgroundTexture: null, visualTimeMs: 300d);

        var cyanOutlines = gumUi.Root.Children
            .OfType<RoundedRectangleRuntime>()
            .Where(IsCyanOutline)
            .ToArray();
        Assert.Equal(2, cyanOutlines.Length);
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

    private static Point GetDetailRootPoint(ResearchDraftTreeCatalogLayoutInfo layout)
    {
        return new Point(
            layout.DetailTreeViewportBounds.Center.X,
            layout.DetailTreeViewportBounds.Bottom - ResearchTreeUiRenderer.DetailNodeRadius - 8);
    }

    private static bool IsCyanOutline(RoundedRectangleRuntime shape)
    {
        return !shape.IsFilled &&
            shape.Color.R == 105 &&
            shape.Color.G == 226 &&
            shape.Color.B == 239;
    }

    private static int CountNodes(ResearchTreeViewNode root)
    {
        var count = 1;
        foreach (var child in root.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }
}
