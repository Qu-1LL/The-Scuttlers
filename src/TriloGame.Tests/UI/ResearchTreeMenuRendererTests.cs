using Microsoft.Xna.Framework;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeMenuRendererTests
{
    [Fact]
    public void ResolveInfoPanelForTreeHover_PreservesUnlockActionForSelectedNode()
    {
        var selectedInfo = new ResearchNodeInfo("Selected", "B1", "Effect");
        var unlockAction = new ResearchNodeUnlockActionModel(
            "Chitinstone",
            Available: 40,
            Cost: 40,
            CanUnlock: true,
            IsUnlocked: false,
            SkillTreeUnlockBlockReason.None);
        var panel = new ResearchTreeInfoPanelModel(
            selectedInfo,
            "Info",
            "Hover a node.",
            UnlockAction: unlockAction);

        var resolved = ResearchTreeMenuRenderer.ResolveInfoPanelForTreeHover(panel, selectedInfo);

        Assert.Equal(unlockAction, resolved.UnlockAction);
    }

    [Fact]
    public void ResolveInfoPanelForTreeHover_HidesUnlockActionForDifferentNode()
    {
        var panel = new ResearchTreeInfoPanelModel(
            new ResearchNodeInfo("Selected", "B1", "Effect"),
            "Info",
            "Hover a node.",
            UnlockAction: new ResearchNodeUnlockActionModel(
                "Chitinstone",
                Available: 40,
                Cost: 40,
                CanUnlock: true,
                IsUnlocked: false,
                SkillTreeUnlockBlockReason.None));

        var resolved = ResearchTreeMenuRenderer.ResolveInfoPanelForTreeHover(
            panel,
            new ResearchNodeInfo("Other", "B2", "Other effect"));

        Assert.Null(resolved.UnlockAction);
    }

    [Fact]
    public void BuildUnlockCostTextLayout_LeavesGlyphSafetySpaceForMultiDigitCount()
    {
        var bounds = new Rectangle(100, 80, 260, 20);
        const string availableText = "10";
        const string suffixText = "/40 Chitinstone to unlock";

        var layout = ResearchTreeMenuRenderer.BuildUnlockCostTextLayout(bounds, availableText, suffixText);

        Assert.True(
            layout.AvailableBounds.Width > GumTextLayout.Measure(availableText, GumTextStyle.Compact).X);
        Assert.Equal(layout.AvailableBounds.Right, layout.SuffixBounds.Left);
        Assert.True(layout.SuffixBounds.Width > 0);
        Assert.True(bounds.Contains(layout.AvailableBounds));
        Assert.True(bounds.Contains(layout.SuffixBounds));
    }

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

    [Fact]
    public void Draw_TrilodexCatalogUsesDisplayFontAtScaleOne()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(1280, 800));
        var layout = ResearchDraftLayout.BuildTreeCatalog(new Point(1280, 800), treeCount: 0);
        var model = new ResearchTreeMenuModel(
            ResearchTreeMenuMode.TrilodexCatalog,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: true,
                CardAreaMode: ResearchTreeCardAreaMode.None,
                ShowTreeViewport: false,
                ShowInfoPanel: false,
                ShowFooter: false,
                EnablePanZoom: false,
                EnableNodeHover: false,
                EnableNodeSelection: false,
                EnableBranchDrafting: false,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: true,
                ShowRootNode: true,
                CanPlaceBranches: false),
            ResearchTreeMenuRenderer.FromCatalogLayout(layout, detailOpen: false),
            "Trilodex",
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            new ResearchTreeViewportModel(null, Vector2.Zero, 1f, null),
            new ResearchTreeInfoPanelModel(null, string.Empty, string.Empty),
            string.Empty);

        ResearchTreeMenuRenderer.Draw(gumUi, session: null!, model, Point.Zero);

        var title = Assert.Single(gumUi.Root.Children.OfType<TextRuntime>(), text => text.UseCustomFont);
        Assert.True(title.UseCustomFont);
        Assert.Equal(GumTextStyleCatalog.DisplayFontFile, title.CustomFontFile);
        Assert.Equal(GumTextLayout.GetMetrics(GumTextStyle.Display).FontSize, title.FontSize);
        Assert.Equal(1f, title.FontScale);
    }

    [Fact]
    public void Draw_DraftRowDoesNotDrawOuterCardFrame()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(640, 480));
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var cardFrameBounds = new Rectangle(20, 60, 500, 120);
        var model = new ResearchTreeMenuModel(
            ResearchTreeMenuMode.Drafting,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: false,
                CardAreaMode: ResearchTreeCardAreaMode.DraftRow,
                ShowTreeViewport: false,
                ShowInfoPanel: false,
                ShowFooter: false,
                EnablePanZoom: false,
                EnableNodeHover: false,
                EnableNodeSelection: false,
                EnableBranchDrafting: true,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: false,
                ShowRootNode: true,
                CanPlaceBranches: true),
            new ResearchTreeMenuLayoutInfo(
                PanelBounds: new Rectangle(0, 0, 640, 480),
                CloseButtonBounds: Rectangle.Empty,
                BackButtonBounds: Rectangle.Empty,
                TitleBounds: Rectangle.Empty,
                SubtitleBounds: Rectangle.Empty,
                CardFrameBounds: cardFrameBounds,
                CardHeaderBounds: Rectangle.Empty,
                CardViewportBounds: Rectangle.Empty,
                CardBounds: [],
                TreeFrameBounds: Rectangle.Empty,
                TreeHeaderBounds: Rectangle.Empty,
                TreeViewportBounds: Rectangle.Empty,
                InfoPanelBounds: Rectangle.Empty,
                FooterBounds: Rectangle.Empty,
                MaxCardScroll: 0f,
                ScrollbarTrackBounds: Rectangle.Empty,
                ScrollbarThumbBounds: Rectangle.Empty),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            new ResearchTreeViewportModel(null, Vector2.Zero, 1f, null),
            new ResearchTreeInfoPanelModel(null, string.Empty, string.Empty),
            string.Empty);

        ResearchTreeMenuRenderer.Draw(gumUi, session, model, Point.Zero);

        Assert.DoesNotContain(
            gumUi.Root.Children.OfType<RoundedRectangleRuntime>(),
            shape =>
                (int)shape.X == cardFrameBounds.X &&
                (int)shape.Y == cardFrameBounds.Y &&
                (int)shape.Width == cardFrameBounds.Width &&
                (int)shape.Height == cardFrameBounds.Height);
    }

    [Fact]
    public void Draw_TreeViewportBorderIsDrawnAfterPlacementOverlay()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(640, 480));
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var treeFrameBounds = new Rectangle(16, 24, 400, 260);
        var model = new ResearchTreeMenuModel(
            ResearchTreeMenuMode.Drafting,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: false,
                CardAreaMode: ResearchTreeCardAreaMode.None,
                ShowTreeViewport: true,
                ShowInfoPanel: false,
                ShowFooter: false,
                EnablePanZoom: true,
                EnableNodeHover: true,
                EnableNodeSelection: true,
                EnableBranchDrafting: false,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: false,
                ShowRootNode: true,
                CanPlaceBranches: false),
            new ResearchTreeMenuLayoutInfo(
                PanelBounds: new Rectangle(0, 0, 640, 480),
                CloseButtonBounds: Rectangle.Empty,
                BackButtonBounds: Rectangle.Empty,
                TitleBounds: Rectangle.Empty,
                SubtitleBounds: Rectangle.Empty,
                CardFrameBounds: Rectangle.Empty,
                CardHeaderBounds: Rectangle.Empty,
                CardViewportBounds: Rectangle.Empty,
                CardBounds: [],
                TreeFrameBounds: treeFrameBounds,
                TreeHeaderBounds: Rectangle.Empty,
                TreeViewportBounds: treeFrameBounds,
                InfoPanelBounds: Rectangle.Empty,
                FooterBounds: Rectangle.Empty,
                MaxCardScroll: 0f,
                ScrollbarTrackBounds: Rectangle.Empty,
                ScrollbarThumbBounds: Rectangle.Empty),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            new ResearchTreeViewportModel(
                Root: ResearchTreeViewNode.FromSkillTree(session.SkillTree),
                PanOffset: Vector2.Zero,
                Zoom: 1f,
                BackgroundTexture: null,
                DrawOverlay: context =>
                {
                    context.GumUi.AddFilledRectangle(treeFrameBounds, Color.Red);
                    return null;
                }),
            new ResearchTreeInfoPanelModel(null, string.Empty, string.Empty),
            string.Empty);

        ResearchTreeMenuRenderer.Draw(gumUi, session, model, Point.Zero);

        var redOverlayIndex = FindChildIndex(gumUi, child =>
            child is ColoredRectangleRuntime rectangle &&
            rectangle.Color == Color.Red &&
            (int)rectangle.X == treeFrameBounds.X &&
            (int)rectangle.Y == treeFrameBounds.Y &&
            (int)rectangle.Width == treeFrameBounds.Width &&
            (int)rectangle.Height == treeFrameBounds.Height);
        var outlineIndex = FindChildIndex(gumUi, child =>
            child is RoundedRectangleRuntime shape &&
            !shape.IsFilled &&
            (int)shape.X == treeFrameBounds.X &&
            (int)shape.Y == treeFrameBounds.Y &&
            (int)shape.Width == treeFrameBounds.Width &&
            (int)shape.Height == treeFrameBounds.Height);

        Assert.True(redOverlayIndex >= 0);
        Assert.True(outlineIndex > redOverlayIndex);
        var outline = Assert.IsType<RoundedRectangleRuntime>(gumUi.Root.Children[outlineIndex]);
        Assert.False(outline.IsFilled);
        Assert.Equal(treeFrameBounds.X, outline.X);
        Assert.Equal(treeFrameBounds.Y, outline.Y);
        Assert.Equal(treeFrameBounds.Width, outline.Width);
        Assert.Equal(treeFrameBounds.Height, outline.Height);
        Assert.Equal(4, outline.StrokeWidth);
        Assert.Equal(16, outline.CornerRadius);
    }

    [Fact]
    public void Draw_TreeViewportPanelIsDrawnBeforeModalSurface()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(640, 480));
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var panelBounds = new Rectangle(0, 0, 640, 480);
        var treeFrameBounds = new Rectangle(16, 96, 400, 260);
        var treeViewportBounds = new Rectangle(
            treeFrameBounds.X + ResearchDraftLayout.TreeViewportRimThickness,
            treeFrameBounds.Y + ResearchDraftLayout.TreeViewportRimThickness,
            treeFrameBounds.Width - (ResearchDraftLayout.TreeViewportRimThickness * 2),
            treeFrameBounds.Height - (ResearchDraftLayout.TreeViewportRimThickness * 2));
        var model = new ResearchTreeMenuModel(
            ResearchTreeMenuMode.Drafting,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: false,
                CardAreaMode: ResearchTreeCardAreaMode.None,
                ShowTreeViewport: true,
                ShowInfoPanel: false,
                ShowFooter: false,
                EnablePanZoom: true,
                EnableNodeHover: true,
                EnableNodeSelection: true,
                EnableBranchDrafting: false,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: false,
                ShowRootNode: true,
                CanPlaceBranches: false),
            new ResearchTreeMenuLayoutInfo(
                PanelBounds: panelBounds,
                CloseButtonBounds: Rectangle.Empty,
                BackButtonBounds: Rectangle.Empty,
                TitleBounds: Rectangle.Empty,
                SubtitleBounds: Rectangle.Empty,
                CardFrameBounds: Rectangle.Empty,
                CardHeaderBounds: Rectangle.Empty,
                CardViewportBounds: Rectangle.Empty,
                CardBounds: [],
                TreeFrameBounds: treeFrameBounds,
                TreeHeaderBounds: Rectangle.Empty,
                TreeViewportBounds: treeViewportBounds,
                InfoPanelBounds: Rectangle.Empty,
                FooterBounds: Rectangle.Empty,
                MaxCardScroll: 0f,
                ScrollbarTrackBounds: Rectangle.Empty,
                ScrollbarThumbBounds: Rectangle.Empty),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            new ResearchTreeViewportModel(
                Root: ResearchTreeViewNode.FromSkillTree(session.SkillTree),
                PanOffset: Vector2.Zero,
                Zoom: 1f,
                BackgroundTexture: null),
            new ResearchTreeInfoPanelModel(null, string.Empty, string.Empty),
            string.Empty);

        ResearchTreeMenuRenderer.Draw(gumUi, session, model, Point.Zero);

        var treeFillIndex = FindChildIndex(gumUi, child =>
            child is RoundedRectangleRuntime shape &&
            shape.IsFilled &&
            (int)shape.X == treeFrameBounds.X &&
            (int)shape.Y == treeFrameBounds.Y &&
            (int)shape.Width == treeFrameBounds.Width &&
            (int)shape.Height == treeFrameBounds.Height);
        var modalSurfaceIndex = FindChildIndex(gumUi, child =>
            child is ColoredRectangleRuntime rectangle &&
            (int)rectangle.X == panelBounds.X &&
            (int)rectangle.Y == panelBounds.Y &&
            (int)rectangle.Width == panelBounds.Width &&
            (int)rectangle.Height == treeViewportBounds.Y - panelBounds.Y);

        Assert.True(treeFillIndex >= 0);
        Assert.True(modalSurfaceIndex > treeFillIndex);
        var modalSurface = Assert.IsType<ColoredRectangleRuntime>(gumUi.Root.Children[modalSurfaceIndex]);
        Assert.Equal(255, modalSurface.Color.A);
    }

    [Fact]
    public void Draw_UserZoomedTreeViewportScalesNodesAndKeepsConnectorsCrisp()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var root = ResearchTreeViewNode.FromFeatureTree(featureTree);

        var zoomedOut = DrawTreeViewportShapeSummary(session, root, zoom: 0.5f);
        var zoomedIn = DrawTreeViewportShapeSummary(session, root, zoom: 2.25f);

        Assert.NotEmpty(zoomedOut.NodeSizes);
        Assert.NotEmpty(zoomedOut.ConnectorThicknesses);
        Assert.True(
            zoomedOut.NodeSizes.Max(size => size.Width) <
            zoomedIn.NodeSizes.Max(size => size.Width));
        Assert.Equal(
            zoomedOut.ConnectorThicknesses.Distinct().OrderBy(thickness => thickness),
            zoomedIn.ConnectorThicknesses.Distinct().OrderBy(thickness => thickness));
        Assert.All(
            zoomedOut.ConnectorThicknesses,
            thickness => Assert.Equal(ResearchTreeUiRenderer.DetailConnectorThickness, thickness));
    }

    private static int FindChildIndex(GumUiRenderer gumUi, Func<GraphicalUiElement, bool> predicate)
    {
        for (var index = 0; index < gumUi.Root.Children.Count; index++)
        {
            if (predicate(gumUi.Root.Children[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static TreeViewportShapeSummary DrawTreeViewportShapeSummary(
        GameSession session,
        ResearchTreeViewNode root,
        float zoom)
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(640, 480));
        var treeFrameBounds = new Rectangle(16, 24, 400, 260);
        var model = new ResearchTreeMenuModel(
            ResearchTreeMenuMode.ReadOnlyDetail,
            new ResearchTreeMenuConfig(
                ShowBackButton: false,
                ShowCloseButton: false,
                CardAreaMode: ResearchTreeCardAreaMode.None,
                ShowTreeViewport: true,
                ShowInfoPanel: false,
                ShowFooter: false,
                EnablePanZoom: true,
                EnableNodeHover: true,
                EnableNodeSelection: true,
                EnableBranchDrafting: false,
                EnablePlacementPreview: false,
                EnableReadOnlyPreview: true,
                ShowRootNode: true,
                CanPlaceBranches: false),
            new ResearchTreeMenuLayoutInfo(
                PanelBounds: new Rectangle(0, 0, 640, 480),
                CloseButtonBounds: Rectangle.Empty,
                BackButtonBounds: Rectangle.Empty,
                TitleBounds: Rectangle.Empty,
                SubtitleBounds: Rectangle.Empty,
                CardFrameBounds: Rectangle.Empty,
                CardHeaderBounds: Rectangle.Empty,
                CardViewportBounds: Rectangle.Empty,
                CardBounds: [],
                TreeFrameBounds: treeFrameBounds,
                TreeHeaderBounds: Rectangle.Empty,
                TreeViewportBounds: treeFrameBounds,
                InfoPanelBounds: Rectangle.Empty,
                FooterBounds: Rectangle.Empty,
                MaxCardScroll: 0f,
                ScrollbarTrackBounds: Rectangle.Empty,
                ScrollbarThumbBounds: Rectangle.Empty),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            new ResearchTreeViewportModel(root, Vector2.Zero, zoom, BackgroundTexture: null),
            new ResearchTreeInfoPanelModel(null, string.Empty, string.Empty),
            string.Empty);

        ResearchTreeMenuRenderer.Draw(gumUi, session, model, Point.Zero);

        var nodeRadius = ResearchTreeUiRenderer.CalculateDetailNodeRadius(zoom);
        var maxNodeDiameter = (nodeRadius + ResearchTreeUiRenderer.CalculateDetailNodeBorderThickness(nodeRadius)) * 2;
        var nodeSizes = gumUi.Root.Children
            .OfType<RoundedRectangleRuntime>()
            .Where(shape => shape.IsFilled && shape.Width <= maxNodeDiameter && shape.Height <= maxNodeDiameter)
            .Select(shape => (Width: (int)shape.Width, Height: (int)shape.Height))
            .OrderBy(size => size.Width)
            .ThenBy(size => size.Height)
            .ToList();
        var connectorThicknesses = gumUi.Root.Children
            .OfType<ColoredRectangleRuntime>()
            .Where(shape =>
                shape.Height == ResearchTreeUiRenderer.DetailConnectorThickness &&
                shape.Width > ResearchTreeUiRenderer.DetailConnectorThickness)
            .Select(shape => (int)shape.Height)
            .OrderBy(thickness => thickness)
            .ToList();

        return new TreeViewportShapeSummary(nodeSizes, connectorThicknesses);
    }

    private sealed record TreeViewportShapeSummary(
        IReadOnlyList<(int Width, int Height)> NodeSizes,
        IReadOnlyList<int> ConnectorThicknesses);
}
