using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Tests.UI;

public sealed class GumUiSurfaceTests
{
    [Fact]
    public void ToLocal_ConvertsScreenRectanglesIntoViewportSpace()
    {
        var surface = new GumUiSurface(new Rectangle(108, 86, 444, 272));

        var local = surface.ToLocal(new Rectangle(120, 320, 140, 190));

        Assert.Equal(new Rectangle(12, 234, 140, 190), local);
    }

    [Fact]
    public void ToLocal_ConvertsScreenPointsAndVectorsIntoViewportSpace()
    {
        var surface = new GumUiSurface(new Rectangle(108, 86, 444, 272));

        Assert.Equal(new Point(12, 234), surface.ToLocal(new Point(120, 320)));
        Assert.Equal(new Vector2(12f, 234f), surface.ToLocal(new Vector2(120f, 320f)));
    }

    [Theory]
    [InlineData(120, 320, 140, 190, true)]
    [InlineData(120, 370, 140, 190, false)]
    [InlineData(108, 86, 1, 1, true)]
    [InlineData(552, 86, 1, 1, false)]
    public void Intersects_ReportsWhetherScreenBoundsTouchTheViewport(
        int x,
        int y,
        int width,
        int height,
        bool expected)
    {
        var surface = new GumUiSurface(new Rectangle(108, 86, 444, 272));

        Assert.Equal(expected, surface.Intersects(new Rectangle(x, y, width, height)));
    }
}
