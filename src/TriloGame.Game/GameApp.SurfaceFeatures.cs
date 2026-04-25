using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private void DrawSurfaceFeatures(Cave cave)
    {
        foreach (var antHole in cave.GetAntHoles())
        {
            var tile = cave.GetTile(antHole.TileKey);
            if (tile is null || !cave.IsTileRevealed(tile))
            {
                continue;
            }

            DrawWorldTextureNative(
                "AntHole",
                new Vector2(tile.Coordinates.X * TileConstants.TileSize, tile.Coordinates.Y * TileConstants.TileSize));
        }
    }
}
