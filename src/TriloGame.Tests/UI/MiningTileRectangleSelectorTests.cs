using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class MiningTileRectangleSelectorTests
{
    [Fact]
    public void SelectTileKeys_ReturnsSelectableIntersectingTilesInScanOrder()
    {
        var (_, cave) = TestWorldFactory.CreateRectangularSession(width: 3, height: 1);
        var selection = new Rectangle(-100, -100, 700, 200);

        var selectedKeys = MiningTileRectangleSelector.SelectTileKeys(
            cave,
            selection,
            point => point.ToVector2(),
            GetTileScreenBounds,
            tile => tile.Coordinates.Y == 0);

        Assert.Equal(["0,0", "1,0"], selectedKeys);
    }

    [Fact]
    public void SelectTileKeys_SkipsTilesRejectedBySelectionPolicy()
    {
        var (_, cave) = TestWorldFactory.CreateRectangularSession(width: 3, height: 1);
        var selection = new Rectangle(-100, -100, 700, 200);

        var selectedKeys = MiningTileRectangleSelector.SelectTileKeys(
            cave,
            selection,
            point => point.ToVector2(),
            GetTileScreenBounds,
            tile => tile.Key != "1,0");

        Assert.Equal(["0,0"], selectedKeys);
    }

    private static Rectangle GetTileScreenBounds(GridPoint point)
    {
        var center = new Vector2(point.X * TileConstants.TileSize, point.Y * TileConstants.TileSize);
        var topLeft = center - new Vector2(TileConstants.TileHalfSize, TileConstants.TileHalfSize);
        var bottomRight = center + new Vector2(TileConstants.TileHalfSize, TileConstants.TileHalfSize);
        var left = (int)MathF.Floor(topLeft.X);
        var top = (int)MathF.Floor(topLeft.Y);
        var right = (int)MathF.Ceiling(bottomRight.X);
        var bottom = (int)MathF.Ceiling(bottomRight.Y);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }
}
