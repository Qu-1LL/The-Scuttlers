using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Rendering;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Game.UI.Menu;

public sealed partial class MenuController
{
    private void DrawBuildingsTab(RenderingContext context, MenuLayout layout, GameSession session)
    {
        DrawFrame(context, layout.ContentFrameBounds, new Color(13, 28, 40), new Color(35, 56, 72));
        DrawFrame(context, layout.PreviewBounds, new Color(18, 37, 52), new Color(74, 114, 132));
        DrawFrame(context, layout.BuildGridFrameBounds, new Color(13, 31, 44), new Color(53, 84, 102));

        DrawTextFitted(
            context,
            "BUILDING PREVIEW",
            new Rectangle(layout.PreviewBounds.X + 12, layout.PreviewBounds.Y + 8, layout.PreviewBounds.Width - 24, 24),
            new Color(159, 195, 210));
        DrawTextFitted(
            context,
            "BUILDINGS",
            new Rectangle(layout.BuildGridFrameBounds.X + 12, layout.BuildGridFrameBounds.Y + 8, layout.BuildGridFrameBounds.Width - 24, 24),
            new Color(159, 195, 210));

        var activeFactory = HoveredBuildOption ?? SelectedBuildOption;
        if (activeFactory is not null)
        {
            DrawTextFitted(
                context,
                activeFactory.Name,
                new Rectangle(layout.PreviewBounds.X + 12, layout.PreviewBounds.Y + 36, Math.Max(100, (layout.PreviewBounds.Width / 2) + 12), 28),
                Color.White,
                large: true);
            DrawTextFitted(
                context,
                $"Size: {activeFactory.Size.X} x {activeFactory.Size.Y}",
                new Rectangle(layout.PreviewBounds.X + 12, layout.PreviewBounds.Y + 66, Math.Max(100, (layout.PreviewBounds.Width / 2) + 12), 20),
                new Color(135, 173, 187));

            var descriptionBounds = new Rectangle(
                layout.PreviewBounds.X + 12,
                layout.PreviewBounds.Y + 98,
                Math.Max(140, (layout.PreviewBounds.Width / 2) - 18),
                layout.PreviewBounds.Height - 110);
            DrawWrappedText(context, activeFactory.Description, descriptionBounds, new Color(226, 238, 244));

            DrawPreviewTexture(
                context,
                activeFactory.TextureKey,
                new Rectangle(layout.PreviewBounds.Right - 160, layout.PreviewBounds.Y + 22, 132, 132));
        }
        else
        {
            DrawWrappedText(
                context,
                "Hover over a building card or click one to keep it selected here.",
                new Rectangle(layout.PreviewBounds.X + 12, layout.PreviewBounds.Y + 44, layout.PreviewBounds.Width - 24, layout.PreviewBounds.Height - 56),
                new Color(210, 228, 236));
        }

        foreach (var card in layout.BuildCards)
        {
            var isSelected = SelectedBuildOption?.Name == card.Factory.Name;
            var isHovered = HoveredBuildOption?.Name == card.Factory.Name || card.Bounds.Contains(_pointerPoint);
            DrawBuildCard(context, card, isSelected, isHovered);
        }

        DrawScrollbar(context, layout.BuildGridScrollbarTrackBounds, layout.BuildGridScrollbarThumbBounds);
    }

