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
        var treeWidth = Math.Max(360, (int)MathF.Round(contentWidth * 0.69f));
        var treeBounds = new Rectangle(panelBounds.X + 24, contentTop, treeWidth, contentHeight);
        var treeHeaderBounds = new Rectangle(treeBounds.X + 16, treeBounds.Y + 10, treeBounds.Width - 32, 20);
        var treeViewportBounds = new Rectangle(treeBounds.X + 14, treeBounds.Y + 38, treeBounds.Width - 28, treeBounds.Height - 52);
        var hoverInfoBounds = BuildLeftHoverInfoBounds(viewport, panelBounds);
        var rightHoverInfoBounds = BuildRightHoverInfoBounds(viewport, panelBounds);
        var branchColumnBounds = new Rectangle(treeBounds.Right + 18, contentTop, panelBounds.Right - treeBounds.Right - 42, contentHeight);

        var branchCards = BuildBranchCards(branchColumnBounds, safeBranchCardCount);
        var footerBounds = new Rectangle(panelBounds.X + 24, panelBounds.Bottom - 36, panelBounds.Width - 48, 18);

        return new ResearchDraftLayoutInfo(
            GetSkillTreeButtonBounds(viewport),
            GetSkipButtonBounds(viewport),
            panelBounds,
            closeButtonBounds,
            titleBounds,
            subtitleBounds,
            treeBounds,
            treeHeaderBounds,
            treeViewportBounds,
            hoverInfoBounds,
            rightHoverInfoBounds,
            branchColumnBounds,
            branchCards,
            footerBounds);
    }

    private static Rectangle BuildLeftHoverInfoBounds(Point viewport, Rectangle panelBounds)
    {
        const int minimumWidth = 176;
        const int maximumWidth = 220;
        const int minimumHeight = 196;
        const int screenInset = 12;
        const int panelOverlap = 28;

        var width = Math.Clamp(panelBounds.X + 96, minimumWidth, maximumWidth);
        var height = Math.Max(minimumHeight, panelBounds.Height);
        var x = Math.Max(screenInset, panelBounds.X - width + panelOverlap);
        var y = Math.Clamp(panelBounds.Y, screenInset, Math.Max(screenInset, viewport.Y - height - screenInset));

        return new Rectangle(x, y, width, height);
    }

    private static Rectangle BuildRightHoverInfoBounds(Point viewport, Rectangle panelBounds)
    {
        const int minimumWidth = 176;
        const int maximumWidth = 220;
        const int minimumHeight = 196;
        const int screenInset = 12;
        const int panelOverlap = 28;

        var width = Math.Clamp((viewport.X - panelBounds.Right) + 96, minimumWidth, maximumWidth);
        var height = Math.Max(minimumHeight, panelBounds.Height);
        var x = Math.Min(viewport.X - width - screenInset, panelBounds.Right - panelOverlap);
        var y = Math.Clamp(panelBounds.Y, screenInset, Math.Max(screenInset, viewport.Y - height - screenInset));

        return new Rectangle(x, y, width, height);
    }

    private static IReadOnlyList<Rectangle> BuildBranchCards(Rectangle columnBounds, int branchCardCount)
    {
        if (branchCardCount <= 0)
        {
            return [];
        }

        const int gap = 16;
        var totalGap = gap * Math.Max(0, branchCardCount - 1);
        var cardHeight = Math.Max(128, (columnBounds.Height - totalGap) / branchCardCount);
        var cards = new List<Rectangle>(branchCardCount);
        var y = columnBounds.Y;
        for (var index = 0; index < branchCardCount; index++)
        {
            cards.Add(new Rectangle(columnBounds.X, y, columnBounds.Width, cardHeight));
            y += cardHeight + gap;
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
    Rectangle TreeBounds,
    Rectangle TreeHeaderBounds,
    Rectangle TreeViewportBounds,
    Rectangle HoverInfoBounds,
    Rectangle RightHoverInfoBounds,
    Rectangle BranchColumnBounds,
    IReadOnlyList<Rectangle> BranchCardBounds,
    Rectangle FooterBounds);
