using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Resources;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private static readonly Color ResourceTotalsPanelFill = new(9, 21, 31, 242);
    private static readonly Color ResourceTotalsPanelBorder = new(75, 117, 136);
    private static readonly Color ResourceTotalsCountColor = new(237, 244, 248);

    private void DrawStoredResourceTotals()
    {
        if (!HasGumUiRenderer)
        {
            return;
        }

        var layout = StoredResourceTotalsLayout.Build(Window.ClientBounds.Size, _session.Resources);
        if (layout.Rows.Count == 0)
        {
            return;
        }

        DrawRoundedScreenFrame(
            layout.PanelBounds,
            ResourceTotalsPanelFill,
            ResourceTotalsPanelBorder,
            layout.PanelBorderThickness,
            layout.PanelRadius);

        var fontStyle = GumTextStyle.Small;
        var fontSize = GumTextLayout.GetMetrics(fontStyle).FontSize;
        foreach (var row in layout.Rows)
        {
            if (_rendering.Sprites.TryGet(row.TextureKey, out var texture))
            {
                _gumUiRenderer.AddSprite(row.IconBounds, texture);
            }

            _gumUiRenderer.AddText(
                row.TextBounds,
                row.CountText,
                ResourceTotalsCountColor,
                fontSize: fontSize,
                verticalAlignment: VerticalAlignment.Center);
        }
    }
}
