using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;
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
        var activeBuildPreview = HoveredBuildOption ?? SelectedBuildOption;
        var activeBuildPreviewKey = activeBuildPreview?.Name ?? string.Empty;
        if (!string.Equals(_buildPreviewScrollKey, activeBuildPreviewKey, StringComparison.Ordinal))
        {
            BuildPreviewDescriptionScroll = 0f;
            _buildPreviewScrollKey = activeBuildPreviewKey;
        }

        // The cost sits on its own row under the size, and the description starts below it. Every
        // offset in this panel is fixed pixels, so the description viewport gives up the same 14px at
        // any panel height - which is why the row is packed tight against the size line above it and
        // the description follows 6px later rather than the 12px the size line used to get. At the
        // smallest window the preview is only 160px tall, and a more generous spacing here left the
        // description too short for even one line plus its scrollbar thumb.
        var buildPreviewCostBounds = activeBuildPreview is null
            ? (Rectangle?)null
            : new Rectangle(
                previewBounds.X + 12,
                previewBounds.Y + 86,
                Math.Max(100, (previewBounds.Width / 2) + 12),
                20);
        var buildPreviewCostText = activeBuildPreview is null
            ? null
            : FormatConstructionCost(activeBuildPreview.Recipe);
        var buildPreviewDescriptionViewportBounds = activeBuildPreview is null
            ? new Rectangle(
                previewBounds.X + 12,
                previewBounds.Y + 44,
                previewBounds.Width - 24,
                previewBounds.Height - 56)
            : new Rectangle(
                previewBounds.X + 12,
                previewBounds.Y + 112,
                Math.Max(140, (previewBounds.Width / 2) - 18),
                Math.Max(32, previewBounds.Height - 124));
        var buildPreviewDescriptionLayout = GumScrollableText.Build(
            buildPreviewDescriptionViewportBounds,
            activeBuildPreview?.Description ?? "Hover over a building card or click one to keep it selected here.",
            GumTextStyle.Small,
            BuildPreviewDescriptionScroll);
        BuildPreviewDescriptionScroll = buildPreviewDescriptionLayout.Scroll;

        var selectedBounds = contentBounds;
        var selectedScale = Clamp(contentBounds.Height / 760f, 0.84f, 1.16f);
        var selectedRenameFieldBounds = Rectangle.Empty;
        Rectangle? selectedRenamePrimaryButtonBounds = null;
        Rectangle? selectedRenameSecondaryButtonBounds = null;
        Rectangle? selectedTraitSummaryBounds = null;
        Rectangle? selectedRecipeBounds = null;
        string? selectedRecipeText = null;
        Rectangle? selectedInventoryFrameBounds = null;
        Rectangle? selectedInventoryViewportBounds = null;
        IReadOnlyList<InventoryEntryRect> selectedInventoryEntries = [];
        float selectedInventoryMaxScroll = 0f;
        Rectangle? selectedInventoryScrollbarTrackBounds = null;
        Rectangle? selectedInventoryScrollbarThumbBounds = null;
        Rectangle? selectedProcessingInputFrameBounds = null;
        Rectangle? selectedProcessingInputViewportBounds = null;
        IReadOnlyList<InventoryEntryRect> selectedProcessingInputEntries = [];
        float selectedProcessingInputMaxScroll = 0f;
        Rectangle? selectedProcessingInputScrollbarTrackBounds = null;
        Rectangle? selectedProcessingInputScrollbarThumbBounds = null;
        Rectangle? selectedProcessingOutputFrameBounds = null;
        Rectangle? selectedProcessingOutputViewportBounds = null;
        IReadOnlyList<InventoryEntryRect> selectedProcessingOutputEntries = [];
        float selectedProcessingOutputMaxScroll = 0f;
        Rectangle? selectedProcessingOutputScrollbarTrackBounds = null;
        Rectangle? selectedProcessingOutputScrollbarThumbBounds = null;
        var selectedDetailTop = selectedBounds.Y + 118;
        GumScrollableTextLayout selectedDescriptionLayout = default;
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

            selectedDetailTop = renameRowY + renameRowHeight;
        }
        else if (SelectedObject is Building)
        {
            selectedDetailTop = selectedBounds.Y + 144;
        }

        var deleteSelectedBounds = new Rectangle(
            selectedBounds.X + 16,
            selectedBounds.Bottom - (int)MathF.Round(68f * selectedScale),
            Math.Min((int)MathF.Round(240f * buildingScale), selectedBounds.Width - 32),
            (int)MathF.Round(50f * selectedScale));
        Rectangle? buildFirstSelectedBounds = null;
        if (SelectedObject is Scaffolding)
        {
            var buildFirstHeight = (int)MathF.Round(50f * selectedScale);
            var buildFirstGap = (int)MathF.Round(8f * selectedScale);
            buildFirstSelectedBounds = new Rectangle(
                deleteSelectedBounds.X,
                deleteSelectedBounds.Y - buildFirstGap - buildFirstHeight,
                deleteSelectedBounds.Width,
                buildFirstHeight);
        }

        if (SelectedObject is Trilobite selectedTrilobite)
        {
            var selectedBodyTop = selectedDetailTop + (int)MathF.Round(14f * selectedScale);
            var bodyBottom = deleteSelectedBounds.Y - (int)MathF.Round(10f * selectedScale);
            var bodyHeight = Math.Max(96, bodyBottom - selectedBodyTop);
            var inventoryDescriptionGap = (int)MathF.Round(12f * selectedScale);
            var minimumInventoryHeight = Math.Max(72, (int)MathF.Round(86f * selectedScale));
            var descriptionHeight = Math.Clamp(
                (int)MathF.Round(78f * selectedScale),
                48,
                Math.Max(48, bodyHeight - minimumInventoryHeight - inventoryDescriptionGap));
            var inventoryHeight = Math.Max(48, bodyHeight - descriptionHeight - inventoryDescriptionGap);

            selectedInventoryFrameBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedBodyTop,
                selectedBounds.Width - 32,
                inventoryHeight);
            selectedInventoryViewportBounds = new Rectangle(
                selectedInventoryFrameBounds.Value.X + 10,
                selectedInventoryFrameBounds.Value.Y + 38,
                selectedInventoryFrameBounds.Value.Width - 20,
                Math.Max(48, selectedInventoryFrameBounds.Value.Height - 48));

            var inventoryEntries = BuildInventoryEntries(selectedTrilobite);
            selectedInventoryEntries = BuildInventoryLayout(
                selectedInventoryViewportBounds.Value,
                inventoryEntries,
                selectedScale,
                SelectedInventoryScroll,
                out selectedInventoryMaxScroll,
                out selectedInventoryScrollbarTrackBounds,
                out selectedInventoryScrollbarThumbBounds);
            SelectedInventoryScroll = Clamp(SelectedInventoryScroll, 0f, selectedInventoryMaxScroll);

            var selectedDescriptionViewportBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedInventoryFrameBounds.Value.Bottom + inventoryDescriptionGap,
                selectedBounds.Width - 32,
                Math.Max(48, deleteSelectedBounds.Y - selectedInventoryFrameBounds.Value.Bottom - inventoryDescriptionGap));
            selectedDescriptionLayout = GumScrollableText.Build(
                selectedDescriptionViewportBounds,
                "Kill this trilobite immediately. This uses the normal in-game removal flow and clears the current selection afterward.",
                GumTextStyle.Small,
                SelectedDescriptionScroll);
            SelectedDescriptionScroll = selectedDescriptionLayout.Scroll;
        }
        else if (SelectedObject is IProcessingBuilding processing)
        {
            var selectedBodyTop = selectedDetailTop + (int)MathF.Round(14f * selectedScale);
            var selectedBodyBottom = deleteSelectedBounds.Y - (int)MathF.Round(14f * selectedScale);
            var sectionGap = Math.Max(8, (int)MathF.Round(12f * selectedScale));
            var sectionHeight = Math.Max(64, (selectedBodyBottom - selectedBodyTop - sectionGap) / 2);
            selectedProcessingInputFrameBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedBodyTop,
                selectedBounds.Width - 32,
                sectionHeight);
            selectedProcessingOutputFrameBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedProcessingInputFrameBounds.Value.Bottom + sectionGap,
                selectedBounds.Width - 32,
                sectionHeight);
            selectedProcessingInputViewportBounds = new Rectangle(
                selectedProcessingInputFrameBounds.Value.X + 10,
                selectedProcessingInputFrameBounds.Value.Y + 38,
                selectedProcessingInputFrameBounds.Value.Width - 20,
                Math.Max(24, selectedProcessingInputFrameBounds.Value.Height - 48));
            selectedProcessingOutputViewportBounds = new Rectangle(
                selectedProcessingOutputFrameBounds.Value.X + 10,
                selectedProcessingOutputFrameBounds.Value.Y + 38,
                selectedProcessingOutputFrameBounds.Value.Width - 20,
                Math.Max(24, selectedProcessingOutputFrameBounds.Value.Height - 48));

            selectedProcessingInputEntries = BuildInventoryLayout(
                selectedProcessingInputViewportBounds.Value,
                BuildProcessingInventoryEntries(processing, isInput: true),
                selectedScale,
                SelectedProcessingInputScroll,
                out selectedProcessingInputMaxScroll,
                out selectedProcessingInputScrollbarTrackBounds,
                out selectedProcessingInputScrollbarThumbBounds);
            SelectedProcessingInputScroll = Clamp(SelectedProcessingInputScroll, 0f, selectedProcessingInputMaxScroll);

            selectedProcessingOutputEntries = BuildInventoryLayout(
                selectedProcessingOutputViewportBounds.Value,
                BuildProcessingInventoryEntries(processing, isInput: false),
                selectedScale,
                SelectedProcessingOutputScroll,
                out selectedProcessingOutputMaxScroll,
                out selectedProcessingOutputScrollbarTrackBounds,
                out selectedProcessingOutputScrollbarThumbBounds);
            SelectedProcessingOutputScroll = Clamp(SelectedProcessingOutputScroll, 0f, selectedProcessingOutputMaxScroll);
        }
        else if (SelectedObject is IResourceStorage storage)
        {
            var minimumInventoryHeight = Math.Max(72, (int)MathF.Round(96f * selectedScale));
            var selectedBodyTop = Math.Min(
                selectedDetailTop + (int)MathF.Round(14f * selectedScale),
                deleteSelectedBounds.Y - minimumInventoryHeight - 14);
            selectedInventoryFrameBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedBodyTop,
                selectedBounds.Width - 32,
                Math.Max(minimumInventoryHeight, deleteSelectedBounds.Y - selectedBodyTop - 14));
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
                SelectedInventoryScroll,
                out selectedInventoryMaxScroll,
                out selectedInventoryScrollbarTrackBounds,
                out selectedInventoryScrollbarThumbBounds);
            SelectedInventoryScroll = Clamp(SelectedInventoryScroll, 0f, selectedInventoryMaxScroll);
        }
        else if (SelectedObject is Scaffolding scaffolding)
        {
            var recipeTop = selectedDetailTop + (int)MathF.Round(10f * selectedScale);
            var recipeHeight = (int)MathF.Round(22f * selectedScale);
            var recipeGap = (int)MathF.Round(12f * selectedScale);
            selectedRecipeBounds = new Rectangle(
                selectedBounds.X + 16,
                recipeTop,
                selectedBounds.Width - 32,
                recipeHeight);
            selectedRecipeText = BuildScaffoldingRecipeText(scaffolding);

            var selectedBodyTop = selectedRecipeBounds.Value.Bottom + recipeGap;
            var actionTop = buildFirstSelectedBounds?.Y ?? deleteSelectedBounds.Y;
            var bodyBottom = actionTop - (int)MathF.Round(10f * selectedScale);
            var bodyHeight = Math.Max(96, bodyBottom - selectedBodyTop);
            var inventoryDescriptionGap = (int)MathF.Round(12f * selectedScale);
            var minimumInventoryHeight = Math.Max(72, (int)MathF.Round(86f * selectedScale));
            var descriptionHeight = Math.Clamp(
                (int)MathF.Round(78f * selectedScale),
                48,
                Math.Max(48, bodyHeight - minimumInventoryHeight - inventoryDescriptionGap));
            var inventoryHeight = Math.Max(48, bodyHeight - descriptionHeight - inventoryDescriptionGap);

            selectedInventoryFrameBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedBodyTop,
                selectedBounds.Width - 32,
                inventoryHeight);
            selectedInventoryViewportBounds = new Rectangle(
                selectedInventoryFrameBounds.Value.X + 10,
                selectedInventoryFrameBounds.Value.Y + 38,
                selectedInventoryFrameBounds.Value.Width - 20,
                Math.Max(48, selectedInventoryFrameBounds.Value.Height - 48));
            var inventoryEntries = BuildInventoryEntries(scaffolding);
            selectedInventoryEntries = BuildInventoryLayout(
                selectedInventoryViewportBounds.Value,
                inventoryEntries,
                selectedScale,
                SelectedInventoryScroll,
                out selectedInventoryMaxScroll,
                out selectedInventoryScrollbarTrackBounds,
                out selectedInventoryScrollbarThumbBounds);
            SelectedInventoryScroll = Clamp(SelectedInventoryScroll, 0f, selectedInventoryMaxScroll);

            var selectedDescriptionViewportBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedInventoryFrameBounds.Value.Bottom + inventoryDescriptionGap,
                selectedBounds.Width - 32,
                Math.Max(48, actionTop - selectedInventoryFrameBounds.Value.Bottom - inventoryDescriptionGap));
            selectedDescriptionLayout = GumScrollableText.Build(
                selectedDescriptionViewportBounds,
                BuildSelectedDescriptionText(scaffolding),
                GumTextStyle.Small,
                SelectedDescriptionScroll);
            SelectedDescriptionScroll = selectedDescriptionLayout.Scroll;
        }
        else if (SelectedObject is Creature or Building)
        {
            const int minimumDescriptionHeight = 48;
            var selectedBodyTop = Math.Min(
                selectedDetailTop + (int)MathF.Round(18f * selectedScale),
                deleteSelectedBounds.Y - minimumDescriptionHeight - 14);
            var selectedDescriptionViewportBounds = new Rectangle(
                selectedBounds.X + 16,
                selectedBodyTop,
                selectedBounds.Width - 32,
                Math.Max(minimumDescriptionHeight, deleteSelectedBounds.Y - selectedBodyTop - 14));
            selectedDescriptionLayout = GumScrollableText.Build(
                selectedDescriptionViewportBounds,
                BuildSelectedDescriptionText(SelectedObject),
                GumTextStyle.Small,
                SelectedDescriptionScroll);
            SelectedDescriptionScroll = selectedDescriptionLayout.Scroll;
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
            buildPreviewCostBounds,
            buildPreviewCostText,
            buildPreviewDescriptionLayout,
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
            selectedRecipeBounds,
            selectedRecipeText,
            selectedInventoryFrameBounds,
            selectedInventoryViewportBounds,
            selectedInventoryEntries,
            selectedInventoryMaxScroll,
            selectedInventoryScrollbarTrackBounds,
            selectedInventoryScrollbarThumbBounds,
            selectedProcessingInputFrameBounds,
            selectedProcessingInputViewportBounds,
            selectedProcessingInputEntries,
            selectedProcessingInputMaxScroll,
            selectedProcessingInputScrollbarTrackBounds,
            selectedProcessingInputScrollbarThumbBounds,
            selectedProcessingOutputFrameBounds,
            selectedProcessingOutputViewportBounds,
            selectedProcessingOutputEntries,
            selectedProcessingOutputMaxScroll,
            selectedProcessingOutputScrollbarTrackBounds,
            selectedProcessingOutputScrollbarThumbBounds,
            selectedDescriptionLayout,
            deleteSelectedBounds,
            buildFirstSelectedBounds,
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
            var factory = options[index];
            var column = index % columns;
            var row = index / columns;
            var x = viewportBounds.X + ((cardSize + columnGap) * column);
            var y = viewportBounds.Y + ((cardSize + rowGap) * row) - (int)MathF.Round(BuildGridScroll);
            var bounds = new Rectangle(x, y, cardSize, cardSize);
            if (bounds.Bottom < viewportBounds.Top || bounds.Top > viewportBounds.Bottom)
            {
                continue;
            }

            cards.Add(new BuildCardRect(factory, bounds));
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

    private IReadOnlyList<InventoryEntryData> BuildInventoryEntries(IResourceStorage storage)
    {
        var result = new List<InventoryEntryData>();
        foreach (var pair in storage.GetStoredResources())
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            result.Add(new InventoryEntryData(ItemCatalog.GetName(pair.Key), pair.Value, ItemCatalog.GetTextureKey(pair.Key)));
        }

        return result;
    }

    // Processing panels list configured resources even when empty so their independent limits stay visible.
    private static IReadOnlyList<InventoryEntryData> BuildProcessingInventoryEntries(IProcessingBuilding processing, bool isInput)
    {
        var definitions = isInput ? processing.InputDefinitions : processing.OutputDefinitions;
        var result = new List<InventoryEntryData>(definitions.Count);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var amount = isInput
                ? processing.GetInputAmount(definition.ResourceType)
                : processing.GetOutputAmount(definition.ResourceType);
            result.Add(new InventoryEntryData(
                ItemCatalog.GetName(definition.ResourceType),
                amount,
                ItemCatalog.GetTextureKey(definition.ResourceType),
                definition.Capacity));
        }

        return result;
    }

    private static IReadOnlyList<InventoryEntryData> BuildInventoryEntries(Scaffolding scaffolding)
    {
        var result = new List<InventoryEntryData>();
        var depositedResources = scaffolding.GetDepositedResources();
        foreach (var item in ItemCatalog.GetStockpileOrder())
        {
            if (!depositedResources.TryGetValue(item.Resource, out var amount) || amount <= 0)
            {
                continue;
            }

            result.Add(new InventoryEntryData(item.Name, amount, item.TextureKey));
        }

        return result;
    }

    private static IReadOnlyList<InventoryEntryData> BuildInventoryEntries(IInventoryCarrier carrier)
    {
        if (!carrier.HasInventory())
        {
            return [];
        }

        var result = new List<InventoryEntryData>(carrier.Inventory.ResourceTypeCount);
        for (var index = 0; index < carrier.Inventory.ResourceTypeCount; index++)
        {
            var resourceType = carrier.Inventory.GetResourceTypeAt(index);
            var amount = carrier.Inventory.GetAmount(resourceType);
            if (amount <= 0)
            {
                continue;
            }

            result.Add(new InventoryEntryData(
                ItemCatalog.GetName(resourceType),
                amount,
                ItemCatalog.GetTextureKey(resourceType)));
        }

        return result;
    }

    private static string BuildSelectedDescriptionText(object? selectedObject)
    {
        return selectedObject switch
        {
            Creature => "Kill this trilobite immediately. This uses the normal in-game removal flow and clears the current selection afterward.",
            Building building when !string.IsNullOrWhiteSpace(building.Description)
                => $"{building.Description}\n\nDelete this building from the cave immediately. This uses the normal in-game removal flow and clears the current selection afterward.",
            _ => "Delete this building from the cave immediately. This uses the normal in-game removal flow and clears the current selection afterward."
        };
    }

    private static string BuildScaffoldingRecipeText(Scaffolding scaffolding)
    {
        var requirements = new string[scaffolding.RecipeRequired.Count];
        for (var index = 0; index < scaffolding.RecipeRequired.Count; index++)
        {
            requirements[index] = FormatRequirement(scaffolding.RecipeRequired[index]);
        }

        return $"Recipe: {string.Join(", ", requirements)}";
    }

    private static int GetScaffoldingRequiredAmount(Scaffolding scaffolding)
    {
        var total = 0;
        for (var index = 0; index < scaffolding.RecipeRequired.Count; index++)
        {
            total += scaffolding.RecipeRequired[index].Amount;
        }

        return total;
    }

    // What a building costs to put up, for the build menu's preview panel.
    //
    // This is the same recipe the scaffold will demand once the site is placed, so the number the
    // player reads here is the number they will have to deliver - it is not a separate estimate.
    //
    // A building with no recipe reports "None" rather than being left off the panel. Silently
    // omitting the row makes a missing recipe look like a rendering gap; and it is worth surfacing,
    // because Scaffolding throws on a recipe-less building rather than treating it as free.
    private static string FormatConstructionCost(IReadOnlyList<ResourceRequirement>? recipe)
    {
        if (recipe is null || recipe.Count == 0)
        {
            return "Cost: None";
        }

        var requirements = new string[recipe.Count];
        for (var index = 0; index < recipe.Count; index++)
        {
            requirements[index] = FormatRequirement(recipe[index]);
        }

        return $"Cost: {string.Join(", ", requirements)}";
    }

    private static string FormatRequirement(ResourceRequirement requirement)
    {
        var label = requirement.SpecificResource is { } resourceType
            ? ItemCatalog.GetName(resourceType)
            : GetResourceCategoryLabel(requirement.Category!.Value);
        return $"{requirement.Amount} {label}";
    }

    private static string GetResourceCategoryLabel(ResourceCategory category)
    {
        return category.ToString();
    }

    private IReadOnlyList<InventoryEntryRect> BuildInventoryLayout(
        Rectangle viewportBounds,
        IReadOnlyList<InventoryEntryData> entries,
        float layoutScale,
        float scroll,
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
        scroll = Clamp(scroll, 0f, maxScroll);

        var cards = new List<InventoryEntryRect>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = viewportBounds.X + ((cardWidth + columnGap) * column);
            var y = viewportBounds.Y + ((cardHeight + rowGap) * row) - (int)MathF.Round(scroll);
            var bounds = new Rectangle(x, y, cardWidth, cardHeight);
            if (bounds.Bottom < viewportBounds.Top || bounds.Top > viewportBounds.Bottom)
            {
                continue;
            }

            cards.Add(new InventoryEntryRect(entries[index].ResourceType, entries[index].TextureKey, entries[index].Quantity, entries[index].Capacity, bounds));
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
        var ratio = maxScroll <= 0f ? 0f : scroll / maxScroll;
        var thumbY = viewportBounds.Y + (int)MathF.Round(ratio * travel);
        var scrollbarX = viewportBounds.Right - 6;
        scrollbarTrackBounds = new Rectangle(scrollbarX, viewportBounds.Y, 6, trackHeight);
        scrollbarThumbBounds = new Rectangle(scrollbarX, thumbY, 6, (int)MathF.Round(thumbHeight));
        return cards;
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

    private readonly record struct InventoryEntryData(string ResourceType, int Quantity, string TextureKey, int? Capacity = null);

    private readonly record struct InventoryEntryRect(string ResourceType, string TextureKey, int Quantity, int? Capacity, Rectangle Bounds);

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
        Rectangle? BuildPreviewCostBounds,
        string? BuildPreviewCostText,
        GumScrollableTextLayout BuildPreviewDescriptionLayout,
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
        Rectangle? SelectedRecipeBounds,
        string? SelectedRecipeText,
        Rectangle? SelectedInventoryFrameBounds,
        Rectangle? SelectedInventoryViewportBounds,
        IReadOnlyList<InventoryEntryRect> SelectedInventoryEntries,
        float SelectedInventoryMaxScroll,
        Rectangle? SelectedInventoryScrollbarTrackBounds,
        Rectangle? SelectedInventoryScrollbarThumbBounds,
        Rectangle? SelectedProcessingInputFrameBounds,
        Rectangle? SelectedProcessingInputViewportBounds,
        IReadOnlyList<InventoryEntryRect> SelectedProcessingInputEntries,
        float SelectedProcessingInputMaxScroll,
        Rectangle? SelectedProcessingInputScrollbarTrackBounds,
        Rectangle? SelectedProcessingInputScrollbarThumbBounds,
        Rectangle? SelectedProcessingOutputFrameBounds,
        Rectangle? SelectedProcessingOutputViewportBounds,
        IReadOnlyList<InventoryEntryRect> SelectedProcessingOutputEntries,
        float SelectedProcessingOutputMaxScroll,
        Rectangle? SelectedProcessingOutputScrollbarTrackBounds,
        Rectangle? SelectedProcessingOutputScrollbarThumbBounds,
        GumScrollableTextLayout SelectedDescriptionLayout,
        Rectangle DeleteSelectedBounds,
        Rectangle? BuildFirstSelectedBounds,
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
