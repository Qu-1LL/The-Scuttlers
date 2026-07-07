using TriloGame.Game.Core.World;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    // Full-map visibility stays render-only so the real reveal set keeps updating underneath.
    private IEnumerable<Tile> GetMapVisibleTiles(Cave cave)
    {
        _camera.GetVisibleWorldBounds(Window.ClientBounds.Size, out var topLeft, out var bottomRight);

        var minWorldX = MathF.Min(topLeft.X, bottomRight.X);
        var minWorldY = MathF.Min(topLeft.Y, bottomRight.Y);
        var maxWorldX = MathF.Max(topLeft.X, bottomRight.X);
        var maxWorldY = MathF.Max(topLeft.Y, bottomRight.Y);

        var minTileX = (int)MathF.Floor((minWorldX - TileConstants.TileHalfSize) / TileConstants.TileSize) - 2;
        var minTileY = (int)MathF.Floor((minWorldY - TileConstants.TileHalfSize) / TileConstants.TileSize) - 2;
        var maxTileX = (int)MathF.Ceiling((maxWorldX + TileConstants.TileHalfSize) / TileConstants.TileSize) + 2;
        var maxTileY = (int)MathF.Ceiling((maxWorldY + TileConstants.TileHalfSize) / TileConstants.TileSize) + 2;

        for (var y = minTileY; y <= maxTileY; y++)
        {
            for (var x = minTileX; x <= maxTileX; x++)
            {
                var tile = cave.GetTile(new GridPoint(x, y));
                if (tile is null)
                {
                    continue;
                }

                if (!_showFullMapVisibility && !cave.IsTileRevealed(tile))
                {
                    continue;
                }

                yield return tile;
            }
        }
    }

    private bool IsMapTileVisible(Cave cave, Tile? tile)
    {
        return tile is not null && (_showFullMapVisibility || cave.IsTileRevealed(tile));
    }
}
