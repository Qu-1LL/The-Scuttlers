using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Rendering;

namespace TriloGame.Game.Audio;

public static class ScreenSpaceAudio
{
    // Convert visible on-screen footprint coverage into an audio gain.
    public static float CalculateVisibleCoverage(
        Vector2 centerScreen,
        float widthTiles,
        float heightTiles,
        CameraController camera)
    {
        var viewportWidth = camera.ViewCenter.X * 2f;
        var viewportHeight = camera.ViewCenter.Y * 2f;
        return CalculateVisibleCoverage(centerScreen, widthTiles, heightTiles, camera.CurrentScale, viewportWidth, viewportHeight);
    }

    internal static float CalculateVisibleCoverage(
        Vector2 centerScreen,
        float widthTiles,
        float heightTiles,
        float cameraScale,
        float viewportWidth,
        float viewportHeight)
    {
        if (widthTiles <= 0f || heightTiles <= 0f || viewportWidth <= 0f || viewportHeight <= 0f || cameraScale <= 0f)
        {
            return 0f;
        }

        var tileScreenSize = TileConstants.TileSize * cameraScale;
        var sourceWidth = widthTiles * tileScreenSize;
        var sourceHeight = heightTiles * tileScreenSize;
        var sourceLeft = centerScreen.X - (sourceWidth / 2f);
        var sourceTop = centerScreen.Y - (sourceHeight / 2f);
        var sourceRight = centerScreen.X + (sourceWidth / 2f);
        var sourceBottom = centerScreen.Y + (sourceHeight / 2f);

        var visibleWidth = MathF.Max(0f, MathF.Min(sourceRight, viewportWidth) - MathF.Max(sourceLeft, 0f));
        var visibleHeight = MathF.Max(0f, MathF.Min(sourceBottom, viewportHeight) - MathF.Max(sourceTop, 0f));
        var visibleArea = visibleWidth * visibleHeight;
        var viewportArea = viewportWidth * viewportHeight;
        return Math.Clamp(visibleArea / viewportArea, 0f, 1f);
    }

    internal static float CalculateSquareCoverageForTesting(float footprintTiles, float cameraScale, int viewportWidth, int viewportHeight)
    {
        var sideTiles = MathF.Sqrt(MathF.Max(0f, footprintTiles));
        return CalculateVisibleCoverage(
            new Vector2(viewportWidth / 2f, viewportHeight / 2f),
            sideTiles,
            sideTiles,
            cameraScale,
            viewportWidth,
            viewportHeight);
    }
}
