using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Debug;

public static class DebugMenuLayout
{
    public static DebugMenuLayoutInfo Build(Point viewport)
    {
        const float baseOuterMargin = 24f;
        const float basePanelWidth = 560f;
        const float baseMinPanelWidth = 400f;
        const float baseContentPadding = 18f;
        const float baseHeaderHeight = 30f;
        const float baseSummaryHeight = 108f;
        const float basePerformanceHeight = 176f;
        const float baseSectionGap = 8f;
        const float baseSectionLabelHeight = 18f;
        const float baseRowGap = 6f;
        const float baseButtonHeight = 40f;
        const float baseFooterHeight = 20f;
        const float baseButtonGap = 10f;
        const float baseRequiredHeight = 744f;

        var outerMargin = (int)MathF.Round(baseOuterMargin);
        var availableWidth = Math.Max(280, viewport.X - (outerMargin * 2));
        var availableHeight = Math.Max(360, viewport.Y - (outerMargin * 2));
        var scale = Math.Clamp(availableHeight / baseRequiredHeight, 0.78f, 1f);

        var contentPadding = (int)MathF.Round(baseContentPadding * scale);
        var headerHeight = (int)MathF.Round(baseHeaderHeight * scale);
        var summaryHeight = (int)MathF.Round(baseSummaryHeight * scale);
        var performanceHeight = (int)MathF.Round(basePerformanceHeight * scale);
        var sectionGap = (int)MathF.Round(baseSectionGap * scale);
        var sectionLabelHeight = (int)MathF.Round(baseSectionLabelHeight * scale);
        var rowGap = (int)MathF.Round(baseRowGap * scale);
        var buttonHeight = (int)MathF.Round(baseButtonHeight * scale);
        var footerHeight = (int)MathF.Round(baseFooterHeight * scale);
        var buttonGap = (int)MathF.Round(baseButtonGap * scale);

        var quickSectionHeight = sectionLabelHeight + rowGap + buttonHeight;
        var speedSectionHeight = sectionLabelHeight + rowGap + buttonHeight;
        var bfsSectionHeight = sectionLabelHeight + rowGap + buttonHeight + rowGap + buttonHeight;
        var visualSectionHeight = sectionLabelHeight + rowGap + buttonHeight;
        var actionsSectionHeight = sectionLabelHeight + rowGap + buttonHeight;

        var requiredPanelHeight = (contentPadding * 2)
            + headerHeight
            + sectionGap
            + summaryHeight
            + sectionGap
            + performanceHeight
            + sectionGap
            + quickSectionHeight
            + sectionGap
            + speedSectionHeight
            + sectionGap
            + bfsSectionHeight
            + sectionGap
            + visualSectionHeight
            + sectionGap
            + actionsSectionHeight
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

        var performanceBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, performanceHeight);
        cursorY = performanceBounds.Bottom + sectionGap;

        var quickControlsLabelBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, sectionLabelHeight);
        var quickControlsRowBounds = new Rectangle(contentBounds.X, quickControlsLabelBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        cursorY = quickControlsRowBounds.Bottom + sectionGap;

        var speedLabelBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, sectionLabelHeight);
        var speedRowBounds = new Rectangle(contentBounds.X, speedLabelBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        cursorY = speedRowBounds.Bottom + sectionGap;

        var bfsLabelBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, sectionLabelHeight);
        var bfsTopRowBounds = new Rectangle(contentBounds.X, bfsLabelBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        var bfsBottomRowBounds = new Rectangle(contentBounds.X, bfsTopRowBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        cursorY = bfsBottomRowBounds.Bottom + sectionGap;

        var visualLabelBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, sectionLabelHeight);
        var visualRowBounds = new Rectangle(contentBounds.X, visualLabelBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        cursorY = visualRowBounds.Bottom + sectionGap;

        var actionsLabelBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, sectionLabelHeight);
        var actionsRowBounds = new Rectangle(contentBounds.X, actionsLabelBounds.Bottom + rowGap, contentBounds.Width, buttonHeight);
        cursorY = actionsRowBounds.Bottom + sectionGap;

        var footerBounds = new Rectangle(contentBounds.X, cursorY, contentBounds.Width, footerHeight);

        return new DebugMenuLayoutInfo(
            Scale: scale,
            PanelBounds: panelBounds,
            ContentBounds: contentBounds,
            HeaderBounds: headerBounds,
            SummaryBounds: summaryBounds,
            PerformanceBounds: performanceBounds,
            QuickControlsLabelBounds: quickControlsLabelBounds,
            QuickControlsRowBounds: quickControlsRowBounds,
            SpeedLabelBounds: speedLabelBounds,
            SpeedRowBounds: speedRowBounds,
            BfsLabelBounds: bfsLabelBounds,
            BfsTopRowBounds: bfsTopRowBounds,
            BfsBottomRowBounds: bfsBottomRowBounds,
            VisualLabelBounds: visualLabelBounds,
            VisualRowBounds: visualRowBounds,
            ActionsLabelBounds: actionsLabelBounds,
            ActionsRowBounds: actionsRowBounds,
            FooterBounds: footerBounds,
            ButtonGap: buttonGap,
            ContentPadding: contentPadding,
            RowGap: rowGap);
    }

    public static IReadOnlyList<Rectangle> SplitRow(Rectangle rowBounds, int columns, int gap)
    {
        if (columns <= 0)
        {
            return [];
        }

        var widths = new int[columns];
        var availableWidth = rowBounds.Width - (gap * (columns - 1));
        var baseWidth = availableWidth / columns;
        var remainder = availableWidth % columns;
        for (var index = 0; index < columns; index++)
        {
            widths[index] = baseWidth + (index < remainder ? 1 : 0);
        }

        var bounds = new Rectangle[columns];
        var x = rowBounds.X;
        for (var index = 0; index < columns; index++)
        {
            bounds[index] = new Rectangle(x, rowBounds.Y, widths[index], rowBounds.Height);
            x += widths[index] + gap;
        }

        return bounds;
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

public readonly record struct DebugMenuLayoutInfo(
    float Scale,
    Rectangle PanelBounds,
    Rectangle ContentBounds,
    Rectangle HeaderBounds,
    Rectangle SummaryBounds,
    Rectangle PerformanceBounds,
    Rectangle QuickControlsLabelBounds,
    Rectangle QuickControlsRowBounds,
    Rectangle SpeedLabelBounds,
    Rectangle SpeedRowBounds,
    Rectangle BfsLabelBounds,
    Rectangle BfsTopRowBounds,
    Rectangle BfsBottomRowBounds,
    Rectangle VisualLabelBounds,
    Rectangle VisualRowBounds,
    Rectangle ActionsLabelBounds,
    Rectangle ActionsRowBounds,
    Rectangle FooterBounds,
    int ButtonGap,
    int ContentPadding,
    int RowGap);
