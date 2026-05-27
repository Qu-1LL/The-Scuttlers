using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Selection;

public sealed class MiningOrderMenuRenderer
{
    private static readonly GumUiFrameStyle PanelFrameStyle = new(new Color(8, 19, 29, 247), new Color(77, 122, 140), 3, 16);
    private static readonly GumUiFrameStyle ListFrameStyle = new(new Color(10, 22, 32), new Color(74, 114, 132), 2, 12);
    private static readonly GumUiFrameStyle ScrollbarTrackStyle = new(new Color(9, 19, 28), new Color(39, 64, 79), 2, 6);
    private static readonly GumUiFrameStyle ScrollbarThumbStyle = new(new Color(109, 170, 192), new Color(191, 230, 244), 2, 6);
    private static readonly GumUiButtonStyle EnabledSendButtonStyle = new(
        new GumUiFrameStyle(new Color(170, 148, 102), new Color(235, 210, 158), 2, 12),
        new GumUiFrameStyle(new Color(194, 171, 122), new Color(255, 232, 184), 2, 12),
        new Color(10, 23, 34),
        GumTextStyle.Small);
    private static readonly GumUiFrameStyle DisabledSendButtonStyle = new(new Color(44, 53, 61), new Color(89, 100, 109), 2, 12);

    public void Draw(
        GumUiRenderer gumUi,
        MiningOrderMenuLayout layout,
        MiningOrderMenuController menu,
        Point pointer,
        int targetTileCount,
        bool hasSelectedTargets)
    {
        GumUiChrome.DrawFrame(gumUi, layout.PanelBounds, PanelFrameStyle);
        GumUiText.AddFittedCentered(gumUi, layout.HeaderBounds, "Send Miners", Color.White, GumTextStyle.UiLarge);
        GumUiText.AddFittedCentered(
            gumUi,
            layout.SubtitleBounds,
            $"{targetTileCount} target tiles",
            new Color(180, 214, 226),
            GumTextStyle.Small);

        GumUiChrome.DrawFrame(gumUi, layout.ListViewportBounds, ListFrameStyle);
        if (layout.Rows.Count == 0)
        {
            GumUiText.AddFittedCentered(gumUi, layout.ListViewportBounds, "No miners available", new Color(171, 198, 208), GumTextStyle.Small);
        }

        foreach (var row in layout.Rows)
        {
            var hovered = row.Bounds.Contains(pointer);
            var selected = menu.SelectedMiners.Contains(row.Miner);
            var fill = selected
                ? hovered ? new Color(39, 86, 109) : new Color(33, 75, 95)
                : hovered ? new Color(20, 48, 68) : new Color(13, 33, 48);
            var border = selected
                ? hovered ? new Color(160, 221, 237) : new Color(140, 207, 224)
                : hovered ? new Color(76, 116, 136) : new Color(53, 88, 106);
            GumUiChrome.DrawFrame(gumUi, row.Bounds, new GumUiFrameStyle(fill, border, 2, 12));
            GumUiText.AddFittedLeft(gumUi, new Rectangle(row.Bounds.X + 12, row.Bounds.Y + 4, row.Bounds.Width - 24, row.Bounds.Height - 8), row.Miner.Name, Color.White, GumTextStyle.Small);
        }

        if (layout.ScrollbarTrackBounds is { } track && layout.ScrollbarThumbBounds is { } thumb)
        {
            GumUiChrome.DrawFrame(gumUi, track, ScrollbarTrackStyle);
            GumUiChrome.DrawFrame(gumUi, thumb, ScrollbarThumbStyle);
        }

        var sendHovered = layout.SendButtonBounds.Contains(pointer);
        var sendEnabled = menu.SelectedMiners.Count > 0 && hasSelectedTargets;
        if (!sendEnabled)
        {
            GumUiChrome.DrawFrame(gumUi, layout.SendButtonBounds, DisabledSendButtonStyle);
            GumUiText.AddFittedCentered(gumUi, layout.SendButtonBounds, "Send Miners", new Color(160, 171, 178), GumTextStyle.Small);
            return;
        }

        GumUiChrome.DrawButton(gumUi, layout.SendButtonBounds, "Send Miners", sendHovered, EnabledSendButtonStyle);
    }
}
