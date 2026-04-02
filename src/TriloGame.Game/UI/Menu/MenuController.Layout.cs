using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.ViewModels;

namespace TriloGame.Game.UI.Menu;

public sealed partial class MenuController
{
    private MenuLayout GetLayout(Point viewport, GameSession? session)
    {
        var metrics = GetMetrics(viewport);
        var menuButton = new Rectangle(metrics.ButtonX, metrics.ButtonY, metrics.ButtonWidth, metrics.ButtonHeight);
        var collapseButton = new Rectangle(
            metrics.PanelX + metrics.ContentPadding,
            metrics.PanelY + (int)MathF.Round(16f * metrics.LayoutScale),
            metrics.ButtonWidth,
            metrics.ButtonHeight);
        var panelBounds = new Rectangle(metrics.PanelX, metrics.PanelY, metrics.PanelWidth, metrics.PanelHeight);
        var contentFrameBounds = new Rectangle(
            metrics.PanelX + metrics.ContentPadding,
            metrics.HeaderHeight,
            metrics.PanelWidth - (metrics.ContentPadding * 2),
            metrics.PanelHeight - metrics.HeaderHeight - metrics.ContentPadding);
        var contentBounds = new Rectangle(
            contentFrameBounds.X + metrics.ContentInset,
            contentFrameBounds.Y + metrics.ContentInset,
            contentFrameBounds.Width - (metrics.ContentInset * 2),
            contentFrameBounds.Height - (metrics.ContentInset * 2));

        var tabs = BuildTabs(metrics);

        var buildableOptions = session is null ? [] : GetBuildableOptions(session);
        if (buildableOptions.Count > 0)
        {
            SyncBuildSelection(buildableOptions);
        }

        var buildingScale = Clamp(contentBounds.Height / 760f, 0.84f, 1.18f);
        var buildingSectionGap = (int)MathF.Round(16f * buildingScale);
        var previewHeight = Math.Min(
            (int)MathF.Round(300f * buildingScale),
            Math.Max((int)MathF.Round(190f * buildingScale), (int)MathF.Floor(contentBounds.Height * 0.34f)));
        var previewBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, previewHeight);
        var buildGridFrameBounds = new Rectangle(
            contentBounds.X,
            previewBounds.Bottom + buildingSectionGap,
            contentBounds.Width,
            Math.Max(96, contentBounds.Bottom - previewBounds.Bottom - buildingSectionGap));
        var buildGridViewportBounds = new Rectangle(
            buildGridFrameBounds.X + 12,
            buildGridFrameBounds.Y + 42,
            Math.Max(60, buildGridFrameBounds.Width - 22),
            Math.Max(32, buildGridFrameBounds.Height - 54));

        var buildCards = BuildCardLayout(
            buildGridViewportBounds,
            buildableOptions,
            buildingScale,
            out var buildGridMaxScroll,
            out var buildGridScrollbarTrack,
            out var buildGridScrollbarThumb);
        BuildGridScroll = Clamp(BuildGridScroll, 0f, buildGridMaxScroll);

        var selectedBounds = contentBounds;
        var deleteSelectedBounds = new Rectangle(
            selectedBounds.X + 16,
            selectedBounds.Bottom - (int)MathF.Round(68f * buildingScale),
            Math.Min((int)MathF.Round(240f * buildingScale), selectedBounds.Width - 32),
            (int)MathF.Round(50f * buildingScale));

        var assignmentScale = Clamp(contentBounds.Height / 760f, 0.84f, 1.16f);
        var filterTabHeight = (int)MathF.Round(38f * assignmentScale);
        var filterGap = (int)MathF.Round(8f * assignmentScale);
        var filterWidth = (contentBounds.Width - (filterGap * 3)) / 4;
        var filterY = contentBounds.Y;
        var assignmentFilters = new[]
        {
            new LabeledRect("miner", "Miner", new Rectangle(contentBounds.X, filterY, filterWidth, filterTabHeight)),
            new LabeledRect("builder", "Builder", new Rectangle(contentBounds.X + filterWidth + filterGap, filterY, filterWidth, filterTabHeight)),
            new LabeledRect("farmer", "Farmer", new Rectangle(contentBounds.X + ((filterWidth + filterGap) * 2), filterY, filterWidth, filterTabHeight)),
            new LabeledRect("fighter", "Fighter", new Rectangle(contentBounds.X + ((filterWidth + filterGap) * 3), filterY, filterWidth, filterTabHeight))
        };

