using Microsoft.Xna.Framework;
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

    private static void AssertVectorEqual(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
        Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
    }
}
