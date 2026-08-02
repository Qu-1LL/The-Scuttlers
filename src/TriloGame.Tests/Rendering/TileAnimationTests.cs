using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class TileAnimationTests
{
    private static WorldSpriteEffectSystem CreateWaterAnimation(float frameSeconds = 0.5f)
    {
        var effects = new WorldSpriteEffectSystem();
        effects.RegisterTileAnimation(
            WorldSceneRenderer.WaterAnimationKey,
            new TileAnimationEffect(["Water1", "Water2", "Water3"], frameSeconds));
        return effects;
    }

    private static void Advance(WorldSpriteEffectSystem effects, float seconds)
    {
        // Update clamps each step to 0.1s, so feed it in small slices like the real game loop.
        var remaining = seconds;
        while (remaining > 0f)
        {
            var step = MathF.Min(0.05f, remaining);
            effects.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(step)));
            remaining -= step;
        }
    }

    // Advances land mid-frame rather than exactly on a boundary: accumulating float deltas cannot
    // hit a boundary precisely, and neither can real frame timing.
    [Fact]
    public void GetAnimatedTextureKey_CyclesThroughFramesInOrder()
    {
        var effects = CreateWaterAnimation(frameSeconds: 0.5f);

        Assert.Equal("Water1", effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey));
        Advance(effects, 0.6f);
        Assert.Equal("Water2", effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey));
        Advance(effects, 0.5f);
        Assert.Equal("Water3", effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey));
    }

    [Fact]
    public void GetAnimatedTextureKey_LoopsBackToTheFirstFrame()
    {
        var effects = CreateWaterAnimation(frameSeconds: 0.5f);
        Advance(effects, 1.6f);

        Assert.Equal("Water1", effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey));
    }

    [Fact]
    public void GetAnimatedTextureKey_PhaseOffsetDesynchronisesNeighbouringTiles()
    {
        var effects = CreateWaterAnimation(frameSeconds: 0.5f);

        var atOrigin = effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey);
        var offsetByOneFrame = effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey, 0.5f);

        // Without a per-tile offset an entire lake would flip frames in unison.
        Assert.NotEqual(atOrigin, offsetByOneFrame);
    }

    [Fact]
    public void GetAnimatedTextureKey_NegativePhaseOffsetStillResolvesToAValidFrame()
    {
        var effects = CreateWaterAnimation(frameSeconds: 0.5f);
        var frames = new[] { "Water1", "Water2", "Water3" };

        // A naive modulo would return a negative index here and throw.
        Assert.Contains(effects.GetAnimatedTextureKey(WorldSceneRenderer.WaterAnimationKey, -1.75f), frames);
    }

    [Fact]
    public void GetAnimatedTextureKey_UnregisteredAnimationFallsBackToTheKeyItself()
    {
        var effects = new WorldSpriteEffectSystem();

        // Degrades to "draw a texture with this name" rather than throwing or drawing nothing,
        // which is what keeps the game running before the water art is added.
        Assert.Equal("Water", effects.GetAnimatedTextureKey("Water"));
        Assert.False(effects.HasTileAnimation("Water"));
    }

    [Fact]
    public void RegisterTileAnimation_IgnoresEmptyFrameLists()
    {
        var effects = new WorldSpriteEffectSystem();
        effects.RegisterTileAnimation("Empty", new TileAnimationEffect([], 0.5f));

        Assert.False(effects.HasTileAnimation("Empty"));
    }

    [Fact]
    public void TileAnimation_DoesNotDisturbAlphaPulseEffects()
    {
        var effects = CreateWaterAnimation();
        effects.RegisterAlphaPulse("Lumenite", new AlphaPulseEffect(0.4f, 1f, 2f));
        Advance(effects, 0.6f);

        // The two effect kinds share the clock; registering one must not break the other.
        Assert.InRange(effects.GetAlphaMultiplier("Lumenite"), 0.4f, 1f);
        Assert.Equal(1f, effects.GetAlphaMultiplier("NotRegistered"));
    }

    [Fact]
    public void IsWater_TrueOnlyForUncoveredUnbuiltFloor()
    {
        var water = new Tile(0, "0,0");
        water.SetFloorCover(false);
        Assert.True(water.IsWater());

        var coveredFloor = new Tile(1, "1,0");
        Assert.False(coveredFloor.IsWater());

        // Solid rock is not water even though it is also impassable.
        var wall = new Tile(2, "2,0");
        wall.SetBase("wall");
        wall.SetFloorCover(false);
        Assert.False(wall.IsWater());
    }

    [Fact]
    public void Water_IsImpassableToCreatures()
    {
        var water = new Tile(0, "0,0");
        water.SetFloorCover(false);

        // Impassability comes from the existing floor-cover rule, so it applies to pathfinding
        // and placement without any water-specific special casing.
        Assert.False(water.CreatureFits());
        Assert.False(water.EnemyFits());
    }

    // The surface is drawn a tile past every edge of a pool and then covered by the floor, so the
    // reads that look sideways from a pixel - the refraction offset, the half-resolution mask -
    // cannot run off the end of it at the shoreline.
    [Fact]
    public void ShouldDrawWaterTile_ReachesOneTilePastThePoolInEveryDirection()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        for (var y = 0; y <= 4; y++)
        {
            for (var x = 0; x <= 4; x++)
            {
                cave.AddTile(new GridPoint(x, y).ToString());
            }
        }

        var water = cave.GetTile(new GridPoint(2, 2))!;
        water.SetFloorCover(false);

        Assert.True(WorldSceneRenderer.ShouldDrawWaterTile(cave, water));
        // Diagonals included: a corner of the hole is as much an edge as a side is.
        Assert.True(WorldSceneRenderer.ShouldDrawWaterTile(cave, cave.GetTile(new GridPoint(1, 1))!));
        Assert.True(WorldSceneRenderer.ShouldDrawWaterTile(cave, cave.GetTile(new GridPoint(3, 2))!));
        // Two tiles out is past the padding and stays dry.
        Assert.False(WorldSceneRenderer.ShouldDrawWaterTile(cave, cave.GetTile(new GridPoint(0, 2))!));
    }

    [Fact]
    public void CollectWaterSurfaceTiles_ReturnsOnlyTilesTheSurfaceCovers()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        for (var x = 0; x <= 3; x++)
        {
            cave.AddTile(new GridPoint(x, 0).ToString());
        }

        cave.GetTile(new GridPoint(0, 0))!.SetFloorCover(false);
        var visible = cave.GetTiles().ToList();
        var surface = new List<Tile>();

        WorldSceneRenderer.CollectWaterSurfaceTiles(cave, visible, surface);

        // The pool tile and its one neighbour along the row; the tile two out is excluded.
        Assert.Equal(2, surface.Count);
        Assert.DoesNotContain(surface, tile => tile.Coordinates.X > 1);
    }

    [Fact]
    public void Water_DoesNotBlockLightEvenThoughItBlocksMovement()
    {
        var water = new Tile(0, "0,0");
        water.SetFloorCover(false);

        // The occluder taxonomy is about light, not movement: water is flat, so it casts no shadow.
        Assert.Equal(
            TriloGame.Game.Rendering.Lighting.LightingOccluderHeight.Flat,
            TriloGame.Game.Rendering.Lighting.LightingTileClassifier.GetOccluderHeight(water));
        Assert.False(TriloGame.Game.Rendering.Lighting.LightingTileClassifier.BlocksLight(water));
    }
}
