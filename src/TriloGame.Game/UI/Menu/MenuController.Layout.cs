using Microsoft.Xna.Framework;
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
        var selectedScale = Clamp(contentBounds.Height / 760f, 0.84f, 1.16f);
        var selectedRenameFieldBounds = Rectangle.Empty;
        Rectangle? selectedRenamePrimaryButtonBounds = null;
        Rectangle? selectedRenameSecondaryButtonBounds = null;
        Rectangle? selectedTraitSummaryBounds = null;
        Rectangle? selectedInventoryFrameBounds = null;
        Rectangle? selectedInventoryViewportBounds = null;
        IReadOnlyList<InventoryEntryRect> selectedInventoryEntries = [];
        float selectedInventoryMaxScroll = 0f;
        Rectangle? selectedInventoryScrollbarTrackBounds = null;
        Rectangle? selectedInventoryScrollbarThumbBounds = null;
        var selectedDescriptionBounds = new Rectangle(
            selectedBounds.X + 16,
            selectedBounds.Y + 132,
            selectedBounds.Width - 32,
            Math.Max(60, selectedBounds.Height - 220));
        if (SelectedObject is Trilobite)
        {
            var traitTop = selectedBounds.Y + (int)MathF.Round(122f * selectedScale);
            var traitLineHeight = (int)MathF.Round(18f * selectedScale);
            var traitLineGap = (int)MathF.Round(6f * selectedScale);
            selectedTraitSummaryBounds = new Rectangle(
                selectedBounds.X + 16,
                traitTop,
                selectedBounds.Width - 32,
                traitLineHeight);

            var renameRowY = selectedTraitSummaryBounds.Value.Bottom + traitLineGap + (int)MathF.Round(16f * selectedScale);
            var renameRowHeight = (int)MathF.Round(44f * selectedScale);
            var renameGap = (int)MathF.Round(10f * selectedScale);
            var buttonWidth = Math.Min((int)MathF.Round(112f * selectedScale), Math.Max(92, selectedBounds.Width / 4));
            var cancelWidth = _renamingSelectedTrilobite
                ? Math.Min((int)MathF.Round(96f * selectedScale), Math.Max(84, selectedBounds.Width / 4))
                : 0;
            var trailingWidth = buttonWidth + (_renamingSelectedTrilobite ? renameGap + cancelWidth : 0);
            selectedRenameFieldBounds = new Rectangle(
                selectedBounds.X + 16,
                renameRowY,
                Math.Max(120, selectedBounds.Width - 32 - trailingWidth - renameGap),
                renameRowHeight);
            selectedRenamePrimaryButtonBounds = new Rectangle(
                selectedRenameFieldBounds.Right + renameGap,
                renameRowY,
                buttonWidth,
                renameRowHeight);
            if (_renamingSelectedTrilobite)
            {
                selectedRenameSecondaryButtonBounds = new Rectangle(
                    selectedRenamePrimaryButtonBounds.Value.Right + renameGap,
                    renameRowY,
                    cancelWidth,
                    renameRowHeight);
            }

            var descriptionTop = renameRowY + renameRowHeight + (int)MathF.Round(18f * selectedScale);
            selectedDescriptionBounds = new Rectangle(
                selectedBounds.X + 16,
                descriptionTop,
                selectedBounds.Width - 32,
                Math.Max(60, selectedBounds.Bottom - descriptionTop - (int)MathF.Round(84f * selectedScale)));
        }

        var deleteSelectedBounds = new Rectangle(
            selectedBounds.X + 16,
            selectedBounds.Bottom - (int)MathF.Round(68f * selectedScale),
            Math.Min((int)MathF.Round(240f * buildingScale), selectedBounds.Width - 32),
            (int)MathF.Round(50f * selectedScale));

        if (SelectedObject is IStorage storage)
        {
            selectedInventoryFrameBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedBounds.Y + 132,
                selectedBounds.Width - 32,
                Math.Max(120, deleteSelectedBounds.Y - selectedBounds.Y - 150));
            selectedInventoryViewportBounds = new Rectangle(
                selectedInventoryFrameBounds.Value.X + 10,
                selectedInventoryFrameBounds.Value.Y + 38,
                selectedInventoryFrameBounds.Value.Width - 20,
                Math.Max(48, selectedInventoryFrameBounds.Value.Height - 48));
            var inventoryEntries = BuildInventoryEntries(storage);
            selectedInventoryEntries = BuildInventoryLayout(
                selectedInventoryViewportBounds.Value,
                inventoryEntries,
                selectedScale,
                out selectedInventoryMaxScroll,
                out selectedInventoryScrollbarTrackBounds,
                out selectedInventoryScrollbarThumbBounds);
            SelectedInventoryScroll = Clamp(SelectedInventoryScroll, 0f, selectedInventoryMaxScroll);
        }

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
            selectedRenameFieldBounds,
            selectedRenamePrimaryButtonBounds,
            selectedRenameSecondaryButtonBounds,
            selectedTraitSummaryBounds,
            selectedInventoryFrameBounds,
            selectedInventoryViewportBounds,
            selectedInventoryEntries,
            selectedInventoryMaxScroll,
            selectedInventoryScrollbarTrackBounds,
            selectedInventoryScrollbarThumbBounds,
            selectedDescriptionBounds,
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

    private IReadOnlyList<InventoryEntryData> BuildInventoryEntries(IStorage storage)
    {
        var result = new List<InventoryEntryData>();
        foreach (var pair in storage.GetInventory())
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            result.Add(new InventoryEntryData(pair.Key, pair.Value, GetInventoryTextureKey(pair.Key)));
        }

        return result;
    }

    private IReadOnlyList<InventoryEntryRect> BuildInventoryLayout(
        Rectangle viewportBounds,
        IReadOnlyList<InventoryEntryData> entries,
        float layoutScale,
        out float maxScroll,
        out Rectangle? scrollbarTrackBounds,
        out Rectangle? scrollbarThumbBounds)
    {
        const int columns = 4;
        var columnGap = (int)MathF.Round(10f * layoutScale);
        var rowGap = (int)MathF.Round(10f * layoutScale);
        var scrollbarGutter = 10;
        var cardWidth = Math.Max(
            76,
            (int)MathF.Floor((viewportBounds.Width - scrollbarGutter - (columnGap * (columns - 1))) / (float)columns));
        var cardHeight = Math.Max((int)MathF.Round(132f * layoutScale), cardWidth + (int)MathF.Round(34f * layoutScale));
        var rowCount = (int)MathF.Ceiling(entries.Count / (float)columns);
        var contentHeight = rowCount == 0 ? 0 : (rowCount * cardHeight) + (Math.Max(0, rowCount - 1) * rowGap);
        maxScroll = Math.Max(0f, contentHeight - viewportBounds.Height);
        SelectedInventoryScroll = Clamp(SelectedInventoryScroll, 0f, maxScroll);

        var cards = new List<InventoryEntryRect>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = viewportBounds.X + ((cardWidth + columnGap) * column);
            var y = viewportBounds.Y + ((cardHeight + rowGap) * row) - (int)MathF.Round(SelectedInventoryScroll);
            var bounds = new Rectangle(x, y, cardWidth, cardHeight);
            if (bounds.Bottom < viewportBounds.Top || bounds.Top > viewportBounds.Bottom)
            {
                continue;
            }

            cards.Add(new InventoryEntryRect(entries[index].ResourceType, entries[index].TextureKey, entries[index].Quantity, bounds));
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
        var ratio = maxScroll <= 0f ? 0f : SelectedInventoryScroll / maxScroll;
        var thumbY = viewportBounds.Y + (int)MathF.Round(ratio * travel);
        var scrollbarX = viewportBounds.Right - 6;
        scrollbarTrackBounds = new Rectangle(scrollbarX, viewportBounds.Y, 6, trackHeight);
        scrollbarThumbBounds = new Rectangle(scrollbarX, thumbY, 6, (int)MathF.Round(thumbHeight));
        return cards;
    }

    private static string GetInventoryTextureKey(string resourceType)
    {
        return resourceType switch
        {
            "wall" => "wall",
            _ => resourceType
        };
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

    private readonly record struct LabeledRect(string Key, string Label, Rectangle Bounds);

    private readonly record struct BuildCardRect(Factory Factory, Rectangle Bounds);

    private readonly record struct AssignmentRowRect(string FromAssignment, string ToAssignment, AssignmentEntryViewModel Entry, Rectangle Bounds);

    private readonly record struct InventoryEntryData(string ResourceType, int Quantity, string TextureKey);

    private readonly record struct InventoryEntryRect(string ResourceType, string TextureKey, int Quantity, Rectangle Bounds);

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
        Rectangle SelectedRenameFieldBounds,
        Rectangle? SelectedRenamePrimaryButtonBounds,
        Rectangle? SelectedRenameSecondaryButtonBounds,
        Rectangle? SelectedTraitSummaryBounds,
        Rectangle? SelectedInventoryFrameBounds,
        Rectangle? SelectedInventoryViewportBounds,
        IReadOnlyList<InventoryEntryRect> SelectedInventoryEntries,
        float SelectedInventoryMaxScroll,
        Rectangle? SelectedInventoryScrollbarTrackBounds,
        Rectangle? SelectedInventoryScrollbarThumbBounds,
        Rectangle SelectedDescriptionBounds,
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