    private void DrawAssignmentsTab(RenderingContext context, MenuLayout layout, GameSession session)
    {
        foreach (var filter in layout.AssignmentFilters)
        {
            var active = AssignmentFilter == filter.Key;
            var hovered = filter.Bounds.Contains(_pointerPoint);
            DrawTabButton(context, filter.Bounds, filter.Label, active, hovered);
        }

        DrawFrame(context, layout.AssignmentActiveBounds, new Color(13, 31, 44), new Color(53, 84, 102));
        DrawText(
            context,
            "Unassigned",
            new Vector2(layout.AssignmentUnassignedLabelBounds.X, layout.AssignmentUnassignedLabelBounds.Y),
            new Color(226, 238, 244));
        DrawFrame(context, layout.AssignmentUnassignedBounds, new Color(13, 31, 44), new Color(53, 84, 102));

        if (layout.ActiveAssignmentRows.Count == 0)
        {
            DrawWrappedText(
                context,
                "No trilobites are in this assignment.",
                Inset(layout.AssignmentActiveViewportBounds, 8),
                new Color(210, 228, 236));
        }

        foreach (var row in layout.ActiveAssignmentRows)
        {
            DrawAssignmentRow(context, row);
        }

        if (layout.UnassignedAssignmentRows.Count == 0)
        {
            DrawWrappedText(
                context,
                "No unassigned trilobites are available.",
                Inset(layout.AssignmentUnassignedViewportBounds, 8),
                new Color(210, 228, 236));
        }

        foreach (var row in layout.UnassignedAssignmentRows)
        {
            DrawAssignmentRow(context, row);
        }

        DrawScrollbar(context, layout.AssignmentActiveScrollbarTrackBounds, layout.AssignmentActiveScrollbarThumbBounds);
        DrawScrollbar(context, layout.AssignmentUnassignedScrollbarTrackBounds, layout.AssignmentUnassignedScrollbarThumbBounds);
    }

    private void DrawSelectedTab(RenderingContext context, MenuLayout layout)
    {
        DrawFrame(context, layout.SelectedBounds, new Color(18, 37, 52), new Color(74, 114, 132));

        var title = SelectedObject switch
        {
            Creature creature => creature.Name,
            IVehicle vehicle => vehicle.Name,
            Building building => building.Name,
            _ => "No Selection"
        };
        var objectType = SelectedObject switch
        {
            Trilobite => "Trilobite",
            Ranch => "Ranch",
            SoilArea => "Soil Area",
            SoilAreaSelection selection => selection.Mode == SoilAreaSelectionMode.Row ? "Soil Row" : "Soil Column",
            Creature => "Creature",
            IVehicle => "Vehicle",
            _ => "Building"
        };
        var healthText = SelectedObject switch
        {
            Creature selectedHealthCreature => $"Health: {selectedHealthCreature.Health}/{selectedHealthCreature.MaxHealth}",
            IVehicle selectedVehicle => $"Health: {selectedVehicle.Health}/{selectedVehicle.MaxHealth}",
            _ => null
        };
        var detailText = SelectedObject switch
        {
            Creature selectedCreature => $"Assignment: {selectedCreature.Assignment}",
            IVehicle selectedVehicle => $"Assignment: {selectedVehicle.AssignmentClassification}",
            IStorage storage => $"Stored: {storage.GetInventoryTotal()}/{storage.Capacity}",
            _ => $"Type: {title}"
        };
        var supplementalText = SelectedObject switch
        {
            Building selectedBuilding => $"Assigned Trilobites: {GetSelectedBuildingAssignmentCount(selectedBuilding)}",
            IVehicle selectedVehicle => $"Stationed Trilobites: {selectedVehicle.StationedCreatures.Count}/{selectedVehicle.MaxStationedCreatures}",
            _ => null
        };
        var canRename = SelectedObject is Trilobite;
        var bodyText = SelectedObject switch
        {
            Trilobite => "Kill this trilobite immediately.",
            Creature => "Kill this creature immediately.",
            IVehicle => "Delete this vehicle from the cave immediately.",
            Ranch => "Delete this ranch, including its garage and connected soil patches.",
            SoilArea => "Delete this soil area from the cave immediately.",
            SoilAreaSelection selection => selection.Mode == SoilAreaSelectionMode.Row
                ? "Delete this selected soil row from the cave immediately."
                : "Delete this selected soil column from the cave immediately.",
            _ => "Delete this building from the cave immediately."
        };
        var headerBounds = new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 10, layout.SelectedBounds.Width - 32, 22);
        var healthBounds = healthText is null
            ? Rectangle.Empty
            : new Rectangle(headerBounds.Right - Math.Min(156, headerBounds.Width / 2), headerBounds.Y, Math.Min(156, headerBounds.Width / 2), headerBounds.Height);
        var titleHeaderBounds = healthText is null
            ? headerBounds
            : new Rectangle(headerBounds.X, headerBounds.Y, Math.Max(80, healthBounds.X - headerBounds.X - 8), headerBounds.Height);

