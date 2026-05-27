using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private void DrawSurfaceFeatures(Cave cave)
    {
        foreach (var antHole in cave.GetAntHoles())
        {
            var tile = cave.GetTile(antHole.TileKey);
            if (tile is null || !IsMapTileVisible(cave, tile))
            {
                continue;
            }

            DrawWorldTextureNative(
                "AntHole",
                new Vector2(tile.Coordinates.X * TileConstants.TileSize, tile.Coordinates.Y * TileConstants.TileSize));
        }

        var opal = cave.GetOpalNode();
        if (opal is null)
        {
            return;
        }

        var opalTile = cave.GetTile(opal.TileKey);
        if (opalTile is null || !IsMapTileVisible(cave, opalTile))
        {
            return;
        }

        var warningProgress = opal.GetWarningProgress();
        var worldCenter = new Vector2(opalTile.Coordinates.X * TileConstants.TileSize, opalTile.Coordinates.Y * TileConstants.TileSize);
        var shakeAmplitude = GameConstants.OpalMaxShakePixels * warningProgress;
        var time = (float)(_uiClockMs * 0.0015d);
        var seed = opalTile.Id * 0.173f;
        var shakeOffset = new Vector2(
            PerlinNoise.Sample(time + seed, seed) * shakeAmplitude,
            PerlinNoise.Sample(seed, time + (seed * 0.5f)) * shakeAmplitude);
        var tint = Color.Lerp(Color.White, Color.Red, opal.GetRedness());

        DrawWorldTextureNative("Opal", worldCenter + shakeOffset, color: tint);
    }
}
