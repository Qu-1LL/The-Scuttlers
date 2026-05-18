using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeMenuRenderer
{
    public static void Draw(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint)
    {
        gumUi.AddRoundedFrame(model.Layout.PanelBounds, new Color(9, 18, 27, 248), new Color(83, 125, 145), 3, 20);
        DrawChrome(gumUi, model, pointerPoint);
        DrawCards(gumUi, session, model);
        var treeHoverInfo = DrawTreeViewport(gumUi, session, model, pointerPoint);
        if (model.Config.ShowInfoPanel)
        {
            DrawInfoPanel(
                gumUi,
                model.Layout.InfoPanelBounds,
                treeHoverInfo is null
                    ? model.InfoPanel
                    : model.InfoPanel with { NodeInfo = treeHoverInfo.Value });
        }

        if (model.Config.ShowFooter && !string.IsNullOrWhiteSpace(model.FooterText))
        {
            AddText(gumUi, model.Layout.FooterBounds, model.FooterText, new Color(223, 233, 239), GumTextStyle.Compact);
        }
    }

    public static ResearchTreeMenuLayoutInfo FromDraftLayout(ResearchDraftLayoutInfo layout)
    {
        return new ResearchTreeMenuLayoutInfo(
            layout.PanelBounds,
            layout.CloseButtonBounds,
            BackButtonBounds: Rectangle.Empty,
            layout.TitleBounds,
            layout.SubtitleBounds,
            layout.DraftAreaBounds,
            layout.DraftHeaderBounds,
            CardViewportBounds: Rectangle.Empty,
            layout.BranchCardBounds,
            layout.TreeBounds,
            layout.TreeHeaderBounds,
            layout.TreeViewportBounds,
            layout.InfoPanelBounds,
            layout.FooterBounds,
            MaxCardScroll: 0f,
            ScrollbarTrackBounds: Rectangle.Empty,
            ScrollbarThumbBounds: Rectangle.Empty);
    }

    public static ResearchTreeMenuLayoutInfo FromCatalogLayout(ResearchDraftTreeCatalogLayoutInfo layout, bool detailOpen)
    {
        return new ResearchTreeMenuLayoutInfo(
            layout.PanelBounds,
            layout.CloseButtonBounds,
            layout.BackButtonBounds,
            layout.TitleBounds,
            layout.SubtitleBounds,
            detailOpen ? Rectangle.Empty : layout.CatalogFrameBounds,
            CardHeaderBounds: Rectangle.Empty,
            detailOpen ? Rectangle.Empty : layout.CatalogViewportBounds,
            detailOpen ? [] : layout.CardBounds,
            detailOpen ? layout.DetailTreeFrameBounds : Rectangle.Empty,
            TreeHeaderBounds: Rectangle.Empty,
            detailOpen ? layout.DetailTreeViewportBounds : Rectangle.Empty,
            detailOpen ? layout.DetailInfoPanelBounds : Rectangle.Empty,
            FooterBounds: Rectangle.Empty,
            detailOpen ? 0f : layout.MaxScroll,
            detailOpen ? Rectangle.Empty : layout.ScrollbarTrackBounds,
            detailOpen ? Rectangle.Empty : layout.ScrollbarThumbBounds);
    }

    private static void DrawChrome(GumUiRenderer gumUi, ResearchTreeMenuModel model, Point pointerPoint)
    {
        if (model.Config.ShowCloseButton)
        {
            gumUi.AddRoundedFrame(
                model.Layout.CloseButtonBounds,
                model.Layout.CloseButtonBounds.Contains(pointerPoint) ? new Color(29, 55, 72) : new Color(20, 42, 58),
                model.Layout.CloseButtonBounds.Contains(pointerPoint) ? new Color(183, 223, 237) : new Color(114, 154, 172),
                2,
                12);
            AddCenteredText(gumUi, model.Layout.CloseButtonBounds, "X", Color.White, GumTextStyle.Small);
        }

        if (model.Config.ShowBackButton && !model.Layout.BackButtonBounds.IsEmpty)
        {
            gumUi.AddRoundedFrame(
                model.Layout.BackButtonBounds,
                model.Layout.BackButtonBounds.Contains(pointerPoint) ? new Color(29, 55, 72) : new Color(20, 42, 58),
                model.Layout.BackButtonBounds.Contains(pointerPoint) ? new Color(183, 223, 237) : new Color(114, 154, 172),
                2,
                12);
            AddCenteredText(gumUi, model.Layout.BackButtonBounds, "<", Color.White, GumTextStyle.Ui);
        }

        AddCenteredText(gumUi, model.Layout.TitleBounds, model.Title, Color.White, GumTextStyle.UiLarge);
        AddCenteredText(gumUi, model.Layout.SubtitleBounds, model.Subtitle, new Color(177, 203, 214), GumTextStyle.Small);
    }

    private static void DrawCards(GumUiRenderer gumUi, GameSession session, ResearchTreeMenuModel model)
    {
        if (model.Config.CardAreaMode == ResearchTreeCardAreaMode.None ||
            model.Layout.CardFrameBounds.IsEmpty)
        {
            return;
        }

        gumUi.AddRoundedFrame(model.Layout.CardFrameBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        if (!model.Layout.CardHeaderBounds.IsEmpty && !string.IsNullOrWhiteSpace(model.CardHeaderText))
        {
            AddText(gumUi, model.Layout.CardHeaderBounds, model.CardHeaderText, new Color(204, 228, 238), GumTextStyle.Small);
        }

        if (model.Config.CardAreaMode == ResearchTreeCardAreaMode.CatalogGrid)
        {
            DrawCatalogCards(gumUi, session, model);
        }
        else
        {
            DrawDraftRowCards(gumUi, session, model);
        }

        if (model.Layout.MaxCardScroll > 0f)
        {
            gumUi.AddRoundedRectangle(model.Layout.ScrollbarTrackBounds, new Color(10, 22, 32, 210), 3);
            gumUi.AddRoundedRectangle(model.Layout.ScrollbarThumbBounds, new Color(92, 137, 154), 3);
        }
    }

    private static void DrawDraftRowCards(GumUiRenderer gumUi, GameSession session, ResearchTreeMenuModel model)
    {
        for (var index = 0; index < Math.Min(model.Cards.Count, model.Layout.CardBounds.Count); index++)
        {
            var source = model.Cards[index];
            ResearchTreeUiRenderer.DrawTreeEntryCard(
                gumUi,
                session,
                new ResearchTreeCardData(
                    source.Title,
                    source.Subtitle,
                    model.Layout.CardBounds[index],
                    source.Root,
                    source.IsHovered,
                    source.IsSelected),
                ResearchTreeUiRenderer.TreeEntryCardConfig,
                Point.Zero);
        }
    }

    private static void DrawCatalogCards(GumUiRenderer gumUi, GameSession session, ResearchTreeMenuModel model)
    {
        if (model.Layout.CardViewportBounds.IsEmpty)
        {
            return;
        }

        var clipLayer = gumUi.AddClippingContainer(model.Layout.CardViewportBounds);
        for (var index = 0; index < Math.Min(model.Cards.Count, model.Layout.CardBounds.Count); index++)
        {
            var bounds = model.Layout.CardBounds[index];
            if (bounds.Bottom < model.Layout.CardViewportBounds.Top ||
                bounds.Top > model.Layout.CardViewportBounds.Bottom)
            {
                continue;
            }

            var localBounds = new Rectangle(
                bounds.X - model.Layout.CardViewportBounds.X,
                bounds.Y - model.Layout.CardViewportBounds.Y,
                bounds.Width,
                bounds.Height);
            var source = model.Cards[index];
            ResearchTreeUiRenderer.DrawTreeEntryCard(
                gumUi,
                clipLayer,
                session,
                new ResearchTreeCardData(
                    source.Title,
                    source.Subtitle,
                    localBounds,
                    source.Root,
                    source.IsHovered,
                    source.IsSelected),
                ResearchTreeUiRenderer.TreeEntryCardConfig,
                Point.Zero);
        }
    }

    private static ResearchTreeNodeInfo? DrawTreeViewport(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint)
    {
        if (!model.Config.ShowTreeViewport ||
            model.Layout.TreeFrameBounds.IsEmpty ||
            model.Layout.TreeViewportBounds.IsEmpty)
        {
            return null;
        }

        gumUi.AddRoundedFrame(model.Layout.TreeFrameBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        if (!model.Layout.TreeHeaderBounds.IsEmpty && !string.IsNullOrWhiteSpace(model.TreeHeaderText))
        {
            AddText(gumUi, model.Layout.TreeHeaderBounds, model.TreeHeaderText, new Color(204, 228, 238), GumTextStyle.Small);
        }

        if (model.TreeViewport.DrawCustomContent is not null)
        {
            model.TreeViewport.DrawCustomContent(gumUi);
            return null;
        }

        if (model.TreeViewport.Root is not null)
        {
            var hoveredNode = ResearchTreeUiRenderer.DrawDetail(
                gumUi,
                session,
                model.Layout.TreeViewportBounds,
                model.TreeViewport.Root,
                model.TreeViewport.PanOffset,
                model.TreeViewport.Zoom,
                model.TreeViewport.BackgroundTexture,
                pointerPoint,
                new ResearchTreeRenderConfig(
                    model.Config.ShowBackButton,
                    model.Config.ShowRootNode,
                    model.Config.EnableNodeSelection,
                    model.Config.EnableBranchDrafting,
                    model.Config.EnablePlacementPreview));
            return hoveredNode is null ? null : ResearchTreeUiRenderer.BuildNodeInfo(session, hoveredNode);
        }

        return null;
    }

    private static void DrawInfoPanel(
        GumUiRenderer gumUi,
        Rectangle bounds,
        ResearchTreeInfoPanelModel model)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        if (model.NodeInfo is ResearchTreeNodeInfo info)
        {
            gumUi.AddRoundedFrame(bounds, new Color(9, 18, 28, 248), new Color(204, 228, 238), 2, 16);
            var contentX = bounds.X + 14;
            var contentWidth = bounds.Width - 28;
            AddText(gumUi, new Rectangle(contentX, bounds.Y + 12, contentWidth, 18), "Node Details", new Color(204, 228, 238), GumTextStyle.Compact);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 38, contentWidth, 44), "Node", info.TitleText);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 88, contentWidth, 40), "Feature Tree", info.FeatureTreeText);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 134, contentWidth, bounds.Height - 148), "Effect", info.EffectText, maxLines: 10);
            return;
        }

        gumUi.AddRoundedFrame(bounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        AddText(gumUi, new Rectangle(bounds.X + 14, bounds.Y + 12, bounds.Width - 28, 18), model.EmptyTitle, new Color(204, 228, 238), GumTextStyle.Compact);
        AddCenteredText(
            gumUi,
            new Rectangle(bounds.X + 18, bounds.Y + 46, bounds.Width - 36, bounds.Height - 64),
            model.EmptyText,
            new Color(177, 203, 214),
            GumTextStyle.Small,
            maxLines: 3);
    }

    private static void DrawInfoSection(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string label,
        string value,
        int maxLines = 2)
    {
        gumUi.AddText(
            new Rectangle(bounds.X, bounds.Y, bounds.Width, 14),
            label,
            new Color(153, 194, 211),
            fontSize: GumTextLayout.GetMetrics(GumTextStyle.Compact).FontSize,
            verticalAlignment: VerticalAlignment.Top);
        gumUi.AddText(
            new Rectangle(bounds.X, bounds.Y + 16, bounds.Width, Math.Max(20, bounds.Height - 16)),
            value,
            Color.White,
            fontSize: GumTextLayout.GetMetrics(GumTextStyle.Small).FontSize,
            verticalAlignment: VerticalAlignment.Top,
            maxLines: maxLines);
    }

    private static void AddText(GumUiRenderer gumUi, Rectangle bounds, string text, Color color, GumTextStyle style)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(bounds, text, color, fontSize: metrics.FontSize, verticalAlignment: VerticalAlignment.Center);
    }

    private static void AddCenteredText(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 0)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        gumUi.AddText(
            bounds,
            text,
            color,
            HorizontalAlignment.Center,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines);
    }
}
