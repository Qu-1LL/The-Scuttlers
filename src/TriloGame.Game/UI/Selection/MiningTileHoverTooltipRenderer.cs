using Microsoft.Xna.Framework;
using TriloGame.Game.Core.World;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Selection;

public sealed class MiningTileHoverTooltipRenderer
{
    private const int PaddingX = 14;
    private const int PaddingY = 8;
    private const int LineGap = 2;

    public void Draw(GumUiRenderer gumUi, Tile tile, Point mousePoint, Rectangle gameplayBounds)
    {
        ArgumentNullException.ThrowIfNull(gumUi);
        var model = BuildModel(tile, mousePoint, gameplayBounds);
        if (model.Lines.Count == 0)
        {
            return;
        }

        gumUi.AddRoundedFrame(model.Bounds, new Color(7, 15, 22, 230), new Color(143, 205, 226), 2, 12);
        var metrics = GumTextLayout.GetMetrics(GumTextStyle.Small);
        var textBounds = new Rectangle(
            model.Bounds.X + PaddingX,
            model.Bounds.Y + PaddingY,
            Math.Max(0, model.Bounds.Width - (PaddingX * 2)),
            metrics.LineHeight);
        for (var index = 0; index < model.Lines.Count; index++)
        {
            var lineBounds = new Rectangle(
                textBounds.X,
                textBounds.Y + (index * (metrics.LineHeight + LineGap)),
                textBounds.Width,
                metrics.LineHeight);
            gumUi.AddText(lineBounds, model.Lines[index], Color.White, fontSize: metrics.FontSize, maxLines: 1);
        }
    }

    internal static MiningTileHoverTooltipModel BuildModel(Tile tile, Point mousePoint, Rectangle gameplayBounds)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var lines = BuildLines(tile);
        var maxWidth = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            maxWidth = Math.Max(maxWidth, GumTextLayout.Measure(lines[index], GumTextStyle.Small).X);
        }

        var lineHeight = GumTextLayout.GetMetrics(GumTextStyle.Small).LineHeight;
        var labelHeight = (lineHeight * lines.Count) + Math.Max(0, (lines.Count - 1) * LineGap);
        var bounds = new Rectangle(
            mousePoint.X + 14,
            mousePoint.Y - (labelHeight + 20),
            Math.Max(132, maxWidth + (PaddingX * 2) + 8),
            labelHeight + (PaddingY * 2));
        if (bounds.Right > gameplayBounds.Right)
        {
            bounds.X = gameplayBounds.Right - bounds.Width;
        }

        if (bounds.Y < gameplayBounds.Top)
        {
            bounds.Y = mousePoint.Y + 14;
        }

        return new MiningTileHoverTooltipModel(bounds, lines);
    }

    private static IReadOnlyList<string> BuildLines(Tile tile)
    {
        var lines = new List<string> { GetTileDisplayName(tile) };
        if (tile.IsOreTile())
        {
            lines.Add($"Yield: {tile.ResourceYield}");
        }

        return lines;
    }

    private static string GetTileDisplayName(Tile tile)
    {
        return tile.Base switch
        {
            "wall" => "Wall",
            "empty" => "Unknown",
            _ => tile.Base
        };
    }
}

internal readonly record struct MiningTileHoverTooltipModel(
    Rectangle Bounds,
    IReadOnlyList<string> Lines);
