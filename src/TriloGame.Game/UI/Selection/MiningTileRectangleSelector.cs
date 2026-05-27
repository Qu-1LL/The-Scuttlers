using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class MiningTileRectangleSelector
{
    public static IReadOnlyList<string> SelectTileKeys(
        Cave cave,
        Rectangle selection,
        Func<Point, Vector2> screenToWorld,
        Func<GridPoint, Rectangle> getTileScreenBounds,
        Predicate<Tile> canSelectTile)
    {
        ArgumentNullException.ThrowIfNull(cave);
        ArgumentNullException.ThrowIfNull(screenToWorld);
        ArgumentNullException.ThrowIfNull(getTileScreenBounds);
        ArgumentNullException.ThrowIfNull(canSelectTile);

        var selectedKeys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var topLeft = screenToWorld(new Point(selection.Left, selection.Top));
        var bottomRight = screenToWorld(new Point(selection.Right, selection.Bottom));
        var minTileX = (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X) / TileConstants.TileSize) - 1;
        var minTileY = (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y) / TileConstants.TileSize) - 1;
        var maxTileX = (int)MathF.Ceiling(MathF.Max(topLeft.X, bottomRight.X) / TileConstants.TileSize) + 1;
        var maxTileY = (int)MathF.Ceiling(MathF.Max(topLeft.Y, bottomRight.Y) / TileConstants.TileSize) + 1;

        for (var y = minTileY; y <= maxTileY; y++)
        {
            for (var x = minTileX; x <= maxTileX; x++)
            {
                var tile = cave.GetTile(new GridPoint(x, y).ToString());
                if (tile is null || !canSelectTile(tile))
                {
                    continue;
                }

                var tileBounds = getTileScreenBounds(tile.Coordinates);
                if (!selection.Intersects(tileBounds) || !seen.Add(tile.Key))
                {
                    continue;
                }

                selectedKeys.Add(tile.Key);
            }
        }

        return selectedKeys;
    }
}
