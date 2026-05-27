using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Research;

public static class ResearchDraftLayout
{
    public const int TreeCatalogCardColumns = 4;
    private const int DraftCardTopPadding = 40;
    private const int DraftCardBottomPadding = 14;
    private const int TreeCatalogCardGap = 14;

    public static Rectangle GetButtonBounds(Point viewport)
    {
        return SettingsMenuLayout.GetTopHudButtonBounds(viewport, 3);
    }

    public static ResearchDraftLayoutInfo Build(Point viewport, int branchCardCount = 3)
    {
        var safeBranchCardCount = Math.Max(0, branchCardCount);
        var width = Math.Min(1360, Math.Max(960, viewport.X - 36));
        var height = Math.Min(880, Math.Max(640, viewport.Y - 36));
        var panelBounds = new Rectangle(
            (viewport.X - width) / 2,
            (viewport.Y - height) / 2,
            width,
            height);
        var closeButtonBounds = new Rectangle(panelBounds.Right - 56, panelBounds.Y + 16, 38, 38);
        var titleBounds = new Rectangle(panelBounds.X + 24, panelBounds.Y + 18, panelBounds.Width - 100, 28);
        var subtitleBounds = new Rectangle(panelBounds.X + 24, panelBounds.Y + 50, panelBounds.Width - 48, 22);

        var contentTop = panelBounds.Y + 86;
        var contentBottom = panelBounds.Bottom - 50;
        var contentHeight = Math.Max(120, contentBottom - contentTop);
        var contentWidth = panelBounds.Width - 48;
        var infoPanelWidth = Math.Clamp((int)MathF.Round(contentWidth * 0.25f), 220, 286);
        var contentGap = 18;
        var leftContentWidth = Math.Max(360, contentWidth - infoPanelWidth - contentGap);
        var hasDraftArea = safeBranchCardCount > 0;
        var preferredDraftAreaHeight = ResearchTreeCardRenderer.PreferredCardHeight + DraftCardTopPadding + DraftCardBottomPadding;
        var maxDraftAreaHeight = Math.Max(150, contentHeight - 180);
        var draftAreaHeight = hasDraftArea
            ? Math.Clamp(
                preferredDraftAreaHeight,
                150,
                maxDraftAreaHeight)
            : 0;
        var draftAreaBounds = hasDraftArea
            ? new Rectangle(panelBounds.X + 24, contentTop, leftContentWidth, draftAreaHeight)
            : Rectangle.Empty;
        var draftHeaderBounds = hasDraftArea
            ? new Rectangle(draftAreaBounds.X + 16, draftAreaBounds.Y + 10, draftAreaBounds.Width - 32, 20)
            : Rectangle.Empty;
        var treeTop = hasDraftArea ? draftAreaBounds.Bottom + contentGap : contentTop;
        var treeBounds = new Rectangle(
            panelBounds.X + 24,
            treeTop,
            leftContentWidth,
            Math.Max(160, contentBottom - treeTop));
        var treeHeaderBounds = new Rectangle(treeBounds.X + 16, treeBounds.Y + 10, treeBounds.Width - 32, 20);
        var treeViewportBounds = new Rectangle(treeBounds.X + 14, treeBounds.Y + 38, treeBounds.Width - 28, treeBounds.Height - 52);
        var infoPanelBounds = new Rectangle(treeBounds.Right + contentGap, contentTop, infoPanelWidth, contentHeight);
        var branchCards = BuildBranchCards(draftAreaBounds, safeBranchCardCount);
        var footerBounds = new Rectangle(panelBounds.X + 24, panelBounds.Bottom - 36, panelBounds.Width - 48, 18);

        return new ResearchDraftLayoutInfo(
            GetButtonBounds(viewport),
            panelBounds,
            closeButtonBounds,
            titleBounds,
            subtitleBounds,
            draftAreaBounds,
            draftHeaderBounds,
            treeBounds,
            treeHeaderBounds,
            treeViewportBounds,
            infoPanelBounds,
            branchCards,
            footerBounds);
    }

