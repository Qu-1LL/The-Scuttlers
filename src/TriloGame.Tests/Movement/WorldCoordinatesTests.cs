using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Movement;

public sealed class WorldCoordinatesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 11)]
    [InlineData(-4, 3)]
    public void GridPoint_RoundTripsThroughFixedPointWorldPosition(int x, int y)
    {
        var cell = new GridPoint(x, y);

        var position = WorldPoint.FromGridPoint(cell);

        Assert.Equal(cell, position.ToGridPoint());
        Assert.Equal(new Vector2(x * TileConstants.TileSize, y * TileConstants.TileSize), position.ToWorldPixels());
    }

    [Fact]
    public void ClampMagnitude_UsesDeterministicIntegerLength()
    {
        var vector = new WorldVector(300, 400);

        var clamped = vector.ClampMagnitude(250);

        Assert.Equal(new WorldVector(150, 200), clamped);
        Assert.Equal(250, clamped.Length);
    }
}