        var assignmentSectionGap = (int)MathF.Round(18f * assignmentScale);
        var assignmentLabelHeight = (int)MathF.Round(22f * assignmentScale);
        var assignmentMinBoxHeight = (int)MathF.Round(140f * assignmentScale);
        var assignmentBoxHeight = Math.Max(
            assignmentMinBoxHeight,
            (int)MathF.Floor((contentBounds.Height - filterTabHeight - assignmentLabelHeight - (assignmentSectionGap * 3)) / 2f));
        var assignmentActiveBounds = new Rectangle(
            contentBounds.X,
            contentBounds.Y + filterTabHeight + assignmentSectionGap,
            contentBounds.Width,
            assignmentBoxHeight);
        var assignmentUnassignedLabelBounds = new Rectangle(
            contentBounds.X + 2,
            assignmentActiveBounds.Bottom + assignmentSectionGap,
            contentBounds.Width,
            assignmentLabelHeight);
        var assignmentUnassignedBounds = new Rectangle(
            contentBounds.X,
            assignmentUnassignedLabelBounds.Bottom + (int)MathF.Round(6f * assignmentScale),
            contentBounds.Width,
            Math.Max(assignmentMinBoxHeight, contentBounds.Bottom - assignmentUnassignedLabelBounds.Bottom - (int)MathF.Round(6f * assignmentScale)));
        var assignmentActiveViewportBounds = new Rectangle(
            assignmentActiveBounds.X + 10,
            assignmentActiveBounds.Y + 10,
            assignmentActiveBounds.Width - 20,
            assignmentActiveBounds.Height - 20);
        var assignmentUnassignedViewportBounds = new Rectangle(
            assignmentUnassignedBounds.X + 10,
            assignmentUnassignedBounds.Y + 10,
            assignmentUnassignedBounds.Width - 20,
            assignmentUnassignedBounds.Height - 20);

        var activeEntries = session?.Cave is null
            ? []
            : BuildAssignmentEntries(session.Cave.Trilobites.Where(trilo => trilo.Assignment == AssignmentFilter).ToArray());
        var unassignedEntries = session?.Cave is null
            ? []
            : BuildAssignmentEntries(session.Cave.Trilobites.Where(trilo => trilo.Assignment == "unassigned").ToArray());
        var activeAssignmentRows = BuildAssignmentRows(
            assignmentActiveViewportBounds,
            activeEntries,
            AssignmentActiveScroll,
            AssignmentFilter,
            "unassigned",
            out var activeMaxScroll,
            out var activeTrackBounds,
            out var activeThumbBounds);
        var unassignedAssignmentRows = BuildAssignmentRows(
            assignmentUnassignedViewportBounds,
            unassignedEntries,
            AssignmentUnassignedScroll,
            "unassigned",
            AssignmentFilter,
            out var unassignedMaxScroll,
            out var unassignedTrackBounds,
            out var unassignedThumbBounds);
        AssignmentActiveScroll = Clamp(AssignmentActiveScroll, 0f, activeMaxScroll);
        AssignmentUnassignedScroll = Clamp(AssignmentUnassignedScroll, 0f, unassignedMaxScroll);

