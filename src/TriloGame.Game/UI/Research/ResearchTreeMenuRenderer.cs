using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeMenuRenderer
{
    private static readonly GumUiButtonStyle CloseButtonStyle = new(
        new GumUiFrameStyle(new Color(20, 42, 58), new Color(114, 154, 172), 2, 12),
        new GumUiFrameStyle(new Color(29, 55, 72), new Color(183, 223, 237), 2, 12),
        Color.White,
        GumTextStyle.Small);
    private static readonly GumUiButtonStyle BackButtonStyle = new(
        new GumUiFrameStyle(new Color(20, 42, 58), new Color(114, 154, 172), 2, 12),
        new GumUiFrameStyle(new Color(29, 55, 72), new Color(183, 223, 237), 2, 12),
        Color.White,
        GumTextStyle.Ui);

    public static void Draw(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint,
        GumRenderTargetViewport? renderTargetViewport = null)
    {
        gumUi.AddRoundedFrame(model.Layout.PanelBounds, new Color(9, 18, 27, 248), new Color(83, 125, 145), 3, 20);
        DrawChrome(gumUi, model, pointerPoint);
        DrawCards(gumUi, session, model, pointerPoint, renderTargetViewport);
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
            GumUiText.Add(gumUi, model.Layout.FooterBounds, model.FooterText, new Color(223, 233, 239), GumTextStyle.Compact);
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
            GumUiChrome.DrawButton(
                gumUi,
                model.Layout.CloseButtonBounds,
                "X",
                model.Layout.CloseButtonBounds.Contains(pointerPoint),
                CloseButtonStyle);
        }

        if (model.Config.ShowBackButton && !model.Layout.BackButtonBounds.IsEmpty)
        {
            GumUiChrome.DrawButton(
                gumUi,
                model.Layout.BackButtonBounds,
                "<",
                model.Layout.BackButtonBounds.Contains(pointerPoint),
                BackButtonStyle);
        }

        GumUiText.AddCentered(gumUi, model.Layout.TitleBounds, model.Title, Color.White, GumTextStyle.UiLarge);
        GumUiText.AddCentered(gumUi, model.Layout.SubtitleBounds, model.Subtitle, new Color(177, 203, 214), GumTextStyle.Small);
    }

    private static void DrawCards(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint,
        GumRenderTargetViewport? renderTargetViewport)
    {
        if (model.Config.CardAreaMode == ResearchTreeCardAreaMode.None ||
            model.Layout.CardFrameBounds.IsEmpty)
        {
            return;
        }

        gumUi.AddRoundedFrame(model.Layout.CardFrameBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        if (model.Config.CardAreaMode == ResearchTreeCardAreaMode.CatalogGrid)
        {
            DrawCatalogCards(gumUi, session, model, pointerPoint, renderTargetViewport);
        }
        else
        {
            DrawDraftRowCards(gumUi, session, model, pointerPoint);
        }

        if (!model.Layout.CardHeaderBounds.IsEmpty && !string.IsNullOrWhiteSpace(model.CardHeaderText))
        {
            GumUiText.Add(gumUi, model.Layout.CardHeaderBounds, model.CardHeaderText, new Color(204, 228, 238), GumTextStyle.Small);
        }

        if (model.Layout.MaxCardScroll > 0f)
        {
            gumUi.AddRoundedRectangle(model.Layout.ScrollbarTrackBounds, new Color(10, 22, 32, 210), 3);
            gumUi.AddRoundedRectangle(model.Layout.ScrollbarThumbBounds, new Color(92, 137, 154), 3);
        }
    }

    private static void DrawDraftRowCards(GumUiRenderer gumUi, GameSession session, ResearchTreeMenuModel model, Point pointerPoint)
    {
        for (var index = 0; index < Math.Min(model.Cards.Count, model.Layout.CardBounds.Count); index++)
        {
            var source = model.Cards[index];
            ResearchTreeCardRenderer.Draw(
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
                pointerPoint);
        }
    }

    private static void DrawCatalogCards(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint,
        GumRenderTargetViewport? renderTargetViewport)
    {
        if (model.Layout.CardViewportBounds.IsEmpty)
        {
            return;
        }

        if (renderTargetViewport is null)
        {
            DrawCatalogCardsDirect(gumUi, session, model, pointerPoint);
            return;
        }

        var texture = renderTargetViewport.Render(
            model.Layout.CardViewportBounds,
            offscreenGumUi => DrawCatalogCardsInto(
                offscreenGumUi,
                session,
                model,
                new GumUiSurface(model.Layout.CardViewportBounds),
                pointerPoint));
        gumUi.AddSprite(model.Layout.CardViewportBounds, texture);
    }

    private static void DrawCatalogCardsDirect(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint)
    {
        for (var index = 0; index < Math.Min(model.Cards.Count, model.Layout.CardBounds.Count); index++)
        {
            var cardBounds = model.Layout.CardBounds[index];
            if (Rectangle.Intersect(model.Layout.CardViewportBounds, cardBounds) is not { Width: > 0, Height: > 0 })
            {
                continue;
            }

            var source = model.Cards[index];
            ResearchTreeCardRenderer.Draw(
                gumUi,
                session,
                new ResearchTreeCardData(
                    source.Title,
                    source.Subtitle,
                    cardBounds,
                    source.Root,
                    source.IsHovered,
                    source.IsSelected),
                ResearchTreeUiRenderer.TreeEntryCardConfig,
                pointerPoint);
        }
    }

    private static void DrawCatalogCardsInto(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        GumUiSurface surface,
        Point pointerPoint)
    {
        var localPointer = surface.ToLocal(pointerPoint);
        for (var index = 0; index < Math.Min(model.Cards.Count, model.Layout.CardBounds.Count); index++)
        {
            var cardBounds = model.Layout.CardBounds[index];
            if (!surface.Intersects(cardBounds))
            {
                continue;
            }

            var source = model.Cards[index];
            ResearchTreeCardRenderer.Draw(
                gumUi,
                session,
                new ResearchTreeCardData(
                    source.Title,
                    source.Subtitle,
                    surface.ToLocal(cardBounds),
                    source.Root,
                    source.IsHovered,
                    source.IsSelected),
                ResearchTreeUiRenderer.TreeEntryCardConfig,
                localPointer);
        }
    }

    private static ResearchNodeInfo? DrawTreeViewport(
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
            GumUiText.Add(gumUi, model.Layout.TreeHeaderBounds, model.TreeHeaderText, new Color(204, 228, 238), GumTextStyle.Small);
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

        if (model.NodeInfo is ResearchNodeInfo info)
        {
            gumUi.AddRoundedFrame(bounds, new Color(9, 18, 28, 248), new Color(204, 228, 238), 2, 16);
            var contentX = bounds.X + 14;
            var contentWidth = bounds.Width - 28;
            GumUiText.Add(gumUi, new Rectangle(contentX, bounds.Y + 12, contentWidth, 18), "Node Details", new Color(204, 228, 238), GumTextStyle.Compact);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 38, contentWidth, 44), "Node", info.TitleText);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 88, contentWidth, 40), "Feature Tree", info.FeatureTreeText);
            var effectLabelBounds = new Rectangle(contentX, bounds.Y + 134, contentWidth, 14);
            GumUiText.Add(gumUi, effectLabelBounds, "Effect", new Color(153, 194, 211), GumTextStyle.Compact, verticalAlignment: VerticalAlignment.Top);
            var effectViewportBounds = new Rectangle(contentX, bounds.Y + 150, contentWidth, Math.Max(20, bounds.Height - 164));
            var effectTextLayout = GumScrollableText.Build(effectViewportBounds, info.EffectText, GumTextStyle.Small, model.ScrollOffset);
            GumScrollableText.Draw(gumUi, effectTextLayout, Color.White, GumTextStyle.Small);
            if (effectTextLayout.ScrollbarTrackBounds is { } trackBounds &&
                effectTextLayout.ScrollbarThumbBounds is { } thumbBounds)
            {
                gumUi.AddRoundedRectangle(trackBounds, new Color(10, 22, 32, 210), 3);
                gumUi.AddRoundedRectangle(thumbBounds, new Color(92, 137, 154), 3);
            }

            return;
        }

        gumUi.AddRoundedFrame(bounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
        GumUiText.Add(gumUi, new Rectangle(bounds.X + 14, bounds.Y + 12, bounds.Width - 28, 18), model.EmptyTitle, new Color(204, 228, 238), GumTextStyle.Compact);
        GumUiText.AddCentered(
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

}
