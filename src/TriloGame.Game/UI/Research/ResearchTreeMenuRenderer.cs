using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeMenuRenderer
{
    private const int PanelRadius = 20;
    private const int PanelBorderThickness = 3;
    private const int TreeViewportFrameRadius = 16;
    private const float UnlockCountWidthSafetyScale = 1.25f;
    private const int UnlockCountMinimumSafetyPixels = 4;
    private static readonly Color PanelFill = new(9, 18, 27);
    private static readonly Color PanelBorder = new(83, 125, 145);
    private static readonly Color TreeViewportFrameFill = new(12, 25, 37);
    private static readonly Color TreeViewportFrameBorder = new(58, 87, 103);
    private static readonly Color DisabledTextColor = new(151, 164, 171);
    private static readonly Color CostWarningColor = new(244, 105, 100);
    private static readonly GumUiFrameStyle DisabledButtonFrame = new(new Color(42, 52, 60), new Color(89, 103, 111), 2, 12);
    private static readonly GumUiFrameStyle TooltipFrame = new(new Color(7, 15, 22, 238), new Color(143, 205, 226), 2, 10);
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
    private static readonly GumUiButtonStyle UnlockButtonStyle = new(
        new GumUiFrameStyle(new Color(129, 103, 45), new Color(231, 199, 103), 2, 12),
        new GumUiFrameStyle(new Color(154, 124, 55), new Color(255, 229, 152), 2, 12),
        new Color(18, 24, 31),
        GumTextStyle.Small);

    public static void Draw(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeMenuModel model,
        Point pointerPoint,
        GumRenderTargetViewport? renderTargetViewport = null)
    {
        var treeHoverInfo = DrawTreeViewport(gumUi, session, model, pointerPoint);
        DrawPanelSurface(gumUi, model);
        DrawTreeViewportHeaderAndBorder(gumUi, model);
        DrawChrome(gumUi, model, pointerPoint);
        DrawCards(gumUi, session, model, pointerPoint, renderTargetViewport);
        if (model.Config.ShowInfoPanel)
        {
            DrawInfoPanel(
                gumUi,
                model.Layout.InfoPanelBounds,
                ResolveInfoPanelForTreeHover(model.InfoPanel, treeHoverInfo),
                pointerPoint);
        }

        if (model.Config.ShowFooter && !string.IsNullOrWhiteSpace(model.FooterText))
        {
            GumUiText.Add(gumUi, model.Layout.FooterBounds, model.FooterText, new Color(223, 233, 239), GumTextStyle.Compact);
        }
    }

    internal static ResearchTreeInfoPanelModel ResolveInfoPanelForTreeHover(
        ResearchTreeInfoPanelModel selectedPanel,
        ResearchNodeInfo? treeHoverInfo)
    {
        if (treeHoverInfo is not ResearchNodeInfo hoveredInfo)
        {
            return selectedPanel;
        }

        var hoveringSelectedNode =
            selectedPanel.NodeInfo is ResearchNodeInfo selectedInfo &&
            selectedInfo == hoveredInfo;
        return selectedPanel with
        {
            NodeInfo = hoveredInfo,
            UnlockAction = hoveringSelectedNode ? selectedPanel.UnlockAction : null
        };
    }

    private static void DrawPanelSurface(GumUiRenderer gumUi, ResearchTreeMenuModel model)
    {
        var panelBounds = model.Layout.PanelBounds;
        if (!model.Config.ShowTreeViewport || model.Layout.TreeViewportBounds.IsEmpty)
        {
            gumUi.AddRoundedFrame(panelBounds, PanelFill, PanelBorder, PanelBorderThickness, PanelRadius);
            return;
        }

        var holeBounds = Rectangle.Intersect(panelBounds, model.Layout.TreeViewportBounds);
        if (holeBounds.Width <= 0 || holeBounds.Height <= 0)
        {
            gumUi.AddRoundedFrame(panelBounds, PanelFill, PanelBorder, PanelBorderThickness, PanelRadius);
            return;
        }

        AddPanelFillBand(gumUi, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, holeBounds.Y - panelBounds.Y));
        AddPanelFillBand(gumUi, new Rectangle(panelBounds.X, holeBounds.Bottom, panelBounds.Width, panelBounds.Bottom - holeBounds.Bottom));
        AddPanelFillBand(gumUi, new Rectangle(panelBounds.X, holeBounds.Y, holeBounds.X - panelBounds.X, holeBounds.Height));
        AddPanelFillBand(gumUi, new Rectangle(holeBounds.Right, holeBounds.Y, panelBounds.Right - holeBounds.Right, holeBounds.Height));
        gumUi.AddRoundedOutline(panelBounds, PanelBorder, PanelBorderThickness, PanelRadius);
    }

    private static void AddPanelFillBand(GumUiRenderer gumUi, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        gumUi.AddFilledRectangle(bounds, PanelFill);
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

        if (model.Mode == ResearchTreeMenuMode.TrilodexCatalog)
        {
            GumUiText.AddFittedCentered(gumUi, model.Layout.TitleBounds, model.Title, Color.White, GumTextStyle.Display);
            return;
        }

        GumUiText.AddFittedCentered(gumUi, model.Layout.TitleBounds, model.Title, Color.White, GumTextStyle.UiLarge);
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

        if (model.Config.CardAreaMode == ResearchTreeCardAreaMode.CatalogGrid)
        {
            gumUi.AddRoundedFrame(model.Layout.CardFrameBounds, new Color(12, 25, 37), new Color(58, 87, 103), 2, 16);
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

        gumUi.AddRoundedRectangle(model.Layout.TreeFrameBounds, TreeViewportFrameFill, TreeViewportFrameRadius);

        ResearchNodeInfo? treeHoverInfo = null;
        if (model.TreeViewport.Root is not null)
        {
            var renderConfig = new ResearchTreeRenderConfig(
                model.Config.ShowBackButton,
                model.Config.ShowRootNode,
                model.Config.EnableNodeSelection,
                model.Config.EnableBranchDrafting,
                model.Config.EnablePlacementPreview);
            var metrics = ResearchTreeUiRenderer.CalculateDetailMetrics(
                model.Layout.TreeViewportBounds,
                model.TreeViewport.Root,
                model.TreeViewport.Zoom,
                renderConfig);
            ResearchTreeUiRenderer.DrawDetailBackground(
                gumUi,
                metrics,
                model.TreeViewport.BackgroundTexture,
                model.TreeViewport.PanOffset,
                model.TreeViewport.Zoom);

            if (!model.TreeViewport.OverlayReplacesTreeContent)
            {
                var hoveredNode = ResearchTreeUiRenderer.DrawDetailContent(
                    gumUi,
                    session,
                    metrics,
                    model.TreeViewport.Root,
                    model.TreeViewport.PanOffset,
                    pointerPoint,
                    renderConfig,
                    model.TreeViewport.VisualTimeMs);
                treeHoverInfo = hoveredNode is null ? null : ResearchTreeUiRenderer.BuildNodeInfo(session, hoveredNode);
            }

            if (model.TreeViewport.DrawOverlay is not null)
            {
                treeHoverInfo = model.TreeViewport.DrawOverlay(new ResearchTreeViewportOverlayContext(
                    gumUi,
                    session,
                    model.Layout.TreeViewportBounds,
                    metrics,
                    pointerPoint)) ?? treeHoverInfo;
            }
        }

        return treeHoverInfo;
    }

    private static void DrawTreeViewportHeaderAndBorder(GumUiRenderer gumUi, ResearchTreeMenuModel model)
    {
        if (!model.Layout.TreeHeaderBounds.IsEmpty && !string.IsNullOrWhiteSpace(model.TreeHeaderText))
        {
            GumUiText.Add(gumUi, model.Layout.TreeHeaderBounds, model.TreeHeaderText, new Color(204, 228, 238), GumTextStyle.Small);
        }

        gumUi.AddRoundedOutline(
            model.Layout.TreeFrameBounds,
            TreeViewportFrameBorder,
            ResearchDraftLayout.TreeViewportRimThickness,
            TreeViewportFrameRadius);
    }

    private static void DrawInfoPanel(
        GumUiRenderer gumUi,
        Rectangle bounds,
        ResearchTreeInfoPanelModel model,
        Point pointerPoint)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        if (model.NodeInfo is ResearchNodeInfo info)
        {
            gumUi.AddRoundedFrame(bounds, new Color(9, 18, 28), new Color(204, 228, 238), 2, 16);
            var contentX = bounds.X + 14;
            var contentWidth = bounds.Width - 28;
            var unlockCostBounds = model.UnlockAction is null
                ? Rectangle.Empty
                : ResearchTreeInfoPanelLayout.GetUnlockCostBounds(bounds);
            var effectBottom = unlockCostBounds.IsEmpty
                ? bounds.Bottom - 14
                : Math.Max(bounds.Y + 174, unlockCostBounds.Y - 8);
            GumUiText.Add(gumUi, new Rectangle(contentX, bounds.Y + 12, contentWidth, 18), "Node Details", new Color(204, 228, 238), GumTextStyle.Compact);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 38, contentWidth, 44), "Node", info.TitleText);
            DrawInfoSection(gumUi, new Rectangle(contentX, bounds.Y + 88, contentWidth, 40), "Feature Tree", info.FeatureTreeText);
            var effectLabelBounds = new Rectangle(contentX, bounds.Y + 134, contentWidth, 14);
            GumUiText.Add(gumUi, effectLabelBounds, "Effect", new Color(153, 194, 211), GumTextStyle.Compact, verticalAlignment: VerticalAlignment.Top);
            var effectViewportBounds = new Rectangle(contentX, bounds.Y + 150, contentWidth, Math.Max(20, effectBottom - (bounds.Y + 150)));
            var effectTextLayout = GumScrollableText.Build(effectViewportBounds, info.EffectText, GumTextStyle.Small, model.ScrollOffset);
            GumScrollableText.Draw(gumUi, effectTextLayout, Color.White, GumTextStyle.Small);
            if (effectTextLayout.ScrollbarTrackBounds is { } trackBounds &&
                effectTextLayout.ScrollbarThumbBounds is { } thumbBounds)
            {
                gumUi.AddRoundedRectangle(trackBounds, new Color(10, 22, 32, 210), 3);
                gumUi.AddRoundedRectangle(thumbBounds, new Color(92, 137, 154), 3);
            }

            if (model.UnlockAction is ResearchNodeUnlockActionModel action)
            {
                DrawUnlockAction(gumUi, bounds, action, pointerPoint);
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

    private static void DrawUnlockAction(
        GumUiRenderer gumUi,
        Rectangle infoPanelBounds,
        ResearchNodeUnlockActionModel action,
        Point pointerPoint)
    {
        var buttonBounds = ResearchTreeInfoPanelLayout.GetUnlockButtonBounds(infoPanelBounds);
        var costBounds = ResearchTreeInfoPanelLayout.GetUnlockCostBounds(infoPanelBounds);
        if (!action.IsUnlocked)
        {
            DrawUnlockCostText(gumUi, costBounds, action);
        }

        if (action.CanUnlock)
        {
            GumUiChrome.DrawButton(gumUi, buttonBounds, "Unlock", buttonBounds.Contains(pointerPoint), UnlockButtonStyle);
        }
        else
        {
            GumUiChrome.DrawFrame(gumUi, buttonBounds, DisabledButtonFrame);
            GumUiText.AddFittedCentered(
                gumUi,
                buttonBounds,
                action.IsUnlocked ? "Unlocked" : "Unlock",
                DisabledTextColor,
                GumTextStyle.Small);
        }

        if (!action.CanUnlock &&
            !action.IsUnlocked &&
            buttonBounds.Contains(pointerPoint) &&
            GetUnlockTooltipText(action.BlockReason) is string tooltipText)
        {
            var tooltipBounds = ResearchTreeInfoPanelLayout.GetUnlockTooltipBounds(
                buttonBounds,
                pointerPoint,
                infoPanelBounds,
                tooltipText);
            var textBounds = new Rectangle(tooltipBounds.X + 8, tooltipBounds.Y + 4, tooltipBounds.Width - 16, tooltipBounds.Height - 8);
            GumUiChrome.DrawFrame(gumUi, tooltipBounds, TooltipFrame);
            GumUiText.AddFittedCentered(gumUi, textBounds, tooltipText, Color.White, GumTextStyle.Small);
        }
    }

    private static void DrawUnlockCostText(
        GumUiRenderer gumUi,
        Rectangle bounds,
        ResearchNodeUnlockActionModel action)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var availableText = action.Available.ToString();
        var suffixText = $"/{action.Cost} {action.ResourceType} to unlock";
        var (availableBounds, suffixBounds) = BuildUnlockCostTextLayout(bounds, availableText, suffixText);
        var availableColor = action.Available < action.Cost ? CostWarningColor : Color.White;
        GumUiText.Add(gumUi, availableBounds, availableText, availableColor, GumTextStyle.Compact, maxLines: 1);
        GumUiText.Add(gumUi, suffixBounds, suffixText, Color.White, GumTextStyle.Compact, maxLines: 1);
    }

    internal static (Rectangle AvailableBounds, Rectangle SuffixBounds) BuildUnlockCostTextLayout(
        Rectangle bounds,
        string availableText,
        string suffixText)
    {
        var measuredAvailableWidth = GumTextLayout.Measure(availableText, GumTextStyle.Compact).X;
        var availableWidth = Math.Max(
            measuredAvailableWidth + UnlockCountMinimumSafetyPixels,
            (int)MathF.Ceiling(measuredAvailableWidth * UnlockCountWidthSafetyScale));
        availableWidth = Math.Min(availableWidth, bounds.Width);

        var suffixWidth = GumTextLayout.Measure(suffixText, GumTextStyle.Compact).X;
        var totalWidth = Math.Min(bounds.Width, availableWidth + suffixWidth);
        var x = bounds.X + Math.Max(0, (bounds.Width - totalWidth) / 2);
        var availableBounds = new Rectangle(x, bounds.Y, availableWidth, bounds.Height);
        var suffixBounds = new Rectangle(
            availableBounds.Right,
            bounds.Y,
            Math.Max(0, bounds.Right - availableBounds.Right),
            bounds.Height);
        return (availableBounds, suffixBounds);
    }

    private static string? GetUnlockTooltipText(SkillTreeUnlockBlockReason reason)
    {
        return reason switch
        {
            SkillTreeUnlockBlockReason.NoPathToNode => "No path to node",
            SkillTreeUnlockBlockReason.NotEnoughResources => "Not enough resources to unlock",
            _ => null
        };
    }

}
