using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Menu;

public sealed partial class MenuController
{
    private void DrawBuildingsTab(RenderingContext context, MenuLayout layout, GameSession session)
    {
        DrawFrame(context, layout.Chrome.ContentFrameBounds, UiPalette.SurfacePanel, UiPalette.BorderSubtle);
        DrawFrame(context, layout.Build.PreviewBounds, UiPalette.SurfaceRaised, UiPalette.BorderPanel);
        DrawFrame(context, layout.Build.GridFrameBounds, UiPalette.SurfacePanel, UiPalette.BorderContent);

        DrawTextFitted(
            context,
            "BUILDING PREVIEW",
            new Rectangle(layout.Build.PreviewBounds.X + 12, layout.Build.PreviewBounds.Y + 8, layout.Build.PreviewBounds.Width - 24, 24),
            UiPalette.TextLabel);
        DrawTextFitted(
            context,
            "BUILDINGS",
            new Rectangle(layout.Build.GridFrameBounds.X + 12, layout.Build.GridFrameBounds.Y + 8, layout.Build.GridFrameBounds.Width - 24, 24),
            UiPalette.TextLabel);

        var activeFactory = HoveredBuildOption ?? SelectedBuildOption;
        if (activeFactory is not null)
        {
            DrawTextFitted(
                context,
                activeFactory.Name,
                new Rectangle(layout.Build.PreviewBounds.X + 12, layout.Build.PreviewBounds.Y + 36, Math.Max(100, (layout.Build.PreviewBounds.Width / 2) + 12), 28),
                Color.White,
                large: true);
            DrawTextFitted(
                context,
                $"Size: {activeFactory.Size.X} x {activeFactory.Size.Y}",
                new Rectangle(layout.Build.PreviewBounds.X + 12, layout.Build.PreviewBounds.Y + 66, Math.Max(100, (layout.Build.PreviewBounds.Width / 2) + 12), 20),
                UiPalette.TextCaption);

            if (!string.IsNullOrWhiteSpace(layout.Build.CostText) &&
                layout.Build.CostBounds is { } costBounds)
            {
                // Warmer than the size line beside it: the cost is the one number on this panel the
                // player has to weigh against their stockpile, so it should not read as a caption.
                DrawTextFitted(
                    context,
                    layout.Build.CostText,
                    costBounds,
                    UiPalette.AccentCost,
                    minScale: 0.56f);
            }

            DrawScrollableText(context, layout.Build.DescriptionLayout, UiPalette.TextBody, GumTextStyle.Small);

            DrawPreviewTexture(
                context,
                activeFactory.TextureKey,
                new Rectangle(layout.Build.PreviewBounds.Right - 160, layout.Build.PreviewBounds.Y + 22, 132, 132));
        }
        else
        {
            DrawScrollableText(context, layout.Build.DescriptionLayout, UiPalette.TextMuted, GumTextStyle.Small);
        }
        DrawScrollbar(context, layout.Build.DescriptionLayout.ScrollbarTrackBounds, layout.Build.DescriptionLayout.ScrollbarThumbBounds);

        foreach (var card in layout.Build.Cards)
        {
            var isSelected = SelectedBuildOption?.Name == card.Factory.Name;
            var isHovered = HoveredBuildOption?.Name == card.Factory.Name || card.Bounds.Contains(_pointerPoint);
            DrawBuildCard(context, card, isSelected, isHovered);
        }

        DrawScrollbar(context, layout.Build.GridScrollbarTrackBounds, layout.Build.GridScrollbarThumbBounds);
    }

    private void DrawAssignmentsTab(RenderingContext context, MenuLayout layout, GameSession session)
    {
        foreach (var filter in layout.Assignments.Filters)
        {
            var active = AssignmentFilter == filter.Key;
            var hovered = filter.Bounds.Contains(_pointerPoint);
            DrawTabButton(context, filter.Bounds, filter.Label, active, hovered);
        }

        DrawFrame(context, layout.Assignments.ActiveBounds, UiPalette.SurfacePanel, UiPalette.BorderContent);
        DrawText(
            context,
            "Unassigned",
            new Vector2(layout.Assignments.UnassignedLabelBounds.X, layout.Assignments.UnassignedLabelBounds.Y),
            UiPalette.TextBody);
        DrawFrame(context, layout.Assignments.UnassignedBounds, UiPalette.SurfacePanel, UiPalette.BorderContent);

        if (layout.Assignments.ActiveRows.Count == 0)
        {
            DrawWrappedText(
                context,
                "No trilobites are in this assignment.",
                Inset(layout.Assignments.ActiveViewportBounds, 8),
                UiPalette.TextMuted);
        }

        foreach (var row in layout.Assignments.ActiveRows)
        {
            DrawAssignmentRow(context, row);
        }

        if (layout.Assignments.UnassignedRows.Count == 0)
        {
            DrawWrappedText(
                context,
                "No unassigned trilobites are available.",
                Inset(layout.Assignments.UnassignedViewportBounds, 8),
                UiPalette.TextMuted);
        }

        foreach (var row in layout.Assignments.UnassignedRows)
        {
            DrawAssignmentRow(context, row);
        }

        DrawScrollbar(context, layout.Assignments.ActiveScrollbarTrackBounds, layout.Assignments.ActiveScrollbarThumbBounds);
        DrawScrollbar(context, layout.Assignments.UnassignedScrollbarTrackBounds, layout.Assignments.UnassignedScrollbarThumbBounds);
    }

