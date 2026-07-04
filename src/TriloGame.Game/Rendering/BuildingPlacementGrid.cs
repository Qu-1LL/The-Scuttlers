using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering;

public static class BuildingPlacementGrid
{
    public static GridPoint GetSnappedTopLeft(CameraController camera, Point cursor, Building building)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(building);

        var cursorWorld = camera.ScreenToWorld(cursor);
        var footprintCenterOffset = GetFootprintCenterOffset(building);
        return new GridPoint(
            (int)MathF.Round((cursorWorld.X - footprintCenterOffset.X) / TileConstants.TileSize),
            (int)MathF.Round((cursorWorld.Y - footprintCenterOffset.Y) / TileConstants.TileSize));
    }

    public static Vector2 GetWorldCenter(GridPoint topLeft, Building building)
    {
        ArgumentNullException.ThrowIfNull(building);

        return new Vector2(
            (topLeft.X * TileConstants.TileSize) + ((building.Size.X - 1) * TileConstants.TileHalfSize),
            (topLeft.Y * TileConstants.TileSize) + ((building.Size.Y - 1) * TileConstants.TileHalfSize));
    }

    public static Vector2 GetWorldCenter(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);
        return GetWorldCenter(building.Location ?? GridPoint.Zero, building);
    }

    public static Vector2 GetTextureCenterOrigin(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);

        var baseSize = building.GetDisplayPivotBaseSize();
        return new Vector2(baseSize.X * TileConstants.TileHalfSize, baseSize.Y * TileConstants.TileHalfSize);
    }

    public static Vector2 GetTextureFootprintScale(Building building, int textureWidth, int textureHeight, float cameraScale)
    {
        ArgumentNullException.ThrowIfNull(building);

        var baseSize = building.GetDisplayPivotBaseSize();
        return new Vector2(
            (baseSize.X * TileConstants.TileSize * cameraScale) / Math.Max(1, textureWidth),
            (baseSize.Y * TileConstants.TileSize * cameraScale) / Math.Max(1, textureHeight));
    }

    private static Vector2 GetFootprintCenterOffset(Building building)
    {
        return new Vector2(
            (building.Size.X - 1) * TileConstants.TileHalfSize,
            (building.Size.Y - 1) * TileConstants.TileHalfSize);
    }
}
