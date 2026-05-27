using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Research;

public static class ResearchDraftLayout
{
    private const int ClosedButtonHeight = 44;
    private const int SkillTreeButtonWidth = 188;
    private const int SkipButtonWidth = 128;
    private const int ClosedButtonGap = 10;

    public static Rectangle GetSkillTreeButtonBounds(Point viewport)
    {
        var settingsBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        return new Rectangle(settingsBounds.X, settingsBounds.Bottom + 10, SkillTreeButtonWidth, ClosedButtonHeight);
    }

    public static Rectangle GetSkipButtonBounds(Point viewport)
    {
        var skillTreeBounds = GetSkillTreeButtonBounds(viewport);
        return new Rectangle(skillTreeBounds.Right + ClosedButtonGap, skillTreeBounds.Y, SkipButtonWidth, skillTreeBounds.Height);
    }

    public static Rectangle GetButtonBounds(Point viewport)
    {
        return GetSkillTreeButtonBounds(viewport);
    }

    public static ResearchDraftLayoutInfo Build(Point viewport, int branchCardCount = 3)
    {
        var safeBranchCardCount = Math.Max(0, branchCardCount);
        var width = Math.Min(1120, Math.Max(840, viewport.X - 64));
        var height = Math.Min(760, Math.Max(560, viewport.Y - 64));
        var panelBounds = new Rectangle(
            (viewport.X - width) / 2,
            (viewport.Y - height) / 2,
            width,
            height);
        var closeButtonBounds = new Rectangle(panelBounds.Right - 56, panelBounds.Y + 16, 38, 38);
        var titleBounds = new Rectangle(panelBounds.X + 24, panelBounds.Y + 18, panelBounds.Width - 100, 28);
        var subtitleBounds = new Rectangle(panelBounds.X + 24, panelBounds.Y + 50, panelBounds.Width - 48, 22);

        var contentTop = panelBounds.Y + 84;
        var contentBottom = panelBounds.Bottom - 52;
        var contentHeight = Math.Max(120, contentBottom - contentTop);
        var contentWidth = panelBounds.Width - 48;
        var contentLeft = panelBounds.X + 24;
        var infoPanelWidth = Math.Clamp((int)MathF.Round(contentWidth * 0.25f), 220, 286);
        var contentGap = 18;
        var hasDraftArea = safeBranchCardCount > 0;
        var draftAreaHeight = hasDraftArea
            ? Math.Clamp((int)MathF.Round(contentHeight * 0.28f), 150, 186)
            : 0;
        var draftAreaBounds = hasDraftArea
            ? new Rectangle(contentLeft, contentTop, contentWidth, draftAreaHeight)
            : Rectangle.Empty;
        var draftHeaderBounds = hasDraftArea
            ? new Rectangle(draftAreaBounds.X + 16, draftAreaBounds.Y + 10, draftAreaBounds.Width - 32, 20)
            : Rectangle.Empty;
        var lowerContentTop = hasDraftArea ? draftAreaBounds.Bottom + contentGap : contentTop;
        var lowerContentHeight = Math.Max(160, contentBottom - lowerContentTop);
        var leftContentWidth = Math.Max(360, contentWidth - infoPanelWidth - contentGap);
        var treeBounds = new Rectangle(
            contentLeft,
            lowerContentTop,
            leftContentWidth,
            lowerContentHeight);
        var treeHeaderBounds = new Rectangle(treeBounds.X + 16, treeBounds.Y + 10, treeBounds.Width - 32, 20);
        var treeViewportBounds = new Rectangle(treeBounds.X + 14, treeBounds.Y + 38, treeBounds.Width - 28, treeBounds.Height - 52);
        var infoPanelBounds = new Rectangle(treeBounds.Right + contentGap, lowerContentTop, infoPanelWidth, lowerContentHeight);
        var branchCards = BuildBranchCards(draftAreaBounds, safeBranchCardCount);
        var footerBounds = new Rectangle(panelBounds.X + 24, panelBounds.Bottom - 36, panelBounds.Width - 48, 18);

        return new ResearchDraftLayoutInfo(
            GetSkillTreeButtonBounds(viewport),
            GetSkipButtonBounds(viewport),
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
    Rectangle SkillTreeButtonBounds,
    Rectangle SkipButtonBounds,
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
