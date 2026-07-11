using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class BuildingPlacementGridTests
{
    [Fact]
    public void GetSnappedTopLeft_UsesCursorAsOddSizedFootprintCenter()
    {
        var camera = CreateCamera();
        var building = new Barracks(new GameSession());
        var expectedTopLeft = new GridPoint(8, -3);
        var cursor = ToCursor(camera, BuildingPlacementGrid.GetWorldCenter(expectedTopLeft, building));

        var topLeft = BuildingPlacementGrid.GetSnappedTopLeft(camera, cursor, building);

        Assert.Equal(expectedTopLeft, topLeft);
    }

    [Fact]
    public void GetSnappedTopLeft_UsesCursorAsEvenSizedFootprintCenter()
    {
        var camera = CreateCamera();
        var building = new AlgaeFarm(new GameSession());
        var expectedTopLeft = new GridPoint(-4, 7);
        var cursor = ToCursor(camera, BuildingPlacementGrid.GetWorldCenter(expectedTopLeft, building));

        var topLeft = BuildingPlacementGrid.GetSnappedTopLeft(camera, cursor, building);

        Assert.Equal(expectedTopLeft, topLeft);
    }

    [Fact]
    public void GetSnappedTopLeft_SnapsToNearestGridLocationAfterCenterOffset()
    {
        var camera = CreateCamera();
        var building = new AlgaeFarm(new GameSession());
        var expectedTopLeft = new GridPoint(3, 5);
        var cursorWorld = BuildingPlacementGrid.GetWorldCenter(expectedTopLeft, building)
            + new Vector2(TileConstants.TileHalfSize - 8f, -TileConstants.TileHalfSize + 8f);
        var cursor = ToCursor(camera, cursorWorld);

        var topLeft = BuildingPlacementGrid.GetSnappedTopLeft(camera, cursor, building);

        Assert.Equal(expectedTopLeft, topLeft);
    }

    [Fact]
    public void GetTextureCenterOrigin_UsesDisplayBaseSizeAfterRotation()
    {
        var building = new AlgaeFarm(new GameSession());

        building.RotateMap();
        var origin = BuildingPlacementGrid.GetTextureCenterOrigin(building);

        Assert.Equal((float)TileConstants.TileSize, origin.X);
        Assert.Equal((float)(TileConstants.TileSize + TileConstants.TileHalfSize), origin.Y);
    }

    [Fact]
    public void GetTextureFootprintScale_ScalesSingleTileImportToBuildingFootprint()
    {
        var building = new Garage(new GameSession());

        var scale = BuildingPlacementGrid.GetTextureFootprintScale(
            building,
            textureWidth: TileConstants.TileSize,
            textureHeight: TileConstants.TileSize,
            cameraScale: 1f);

        Assert.Equal(2f, scale.X);
        Assert.Equal(2f, scale.Y);
    }

    [Fact]
    public void GetTextureFootprintScale_KeepsAlreadySizedFootprintTextureAtCameraScale()
    {
        var building = new AlgaeFarm(new GameSession());

        var scale = BuildingPlacementGrid.GetTextureFootprintScale(
            building,
            textureWidth: TileConstants.TileSize * 2,
            textureHeight: TileConstants.TileSize * 3,
            cameraScale: 0.5f);

        Assert.Equal(0.5f, scale.X);
        Assert.Equal(0.5f, scale.Y);
    }

    private static CameraController CreateCamera()
    {
        var camera = new CameraController { CurrentScale = 1f };
        camera.SetViewport(1000, 800);
        return camera;
    }

    private static Point ToCursor(CameraController camera, Vector2 world)
    {
        var screen = camera.WorldToScreen(world);
        return new Point((int)MathF.Round(screen.X), (int)MathF.Round(screen.Y));
    }
}
