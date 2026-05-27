using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Constants;
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
}