    private void DrawSelectedTab(RenderingContext context, MenuLayout layout)
    {
        DrawFrame(context, layout.Selected.Bounds, UiPalette.SurfaceRaised, UiPalette.BorderPanel);

        var title = SelectedObject is Creature creature ? creature.Name : (SelectedObject as Core.Buildings.Building)?.Name ?? "No Selection";
        var objectType = SelectedObject is Creature ? "Trilobite" : "Building";
        var healthText = SelectedObject is Creature selectedHealthCreature
            ? $"Health: {selectedHealthCreature.Health}/{selectedHealthCreature.MaxHealth}"
            : null;
        var assignmentText = SelectedObject switch
        {
            Creature selectedCreature => $"Assignment: {selectedCreature.Assignment}",
            IResourceStorage storage => $"Stored: {storage.GetInventoryTotal()}/{storage.Capacity}",
            _ => $"Type: {title}"
        };
        var buildingAssignmentText = SelectedObject is Building selectedBuilding
            ? $"Assigned Trilobites: {GetSelectedBuildingAssignmentCount(selectedBuilding)}"
            : null;
        var canRename = SelectedObject is Trilobite;
        var headerBounds = new Rectangle(layout.Selected.Bounds.X + 16, layout.Selected.Bounds.Y + 10, layout.Selected.Bounds.Width - 32, 22);
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
            UiPalette.TextLabel);
        if (healthText is not null)
        {
            DrawTextFittedRight(
                context,
                healthText,
                healthBounds,
                UiPalette.TextMuted);
        }
        DrawTextFitted(
            context,
            title,
            new Rectangle(layout.Selected.Bounds.X + 16, layout.Selected.Bounds.Y + 38, layout.Selected.Bounds.Width - 32, 30),
            Color.White,
            large: true);
        DrawTextFitted(
            context,
            objectType,
            new Rectangle(layout.Selected.Bounds.X + 16, layout.Selected.Bounds.Y + 72, layout.Selected.Bounds.Width - 32, 20),
            UiPalette.TextCaption);
        DrawTextFitted(
            context,
            assignmentText,
            new Rectangle(layout.Selected.Bounds.X + 16, layout.Selected.Bounds.Y + 98, layout.Selected.Bounds.Width - 32, 20),
            UiPalette.TextCaption);
        if (buildingAssignmentText is not null)
        {
            DrawTextFitted(
                context,
                buildingAssignmentText,
                new Rectangle(layout.Selected.Bounds.X + 16, layout.Selected.Bounds.Y + 124, layout.Selected.Bounds.Width - 32, 20),
                UiPalette.TextCaption);
        }

        if (SelectedObject is Trilobite selectedTrilobite &&
            layout.Selected.TraitSummaryBounds is { } traitBounds)
        {
            DrawTextFitted(
                context,
                $"Trait: {selectedTrilobite.TraitState.GetTraitSummary()}",
                traitBounds,
                UiPalette.TextMuted);
        }

        if (!string.IsNullOrWhiteSpace(layout.Selected.RecipeText) &&
            layout.Selected.RecipeBounds is { } recipeBounds)
        {
            DrawTextFitted(
                context,
                layout.Selected.RecipeText,
                recipeBounds,
                UiPalette.TextMuted,
                minScale: 0.56f);
        }