        return new MenuLayout(
            metrics.LayoutScale,
            metrics.ContentPadding,
            menuButton,
            collapseButton,
            panelBounds,
            contentFrameBounds,
            tabs,
            previewBounds,
            buildGridFrameBounds,
            buildGridViewportBounds,
            buildCards,
            buildGridMaxScroll,
            buildGridScrollbarTrack,
            buildGridScrollbarThumb,
            selectedBounds,
            deleteSelectedBounds,
            assignmentFilters,
            assignmentActiveBounds,
            assignmentActiveViewportBounds,
            assignmentUnassignedLabelBounds,
            assignmentUnassignedBounds,
            assignmentUnassignedViewportBounds,
            activeAssignmentRows,
            unassignedAssignmentRows,
            activeMaxScroll,
            activeTrackBounds,
            activeThumbBounds,
            unassignedMaxScroll,
            unassignedTrackBounds,
            unassignedThumbBounds);
    }

    private IReadOnlyList<LabeledRect> BuildTabs(MenuMetrics metrics)
    {
        var tabs = GetAvailableTabs();
        var tabGap = (int)MathF.Round(12f * metrics.LayoutScale);
        var totalGapWidth = tabGap * Math.Max(0, tabs.Count - 1);
        var tabWidth = ((metrics.PanelWidth - (metrics.ContentPadding * 2)) - totalGapWidth) / Math.Max(1, tabs.Count);
        var tabX = metrics.PanelX + metrics.ContentPadding;
        var tabY = metrics.PanelY + metrics.HeaderHeight - metrics.TabHeight - (int)MathF.Round(12f * metrics.LayoutScale);

        var result = new List<LabeledRect>(tabs.Count);
        foreach (var tab in tabs)
        {
            result.Add(new LabeledRect(tab.Key, tab.Label, new Rectangle(tabX, tabY, tabWidth, metrics.TabHeight)));
            tabX += tabWidth + tabGap;
        }

        return result;
    }

    private IReadOnlyList<BuildCardRect> BuildCardLayout(
        Rectangle viewportBounds,
        IReadOnlyList<Factory> options,
        float layoutScale,
        out float maxScroll,
        out Rectangle? scrollbarTrackBounds,
        out Rectangle? scrollbarThumbBounds)
    {
        var columns = 4;
        var columnGap = (int)MathF.Round(10f * layoutScale);
        var rowGap = (int)MathF.Round(10f * layoutScale);
        var scrollbarGutter = 10;
        var cardSize = Math.Max(
            72,
            (int)MathF.Floor((viewportBounds.Width - scrollbarGutter - (columnGap * (columns - 1))) / (float)columns));
        var rowCount = (int)MathF.Ceiling(options.Count / (float)columns);
        var contentHeight = rowCount == 0 ? 0 : (rowCount * cardSize) + (Math.Max(0, rowCount - 1) * rowGap);
        maxScroll = Math.Max(0f, contentHeight - viewportBounds.Height);
        BuildGridScroll = Clamp(BuildGridScroll, 0f, maxScroll);

        var cards = new List<BuildCardRect>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = viewportBounds.X + ((cardSize + columnGap) * column);
            var y = viewportBounds.Y + ((cardSize + rowGap) * row) - (int)MathF.Round(BuildGridScroll);
            var bounds = new Rectangle(x, y, cardSize, cardSize);
            if (bounds.Bottom < viewportBounds.Top || bounds.Top > viewportBounds.Bottom)
            {
                continue;
            }

            cards.Add(new BuildCardRect(options[index], bounds));
        }

        if (maxScroll <= 0f)
        {
            scrollbarTrackBounds = null;
            scrollbarThumbBounds = null;
            return cards;
        }

        var trackHeight = viewportBounds.Height;
        var thumbHeight = Math.Max(32f, (viewportBounds.Height / (float)contentHeight) * trackHeight);
        var travel = Math.Max(0f, trackHeight - thumbHeight);
        var ratio = maxScroll <= 0f ? 0f : BuildGridScroll / maxScroll;
        var thumbY = viewportBounds.Y + (int)MathF.Round(ratio * travel);
        var scrollbarX = viewportBounds.Right - 6;
        scrollbarTrackBounds = new Rectangle(scrollbarX, viewportBounds.Y, 6, trackHeight);
        scrollbarThumbBounds = new Rectangle(scrollbarX, thumbY, 6, (int)MathF.Round(thumbHeight));
        return cards;
    }

    private IReadOnlyList<AssignmentRowRect> BuildAssignmentRows(
        Rectangle viewportBounds,
        IReadOnlyList<AssignmentEntryViewModel> entries,
        float requestedScroll,
        string fromAssignment,
        string toAssignment,
        out float maxScroll,
        out Rectangle? scrollbarTrackBounds,
        out Rectangle? scrollbarThumbBounds)
    {
        var rowWidth = viewportBounds.Width - 18;
        var contentHeight = entries.Count == 0 ? 0 : (entries.Count * AssignmentRowHeight) + (Math.Max(0, entries.Count - 1) * AssignmentRowGap);
        maxScroll = Math.Max(0f, contentHeight - viewportBounds.Height);
        var scroll = Clamp(requestedScroll, 0f, maxScroll);

        var rows = new List<AssignmentRowRect>(entries.Count);
        var currentY = viewportBounds.Y - (int)MathF.Round(scroll);
        foreach (var entry in entries)
        {
            var bounds = new Rectangle(viewportBounds.X, currentY, rowWidth, AssignmentRowHeight);
            if (bounds.Bottom >= viewportBounds.Top && bounds.Top <= viewportBounds.Bottom)
            {
                rows.Add(new AssignmentRowRect(fromAssignment, toAssignment, entry, bounds));
            }

            currentY += AssignmentRowHeight + AssignmentRowGap;
        }

        if (maxScroll <= 0f)
        {
            scrollbarTrackBounds = null;
            scrollbarThumbBounds = null;
            return rows;
        }

        var trackHeight = viewportBounds.Height;
        var thumbHeight = Math.Max(32f, (viewportBounds.Height / (float)contentHeight) * trackHeight);
        var travel = Math.Max(0f, trackHeight - thumbHeight);
        var ratio = maxScroll <= 0f ? 0f : scroll / maxScroll;
        var thumbY = viewportBounds.Y + (int)MathF.Round(ratio * travel);
        var scrollbarX = viewportBounds.Right - 6;
        scrollbarTrackBounds = new Rectangle(scrollbarX, viewportBounds.Y, 6, trackHeight);
        scrollbarThumbBounds = new Rectangle(scrollbarX, thumbY, 6, (int)MathF.Round(thumbHeight));
        return rows;
    }

    private static MenuMetrics GetMetrics(Point viewport)
    {
        var layoutScale = Clamp(viewport.Y / 920f, 0.82f, 1.16f);
        var screenPadding = (int)MathF.Round(16f * layoutScale);
        var buttonSize = (int)MathF.Round(44f * layoutScale);
        var availableWidth = Math.Max(300, viewport.X - (screenPadding * 2));
        var panelWidth = Math.Min(520, availableWidth);
        var panelHeight = viewport.Y;
        return new MenuMetrics(
            layoutScale,
            buttonSize,
            buttonSize,
            viewport.X - buttonSize - screenPadding,
            screenPadding,
            panelWidth,
            panelHeight,
            viewport.X - panelWidth,
            0,
            (int)MathF.Round(18f * layoutScale),
            (int)MathF.Round(16f * layoutScale),
            (int)MathF.Round(42f * layoutScale),
            (int)MathF.Round(140f * layoutScale));
    }

    private static Rectangle Inset(Rectangle bounds, int inset)
    {
        return new Rectangle(bounds.X + inset, bounds.Y + inset, Math.Max(0, bounds.Width - (inset * 2)), Math.Max(0, bounds.Height - (inset * 2)));
    }

    private static float Clamp(float value, float min, float max)
    {
        return MathF.Max(min, MathF.Min(max, value));
    }

    private static IReadOnlyList<string> WrapText(SpriteFont font, string text, int maxWidth, int maxLines = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = string.Empty;
            for (var index = 0; index < words.Length; index++)
            {
                var word = words[index];
                if (string.IsNullOrEmpty(current))
                {
                    if (font.MeasureString(word).X > maxWidth)
                    {
                        lines.Add(FitTextToWidth(font, word, maxWidth));
                        continue;
                    }

                    current = word;
                    continue;
                }

                var candidate = $"{current} {word}";
                if (font.MeasureString(candidate).X <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                lines.Add(current);
                current = word;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }
        }

        if (lines.Count > maxLines)
        {
            var clipped = lines.Take(maxLines).ToArray();
            clipped[^1] = FitTextToWidth(font, $"{clipped[^1].TrimEnd()}...", maxWidth);
            return clipped;
        }

        return lines;
    }

    private readonly record struct LabeledRect(string Key, string Label, Rectangle Bounds);

    private readonly record struct BuildCardRect(Factory Factory, Rectangle Bounds);

    private readonly record struct AssignmentRowRect(string FromAssignment, string ToAssignment, AssignmentEntryViewModel Entry, Rectangle Bounds);

    private readonly record struct MenuMetrics(
        float LayoutScale,
        int ButtonWidth,
        int ButtonHeight,
        int ButtonX,
        int ButtonY,
        int PanelWidth,
        int PanelHeight,
        int PanelX,
        int PanelY,
        int ContentPadding,
        int ContentInset,
        int TabHeight,
        int HeaderHeight);

    private sealed record MenuLayout(
        float LayoutScale,
        int ContentPadding,
        Rectangle MenuButton,
        Rectangle CollapseButton,
        Rectangle PanelBounds,
        Rectangle ContentFrameBounds,
        IReadOnlyList<LabeledRect> Tabs,
        Rectangle PreviewBounds,
        Rectangle BuildGridFrameBounds,
        Rectangle BuildGridViewportBounds,
        IReadOnlyList<BuildCardRect> BuildCards,
        float BuildGridMaxScroll,
        Rectangle? BuildGridScrollbarTrackBounds,
        Rectangle? BuildGridScrollbarThumbBounds,
        Rectangle SelectedBounds,
        Rectangle DeleteSelectedBounds,
        IReadOnlyList<LabeledRect> AssignmentFilters,
        Rectangle AssignmentActiveBounds,
        Rectangle AssignmentActiveViewportBounds,
        Rectangle AssignmentUnassignedLabelBounds,
        Rectangle AssignmentUnassignedBounds,
        Rectangle AssignmentUnassignedViewportBounds,
        IReadOnlyList<AssignmentRowRect> ActiveAssignmentRows,
        IReadOnlyList<AssignmentRowRect> UnassignedAssignmentRows,
        float AssignmentActiveMaxScroll,
        Rectangle? AssignmentActiveScrollbarTrackBounds,
        Rectangle? AssignmentActiveScrollbarThumbBounds,
        float AssignmentUnassignedMaxScroll,
        Rectangle? AssignmentUnassignedScrollbarTrackBounds,
        Rectangle? AssignmentUnassignedScrollbarThumbBounds);
}