        DrawTextFitted(
            context,
            "SELECTED OBJECT",
            titleHeaderBounds,
            new Color(159, 195, 210));
        if (healthText is not null)
        {
            DrawTextFittedRight(
                context,
                healthText,
                healthBounds,
                new Color(210, 228, 236));
        }
        DrawTextFitted(
            context,
            title,
            new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 38, layout.SelectedBounds.Width - 32, 30),
            Color.White,
            large: true);
        DrawTextFitted(
            context,
            objectType,
            new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 72, layout.SelectedBounds.Width - 32, 20),
            new Color(135, 173, 187));
        DrawTextFitted(
            context,
            detailText,
            new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 98, layout.SelectedBounds.Width - 32, 20),
            new Color(135, 173, 187));
        if (supplementalText is not null)
        {
            DrawTextFitted(
                context,
                supplementalText,
                new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 124, layout.SelectedBounds.Width - 32, 20),
                new Color(135, 173, 187));
        }

        if (SelectedObject is Trilobite selectedTrilobite &&
            layout.SelectedTraitSummaryBounds is { } traitBounds)
        {
            DrawTextFitted(
                context,
                $"Trait: {selectedTrilobite.TraitState.GetTraitSummary()}",
                traitBounds,
                new Color(210, 228, 236));
        }

        if (canRename)
        {
            DrawTextFitted(
                context,
                "NAME",
                new Rectangle(layout.SelectedRenameFieldBounds.X, layout.SelectedRenameFieldBounds.Y - 20, layout.SelectedRenameFieldBounds.Width, 18),
                new Color(159, 195, 210));
            DrawFrame(
                context,
                layout.SelectedRenameFieldBounds,
                _renamingSelectedTrilobite ? new Color(21, 49, 67) : new Color(12, 28, 40),
                _renamingSelectedTrilobite ? new Color(158, 214, 229) : new Color(66, 105, 124));
            DrawTextFitted(
                context,
                _renamingSelectedTrilobite ? $"{_renameBuffer}|" : title,
                Inset(layout.SelectedRenameFieldBounds, 10),
                Color.White,
                minScale: 0.6f);

            if (_renamingSelectedTrilobite)
            {
                if (layout.SelectedRenamePrimaryButtonBounds is { } saveBounds)
                {
                    DrawButton(
                        context,
                        saveBounds,
                        "Save",
                        saveBounds.Contains(_pointerPoint) ? new Color(52, 107, 89) : new Color(39, 88, 72),
                        saveBounds.Contains(_pointerPoint) ? new Color(176, 233, 214) : new Color(126, 189, 169),
                        Color.White);
                }

                if (layout.SelectedRenameSecondaryButtonBounds is { } cancelBounds)
                {
                    DrawButton(
                        context,
                        cancelBounds,
                        "Cancel",
                        cancelBounds.Contains(_pointerPoint) ? new Color(74, 82, 94) : new Color(58, 66, 77),
                        cancelBounds.Contains(_pointerPoint) ? new Color(197, 209, 220) : new Color(153, 167, 181),
                        Color.White);
                }
            }
            else if (layout.SelectedRenamePrimaryButtonBounds is { } renameBounds)
            {
                DrawButton(
                    context,
                    renameBounds,
                    "Rename",
                    renameBounds.Contains(_pointerPoint) ? new Color(39, 86, 109) : new Color(33, 75, 95),
                    renameBounds.Contains(_pointerPoint) ? new Color(160, 221, 237) : new Color(140, 207, 224),
                    Color.White);
            }
        }

        if (SelectedObject is IStorage storageSelected && layout.SelectedInventoryFrameBounds is { } inventoryFrameBounds)
        {
            DrawFrame(context, inventoryFrameBounds, new Color(13, 31, 44), new Color(53, 84, 102));
            DrawTextFitted(
                context,
                "STORAGE",
                new Rectangle(inventoryFrameBounds.X + 12, inventoryFrameBounds.Y + 8, inventoryFrameBounds.Width / 2, 20),
                new Color(159, 195, 210));
            DrawTextFittedRight(
                context,
                $"{storageSelected.GetInventoryTotal()}/{storageSelected.Capacity}",
                new Rectangle(inventoryFrameBounds.Right - 120, inventoryFrameBounds.Y + 8, 108, 20),
                new Color(210, 228, 236));

            if (layout.SelectedInventoryEntries.Count == 0)
            {
                DrawWrappedText(
                    context,
                    "No resources are stored here yet.",
                    Inset(layout.SelectedInventoryViewportBounds ?? inventoryFrameBounds, 10),
                    new Color(210, 228, 236));
            }
            else
            {
                foreach (var entry in layout.SelectedInventoryEntries)
                {
                    DrawInventoryEntry(context, entry);
                }
            }

            DrawScrollbar(context, layout.SelectedInventoryScrollbarTrackBounds, layout.SelectedInventoryScrollbarThumbBounds);
        }
        else
        {
            DrawWrappedText(
                context,
                $"{bodyText} This uses the normal in-game removal flow and clears the current selection afterward.",
                layout.SelectedDescriptionBounds,
                new Color(226, 238, 244));
        }

        var hovered = layout.DeleteSelectedBounds.Contains(_pointerPoint);
        DrawButton(
            context,
            layout.DeleteSelectedBounds,
            SelectedObject switch
            {
                Trilobite => "Kill Trilobite",
                Creature => "Kill Creature",
                IVehicle => "Delete Vehicle",
                Ranch => "Delete Ranch",
                SoilArea => "Delete Soil Area",
                SoilAreaSelection selection => selection.Mode == SoilAreaSelectionMode.Row ? "Delete Row" : "Delete Column",
                _ => "Delete Building"
            },
            hovered ? new Color(184, 86, 79) : new Color(163, 74, 67),
            hovered ? new Color(255, 195, 188) : new Color(242, 176, 170),
            Color.White);
    }

    private static int GetSelectedBuildingAssignmentCount(Building building)
    {
        return building switch
        {
            MiningPost post => post.GetVolume(),
            AlgaeFarm farm => farm.GetVolume(),
            Barracks barracks => barracks.GetVolume(),
            Scaffolding scaffolding => scaffolding.GetVolume(),
            _ => 0
        };
    }

    private void DrawPanelFrame(RenderingContext context, Rectangle bounds)
    {
        DrawShadow(context, bounds, 4, 16, new Color(0, 0, 0, 90));
        DrawRoundedFrame(context, bounds, new Color(8, 19, 29, 247), new Color(77, 122, 140), 3, 16);
    }

    private void DrawFrame(RenderingContext context, Rectangle bounds, Color fill, Color border)
    {
        var radius = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 7, 6, 14);
        DrawShadow(context, bounds, 2, radius, new Color(0, 0, 0, 36));
        DrawRoundedFrame(context, bounds, fill, border, 2, radius);
    }

    private void DrawBuildCard(RenderingContext context, BuildCardRect card, bool isSelected, bool isHovered)
    {
        var fill = isSelected
            ? new Color(27, 65, 88)
            : isHovered ? new Color(22, 50, 71) : new Color(16, 38, 54);
        var border = isSelected
            ? new Color(163, 217, 235)
            : isHovered ? new Color(125, 179, 196) : new Color(54, 88, 107);
        DrawFrame(context, card.Bounds, fill, border);

        var iconFrame = new Rectangle(card.Bounds.X + 8, card.Bounds.Y + 34, card.Bounds.Width - 16, card.Bounds.Height - 44);
        DrawFrame(context, iconFrame, new Color(11, 23, 33), new Color(63, 98, 117));
        DrawTextCentered(context, card.Factory.Name, new Rectangle(card.Bounds.X + 6, card.Bounds.Y + 6, card.Bounds.Width - 12, 20), Color.White, minScale: 0.58f);
        DrawPreviewTexture(context, card.Factory.TextureKey, Inset(iconFrame, 6));
    }

    private void DrawAssignmentRow(RenderingContext context, AssignmentRowRect row)
    {
        var hovered = row.Bounds.Contains(_pointerPoint);
        DrawFrame(
            context,
            row.Bounds,
            hovered ? new Color(22, 50, 71) : new Color(16, 38, 54),
            hovered ? new Color(125, 179, 196) : new Color(54, 88, 107));

        var portraitBounds = new Rectangle(row.Bounds.X + 14, row.Bounds.Y + 10, 56, 56);
        DrawFrame(context, portraitBounds, new Color(11, 23, 33), new Color(163, 217, 235));
        DrawPreviewTexture(context, "Trilobite", Inset(portraitBounds, 7));
        DrawTextFitted(
            context,
            row.Entry.Count.ToString(),
            new Rectangle(row.Bounds.X + 82, row.Bounds.Y + 12, row.Bounds.Width - 96, row.Bounds.Height - 24),
            Color.White,
            true);
    }

    private void DrawInventoryEntry(RenderingContext context, InventoryEntryRect entry)
    {
        var hovered = entry.Bounds.Contains(_pointerPoint);
        DrawFrame(
            context,
            entry.Bounds,
            hovered ? new Color(22, 50, 71) : new Color(16, 38, 54),
            hovered ? new Color(125, 179, 196) : new Color(54, 88, 107));

        var quantityBadgeBounds = new Rectangle(entry.Bounds.Right - 60, entry.Bounds.Y + 8, 50, 22);
        DrawFrame(context, quantityBadgeBounds, new Color(10, 22, 32), new Color(80, 122, 141));
        DrawTextFittedRight(context, entry.Quantity.ToString(), Inset(quantityBadgeBounds, 5), Color.White, minScale: 0.62f);

        var iconBounds = new Rectangle(entry.Bounds.X + 10, entry.Bounds.Y + 34, entry.Bounds.Width - 20, Math.Max(24, entry.Bounds.Height - 72));
        DrawFrame(context, iconBounds, new Color(11, 23, 33), new Color(63, 98, 117));
        DrawPreviewTexture(context, entry.TextureKey, Inset(iconBounds, 6));

        DrawTextCentered(
            context,
            entry.ResourceType,
            new Rectangle(entry.Bounds.X + 6, entry.Bounds.Bottom - 28, entry.Bounds.Width - 12, 20),
            Color.White,
            minScale: 0.56f);
    }

    private void DrawScrollbar(RenderingContext context, Rectangle? trackBounds, Rectangle? thumbBounds)
    {
        if (trackBounds is null || thumbBounds is null)
        {
            return;
        }

        DrawFrame(context, trackBounds.Value, new Color(9, 19, 28), new Color(39, 64, 79));
        DrawFrame(context, thumbBounds.Value, new Color(109, 170, 192), new Color(191, 230, 244));
    }

    private void DrawRect(RenderingContext context, Rectangle bounds, Color color)
    {
        if (!HasRenderer)
        {
            return;
        }

        _gumUi!.AddFilledRectangle(bounds, color);
    }

    private void DrawRoundedFrame(RenderingContext context, Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (!HasRenderer || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _gumUi!.AddRoundedFrame(bounds, fill, border, thickness, radius);
    }

    private void DrawRoundedRect(RenderingContext context, Rectangle bounds, Color color, int radius)
    {
        if (!HasRenderer || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _gumUi!.AddRoundedRectangle(bounds, color, radius);
    }

    private void DrawShadow(RenderingContext context, Rectangle bounds, int offset, int radius, Color color)
    {
        if (!HasRenderer || color.A == 0)
        {
            return;
        }

        DrawRoundedRect(
            context,
            new Rectangle(bounds.X, bounds.Y + offset, bounds.Width, bounds.Height),
            color,
            radius);
    }

    private void DrawOutline(RenderingContext context, Rectangle bounds, Color color, int thickness)
    {
        DrawRect(context, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        DrawRect(context, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        DrawRect(context, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        DrawRect(context, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private void DrawButton(RenderingContext context, Rectangle bounds, string label, Color fill, Color border, Color text)
    {
        var radius = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 8, 14);
        DrawShadow(context, bounds, 2, radius, new Color(0, 0, 0, 44));
        DrawRoundedFrame(context, bounds, fill, border, 2, radius);
        if (HasRenderer)
        {
            DrawTextCentered(context, label, bounds, text, minScale: 0.66f);
        }
    }

    private void DrawIconButton(
        RenderingContext context,
        Rectangle bounds,
        Color fill,
        Color border,
        Color iconColor,
        Action<RenderingContext, Rectangle, Color> iconDrawer)
    {
        var radius = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 4, 10, 16);
        DrawShadow(context, bounds, 2, radius, new Color(0, 0, 0, 44));
        DrawRoundedFrame(context, bounds, fill, border, 2, radius);
        if (HasRenderer)
        {
            var iconInset = Math.Max(8, Math.Min(bounds.Width, bounds.Height) / 5);
            iconDrawer(context, Inset(bounds, iconInset), iconColor);
        }
    }

    private void DrawTabButton(RenderingContext context, Rectangle bounds, string label, bool active, bool hovered)
    {
        var fill = active
            ? hovered ? new Color(39, 86, 109) : new Color(33, 75, 95)
            : hovered ? new Color(20, 48, 68) : new Color(13, 33, 48);
        var border = active
            ? hovered ? new Color(160, 221, 237) : new Color(140, 207, 224)
            : hovered ? new Color(76, 116, 136) : new Color(53, 88, 106);
        var text = active ? Color.White : new Color(149, 183, 198);
        DrawButton(context, bounds, label, fill, border, text);
    }

    private void DrawBackArrowIcon(RenderingContext context, Rectangle bounds, Color color)
    {
        if (!HasRenderer)
        {
            return;
        }

        if (context.Sprites.TryGet("BackArrow", out var texture))
        {
            var scale = MathF.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
            var width = Math.Max(1, (int)MathF.Round(texture.Width * scale));
            var height = Math.Max(1, (int)MathF.Round(texture.Height * scale));
            var destination = new Rectangle(
                bounds.X + ((bounds.Width - width) / 2),
                bounds.Y + ((bounds.Height - height) / 2),
                width,
                height);
            _gumUi!.AddSprite(destination, texture, color);
            return;
        }

        var stroke = Math.Max(2, Math.Min(bounds.Width, bounds.Height) / 7);
        var bodyWidth = Math.Max(stroke * 2, bounds.Width - (stroke * 4));
        var bodyHeight = Math.Max(stroke, stroke + 1);
        var bodyBounds = new Rectangle(
            bounds.Center.X - (bodyWidth / 4),
            bounds.Center.Y - (bodyHeight / 2),
            bodyWidth,
            bodyHeight);
        DrawRect(context, bodyBounds, color);

        var wingSize = Math.Max(stroke * 2, Math.Min(bounds.Width, bounds.Height) / 2);
        DrawRect(context, new Rectangle(bounds.X + stroke, bounds.Center.Y - stroke, wingSize / 2, stroke), color);
        DrawRect(context, new Rectangle(bounds.X + stroke * 2, bounds.Center.Y - wingSize / 2, stroke, wingSize / 2), color);
        DrawRect(context, new Rectangle(bounds.X + stroke * 2, bounds.Center.Y, stroke, wingSize / 2), color);
    }

    private void DrawGearIcon(RenderingContext context, Rectangle bounds, Color color)
    {
        if (!HasRenderer)
        {
            return;
        }

        var iconSize = Math.Min(bounds.Width, bounds.Height);
        if (iconSize <= 0)
        {
            return;
        }

        var centerSize = Math.Max(8, iconSize / 2);
        var toothThickness = Math.Max(2, iconSize / 8);
        var toothLength = Math.Max(3, iconSize / 6);
        var centerBounds = new Rectangle(
            bounds.Center.X - (centerSize / 2),
            bounds.Center.Y - (centerSize / 2),
            centerSize,
            centerSize);
        DrawRect(context, centerBounds, color);

        DrawRect(context, new Rectangle(centerBounds.Center.X - (toothThickness / 2), bounds.Y, toothThickness, toothLength), color);
        DrawRect(context, new Rectangle(centerBounds.Center.X - (toothThickness / 2), bounds.Bottom - toothLength, toothThickness, toothLength), color);
        DrawRect(context, new Rectangle(bounds.X, centerBounds.Center.Y - (toothThickness / 2), toothLength, toothThickness), color);
        DrawRect(context, new Rectangle(bounds.Right - toothLength, centerBounds.Center.Y - (toothThickness / 2), toothLength, toothThickness), color);

        var diagonalTooth = Math.Max(3, toothThickness + 1);
        DrawRect(context, new Rectangle(bounds.X + toothThickness, bounds.Y + toothThickness, diagonalTooth, diagonalTooth), color);
        DrawRect(context, new Rectangle(bounds.Right - toothThickness - diagonalTooth, bounds.Y + toothThickness, diagonalTooth, diagonalTooth), color);
        DrawRect(context, new Rectangle(bounds.X + toothThickness, bounds.Bottom - toothThickness - diagonalTooth, diagonalTooth, diagonalTooth), color);
        DrawRect(context, new Rectangle(bounds.Right - toothThickness - diagonalTooth, bounds.Bottom - toothThickness - diagonalTooth, diagonalTooth, diagonalTooth), color);
    }

    private void DrawText(RenderingContext context, string text, Vector2 position, Color color, bool large = false)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        var size = GumTextLayout.Measure(text, style);
        var bounds = new Rectangle(
            (int)MathF.Round(position.X),
            (int)MathF.Round(position.Y),
            Math.Max(1, size.X),
            Math.Max(1, size.Y));
        _gumUi!.AddText(bounds, text, color, verticalAlignment: VerticalAlignment.Top, fontSize: GumTextLayout.GetMetrics(style).FontSize);
    }

    private void DrawTextFitted(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        var textToDraw = GumTextLayout.FitToWidth(text, bounds.Width, style);
        var metrics = GumTextLayout.GetMetrics(style);
        _gumUi!.AddText(
            bounds,
            textToDraw,
            color,
            HorizontalAlignment.Left,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines: 1);
    }

    private void DrawWrappedText(RenderingContext context, string text, Rectangle bounds, Color color)
    {
        if (!HasRenderer)
        {
            return;
        }

        var style = GumTextStyle.Small;
        var metrics = GumTextLayout.GetMetrics(style);
        var lines = GumTextLayout.Wrap([text], bounds.Width, Math.Max(1, bounds.Height / metrics.LineHeight), style);
        _gumUi!.AddText(bounds, string.Join('\n', lines), color, verticalAlignment: VerticalAlignment.Top, fontSize: metrics.FontSize, maxLines: lines.Count);
    }

    private void DrawTextCentered(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        var textToDraw = GumTextLayout.FitToWidth(text, bounds.Width, style);
        var metrics = GumTextLayout.GetMetrics(style);
        _gumUi!.AddText(
            bounds,
            textToDraw,
            color,
            HorizontalAlignment.Center,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines: 1);
    }

    private void DrawTextFittedRight(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        var textToDraw = GumTextLayout.FitToWidth(text, bounds.Width, style);
        var metrics = GumTextLayout.GetMetrics(style);
        _gumUi!.AddText(
            bounds,
            textToDraw,
            color,
            HorizontalAlignment.Right,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines: 1);
    }

    private void DrawPreviewTexture(RenderingContext context, string textureKey, Rectangle bounds)
    {
        if (!HasRenderer)
        {
            return;
        }

        if (!context.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        var scale = MathF.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
        var width = Math.Max(1, (int)MathF.Round(texture.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(texture.Height * scale));
        var destination = new Rectangle(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
        _gumUi!.AddSprite(destination, texture);
    }
}
