using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Tests.UI;

public sealed class GumUiViewportTests
{
    [Fact]
    public void TryClipLine_TrimsConnectorToVisibleBounds()
    {
        var start = new Vector2(160f, 346f);
        var end = new Vector2(160f, 520f);

        var clipped = GumUiViewport.TryClipLine(new Rectangle(108, 86, 444, 272), ref start, ref end);

        Assert.True(clipped);
        Assert.Equal(new Vector2(160f, 346f), start);
        Assert.Equal(new Vector2(160f, 358f), end);
    }
}