    public static ResearchDraftTreeCatalogLayoutInfo BuildTreeCatalog(Point viewport, int treeCount, float scroll = 0f)
    {
        var panelLayout = Build(viewport, branchCardCount: 0);
        var panelBounds = panelLayout.PanelBounds;
        var closeButtonBounds = panelLayout.CloseButtonBounds;
        var backButtonBounds = new Rectangle(panelBounds.X + 18, panelBounds.Y + 16, 44, 38);
        var titleBounds = new Rectangle(panelBounds.X + 78, panelBounds.Y + 16, panelBounds.Width - 156, 34);
        var subtitleBounds = new Rectangle(panelBounds.X + 78, panelBounds.Y + 52, panelBounds.Width - 156, 22);

        var catalogFrameBounds = new Rectangle(
            panelBounds.X + 24,
            panelBounds.Y + 88,
            panelBounds.Width - 48,
            panelBounds.Height - 138);
        var catalogViewportBounds = new Rectangle(
            catalogFrameBounds.X + 14,
            catalogFrameBounds.Y + 14,
            catalogFrameBounds.Width - 28,
            catalogFrameBounds.Height - 28);

        var detailGap = 18;
        var detailInfoPanelWidth = Math.Clamp((int)MathF.Round(catalogFrameBounds.Width * 0.25f), 220, 286);
        var detailTreeFrameBounds = new Rectangle(
            catalogFrameBounds.X,
            catalogFrameBounds.Y,
            Math.Max(360, catalogFrameBounds.Width - detailInfoPanelWidth - detailGap),
            catalogFrameBounds.Height);
        var detailTreeViewportBounds = new Rectangle(
            detailTreeFrameBounds.X + 14,
            detailTreeFrameBounds.Y + 14,
            detailTreeFrameBounds.Width - 28,
            detailTreeFrameBounds.Height - 28);
        var detailInfoPanelBounds = new Rectangle(
            detailTreeFrameBounds.Right + detailGap,
            catalogFrameBounds.Y,
            detailInfoPanelWidth,
            catalogFrameBounds.Height);

        var cardBounds = BuildTreeCatalogCards(catalogViewportBounds, treeCount, scroll, out var maxScroll);
        var scrollbarTrackBounds = maxScroll > 0f
            ? new Rectangle(catalogFrameBounds.Right - 10, catalogViewportBounds.Y, 5, catalogViewportBounds.Height)
            : Rectangle.Empty;
        var scrollbarThumbBounds = maxScroll > 0f
            ? BuildTreeCatalogScrollbarThumb(
                scrollbarTrackBounds,
                scroll,
                maxScroll,
                catalogViewportBounds.Height,
                CalculateTreeCatalogContentHeight(treeCount))
            : Rectangle.Empty;

        return new ResearchDraftTreeCatalogLayoutInfo(
            panelLayout.ButtonBounds,
            panelBounds,
            closeButtonBounds,
            backButtonBounds,
            titleBounds,
            subtitleBounds,
            catalogFrameBounds,
            catalogViewportBounds,
            detailTreeFrameBounds,
            detailTreeViewportBounds,
            detailInfoPanelBounds,
            cardBounds,
            maxScroll,
            scrollbarTrackBounds,
            scrollbarThumbBounds);
    }

    private static IReadOnlyList<Rectangle> BuildBranchCards(Rectangle draftAreaBounds, int branchCardCount)
    {
        if (branchCardCount <= 0)
        {
            return [];
        }

        const int gap = 16;
        const int sidePadding = 14;
        var totalGap = gap * Math.Max(0, branchCardCount - 1);
        var availableWidth = Math.Max(120, draftAreaBounds.Width - (sidePadding * 2) - totalGap);
        var cardWidth = Math.Max(120, availableWidth / branchCardCount);
        var cardHeight = Math.Max(96, draftAreaBounds.Height - DraftCardTopPadding - DraftCardBottomPadding);
        var cards = new List<Rectangle>(branchCardCount);
        var x = draftAreaBounds.X + sidePadding;
        var y = draftAreaBounds.Y + DraftCardTopPadding;
        for (var index = 0; index < branchCardCount; index++)
        {
            cards.Add(new Rectangle(x, y, cardWidth, cardHeight));
            x += cardWidth + gap;
        }

        return cards;
    }

