using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Research;

public static class TrilodexLayout
{
    public const int CardColumns = 4;

    public static Rectangle GetButtonBounds(Point viewport)
    {
        return SettingsMenuLayout.GetTopHudButtonBounds(viewport, 4);
    }

    public static TrilodexLayoutInfo Build(Point viewport, int treeCount, float scroll = 0f)
    {
        var panelBounds = ResearchDraftLayout.Build(viewport, branchCardCount: 0).PanelBounds;
        var closeButtonBounds = new Rectangle(panelBounds.Right - 56, panelBounds.Y + 16, 38, 38);
        var backButtonBounds = new Rectangle(panelBounds.X + 18, panelBounds.Y + 16, 44, 38);
        var titleBounds = new Rectangle(panelBounds.X + 78, panelBounds.Y + 16, panelBounds.Width - 156, 34);
        var subtitleBounds = new Rectangle(panelBounds.X + 78, panelBounds.Y + 52, panelBounds.Width - 156, 22);
        var gridFrameBounds = new Rectangle(
            panelBounds.X + 24,
            panelBounds.Y + 88,
            panelBounds.Width - 48,
            panelBounds.Height - 138);
        var gridViewportBounds = new Rectangle(
            gridFrameBounds.X + 14,
            gridFrameBounds.Y + 14,
            gridFrameBounds.Width - 28,
            gridFrameBounds.Height - 28);
        var detailGap = 18;
        var detailInfoPanelWidth = Math.Clamp((int)MathF.Round(gridFrameBounds.Width * 0.25f), 220, 286);
        var detailTreeFrameBounds = new Rectangle(
            gridFrameBounds.X,
            gridFrameBounds.Y,
            Math.Max(360, gridFrameBounds.Width - detailInfoPanelWidth - detailGap),
            gridFrameBounds.Height);
        var detailTreeViewportBounds = new Rectangle(
            detailTreeFrameBounds.X + 14,
            detailTreeFrameBounds.Y + 14,
            detailTreeFrameBounds.Width - 28,
            detailTreeFrameBounds.Height - 28);
        var detailInfoPanelBounds = new Rectangle(
            detailTreeFrameBounds.Right + detailGap,
            gridFrameBounds.Y,
            detailInfoPanelWidth,
            gridFrameBounds.Height);

        var cardBounds = BuildCards(gridViewportBounds, treeCount, scroll, out var maxScroll);
        var scrollbarTrackBounds = maxScroll > 0f
            ? new Rectangle(gridFrameBounds.Right - 10, gridViewportBounds.Y, 5, gridViewportBounds.Height)
            : Rectangle.Empty;
        var scrollbarThumbBounds = maxScroll > 0f
            ? BuildScrollbarThumb(scrollbarTrackBounds, scroll, maxScroll, gridViewportBounds.Height, CalculateContentHeight(treeCount))
            : Rectangle.Empty;

        return new TrilodexLayoutInfo(
            GetButtonBounds(viewport),
            panelBounds,
            closeButtonBounds,
            backButtonBounds,
            titleBounds,
            subtitleBounds,
            gridFrameBounds,
            gridViewportBounds,
            detailTreeFrameBounds,
            detailTreeViewportBounds,
            detailInfoPanelBounds,
            cardBounds,
            maxScroll,
            scrollbarTrackBounds,
            scrollbarThumbBounds);
    }

    private static IReadOnlyList<Rectangle> BuildCards(Rectangle viewportBounds, int treeCount, float scroll, out float maxScroll)
    {
        if (treeCount <= 0)
        {
            maxScroll = 0f;
            return [];
        }

        const int gap = 14;
        const int cardHeight = 190;
        var contentHeight = CalculateContentHeight(treeCount);
        maxScroll = Math.Max(0f, contentHeight - viewportBounds.Height);
        var clampedScroll = Math.Clamp(scroll, 0f, maxScroll);
        var cardWidth = Math.Max(120, (viewportBounds.Width - (gap * (CardColumns - 1))) / CardColumns);
        var cards = new List<Rectangle>(treeCount);
        for (var index = 0; index < treeCount; index++)
        {
            var column = index % CardColumns;
            var row = index / CardColumns;
            cards.Add(new Rectangle(
                viewportBounds.X + (column * (cardWidth + gap)),
                viewportBounds.Y + (row * (cardHeight + gap)) - (int)MathF.Round(clampedScroll),
                cardWidth,
                cardHeight));
        }

        return cards;
    }

    private static int CalculateContentHeight(int treeCount)
    {
        if (treeCount <= 0)
        {
            return 0;
        }

        const int gap = 14;
        const int cardHeight = 190;
        var rows = (int)MathF.Ceiling(treeCount / (float)CardColumns);
        return (rows * cardHeight) + (Math.Max(0, rows - 1) * gap);
    }

    private static Rectangle BuildScrollbarThumb(Rectangle trackBounds, float scroll, float maxScroll, int viewportHeight, int contentHeight)
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

public readonly record struct TrilodexLayoutInfo(
    Rectangle ButtonBounds,
    Rectangle PanelBounds,
    Rectangle CloseButtonBounds,
    Rectangle BackButtonBounds,
    Rectangle TitleBounds,
    Rectangle SubtitleBounds,
    Rectangle GridFrameBounds,
    Rectangle GridViewportBounds,
    Rectangle DetailTreeFrameBounds,
    Rectangle DetailTreeViewportBounds,
    Rectangle DetailInfoPanelBounds,
    IReadOnlyList<Rectangle> CardBounds,
    float MaxScroll,
    Rectangle ScrollbarTrackBounds,
    Rectangle ScrollbarThumbBounds);
