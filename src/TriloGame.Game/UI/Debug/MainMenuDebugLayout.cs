using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Debug;

public static class MainMenuDebugLayout
{
    // Keep the main-menu debug panel visually aligned with the in-session debug overlay while sizing for startup-only controls.
    public static MainMenuDebugLayoutInfo Build(Point viewport, int optionCount, bool dropdownExpanded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);

        const float baseOuterMargin = 24f;
        const float basePanelWidth = 560f;
        const float baseMinPanelWidth = 400f;
        const float baseContentPadding = 18f;
        const float baseHeaderHeight = 30f;
        const float baseSummaryHeight = 78f;
        const float baseSectionGap = 8f;
        const float baseSectionLabelHeight = 18f;
        const float baseRowGap = 6f;
        const float baseButtonHeight = 40f;
        const float baseFooterHeight = 20f;

        var outerMargin = (int)MathF.Round(baseOuterMargin);
        var availableWidth = Math.Max(280, viewport.X - (outerMargin * 2));
        var availableHeight = Math.Max(220, viewport.Y - (outerMargin * 2));
        var baseOptionsHeight = dropdownExpanded && optionCount > 0
            ? (optionCount * baseButtonHeight) + ((optionCount - 1) * baseRowGap)
            : 0f;
        var baseRequiredHeight = (baseContentPadding * 2f)
            + baseHeaderHeight
            + baseSectionGap
            + baseSummaryHeight
            + baseSectionGap
            + baseSectionLabelHeight
            + baseRowGap
            + baseButtonHeight
            + (baseOptionsHeight > 0f ? baseRowGap + baseOptionsHeight : 0f)
            + baseSectionGap
            + baseFooterHeight;
        var scale = MathF.Min(1f, availableHeight / baseRequiredHeight);

        var contentPadding = (int)MathF.Round(baseContentPadding * scale);
        var headerHeight = (int)MathF.Round(baseHeaderHeight * scale);
        var summaryHeight = (int)MathF.Round(baseSummaryHeight * scale);
        var sectionGap = (int)MathF.Round(baseSectionGap * scale);
        var sectionLabelHeight = (int)MathF.Round(baseSectionLabelHeight * scale);
        var rowGap = (int)MathF.Round(baseRowGap * scale);
        var buttonHeight = (int)MathF.Round(baseButtonHeight * scale);
        var footerHeight = (int)MathF.Round(baseFooterHeight * scale);

        var optionsHeight = dropdownExpanded && optionCount > 0
            ? (optionCount * buttonHeight) + ((optionCount - 1) * rowGap)
            : 0;
        var requiredPanelHeight = (contentPadding * 2)
            + headerHeight
            + sectionGap
            + summaryHeight
            + sectionGap
            + sectionLabelHeight
            + rowGap
            + buttonHeight
            + (optionsHeight > 0 ? rowGap + optionsHeight : 0)
            + sectionGap
            + footerHeight;

        var minPanelWidth = Math.Min(availableWidth, (int)MathF.Round(baseMinPanelWidth * scale));
        var panelWidth = Math.Clamp((int)MathF.Round(basePanelWidth), minPanelWidth, availableWidth);
        var panelHeight = Math.Min(requiredPanelHeight, availableHeight);

        var panelBounds = new Rectangle(outerMargin, outerMargin, panelWidth, panelHeight);
        var contentBounds = Inset(panelBounds, contentPadding);
        var cursorY = contentBounds.Y;

        var headerBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, headerHeight);
        cursorY = headerBounds.Bottom + sectionGap;

        var summaryBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, summaryHeight);
        cursorY = summaryBounds.Bottom + sectionGap;

        var worldGenerationLabelBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, sectionLabelHeight);
        var dropdownBounds = new Rectangle(contentBounds.X, worldGenerationLabelBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        cursorY = dropdownBounds.Bottom;

        Rectangle? dropdownOptionsBounds = null;
        if (optionsHeight > 0)
        {
            dropdownOptionsBounds = new Rectangle(contentBounds.X, cursorY + rowGap, contentBounds.Width, optionsHeight);
            cursorY = dropdownOptionsBounds.Value.Bottom;
        }

        var footerBounds = new Rectangle(contentBounds.X, cursorY + sectionGap, contentBounds.Width, footerHeight);

        return new MainMenuDebugLayoutInfo(
            Scale: scale,
            PanelBounds: panelBounds,
            ContentBounds: contentBounds,
            HeaderBounds: headerBounds,
            SummaryBounds: summaryBounds,
            WorldGenerationLabelBounds: worldGenerationLabelBounds,
            DropdownBounds: dropdownBounds,
            DropdownOptionsBounds: dropdownOptionsBounds,
            FooterBounds: footerBounds,
            RowGap: rowGap);
    }

    public static IReadOnlyList<Rectangle> StackRows(Rectangle bounds, int rowCount, int gap)
    {
        if (rowCount <= 0)
        {
            return [];
        }

        var heights = new int[rowCount];
        var availableHeight = bounds.Height - (gap * (rowCount - 1));
        var baseHeight = availableHeight / rowCount;
        var remainder = availableHeight % rowCount;
        for (var index = 0; index < rowCount; index++)
        {
            heights[index] = baseHeight + (index < remainder ? 1 : 0);
        }

        var rows = new Rectangle[rowCount];
        var y = bounds.Y;
        for (var index = 0; index < rowCount; index++)
        {
            rows[index] = new Rectangle(bounds.X, y, bounds.Width, heights[index]);
            y += heights[index] + gap;
        }

        return rows;
    }

    private static Rectangle Inset(Rectangle bounds, int inset)
    {
        return new Rectangle(
            bounds.X + inset,
            bounds.Y + inset,
            Math.Max(0, bounds.Width - (inset * 2)),
            Math.Max(0, bounds.Height - (inset * 2)));
    }
}

public readonly record struct MainMenuDebugLayoutInfo(
    float Scale,
    Rectangle PanelBounds,
    Rectangle ContentBounds,
    Rectangle HeaderBounds,
    Rectangle SummaryBounds,
    Rectangle WorldGenerationLabelBounds,
    Rectangle DropdownBounds,
    Rectangle? DropdownOptionsBounds,
    Rectangle FooterBounds,
    int RowGap);