        if (canRename)
        {
            DrawTextFitted(
                context,
                "NAME",
                new Rectangle(layout.Selected.RenameFieldBounds.X, layout.Selected.RenameFieldBounds.Y - 20, layout.Selected.RenameFieldBounds.Width, 18),
                UiPalette.TextLabel);
            DrawFrame(
                context,
                layout.Selected.RenameFieldBounds,
                _renamingSelectedTrilobite ? new Color(21, 49, 67) : new Color(12, 28, 40),
                _renamingSelectedTrilobite ? new Color(158, 214, 229) : new Color(66, 105, 124));
            DrawTextFitted(
                context,
                _renamingSelectedTrilobite ? $"{_renameBuffer}|" : title,
                Inset(layout.Selected.RenameFieldBounds, 10),
                Color.White,
                minScale: 0.6f);

            if (_renamingSelectedTrilobite)
            {
                if (layout.Selected.RenamePrimaryButtonBounds is { } saveBounds)
                {
                    DrawButton(
                        context,
                        saveBounds,
                        "Save",
                        saveBounds.Contains(_pointerPoint) ? new Color(52, 107, 89) : new Color(39, 88, 72),
                        saveBounds.Contains(_pointerPoint) ? new Color(176, 233, 214) : new Color(126, 189, 169),
                        Color.White);
                }

                if (layout.Selected.RenameSecondaryButtonBounds is { } cancelBounds)
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
            else if (layout.Selected.RenamePrimaryButtonBounds is { } renameBounds)
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

        if (TryGetSelectedInventorySummary(
                out var inventoryTitle,
                out var inventoryAmountText,
                out var emptyInventoryText) &&
            layout.Selected.InventoryFrameBounds is { } inventoryFrameBounds)
        {
            DrawFrame(context, inventoryFrameBounds, UiPalette.SurfacePanel, UiPalette.BorderContent);
            DrawTextFitted(
                context,
                inventoryTitle,
                new Rectangle(inventoryFrameBounds.X + 12, inventoryFrameBounds.Y + 8, inventoryFrameBounds.Width / 2, 20),
                UiPalette.TextLabel);
            DrawTextFittedRight(
                context,
                inventoryAmountText,
                new Rectangle(inventoryFrameBounds.Right - 120, inventoryFrameBounds.Y + 8, 108, 20),
                UiPalette.TextMuted);

            if (layout.Selected.InventoryEntries.Count == 0)
            {
                DrawWrappedText(
                    context,
                    emptyInventoryText,
                    Inset(layout.Selected.InventoryViewportBounds ?? inventoryFrameBounds, 10),
                    UiPalette.TextMuted);
            }
            else
            {
                foreach (var entry in layout.Selected.InventoryEntries)
                {
                    DrawInventoryEntry(context, entry);
                }
            }

            DrawScrollbar(context, layout.Selected.InventoryScrollbarTrackBounds, layout.Selected.InventoryScrollbarThumbBounds);
        }

        if (layout.Selected.DescriptionLayout.ViewportBounds.Width > 0 &&
            layout.Selected.DescriptionLayout.ViewportBounds.Height > 0)
        {
            DrawScrollableText(context, layout.Selected.DescriptionLayout, UiPalette.TextBody, GumTextStyle.Small);
            DrawScrollbar(context, layout.Selected.DescriptionLayout.ScrollbarTrackBounds, layout.Selected.DescriptionLayout.ScrollbarThumbBounds);
        }

        if (SelectedObject is Scaffolding scaffolding &&
            layout.Selected.BuildFirstBounds is { } buildFirstBounds)
        {
            var buildFirstHovered = buildFirstBounds.Contains(_pointerPoint);
            DrawButton(
                context,
                buildFirstBounds,
                scaffolding.BuildFirst ? "Build First (On)" : "Build First",
                buildFirstHovered ? new Color(83, 133, 104) : new Color(65, 108, 84),
                buildFirstHovered ? new Color(194, 239, 203) : new Color(171, 220, 181),
                Color.White);
        }

        var hovered = layout.Selected.DeleteBounds.Contains(_pointerPoint);
        DrawButton(
            context,
            layout.Selected.DeleteBounds,
            SelectedObject is Creature ? "Kill Trilobite" : "Delete Building",
            hovered ? new Color(184, 86, 79) : new Color(163, 74, 67),
            hovered ? new Color(255, 195, 188) : new Color(242, 176, 170),
            Color.White);
    }

    internal static int GetSelectedBuildingAssignmentCount(Building building)
    {
        return building switch
        {
            MiningPost post => post.GetVolume(),
            AlgaeFarm farm => farm.GetVolume(),
            StationBuilding station => station.GetVolume(),
            Scaffolding scaffolding => scaffolding.GetVolume(),
            _ => 0
        };
    }

    private bool TryGetSelectedInventorySummary(
        out string title,
        out string amountText,
        out string emptyText)
    {
        switch (SelectedObject)
        {
            case Scaffolding scaffolding:
                title = "MATERIALS ADDED";
                amountText = $"{scaffolding.GetTotalDepositedAmount()}/{GetScaffoldingRequiredAmount(scaffolding)}";
                emptyText = "No materials delivered yet.";
                return true;
            case IResourceStorage storage:
                title = "STORAGE";
                amountText = $"{storage.GetInventoryTotal()}/{storage.Capacity}";
                emptyText = "No resources are stored here yet.";
                return true;
            case IInventoryCarrier carrier:
                title = "INVENTORY";
                amountText = $"{carrier.Inventory.Amount}/{carrier.InventoryCapacity}";
                emptyText = "No resources carried.";
                return true;
            default:
                title = string.Empty;
                amountText = string.Empty;
                emptyText = string.Empty;
                return false;
        }
    }

    private void DrawPanelFrame(RenderingContext context, Rectangle bounds)
    {
        DrawShadow(context, bounds, 4, 16, new Color(0, 0, 0, 90));
        DrawRoundedFrame(context, bounds, UiPalette.SurfaceOverlay, UiPalette.BorderPanel, 3, 16);
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
            ? UiPalette.SurfaceSelected
            : isHovered ? UiPalette.SurfaceRaisedHover : UiPalette.SurfaceRaised;
        var border = isSelected
            ? UiPalette.BorderFocus
            : isHovered ? UiPalette.BorderHover : UiPalette.BorderControl;
        DrawFrame(context, card.Bounds, fill, border);

        var iconFrame = new Rectangle(card.Bounds.X + 8, card.Bounds.Y + 34, card.Bounds.Width - 16, card.Bounds.Height - 44);
        DrawFrame(context, iconFrame, UiPalette.SurfaceSunken, new Color(63, 98, 117));
        DrawTextCentered(context, card.Factory.Name, new Rectangle(card.Bounds.X + 6, card.Bounds.Y + 6, card.Bounds.Width - 12, 20), Color.White, minScale: 0.58f);
        DrawPreviewTexture(context, card.Factory.TextureKey, Inset(iconFrame, 6));
    }

    private void DrawAssignmentRow(RenderingContext context, AssignmentRowRect row)
    {
        var hovered = row.Bounds.Contains(_pointerPoint);
        DrawFrame(
            context,
            row.Bounds,
            hovered ? UiPalette.SurfaceRaisedHover : UiPalette.SurfaceRaised,
            hovered ? UiPalette.BorderHover : UiPalette.BorderControl);

        var portraitBounds = new Rectangle(row.Bounds.X + 14, row.Bounds.Y + 10, 56, 56);
        DrawFrame(context, portraitBounds, UiPalette.SurfaceSunken, UiPalette.BorderFocus);
        // FromAssignment is the role the row's trilobites currently hold, so the portrait shows the
        // same sprite they wear in the cave rather than a generic one for every row.
        DrawPreviewTexture(
            context,
            TrilobiteSpriteCatalog.ResolveTextureKey(context.Sprites, row.FromAssignment),
            Inset(portraitBounds, 7));
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
            hovered ? UiPalette.SurfaceRaisedHover : UiPalette.SurfaceRaised,
            hovered ? UiPalette.BorderHover : UiPalette.BorderControl);

        var quantityBadgeBounds = new Rectangle(entry.Bounds.Right - 60, entry.Bounds.Y + 8, 50, 22);
        DrawFrame(context, quantityBadgeBounds, UiPalette.SurfaceSunken, new Color(80, 122, 141));
        DrawTextFittedRight(context, entry.Quantity.ToString(), Inset(quantityBadgeBounds, 5), Color.White, minScale: 0.62f);

        var iconBounds = new Rectangle(entry.Bounds.X + 10, entry.Bounds.Y + 34, entry.Bounds.Width - 20, Math.Max(24, entry.Bounds.Height - 72));
        DrawFrame(context, iconBounds, UiPalette.SurfaceSunken, new Color(63, 98, 117));
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

    private void DrawScrollableText(RenderingContext context, GumScrollableTextLayout layout, Color color, GumTextStyle style)
    {
        if (!HasRenderer)
        {
            return;
        }

        GumScrollableText.Draw(_gumUi!, layout, color, style);
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
        GumUiText.Add(_gumUi!, bounds, text, color, style, verticalAlignment: VerticalAlignment.Top);
    }

    private void DrawTextFitted(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        GumUiText.AddFittedLeft(_gumUi!, bounds, text, color, style);
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
        GumUiText.Add(_gumUi!, bounds, string.Join('\n', lines), color, style, verticalAlignment: VerticalAlignment.Top, maxLines: lines.Count);
    }

    private void DrawTextCentered(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        GumUiText.AddFittedCentered(_gumUi!, bounds, text, color, style);
    }

    private void DrawTextFittedRight(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (!HasRenderer || string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = large ? GumTextStyle.UiLarge : GumTextStyle.Small;
        GumUiText.Add(
            _gumUi!,
            bounds,
            GumTextLayout.FitToWidth(text, bounds.Width, style),
            color,
            style,
            HorizontalAlignment.Right,
            VerticalAlignment.Center,
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
