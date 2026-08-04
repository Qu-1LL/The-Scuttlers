using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Gum;

internal readonly record struct GumScrollableTextLayout(
    Rectangle ViewportBounds,
    Rectangle LocalTextBounds,
    string WrappedText,
    int LineCount,
    float Scroll,
    float MaxScroll,
    Rectangle? ScrollbarTrackBounds,
    Rectangle? ScrollbarThumbBounds);

internal static class GumScrollableText
{
    public static GumScrollableTextLayout Build(
        Rectangle viewportBounds,
        string text,
        GumTextStyle style,
        float requestedScroll,
        int scrollbarWidth = 6,
        int scrollbarGap = 8,
        int minThumbHeight = 32)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        var wrappedText = string.Empty;
        var lineCount = 0;
        var maxScroll = 0f;
        var scroll = 0f;
        Rectangle? scrollbarTrackBounds = null;
        Rectangle? scrollbarThumbBounds = null;
        var textWidth = Math.Max(0, viewportBounds.Width);

        if (!string.IsNullOrWhiteSpace(text) && viewportBounds.Width > 0 && viewportBounds.Height > 0)
        {
            (wrappedText, lineCount, maxScroll) = Wrap(text, textWidth, viewportBounds.Height, style, metrics);
            if (maxScroll > 0f)
            {
                textWidth = Math.Max(24, viewportBounds.Width - scrollbarGap - scrollbarWidth);
                (wrappedText, lineCount, maxScroll) = Wrap(text, textWidth, viewportBounds.Height, style, metrics);
                scroll = Math.Clamp(requestedScroll, 0f, maxScroll);

                var trackHeight = viewportBounds.Height;
                var contentHeight = Math.Max(metrics.LineHeight, lineCount * metrics.LineHeight);
                // The minimum thumb yields to the track: a thumb can never be taller than the track
                // it slides in. Written with a bare Math.Clamp this threw whenever a viewport was
                // shorter than minThumbHeight, which took the whole menu down over a merely cramped
                // panel. See UiMath.ClampAtMost.
                var thumbHeight = UiMath.ClampAtMost(
                    (int)MathF.Round(trackHeight * (viewportBounds.Height / (float)Math.Max(viewportBounds.Height, contentHeight))),
                    minThumbHeight,
                    trackHeight);
                var travel = Math.Max(0, trackHeight - thumbHeight);
                var thumbY = viewportBounds.Y + (int)MathF.Round(travel * (scroll / maxScroll));
                var scrollbarX = viewportBounds.Right - scrollbarWidth;
                scrollbarTrackBounds = new Rectangle(scrollbarX, viewportBounds.Y, scrollbarWidth, trackHeight);
                scrollbarThumbBounds = new Rectangle(scrollbarX, thumbY, scrollbarWidth, thumbHeight);
            }
        }

        var contentHeightWithFallback = Math.Max(metrics.LineHeight, lineCount * metrics.LineHeight);
        return new GumScrollableTextLayout(
            viewportBounds,
            new Rectangle(0, -(int)MathF.Round(scroll), Math.Max(0, textWidth), contentHeightWithFallback),
            wrappedText,
            lineCount,
            scroll,
            maxScroll,
            scrollbarTrackBounds,
            scrollbarThumbBounds);
    }

    public static void Draw(GumUiRenderer gumUi, GumScrollableTextLayout layout, Color color, GumTextStyle style)
    {
        if (string.IsNullOrWhiteSpace(layout.WrappedText) ||
            layout.ViewportBounds.Width <= 0 ||
            layout.ViewportBounds.Height <= 0)
        {
            return;
        }

        var clipLayer = gumUi.AddClippingContainer(layout.ViewportBounds);
        GumUiText.Add(
            gumUi,
            clipLayer,
            layout.LocalTextBounds,
            layout.WrappedText,
            color,
            style,
            verticalAlignment: RenderingLibrary.Graphics.VerticalAlignment.Top,
            maxLines: layout.LineCount);
    }

    private static (string WrappedText, int LineCount, float MaxScroll) Wrap(
        string text,
        int width,
        int viewportHeight,
        GumTextStyle style,
        GumTextMetrics metrics)
    {
        var lines = GumTextLayout.WrapAll(text.Replace("\r", string.Empty).Split('\n'), width, style);
        var lineCount = lines.Count;
        var contentHeight = Math.Max(metrics.LineHeight, lineCount * metrics.LineHeight);
        return (string.Join('\n', lines), lineCount, Math.Max(0f, contentHeight - viewportHeight));
    }
}
