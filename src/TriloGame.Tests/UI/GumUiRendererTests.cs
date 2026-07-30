using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Tests.UI;

public sealed class GumUiRendererTests
{
    [Theory]
    [InlineData("Skill Tree", 0, 1)]
    [InlineData("Status\nDefend!", 0, 2)]
    [InlineData("Wrapped", 3, 3)]
    public void ResolveMaxNumberOfLines_UsesExplicitValuesOrDerivesFromText(
        string text,
        int requestedMaxLines,
        int expected)
    {
        Assert.Equal(expected, GumUiRenderer.ResolveMaxNumberOfLines(text, requestedMaxLines));
    }

    [Theory]
    [InlineData(120f, 240f, 200f, 240f, 4)]
    [InlineData(120f, 240f, 72f, 192f, 3)]
    [InlineData(120f, 240f, 168f, 144f, 5)]
    public void CreateLineLayout_ReconstructsTheRequestedEndpoints(
        float startX,
        float startY,
        float endX,
        float endY,
        int thickness)
    {
        var start = new Vector2(startX, startY);
        var end = new Vector2(endX, endY);

        var layout = GumUiRenderer.CreateLineLayout(start, end, thickness);

        var actualStart = new Vector2(
            layout.X + (MathF.Sin(MathHelper.ToRadians(layout.Rotation)) * (layout.Height / 2f)),
            layout.Y + (MathF.Cos(MathHelper.ToRadians(layout.Rotation)) * (layout.Height / 2f)));
        var rotationRadians = MathHelper.ToRadians(layout.Rotation);
        var actualEnd = actualStart + (new Vector2(MathF.Cos(rotationRadians), -MathF.Sin(rotationRadians)) * layout.Width);

        AssertVectorEqual(start, actualStart);
        AssertVectorEqual(end, actualEnd);
        Assert.Equal(thickness, layout.Height);
    }

    [Fact]
    public void CreateLineLayout_RejectsZeroLengthLines()
    {
        Assert.Throws<ArgumentException>(() => GumUiRenderer.CreateLineLayout(new Vector2(12f, 18f), new Vector2(12f, 18f), 2));
    }

    [Fact]
    public void AddText_UsesRequestedFontSizeWithoutScaling()
    {
        var renderer = new GumUiRenderer(addToManagers: false);
        renderer.BeginFrame(new Point(800, 600));

        renderer.AddText(
            new Rectangle(10, 12, 400, 150),
            "Trilodex",
            Color.White,
            fontSize: 120);

        var text = Assert.IsType<TextRuntime>(renderer.Root.Children[^1]);
        Assert.False(text.UseCustomFont);
        Assert.Equal(GumTextStyleCatalog.DefaultFontFamily, text.Font);
        Assert.Equal(120, text.FontSize);
        Assert.Equal(1f, text.FontScale);
    }

    [Fact]
    public void AddFilledRectangle_UsesFilledRectangleRuntime()
    {
        var renderer = new GumUiRenderer(addToManagers: false);
        renderer.BeginFrame(new Point(800, 600));

        renderer.AddFilledRectangle(new Rectangle(10, 12, 40, 24), Color.CornflowerBlue);

        var rectangle = Assert.IsType<RectangleRuntime>(renderer.Root.Children[^1]);
        Assert.Equal(10, rectangle.X);
        Assert.Equal(12, rectangle.Y);
        Assert.Equal(40, rectangle.Width);
        Assert.Equal(24, rectangle.Height);
        Assert.Equal(Color.CornflowerBlue, rectangle.FillColor);
    }

    [Fact]
    public void AddRoundedFrame_UsesFilledRoundedRectangleWithStrokedOutline()
    {
        var renderer = new GumUiRenderer(addToManagers: false);
        renderer.BeginFrame(new Point(800, 600));

        var fill = new Color(8, 19, 29, 247);
        var border = new Color(77, 122, 140);
        renderer.AddRoundedFrame(new Rectangle(10, 12, 200, 80), fill, border, thickness: 3, radius: 16);

        var filledRectangle = Assert.IsType<RectangleRuntime>(renderer.Root.Children[^2]);
        var outline = Assert.IsType<RectangleRuntime>(renderer.Root.Children[^1]);
        Assert.True(filledRectangle.IsFilled);
        Assert.Equal(fill, filledRectangle.FillColor);
        Assert.Equal(16, filledRectangle.CornerRadius);
        Assert.False(outline.IsFilled);
        Assert.Equal(border, outline.StrokeColor);
        Assert.Equal(3, outline.StrokeWidth);
        Assert.Equal(16, outline.CornerRadius);
    }

    private static void AssertVectorEqual(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
        Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
    }
}
