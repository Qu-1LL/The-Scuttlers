using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class WorldSceneRendererTests
{
    [Fact]
    public void NormalizeParallaxOffset_WrapsToNearestTilePeriod()
    {
        Assert.Equal(10f, WorldSceneRenderer.NormalizeParallaxOffset(110f, 100f));
        Assert.Equal(-10f, WorldSceneRenderer.NormalizeParallaxOffset(-110f, 100f));
    }

    [Fact]
    public void NormalizeParallaxOffset_ReturnsZeroForInvalidPeriod()
    {
        Assert.Equal(0f, WorldSceneRenderer.NormalizeParallaxOffset(42f, 0f));
        Assert.Equal(0f, WorldSceneRenderer.NormalizeParallaxOffset(42f, -4f));
    }

    [Fact]
    public void CalculateParallaxOffset_UsesScreenOffsetWithoutCameraScale()
    {
        var parallaxScreenOffset = new Vector2(200f, -80f);

        var offset = WorldSceneRenderer.CalculateParallaxOffset(parallaxScreenOffset, periodWidth: 1000f, periodHeight: 1000f);

        Assert.Equal(200f * GameConstants.CaveBackgroundParallaxFactor, offset.X);
        Assert.Equal(-80f * GameConstants.CaveBackgroundParallaxFactor, offset.Y);
    }

    [Fact]
    public void GetWorldSpritePhaseOffsetSeconds_OnlyOffsetsLumenite()
    {
        var coordinates = new GridPoint(7, -3);

        var lumeniteOffset = WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds(OreType.LUMENITE.Name, coordinates);
        var chitinstoneOffset = WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds(OreType.CHITINSTONE.Name, coordinates);

        Assert.InRange(lumeniteOffset, 0f, 0.999f);
        Assert.Equal(0f, chitinstoneOffset);
    }

    [Fact]
    public void GetTileDrawColor_AppliesLumeniteAlphaPulseOverTime()
    {
        var tile = new Tile(0, "0,0");
        tile.SetBase(OreType.LUMENITE.Name);
        tile.ConfigureOre(GameConstants.MaxOreYield, 1);
        var spriteEffects = new WorldSpriteEffectSystem();
        spriteEffects.RegisterAlphaPulse(OreType.LUMENITE.Name, new AlphaPulseEffect(0.38f, 1f, 2.1f));

        var initial = WorldSceneRenderer.GetTileDrawColor(spriteEffects, tile, tile.Base, tile.Coordinates);
        for (var index = 0; index < 5; index++)
        {
            spriteEffects.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(100)));
        }

        var pulsed = WorldSceneRenderer.GetTileDrawColor(spriteEffects, tile, tile.Base, tile.Coordinates);

        Assert.True(initial.A < pulsed.A, $"Expected lumenite alpha to pulse. Initial: {initial.A}, pulsed: {pulsed.A}.");
    }

    [Theory]
    [InlineData("Sandstone")]
    [InlineData("Magnetite")]
    [InlineData("Malachite")]
    [InlineData("Perotene")]
    [InlineData("Ilmenite")]
    [InlineData("Cochinium")]
    [InlineData("Lumenite")]
    [InlineData("Chitinstone")]
    [InlineData("Mycocore")]
    public void GetTileOverlayTextureKey_ReturnsTileBaseForOreDeposits(string oreName)
    {
        var tile = new Tile(0, "0,0");
        tile.SetBase(oreName);
        tile.ConfigureOre(2, 1);

        Assert.Equal(oreName, WorldSceneRenderer.GetTileOverlayTextureKey(tile));
    }

    [Fact]
    public void GetTileOverlayTextureKey_UsesDedicatedCrystalTextureKey()
    {
        var tile = new Tile(0, "0,0");
        tile.SetBase(Tile.CaveCrystalBase);
        tile.ConfigureCaveCrystal(2);

        Assert.Equal(Tile.CaveCrystalBase, WorldSceneRenderer.GetTileOverlayTextureKey(tile));
    }

    [Fact]
    public void ShouldDrawFloorTile_RequiresFloorCoverWhenTileHasNoBuilding()
    {
        var tile = new Tile(0, "0,0");
        Assert.True(WorldSceneRenderer.ShouldDrawFloorTile(tile));

        tile.SetFloorCover(false);
        Assert.False(WorldSceneRenderer.ShouldDrawFloorTile(tile));
    }

    [Fact]
    public void GetTileOverlayRotationRadians_UsesGeneratedOreQuarterTurns()
    {
        var tile = new Tile(0, "0,0");
        tile.SetBase(OreType.CHITINSTONE.Name);
        tile.ConfigureOre(2, 1);
        tile.SetOreRotationQuarterTurns(3);

        Assert.Equal(MathF.PI * 1.5f, WorldSceneRenderer.GetTileOverlayRotationRadians(tile));

        tile.ClearResourceState();
        Assert.Equal(0f, WorldSceneRenderer.GetTileOverlayRotationRadians(tile));
    }

    [Fact]
    public void EnumerateVisibleTiles_UsesCameraFootprintAndRevealState()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var visibleTile = cave.AddTile(GridPoint.Zero.ToString());
        var hiddenVisibleTile = cave.AddTile(new GridPoint(1, 0).ToString());
        var offscreenTile = cave.AddTile(new GridPoint(8, 0).ToString());
        cave.RevealedTiles.Add(visibleTile);
        cave.RevealedTiles.Add(offscreenTile);

        var camera = new CameraController();
        camera.SetViewport(128, 128);

        var visibleKeys = WorldSceneRenderer
            .EnumerateVisibleTiles(cave, camera, new Point(128, 128), showFullMapVisibility: false)
            .Select(tile => tile.Key)
            .ToArray();

        Assert.Contains(visibleTile.Key, visibleKeys);
        Assert.DoesNotContain(hiddenVisibleTile.Key, visibleKeys);
        Assert.DoesNotContain(offscreenTile.Key, visibleKeys);
    }

    [Fact]
    public void EnumerateVisibleTiles_IncludesHiddenTilesWhenFullMapVisibilityIsEnabled()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var hiddenTile = cave.AddTile(GridPoint.Zero.ToString());

        var camera = new CameraController();
        camera.SetViewport(128, 128);

        var visibleKeys = WorldSceneRenderer
            .EnumerateVisibleTiles(cave, camera, new Point(128, 128), showFullMapVisibility: true)
            .Select(tile => tile.Key)
            .ToArray();

        Assert.Contains(hiddenTile.Key, visibleKeys);
    }

    [Fact]
    public void ShouldRenderTile_RespectsRevealStateUnlessFullMapVisibilityIsEnabled()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var tile = cave.AddTile(GridPoint.Zero.ToString());

        Assert.False(WorldSceneRenderer.ShouldRenderTile(cave, tile, showFullMapVisibility: false));
        Assert.True(WorldSceneRenderer.ShouldRenderTile(cave, tile, showFullMapVisibility: true));

        cave.RevealedTiles.Add(tile);

        Assert.True(WorldSceneRenderer.ShouldRenderTile(cave, tile, showFullMapVisibility: false));
    }
}
