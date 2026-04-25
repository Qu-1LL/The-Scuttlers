using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Research;

public static class ResearchDraftLayout
{
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
        var draftAreaHeight = hasDraftArea
            ? Math.Clamp((int)MathF.Round(contentHeight * 0.28f), 150, 186)
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

    private static IReadOnlyList<Rectangle> BuildBranchCards(Rectangle draftAreaBounds, int branchCardCount)
    {
        if (branchCardCount <= 0)
        {
            return [];
        }

        const int gap = 16;
        const int sidePadding = 14;
        const int topPadding = 40;
        const int bottomPadding = 14;
        var totalGap = gap * Math.Max(0, branchCardCount - 1);
        var availableWidth = Math.Max(120, draftAreaBounds.Width - (sidePadding * 2) - totalGap);
        var cardWidth = Math.Max(120, availableWidth / branchCardCount);
        var cardHeight = Math.Max(96, draftAreaBounds.Height - topPadding - bottomPadding);
        var cards = new List<Rectangle>(branchCardCount);
        var x = draftAreaBounds.X + sidePadding;
        var y = draftAreaBounds.Y + topPadding;
        for (var index = 0; index < branchCardCount; index++)
        {
            cards.Add(new Rectangle(x, y, cardWidth, cardHeight));
            x += cardWidth + gap;
        }

        return cards;
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
