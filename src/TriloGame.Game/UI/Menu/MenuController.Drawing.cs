using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;

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

        var title = SelectedObject is Creature creature ? creature.Name : (SelectedObject as Core.Buildings.Building)?.Name ?? "No Selection";
        var objectType = SelectedObject is Creature ? "Trilobite" : "Building";
        var assignmentText = SelectedObject is Creature selectedCreature
            ? $"Assignment: {selectedCreature.Assignment}"
            : $"Type: {title}";
        var bodyText = SelectedObject is Creature
            ? "Delete this trilobite from the colony immediately."
            : "Delete this building from the cave immediately.";

        DrawTextFitted(
            context,
            "SELECTED OBJECT",
            new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 10, layout.SelectedBounds.Width - 32, 22),
            new Color(159, 195, 210));
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
            assignmentText,
            new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 98, layout.SelectedBounds.Width - 32, 20),
            new Color(135, 173, 187));

        DrawWrappedText(
            context,
            $"{bodyText} This uses the normal in-game removal flow and clears the current selection afterward.",
            new Rectangle(layout.SelectedBounds.X + 16, layout.SelectedBounds.Y + 132, layout.SelectedBounds.Width - 32, Math.Max(60, layout.SelectedBounds.Height - 220)),
            new Color(226, 238, 244));

        var hovered = layout.DeleteSelectedBounds.Contains(_pointerPoint);
        DrawButton(
            context,
            layout.DeleteSelectedBounds,
            SelectedObject is Creature ? "Delete Trilobite" : "Delete Building",
            hovered ? new Color(184, 86, 79) : new Color(163, 74, 67),
            hovered ? new Color(255, 195, 188) : new Color(242, 176, 170),
            Color.White);
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
        context.SpriteBatch.Draw(context.WhitePixel, bounds, color);
    }

    private void DrawRoundedFrame(RenderingContext context, Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var clampedRadius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        DrawRoundedRect(context, bounds, border, clampedRadius);
        if (thickness <= 0)
        {
            return;
        }

        var innerBounds = Inset(bounds, thickness);
        if (innerBounds.Width <= 0 || innerBounds.Height <= 0)
        {
            return;
        }

        DrawRoundedRect(context, innerBounds, fill, Math.Max(0, clampedRadius - thickness));
    }

    private void DrawRoundedRect(RenderingContext context, Rectangle bounds, Color color, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var clampedRadius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        if (clampedRadius <= 1)
        {
            DrawRect(context, bounds, color);
            return;
        }

        for (var row = 0; row < bounds.Height; row++)
        {
            var inset = GetRoundedInset(clampedRadius, row, bounds.Height);
            var width = bounds.Width - (inset * 2);
            if (width <= 0)
            {
                continue;
            }

            DrawRect(context, new Rectangle(bounds.X + inset, bounds.Y + row, width, 1), color);
        }
    }

    private void DrawShadow(RenderingContext context, Rectangle bounds, int offset, int radius, Color color)
    {
        if (color.A == 0)
        {
            return;
        }

        DrawRoundedRect(
            context,
            new Rectangle(bounds.X, bounds.Y + offset, bounds.Width, bounds.Height),
            color,
            radius);
    }

    private static int GetRoundedInset(int radius, int row, int height)
    {
        if (radius <= 1)
        {
            return 0;
        }

        if (row < radius)
        {
            return GetCircleInset(radius, row);
        }

        var inverseRow = height - row - 1;
        return inverseRow < radius ? GetCircleInset(radius, inverseRow) : 0;
    }

    private static int GetCircleInset(int radius, int rowFromEdge)
    {
        var dy = radius - rowFromEdge - 0.5f;
        var chordHalfWidth = MathF.Sqrt(MathF.Max(0f, (radius * radius) - (dy * dy)));
        return Math.Max(0, radius - (int)MathF.Ceiling(chordHalfWidth));
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
        DrawTextCentered(context, label, bounds, text, minScale: 0.66f);
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
        var iconInset = Math.Max(8, Math.Min(bounds.Width, bounds.Height) / 5);
        iconDrawer(context, Inset(bounds, iconInset), iconColor);
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
            context.SpriteBatch.Draw(texture, destination, color);
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
        DrawRoundedRect(context, centerBounds, color, Math.Max(3, centerSize / 4));

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
        context.SpriteBatch.DrawString(large ? context.UiFont : context.SmallFont, text, position, color);
    }

    private void DrawTextFitted(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var font = large ? context.UiFont : context.SmallFont;
        var scale = 1f;
        var textToDraw = text;
        var measure = font.MeasureString(textToDraw);
        if (measure.Y <= 0f)
        {
            return;
        }

        var heightScale = bounds.Height / measure.Y;
        scale = MathF.Min(scale, heightScale);

        if (measure.X > bounds.Width)
        {
            var widthScale = bounds.Width / measure.X;
            if (widthScale >= minScale)
            {
                scale = MathF.Min(scale, widthScale);
            }
            else
            {
                scale = MathF.Min(scale, minScale);
                textToDraw = FitTextToWidth(font, textToDraw, bounds.Width / scale);
                measure = font.MeasureString(textToDraw);
            }
        }

        scale = MathF.Min(scale, 1f);
        var scaledSize = measure * scale;
        var position = new Vector2(bounds.X, bounds.Y + MathF.Max(0f, (bounds.Height - scaledSize.Y) / 2f));
        context.SpriteBatch.DrawString(font, textToDraw, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawWrappedText(RenderingContext context, string text, Rectangle bounds, Color color)
    {
        var lines = WrapText(context.SmallFont, text, bounds.Width, Math.Max(1, bounds.Height / context.SmallFont.LineSpacing));
        var y = bounds.Y;
        foreach (var line in lines)
        {
            if (y > bounds.Bottom - context.SmallFont.LineSpacing)
            {
                break;
            }

            context.SpriteBatch.DrawString(context.SmallFont, line, new Vector2(bounds.X, y), color);
            y += context.SmallFont.LineSpacing;
        }
    }

    private void DrawTextCentered(RenderingContext context, string text, Rectangle bounds, Color color, bool large = false, float minScale = 0.72f)
    {
        if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var font = large ? context.UiFont : context.SmallFont;
        var scale = 1f;
        var textToDraw = text;
        var measure = font.MeasureString(textToDraw);
        if (measure.Y <= 0f)
        {
            return;
        }

        var heightScale = bounds.Height / measure.Y;
        scale = MathF.Min(scale, heightScale);

        if (measure.X > bounds.Width)
        {
            var widthScale = bounds.Width / measure.X;
            if (widthScale >= minScale)
            {
                scale = MathF.Min(scale, widthScale);
            }
            else
            {
                scale = MathF.Min(scale, minScale);
                textToDraw = FitTextToWidth(font, textToDraw, bounds.Width / scale);
                measure = font.MeasureString(textToDraw);
            }
        }

        scale = MathF.Min(scale, 1f);
        var scaledSize = measure * scale;
        var position = new Vector2(
            bounds.X + ((bounds.Width - scaledSize.X) / 2f),
            bounds.Y + MathF.Max(0f, (bounds.Height - scaledSize.Y) / 2f));
        context.SpriteBatch.DrawString(font, textToDraw, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawPreviewTexture(RenderingContext context, string textureKey, Rectangle bounds)
    {
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
        context.SpriteBatch.Draw(texture, destination, Color.White);
    }

    private static string FitTextToWidth(SpriteFont font, string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (font.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        if (font.MeasureString(ellipsis).X > maxWidth)
        {
            return string.Empty;
        }

        var endIndex = text.Length;
        while (endIndex > 0)
        {
            var candidate = $"{text[..endIndex].TrimEnd()}{ellipsis}";
            if (font.MeasureString(candidate).X <= maxWidth)
            {
                return candidate;
            }

            endIndex--;
        }

        return ellipsis;
    }
}
