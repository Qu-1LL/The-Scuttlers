using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Selection;

public readonly record struct MiningOrderMenuLayout(
    Rectangle PanelBounds,
    Rectangle HeaderBounds,
    Rectangle SubtitleBounds,
    Rectangle ListViewportBounds,
    IReadOnlyList<MiningOrderMenuRow> Rows,
    float MaxScroll,
    Rectangle? ScrollbarTrackBounds,
    Rectangle? ScrollbarThumbBounds,
    Rectangle SendButtonBounds)
{
    public static MiningOrderMenuLayout Build(MiningOrderMenuController state, Point viewport, float openPanelWidth)
    {
        var gameplayBounds = SelectionFocusLayout.GetGameplayBounds(viewport, openPanelWidth);
        const int panelWidth = 300;
        const int rowHeight = 36;
        const int rowGap = 6;
        const int headerHeight = 32;
        const int subtitleHeight = 20;
        const int sendHeight = 42;
        const int viewportHeight = 240;

        var listBounds = new Rectangle(0, 0, panelWidth - 32, viewportHeight);
        var contentHeight = state.Miners.Count == 0 ? 0 : (state.Miners.Count * rowHeight) + (Math.Max(0, state.Miners.Count - 1) * rowGap);
        var maxScroll = Math.Max(0f, contentHeight - listBounds.Height);
        state.ClampScroll(maxScroll);

        var panelHeight = 16 + headerHeight + subtitleHeight + 12 + viewportHeight + 14 + sendHeight + 16;
        var panelX = (int)MathF.Round(Math.Clamp(state.AnchorScreen.X, gameplayBounds.Left + 8f, gameplayBounds.Right - panelWidth - 8f));
        var panelY = (int)MathF.Round(Math.Clamp(state.AnchorScreen.Y, gameplayBounds.Top + 8f, gameplayBounds.Bottom - panelHeight - 8f));
        var panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        var headerBounds = new Rectangle(panelBounds.X + 16, panelBounds.Y + 14, panelBounds.Width - 32, headerHeight);
        var subtitleBounds = new Rectangle(panelBounds.X + 16, headerBounds.Bottom + 2, panelBounds.Width - 32, subtitleHeight);
        var viewportBounds = new Rectangle(panelBounds.X + 16, subtitleBounds.Bottom + 10, panelBounds.Width - 32, viewportHeight);
        var sendButtonBounds = new Rectangle(panelBounds.X + 16, viewportBounds.Bottom + 14, panelBounds.Width - 32, sendHeight);

        var rows = new List<MiningOrderMenuRow>(state.Miners.Count);
        var rowY = viewportBounds.Y - (int)MathF.Round(state.Scroll);
        foreach (var miner in state.Miners)
        {
            var bounds = new Rectangle(viewportBounds.X + 6, rowY, viewportBounds.Width - 18, rowHeight);
            if (bounds.Bottom >= viewportBounds.Top && bounds.Top <= viewportBounds.Bottom)
            {
                rows.Add(new MiningOrderMenuRow(miner, bounds));
            }

            rowY += rowHeight + rowGap;
        }

        Rectangle? trackBounds = null;
        Rectangle? thumbBounds = null;
        if (maxScroll > 0f)
        {
            var trackHeight = viewportBounds.Height;
            var thumbHeight = Math.Max(32f, (viewportBounds.Height / (float)contentHeight) * trackHeight);
            var travel = Math.Max(0f, trackHeight - thumbHeight);
            var ratio = state.Scroll / maxScroll;
            var thumbY = viewportBounds.Y + (int)MathF.Round(ratio * travel);
            trackBounds = new Rectangle(viewportBounds.Right - 6, viewportBounds.Y, 6, trackHeight);
            thumbBounds = new Rectangle(viewportBounds.Right - 6, thumbY, 6, (int)MathF.Round(thumbHeight));
        }

        return new MiningOrderMenuLayout(panelBounds, headerBounds, subtitleBounds, viewportBounds, rows, maxScroll, trackBounds, thumbBounds, sendButtonBounds);
    }
}