    private static IReadOnlyList<Rectangle> BuildTreeCatalogCards(Rectangle viewportBounds, int treeCount, float scroll, out float maxScroll)
    {
        if (treeCount <= 0)
        {
            maxScroll = 0f;
            return [];
        }

        var contentHeight = CalculateTreeCatalogContentHeight(treeCount);
        maxScroll = Math.Max(0f, contentHeight - viewportBounds.Height);
        var clampedScroll = Math.Clamp(scroll, 0f, maxScroll);
        var cardWidth = Math.Max(120, (viewportBounds.Width - (TreeCatalogCardGap * (TreeCatalogCardColumns - 1))) / TreeCatalogCardColumns);
        var cards = new List<Rectangle>(treeCount);
        for (var index = 0; index < treeCount; index++)
        {
            var column = index % TreeCatalogCardColumns;
            var row = index / TreeCatalogCardColumns;
            cards.Add(new Rectangle(
                viewportBounds.X + (column * (cardWidth + TreeCatalogCardGap)),
                viewportBounds.Y + (row * (ResearchTreeCardRenderer.PreferredCardHeight + TreeCatalogCardGap)) - (int)MathF.Round(clampedScroll),
                cardWidth,
                ResearchTreeCardRenderer.PreferredCardHeight));
        }

        return cards;
    }

    private static int CalculateTreeCatalogContentHeight(int treeCount)
    {
        if (treeCount <= 0)
        {
            return 0;
        }

        var rows = (int)MathF.Ceiling(treeCount / (float)TreeCatalogCardColumns);
        return (rows * ResearchTreeCardRenderer.PreferredCardHeight) + (Math.Max(0, rows - 1) * TreeCatalogCardGap);
    }

    private static Rectangle BuildTreeCatalogScrollbarThumb(Rectangle trackBounds, float scroll, float maxScroll, int viewportHeight, int contentHeight)
    {
        var thumbHeight = Math.Clamp(
            (int)MathF.Round(trackBounds.Height * (viewportHeight / (float)Math.Max(viewportHeight, contentHeight))),
            36,
            trackBounds.Height);
        var travel = Math.Max(0, trackBounds.Height - thumbHeight);
        var y = trackBounds.Y + (int)MathF.Round(travel * (Math.Clamp(scroll, 0f, maxScroll) / maxScroll));
        return new Rectangle(trackBounds.X, y, trackBounds.Width, thumbHeight);
    }
}

public readonly record struct ResearchDraftLayoutInfo(
    Rectangle ButtonBounds,
    Rectangle PanelBounds,
    Rectangle CloseButtonBounds,
    Rectangle TitleBounds,
    Rectangle SubtitleBounds,
    Rectangle DraftAreaBounds,
    Rectangle DraftHeaderBounds,
    Rectangle TreeBounds,
    Rectangle TreeHeaderBounds,
    Rectangle TreeViewportBounds,
    Rectangle InfoPanelBounds,
    IReadOnlyList<Rectangle> BranchCardBounds,
    Rectangle FooterBounds);

public readonly record struct ResearchDraftTreeCatalogLayoutInfo(
    Rectangle ButtonBounds,
    Rectangle PanelBounds,
    Rectangle CloseButtonBounds,
    Rectangle BackButtonBounds,
    Rectangle TitleBounds,
    Rectangle SubtitleBounds,
    Rectangle CatalogFrameBounds,
    Rectangle CatalogViewportBounds,
    Rectangle DetailTreeFrameBounds,
    Rectangle DetailTreeViewportBounds,
    Rectangle DetailInfoPanelBounds,
    IReadOnlyList<Rectangle> CardBounds,
    float MaxScroll,
    Rectangle ScrollbarTrackBounds,
    Rectangle ScrollbarThumbBounds);
