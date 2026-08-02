using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Rendering.Lighting;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class RadianceCascadeLightingTests
{
    [Fact]
    public void CollectOreEmitters_CollectsEveryIntactOreType()
    {
        var tiles = OreType.GetOres()
            .Select((ore, index) => CreateOreTile(index, ore.Name))
            .ToArray();
        var emitters = new List<OreLightEmitter>();
        var collector = new LightingSourceCollector();

        var count = collector.CollectOreEmitters(tiles, new WorldSpriteEffectSystem(), emitters);

        Assert.Equal(OreType.GetOres().Count, count);
        Assert.Equal(OreType.GetOres().Select(ore => ore.Name), emitters.Select(emitter => emitter.OreName));
        Assert.All(emitters, emitter => Assert.Equal(OreLightSettings.OreIntensity, emitter.Intensity));
    }

    [Fact]
    public void CollectOreEmitters_ExcludesCrystalsAndDroppedResources()
    {
        var crystal = new Tile(0, "0,0");
        crystal.SetBase(Tile.CaveCrystalBase);
        crystal.ConfigureCaveCrystal(3);
        crystal.AddDroppedResource(OreType.SANDSTONE.Name, 4);
        var floor = new Tile(1, "1,0");
        floor.AddDroppedResource(OreType.LUMENITE.Name, 4);
        var emitters = new List<OreLightEmitter>();

        var count = new LightingSourceCollector().CollectOreEmitters(
            [crystal, floor],
            new WorldSpriteEffectSystem(),
            emitters);

        Assert.Equal(0, count);
        Assert.Empty(emitters);
    }

    [Fact]
    public void CollectOreEmitters_UsesLumenitePulseForIntensityOnly()
    {
        var tile = CreateOreTile(0, OreType.LUMENITE.Name);
        var effects = new WorldSpriteEffectSystem();
        effects.RegisterAlphaPulse(OreType.LUMENITE.Name, new AlphaPulseEffect(0.38f, 1f, 2.1f));
        var emitters = new List<OreLightEmitter>();

        new LightingSourceCollector().CollectOreEmitters([tile], effects, emitters);

        Assert.Single(emitters);
        Assert.Equal(new Vector2(0f, 0f), emitters[0].WorldPosition);
        Assert.InRange(emitters[0].Intensity, OreLightSettings.OreIntensity * OreLightSettings.LumeniteMinimumPulse, OreLightSettings.OreIntensity);
    }

    // The ray march reads emission straight off the tile grid now, instead of from the collector's
    // list, so that a deposit beyond the drawn area still lights what is on screen. The two have to
    // agree exactly: if they drift, light changes depending on which path happened to see a deposit.
    [Fact]
    public void LightingTileGrid_EmissionAgreesWithTheCollectorForEveryOre()
    {
        foreach (var ore in OreType.GetOres())
        {
            var tile = CreateOreTile(0, ore.Name);
            var effects = new WorldSpriteEffectSystem();
            effects.RegisterAlphaPulse(OreType.LUMENITE.Name, new AlphaPulseEffect(0.38f, 1f, 2.1f));
            var emitters = new List<OreLightEmitter>();
            new LightingSourceCollector().CollectOreEmitters([tile], effects, emitters);

            var strength = LightingTileGrid.GetEmissionStrength(tile, known: true, effects, palette: null, out _);

            Assert.Single(emitters);
            Assert.Equal(emitters[0].Intensity, strength, 5);
            Assert.True(strength > 0f, $"{ore.Name}: an intact ore tile must emit");
        }
    }

    // Unrevealed cells must stay dark, matching the collector, which only ever sees revealed tiles.
    [Fact]
    public void LightingTileGrid_EmitsNothingFromAnUnrevealedTile()
    {
        var tile = CreateOreTile(0, OreType.SANDSTONE.Name);
        var strength = LightingTileGrid.GetEmissionStrength(
            tile,
            known: false,
            new WorldSpriteEffectSystem(),
            palette: null,
            out _);

        Assert.Equal(0f, strength);
    }

    [Fact]
    public void CalculateLightSize_UsesHalfViewportDimensionsWithOddSizeRoundingUp()
    {
        Assert.Equal(new Point(720, 450), LightingRenderTargets.CalculateLightSize(new Point(1440, 900)));
        Assert.Equal(new Point(5, 4), LightingRenderTargets.CalculateLightSize(new Point(9, 7)));
    }

    // The property the snap exists for, and the one that matters most in play: panning must never
    // re-seat a probe on a new world position. If it does, each probe's rays start hitting different
    // tiles every frame and occlusion flickers. Holds at every zoom rung.
    [Fact]
    public void ProbeLattice_KeepsProbesOnTheSameWorldGridWhilePanning()
    {
        var viewportSize = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewportSize);

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewportSize, lightSize, scale);
            var reference = SnapLattice(new Vector2(1234.5f, -987.25f), scale, viewportSize, lightSize);

            foreach (var pan in new[] { 3f, 91.75f, -517.5f, 4096.25f })
            {
                var moved = SnapLattice(new Vector2(1234.5f + pan, -987.25f - pan), scale, viewportSize, lightSize);
                AssertWholeMultipleOfSpacing(moved - reference, spacing, $"zoom step {step}, pan {pan}");
            }
        }
    }

    // Screen shake is a screen-space offset divided by the camera scale on its way into world space,
    // so it slides the lattice continuously unless it is snapped along with everything else. It used
    // to sit outside the snapped quantity entirely.
    [Fact]
    public void ProbeLattice_KeepsScreenShakeOnTheSameWorldGrid()
    {
        var viewportSize = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewportSize);
        var scale = CameraController.GetScaleForZoomStep(1);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewportSize, lightSize, scale);

        var still = SnapLattice(new Vector2(1234.5f, -987.25f), scale, viewportSize, lightSize);
        var shaken = SnapLattice(new Vector2(1234.5f, -987.25f), scale, viewportSize, lightSize, new Vector2(11.5f, -7.25f));

        AssertWholeMultipleOfSpacing(shaken - still, spacing, "shake");
    }

    // Within a rung the grid is exact: the snapped origin is a whole multiple of the spacing whatever
    // the camera is doing, which is what makes panning stable. Across rungs the grids are shared or
    // nested rather than unrelated - see ZoomRungs_ShareWorldGridsRatherThanHavingOneEach.
    [Fact]
    public void ProbeLattice_OriginIsAWholeMultipleOfItsSpacingAtEveryZoom()
    {
        var viewportSize = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewportSize);

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewportSize, lightSize, scale);

            var snapped = SnapLattice(new Vector2(1234.5f, -987.25f), scale, viewportSize, lightSize);
            var stepsX = snapped.X / spacing.X;
            Assert.True(
                MathF.Abs(stepsX - MathF.Round(stepsX)) < 0.001f,
                $"zoom step {step}: origin is {stepsX} spacings from the world origin, not a whole number");
        }
    }

    // (GetHistoryUvOffset_ShiftsByWholeTexels was removed alongside the temporal accumulator. The
    // property it guarded - a field texel covering a fixed world distance - is now covered directly
    // by GetProbeWorldSpacing_IsOneCascadeZeroProbeAtTheReferenceZoom and by the pan sweep below,
    // neither of which depends on a history buffer existing.)

    [Fact]
    public void GetProbeWorldSpacing_IsOneCascadeZeroProbeAtTheReferenceZoom()
    {
        var viewportSize = new Point(1440, 900);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(
            viewportSize,
            LightingRenderTargets.CalculateLightSize(viewportSize),
            GameConstants.DefaultCameraScale);

        // Half-resolution light buffer, so one source pixel spans two viewport pixels.
        var expected = OreLightSettings.BaseProbeSpacing * 2f / GameConstants.DefaultCameraScale;
        Assert.Equal(expected, spacing.X, 3);
        Assert.Equal(expected, spacing.Y, 3);
    }

    // Panning must move the grid's ORIGIN and nothing else. A size derived from the gap between two
    // independently floored tile coordinates oscillates by one as the camera slides, and every
    // oscillation reallocates both grid textures in the middle of the pan.
    [Fact]
    public void LightingTileGrid_KeepsAConstantSizeWhilePanning()
    {
        var viewport = new Point(1440, 900);
        var camera = CreateCamera(viewport, GameConstants.DefaultCameraScale);
        var range = RangeTilesAt(viewport, GameConstants.DefaultCameraScale);
        var reference = LightingTileGridLayout.Create(camera, viewport, range);

        foreach (var offset in new[] { 1f, 37.5f, 211.25f, 512f, 999.75f, -4321.5f })
        {
            camera.SetOrigin(new Vector2(offset, offset * -0.5f));
            var moved = LightingTileGridLayout.Create(camera, viewport, range);
            Assert.Equal(reference.Size, moved.Size);
        }
    }

    // The light range at a given zoom, which is what the tile grid has to cover.
    private static float RangeTilesAt(Point viewport, float cameraScale)
    {
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, cameraScale);
        return RadianceCascadeRenderer.CalculateLightRangeTiles(layout, MathF.Max(spacing.X, spacing.Y));
    }

    // Outside the grid SampleTileCell reports an unlit solid blocker, so anything the march can
    // reach but the grid does not cover contributes nothing AND blocks. If that boundary sits inside
    // the ray's reach, panning slides it across the world and deposits switch between full light and
    // none purely because of where the camera points.
    [Fact]
    public void LightingTileGrid_CoversEverythingTheRayMarchCanReach()
    {
        var viewport = new Point(1440, 900);
        // The grid has to cover the RANGE - the capped reach. Light past the cap has already faded to
        // nothing, so covering it would cost cells for no light.
        var reachTiles = RangeTilesAt(viewport, GameConstants.DefaultCameraScale);

        var camera = CreateCamera(viewport, GameConstants.DefaultCameraScale);
        var grid = LightingTileGridLayout.Create(camera, viewport, reachTiles);
        camera.GetVisibleWorldBounds(viewport, out var topLeft, out var bottomRight);

        var visibleMinTileX = ToTileCoordinate(MathF.Min(topLeft.X, bottomRight.X));
        var visibleMinTileY = ToTileCoordinate(MathF.Min(topLeft.Y, bottomRight.Y));
        var visibleMaxTileX = ToTileCoordinate(MathF.Max(topLeft.X, bottomRight.X));
        var visibleMaxTileY = ToTileCoordinate(MathF.Max(topLeft.Y, bottomRight.Y));

        Assert.True(visibleMinTileX - grid.Origin.X >= reachTiles, "grid stops short of the reach on the left");
        Assert.True(visibleMinTileY - grid.Origin.Y >= reachTiles, "grid stops short of the reach on the top");
        Assert.True(grid.Origin.X + grid.Size.X - 1 - visibleMaxTileX >= reachTiles, "grid stops short on the right");
        Assert.True(grid.Origin.Y + grid.Size.Y - 1 - visibleMaxTileY >= reachTiles, "grid stops short on the bottom");
    }

    private static CameraController CreateCamera(Point viewport, float scale)
    {
        var camera = new CameraController();
        camera.SetViewport(viewport.X, viewport.Y);
        camera.CurrentScale = scale;
        camera.SetOrigin(Vector2.Zero);
        return camera;
    }

    private static int ToTileCoordinate(float world)
    {
        return (int)MathF.Floor((world + TileConstants.TileHalfSize) / TileConstants.TileSize);
    }

    // Mirrors UpdateProbeLattice for cascade 0: anchor at the view centre, step back half the lattice
    // and half a probe, then floor onto the spacing.
    private static Vector2 SnapLattice(
        Vector2 cameraOrigin,
        float cameraScale,
        Point viewportSize,
        Point lightSize,
        Vector2 shakeOffset = default)
    {
        var layout = LightingCascadeLayout.Create(lightSize);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewportSize, lightSize, cameraScale);
        var count = layout.GetProbeCount(0);
        var latticeSpan = new Vector2(count.X * spacing.X, count.Y * spacing.Y);
        var anchor = RadianceCascadeRenderer.GetLatticeAnchorWorld(cameraOrigin, shakeOffset, cameraScale);

        return RadianceCascadeRenderer.SnapToLattice(anchor - (latticeSpan * 0.5f), spacing);
    }

    private static void AssertWholeMultipleOfSpacing(Vector2 delta, Vector2 spacing, string because)
    {
        var stepsX = delta.X / spacing.X;
        var stepsY = delta.Y / spacing.Y;
        Assert.True(MathF.Abs(stepsX - MathF.Round(stepsX)) < 0.001f, $"{because}: x moved {stepsX} probes");
        Assert.True(MathF.Abs(stepsY - MathF.Round(stepsY)) < 0.001f, $"{because}: y moved {stepsY} probes");
    }

    [Fact]
    public void CollectOreEmitters_PreservesWorldTileCoordinatesForCameraProjection()
    {
        var tile = CreateOreTile(0, OreType.SANDSTONE.Name, new GridPoint(3, -2));
        var emitters = new List<OreLightEmitter>();
        new LightingSourceCollector().CollectOreEmitters([tile], new WorldSpriteEffectSystem(), emitters);
        var camera = new CameraController();
        camera.SetViewport(800, 600);
        camera.SetOrigin(new Vector2(100f, -200f));

        var screenPosition = camera.WorldToScreen(emitters[0].WorldPosition);

        Assert.Equal(new Vector2(400f, 300f) + (emitters[0].WorldPosition - new Vector2(100f, -200f)) * camera.CurrentScale, screenPosition);
    }

    [Fact]
    public void IsLightBlockingTile_BlocksSolidTilesButNotOpenFloor()
    {
        var floor = new Tile(0, "0,0");
        var wall = new Tile(1, "1,0");
        wall.SetBase("wall");
        var ore = CreateOreTile(2, OreType.SANDSTONE.Name);
        var crystal = new Tile(3, "3,0");
        crystal.SetBase(Tile.CaveCrystalBase);
        crystal.ConfigureCaveCrystal(2);
        var wallBuildingTile = new Tile(4, "4,0");
        wallBuildingTile.SetBuilt(new Wall(new GameSession()));

        Assert.False(WorldSceneRenderer.IsLightBlockingTile(floor));
        Assert.True(WorldSceneRenderer.IsLightBlockingTile(wall));
        Assert.True(WorldSceneRenderer.IsLightBlockingTile(ore));
        Assert.True(WorldSceneRenderer.IsLightBlockingTile(crystal));
        Assert.True(WorldSceneRenderer.IsLightBlockingTile(wallBuildingTile));
    }

    [Fact]
    public void GetOccluderHeight_ClassifiesTerrainByHeight()
    {
        var floor = new Tile(0, "0,0");

        var wall = new Tile(1, "1,0");
        wall.SetBase("wall");

        var ore = CreateOreTile(2, OreType.SANDSTONE.Name);

        var crystal = new Tile(3, "3,0");
        crystal.SetBase(Tile.CaveCrystalBase);
        crystal.ConfigureCaveCrystal(2);

        Assert.Equal(LightingOccluderHeight.Flat, LightingTileClassifier.GetOccluderHeight(floor));
        Assert.Equal(LightingOccluderHeight.Tall, LightingTileClassifier.GetOccluderHeight(wall));
        // Solid rock the colony cannot walk through: full height, so it shadows like a wall.
        Assert.Equal(LightingOccluderHeight.Impassable, LightingTileClassifier.GetOccluderHeight(ore));
        Assert.Equal(LightingOccluderHeight.Impassable, LightingTileClassifier.GetOccluderHeight(crystal));
    }

    [Fact]
    public void GetOccluderHeight_TreatsPassableBuildingsAsFlatAndTallOnesAsTall()
    {
        var session = new GameSession();

        var wallTile = new Tile(0, "0,0");
        wallTile.SetBuilt(new Wall(session));
        var radarTile = new Tile(1, "1,0");
        radarTile.SetBuilt(new Radar(session));
        var farmTile = new Tile(2, "2,0");
        farmTile.SetBuilt(new AlgaeFarm(session));
        var storageTile = new Tile(3, "3,0");
        storageTile.SetBuilt(new Storage(session));

        Assert.Equal(LightingOccluderHeight.Tall, LightingTileClassifier.GetOccluderHeight(wallTile));
        Assert.Equal(LightingOccluderHeight.Tall, LightingTileClassifier.GetOccluderHeight(radarTile));
        Assert.Equal(LightingOccluderHeight.Flat, LightingTileClassifier.GetOccluderHeight(farmTile));
        Assert.Equal(LightingOccluderHeight.Flat, LightingTileClassifier.GetOccluderHeight(storageTile));
    }

    [Fact]
    public void OccluderHeight_OnlyFullHeightCastersBlockAtAnyDistance()
    {
        Assert.False(LightingOccluderHeight.Flat.IsFullHeight());
        // Short casters must not block at arbitrary distance - that is what keeps their shadow short.
        Assert.False(LightingOccluderHeight.Short.IsFullHeight());
        Assert.True(LightingOccluderHeight.Impassable.IsFullHeight());
        Assert.True(LightingOccluderHeight.Tall.IsFullHeight());

        Assert.False(LightingOccluderHeight.Flat.CastsShadow());
        Assert.True(LightingOccluderHeight.Short.CastsShadow());
        Assert.True(LightingOccluderHeight.Tall.CastsShadow());
    }

    [Fact]
    public void BlocksLightAtAnyDistance_ExcludesFlatTilesSoTheOffScreenFallbackStaysCorrect()
    {
        var session = new GameSession();
        var floor = new Tile(0, "0,0");
        var farmTile = new Tile(1, "1,0");
        farmTile.SetBuilt(new AlgaeFarm(session));
        var wall = new Tile(2, "2,0");
        wall.SetBase("wall");

        Assert.False(LightingTileClassifier.BlocksLightAtAnyDistance(floor));
        Assert.False(LightingTileClassifier.BlocksLightAtAnyDistance(farmTile));
        Assert.True(LightingTileClassifier.BlocksLightAtAnyDistance(wall));
    }

    // Only Wall and Radar are solid enough to stop light. Treating every finished building as a
    // light barrier turned the colony into a maze of hard shadows.
    [Fact]
    public void LightingTileClassifier_OnlyWallsAndRadarsOccludeLight()
    {
        var session = new GameSession();

        Assert.True(LightingTileClassifier.IsBuildingOccluder(new Wall(session)));
        Assert.True(LightingTileClassifier.IsBuildingOccluder(new Radar(session)));

        Assert.False(LightingTileClassifier.IsBuildingOccluder(new Storage(session)));
        Assert.False(LightingTileClassifier.IsBuildingOccluder(new AlgaeFarm(session)));
        Assert.False(LightingTileClassifier.IsBuildingOccluder(new Barracks(session)));
        Assert.False(LightingTileClassifier.IsBuildingOccluder(new MiningPost(session)));
        Assert.False(LightingTileClassifier.IsBuildingOccluder(new Turret(session)));
    }

    [Fact]
    public void LightingTileClassifier_ScaffoldingAndPassableBuildingsDoNotBlockLight()
    {
        var session = new GameSession();
        var storage = new Storage(session);
        var scaffold = new Scaffolding(session, storage);

        var storageTile = new Tile(0, "0,0");
        storageTile.SetBuilt(storage);
        var scaffoldTile = new Tile(1, "1,0");
        scaffoldTile.SetBuilt(scaffold);
        var wallTile = new Tile(2, "2,0");
        wallTile.SetBuilt(new Wall(session));

        Assert.False(LightingTileClassifier.BlocksLight(storageTile));
        Assert.False(LightingTileClassifier.BlocksLight(scaffoldTile));
        Assert.True(LightingTileClassifier.BlocksLight(wallTile));
    }

    // R is an OPACITY now, not a blocker flag. Open floor passes light untouched; unknown and
    // off-map cells stay fully opaque so light cannot leak in from unexplored space.
    [Fact]
    public void LightingTileGrid_EncodesKnownUnknownAndOutOfMapCells()
    {
        var empty = new Tile(0, "0,0");

        var known = LightingTileGrid.EncodeCell(empty, known: true);
        var unknown = LightingTileGrid.EncodeCell(empty, known: false);
        var outOfMap = LightingTileGrid.EncodeCell(null, known: false);

        Assert.Equal(0, known.R);
        Assert.Equal(255, known.G);
        Assert.Equal(0, unknown.G);
        Assert.Equal(255, unknown.R);
        Assert.Equal(255, outOfMap.R);
    }

    [Fact]
    public void LightingTileGrid_EncodesEmissionWithoutClearingOpacity()
    {
        var ore = CreateOreTile(0, OreType.SANDSTONE.Name);
        var encoded = LightingTileGrid.EncodeCell(ore, known: true, emission: 0.75f);

        // An intact deposit is Impassable rock, so it is nearly - but not fully - opaque. Asserted
        // within a byte because the channel quantises a float constant to 8 bits.
        var expectedOpacity = (1f - OreLightSettings.RockTransmission) * 255f;
        Assert.InRange(encoded.R, (int)expectedOpacity - 1, (int)expectedOpacity + 1);
        Assert.Equal(255, encoded.G);
        Assert.InRange(encoded.B, 190, 195);
    }

    // The split the two classes exist for: a built wall and solid rock block at the same distances,
    // but they are not the same material and must not attenuate identically. Before this they shared
    // one constant, so the enum described a distinction the renderer ignored.
    [Fact]
    public void WallsPassMoreLightThanSolidRock()
    {
        Assert.True(
            OreLightSettings.WallTransmission > OreLightSettings.RockTransmission,
            "a built wall should pass more light than metres of bedrock");

        var session = new GameSession();
        var wallTile = new Tile(0, "0,0");
        wallTile.SetBuilt(new Wall(session));
        var rock = CreateOreTile(1, OreType.SANDSTONE.Name);

        var wall = LightingTileGrid.EncodeCell(wallTile, known: true);
        var stone = LightingTileGrid.EncodeCell(rock, known: true);

        Assert.True(
            wall.R < stone.R,
            $"the wall cell should be the less opaque of the two, got wall {wall.R} vs rock {stone.R}");

        // Both must still stop most of the light, or they stop reading as barriers at all.
        Assert.True(wall.R > 128, "a wall must still block the majority of the light reaching it");
    }

    // Coverage is what stops a round sprite on a square footprint occluding as a rectangle.
    [Fact]
    public void BuildingOccluderCoverage_FollowsTheSpriteRatherThanTheFootprint()
    {
        // A 4x4 texture with only its two middle columns opaque, measured over a 4x4 footprint.
        var pixels = new Color[16];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                pixels[(y * 4) + x] = x is 1 or 2 ? Color.White : Color.Transparent;
            }
        }

        var coverage = BuildingOccluderCoverage.MeasureCoverage(pixels, 4, 4, 4, 4);

        for (var y = 0; y < 4; y++)
        {
            Assert.Equal(0f, coverage[(y * 4) + 0], 2);
            Assert.Equal(1f, coverage[(y * 4) + 1], 2);
            Assert.Equal(1f, coverage[(y * 4) + 2], 2);
            Assert.Equal(0f, coverage[(y * 4) + 3], 2);
        }
    }

    // The bug coverage weighting shipped with the first time: EncodeCell resolves a tile's height
    // through Tile.Built, so the terrain pass writes an occluding building's whole rectangular
    // footprint before the coverage pass runs. Combining the two with a maximum let that rectangle
    // win every cell, and the radar's shadow stayed exactly as square as it had been.
    //
    // Reading the TERRAIN height instead is what makes the coverage reachable, so assert the terrain
    // view genuinely ignores what is built on it.
    [Fact]
    public void TerrainOccluderHeight_IgnoresBuildingsSoCoverageCanApply()
    {
        var session = new GameSession();
        var floorUnderRadar = new Tile(0, "0,0");
        floorUnderRadar.SetBuilt(new Radar(session));

        // The tile still blocks as far as the classifier is concerned...
        Assert.Equal(LightingOccluderHeight.Tall, LightingTileClassifier.GetOccluderHeight(floorUnderRadar));
        // ...but the terrain beneath it is bare floor, so the grid leaves the occlusion to the
        // coverage-weighted pass instead of stamping a rectangle.
        Assert.Equal(
            LightingOccluderHeight.Flat,
            LightingTileClassifier.GetTerrainOccluderHeight(floorUnderRadar));
        Assert.Equal(1f, LightingTileClassifier.GetTerrainOccluderHeight(floorUnderRadar).GetLightTransmission(), 3);

        // Real rock still reports itself, so a wall built on rock cannot become a window.
        var rock = CreateOreTile(1, OreType.SANDSTONE.Name);
        rock.SetBuilt(new Wall(session));
        Assert.Equal(LightingOccluderHeight.Impassable, LightingTileClassifier.GetTerrainOccluderHeight(rock));
    }

    // The radar's own art, measured the way the renderer measures it. If this ever comes back uniform
    // the coverage pass is a no-op again and the square is back, so pin the shape rather than just
    // the mechanism.
    [Fact]
    public void RadarFootprintCoverage_IsNotUniform()
    {
        // A stand-in for the dish: an opaque disc on a transparent 4x4-tile sheet. The radius is
        // chosen so the two cell classes being asserted are unambiguous - large enough that the
        // middle cell lies wholly inside it (its far corner is 21.9px out), small enough that the
        // corner cell lies wholly outside (its nearest point is 23.3px out). A disc inscribed in the
        // full square would not separate them: its corner cells still overlap it heavily.
        const int size = 64;
        const float radius = 22.5f;
        var pixels = new Color[size * size];
        var centre = (size - 1) / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - centre;
                var dy = y - centre;
                pixels[(y * size) + x] = (dx * dx) + (dy * dy) <= radius * radius
                    ? Color.White
                    : Color.Transparent;
            }
        }

        var coverage = BuildingOccluderCoverage.MeasureCoverage(pixels, size, size, 4, 4);

        // The centre cells are solid and the corners are nearly empty - which is the whole point.
        var centreCell = coverage[(1 * 4) + 1];
        var cornerCell = coverage[0];
        Assert.True(centreCell > 0.95f, $"the middle of a disc should be solid, got {centreCell}");
        Assert.True(cornerCell < 0.05f, $"a corner outside a disc should be clear, got {cornerCell}");
    }

    // Rotating a building rotates the footprint it occupies, so the lookup has to be un-rotated
    // before it indexes a measurement taken in the texture's own orientation. Getting this wrong
    // mirrors the occlusion against the sprite rather than failing outright.
    [Theory]
    [InlineData(0, 1, 0, 1, 0)]
    [InlineData(1, 1, 0, 0, 2)]
    [InlineData(2, 1, 0, 2, 3)]
    [InlineData(3, 1, 0, 3, 1)]
    public void BuildingOccluderCoverage_UnrotatesTheLookup(
        int turns, int x, int y, int expectedX, int expectedY)
    {
        var natural = BuildingOccluderCoverage.ToNaturalCell(x, y, new Point(4, 4), turns);

        Assert.Equal(expectedX, natural.X);
        Assert.Equal(expectedY, natural.Y);
    }

    [Fact]
    public void LightingTileGridLayout_TracksCameraFootprintAndZoom()
    {
        var camera = new CameraController();
        camera.SetViewport(801, 601);
        camera.SetOrigin(Vector2.Zero);

        var wide = LightingTileGridLayout.Create(camera, new Point(801, 601), 28f);
        camera.CurrentScale *= 2f;
        var zoomed = LightingTileGridLayout.Create(camera, new Point(801, 601), 28f);

        Assert.True(wide.Contains(new Point(0, 0)));
        Assert.True(zoomed.Contains(new Point(0, 0)));
        Assert.True(zoomed.Width < wide.Width);
        Assert.True(zoomed.Height < wide.Height);
    }

    [Fact]
    public void LightingTileClassifier_DepletedOreIsNoLongerAnEmitterOrBlockerAfterTileClears()
    {
        var tile = CreateOreTile(0, OreType.SANDSTONE.Name);

        Assert.True(LightingTileClassifier.EmitsLight(tile));
        Assert.True(LightingTileClassifier.BlocksLight(tile));

        tile.SetBase("empty");
        tile.ClearResourceState();

        Assert.False(LightingTileClassifier.EmitsLight(tile));
        Assert.False(LightingTileClassifier.BlocksLight(tile));
    }

    [Fact]
    public void LightingCascadeLayout_UsesPackedPowerOfTwoAlignment()
    {
        var layout = LightingCascadeLayout.Create(new Point(720, 450));

        // Four levels now - see OreLightSettings.MaxCascadeCount for why the hierarchy was made
        // shallower, and note the top cascade gains probes as a result (13x9 rather than 7x5), which
        // is what makes covering its merge look-ahead affordable.
        Assert.Equal(4, layout.CascadeCount);
        // 720x450 rounded up to the 64px alignment WITH a full alignment block of slack for the
        // one-sided snap - see PackedSize_LeavesRoomForTheLatticeSnapOffset.
        Assert.Equal(new Point(832, 576), layout.PackedSize);
        Assert.Equal(new Point(104, 72), layout.LightingFieldSize);
        Assert.Equal(new Point(104, 72), layout.GetProbeCount(0));
        Assert.Equal(new Point(13, 9), layout.GetProbeCount(layout.CascadeCount - 1));
        Assert.Equal(8, layout.GetRayDimension(0));
        Assert.Equal(64, layout.GetRayDimension(layout.CascadeCount - 1));
    }

    [Fact]
    public void LightingCascadeLayout_UsesGeometricIntervalGrowth()
    {
        var layout = LightingCascadeLayout.Create(new Point(320, 240));
        // A spacing fine enough that the authored floor is what sets the base.
        const float fine = 1f;
        var b = LightingCascadeLayout.GetBaseIntervalWorld(layout.CascadeCount, fine);

        Assert.Equal(b, layout.GetIntervalWorldLength(0, fine), 2);
        Assert.Equal(b * 4f, layout.GetIntervalWorldLength(1, fine), 2);
        Assert.Equal(b * 16f, layout.GetIntervalWorldLength(2, fine), 2);
        Assert.Equal(0f, layout.GetIntervalWorldOrigin(0, fine), 2);
        Assert.Equal(b, layout.GetIntervalWorldOrigin(1, fine), 2);
        Assert.Equal(b * 5f, layout.GetIntervalWorldOrigin(2, fine), 2);
    }

    // While the authored floor is in charge, the far end of the top cascade must come back to exactly
    // the reach that was asked for - at every cascade count, since the base absorbs the difference.
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void LightingCascadeLayout_TotalReachMatchesTheAuthoredWorldReach(int cascadeCount)
    {
        var b = LightingCascadeLayout.GetBaseIntervalWorld(cascadeCount, 1f);
        var total = b * (MathF.Pow(4f, cascadeCount) - 1f) / 3f;

        Assert.Equal(OreLightSettings.LightReachTiles * TileConstants.TileSize, total, 1);
    }

    // The requirement the whole inverted fix exists for: probe spacing must never exceed cascade 0's
    // ray interval, at any zoom rung. A probe gathers light only along its own interval, so once its
    // neighbours are further away than that interval is long, light between probes is gathered by
    // nobody. Measured at the widest rung before this held, the scene lost 77% of its light.
    [Fact]
    public void ProbeSpacing_NeverExceedsCascadeZeroInterval()
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);
            var basis = MathF.Max(spacing.X, spacing.Y);

            for (var cascade = 0; cascade < layout.CascadeCount; cascade++)
            {
                var cascadeSpacing = RadianceCascadeRenderer.GetCascadeProbeWorldSpacing(spacing, cascade);
                var interval = layout.GetIntervalWorldLength(cascade, basis);

                Assert.True(
                    MathF.Max(cascadeSpacing.X, cascadeSpacing.Y) <= interval + 0.001f,
                    $"zoom {step} cascade {cascade}: spacing {cascadeSpacing.X} exceeds its interval {interval} - " +
                    "light between probes is gathered by nobody");
            }
        }
    }

    // And the floor must still hold while zoomed in, or the original "light range shrinks as you zoom
    // in" bug comes straight back.
    [Fact]
    public void LightRange_HoldsAtTheAuthoredReachWhileZoomedIn()
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);

        for (var step = 0; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);
            var reach = layout.GetTotalReachWorld(MathF.Max(spacing.X, spacing.Y)) / TileConstants.TileSize;

            Assert.Equal(OreLightSettings.LightReachTiles, reach, 1);
        }

        // Zooming out, the reach grows rather than the hierarchy breaking.
        var wide = RadianceCascadeRenderer.GetProbeWorldSpacing(
            viewport, lightSize, CameraController.GetScaleForZoomStep(-GameConstants.MaxZoomSteps));
        var wideReach = layout.GetTotalReachWorld(MathF.Max(wide.X, wide.Y)) / TileConstants.TileSize;
        Assert.True(
            wideReach > OreLightSettings.LightReachTiles,
            $"expected the reach to widen when zoomed out, got {wideReach}");
    }

    // (TotalReach_IsTheSameNumberOfTilesAtEveryZoom was removed: the reach is deliberately NOT the
    // same at every rung any more. It holds at the authored value while zoomed in and widens with the
    // probe spacing when zoomed out, which is what keeps the hierarchy valid there. The half that
    // still matters - that it holds while zoomed in - is asserted by
    // LightRange_HoldsAtTheAuthoredReachWhileZoomedIn above.)
    //
    // (CascadeMergeLookAhead_ExceedsTheLatticeOverhangWhenZoomedIn was removed with the quantity it
    // measured. The merge samples the higher cascade at the probe's own position now, so there is no
    // look-ahead to outrun the lattice.)

    // OreLightRangeTiles is a CAP, not the working range. While zoomed in the hierarchy's own reach is
    // the binding limit and the cap never engages - a falloff tuned to the cap alone would be
    // unreachable, and light would run at full strength into the hierarchy's hard edge. Zoomed out,
    // where the intervals widen, the cap DOES bind, and that is what bounds the cost of the widening.
    [Fact]
    public void OreLightRangeTiles_BindsOnlyWhenZoomedOut()
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);

        var atDefault = RadianceCascadeRenderer.GetProbeWorldSpacing(
            viewport, lightSize, GameConstants.DefaultCameraScale);
        var reachAtDefault = layout.GetTotalReachWorld(MathF.Max(atDefault.X, atDefault.Y)) / TileConstants.TileSize;
        Assert.True(
            reachAtDefault < OreLightSettings.OreLightRangeTiles,
            $"at the default zoom the reach ({reachAtDefault}) should be the binding limit, not the cap");
        Assert.Equal(
            reachAtDefault,
            RadianceCascadeRenderer.CalculateLightRangeTiles(layout, MathF.Max(atDefault.X, atDefault.Y)),
            3);

        var wide = RadianceCascadeRenderer.GetProbeWorldSpacing(
            viewport, lightSize, CameraController.GetScaleForZoomStep(-GameConstants.MaxZoomSteps));
        var basis = MathF.Max(wide.X, wide.Y);
        Assert.True(
            layout.GetTotalReachWorld(basis) / TileConstants.TileSize > OreLightSettings.OreLightRangeTiles,
            "zoomed fully out the widened reach should exceed the cap");
        Assert.Equal(
            OreLightSettings.OreLightRangeTiles,
            RadianceCascadeRenderer.CalculateLightRangeTiles(layout, basis),
            3);
    }

    // Two properties the merge's edge handling must have together, because getting one without the
    // other has now failed in both directions:
    //
    //  - the probe pair it samples must be inside the real lattice, or SampleHigherProbe builds a
    //    packed UV outside 0..1 and the sampler answers with a different RAY of a different probe;
    //  - a position outside the lattice must contribute NOTHING. Clamping alone substitutes the edge
    //    probe's real radiance, which is not small, and injects a large spurious far-field term into
    //    every outward-facing ray past the edge.
    [Theory]
    [InlineData(-50f)]
    [InlineData(-1f)]
    [InlineData(-0.4f)]
    [InlineData(0f)]
    [InlineData(3.7f)]
    [InlineData(999f)]
    public void CascadeMerge_StaysInRangeAndFadesOutBeyondTheLattice(float coordinate)
    {
        foreach (var probeCount in new[] { 2f, 5f, 9f, 144f })
        {
            // Mirrors CascadeMergePixel.
            var outside = MathF.Max(MathF.Max(-coordinate, coordinate - (probeCount - 1f)), 0f);
            var edgeFade = Math.Clamp(1f - outside, 0f, 1f);
            var maxBase = MathF.Max(0f, probeCount - 2f);
            var clamped = Math.Clamp(coordinate, 0f, probeCount - 1f);
            var probeBase = MathF.Min(MathF.Floor(clamped), maxBase);
            var interpolation = Math.Clamp(clamped - probeBase, 0f, 1f);

            Assert.InRange(probeBase, 0f, probeCount - 2f);
            Assert.InRange(probeBase + 1f, 0f, probeCount - 1f);
            Assert.InRange(interpolation, 0f, 1f);

            // A full probe or more outside contributes nothing at all.
            if (coordinate < -1f || coordinate > probeCount)
            {
                Assert.Equal(0f, edgeFade);
            }

            // And inside the lattice nothing is faded away.
            if (coordinate >= 0f && coordinate <= probeCount - 1f)
            {
                Assert.Equal(1f, edgeFade);
            }
        }
    }

    // With the zoom factor gone, the cascade-0 lattice spans exactly PackedSize source pixels, which
    // the layout guarantees is at least the light buffer plus a full coarsest-cascade spacing. That
    // is now true at every rung with no clamp involved, because probe spacing is once again a fixed
    // number of screen pixels.
    [Fact]
    public void ProbeLattice_CoversTheLightBufferAtEveryZoomWithoutAZoomFactor()
    {
        var viewport = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);

            // World span of the cascade-0 lattice against the world span of the screen.
            var latticeWorld = layout.GetProbeCount(0).X * spacing.X;
            var screenWorld = viewport.X / scale;
            Assert.True(
                latticeWorld >= screenWorld,
                $"zoom step {step}: lattice spans {latticeWorld} world units, screen needs {screenWorld}");
        }
    }

    // The composite compresses LUMINANCE and carries chroma through, rather than compressing each
    // channel independently.
    //
    // Per-channel Reinhard desaturates as it compresses, because each channel approaches 1 on its own
    // and their ratios collapse. At LightGain 20 the shared ore amber (1.00 : 0.82 : 0.62) came out at
    // (1.00 : 0.98 : 0.95) - white - so colour read correctly in the raw LightingField view and washed
    // out in the composite.
    [Fact]
    public void TonemapRadiance_KeepsHueAndNeverExceedsOne()
    {
        var oreColour = OreLightSettings.SharedOreLightColor.ToVector3();

        foreach (var magnitude in new[] { 0.005f, 0.05f, 0.25f, 1f, 10f })
        {
            var radiance = oreColour * magnitude * OreLightSettings.LightGain;
            var mapped = Tonemap(radiance);

            // Must still fit the display range, or the core clips into a hard-edged disc.
            Assert.InRange(mapped.X, 0f, 1.001f);
            Assert.InRange(mapped.Y, 0f, 1.001f);
            Assert.InRange(mapped.Z, 0f, 1.001f);

            // And must never be further from the true hue than the per-channel curve it replaced.
            var kept = HueError(radiance, mapped);
            var perChannel = HueError(radiance, PerChannel(radiance));
            Assert.True(
                kept <= perChannel + 0.0001f,
                $"magnitude {magnitude}: hue error {kept} is worse than per-channel's {perChannel}");
        }

        // Below the point where a channel would overshoot, the hue is preserved exactly.
        var dim = oreColour * 0.05f * OreLightSettings.LightGain;
        Assert.Equal(0f, HueError(dim, Tonemap(dim)), 4);

        // No division-by-zero on unlit pixels.
        Assert.Equal(Vector3.Zero, Tonemap(Vector3.Zero));
    }

    // Mirrors TonemapRadiance in RadianceCascade.fx.
    private static Vector3 Tonemap(Vector3 radiance)
    {
        var luminance = (radiance.X * 0.2126f) + (radiance.Y * 0.7152f) + (radiance.Z * 0.0722f);
        var compressed = luminance / (1f + luminance);
        var tinted = radiance * (compressed / MathF.Max(luminance, 0.00001f));
        var peak = MathF.Max(tinted.X, MathF.Max(tinted.Y, tinted.Z));
        var desaturate = peak > 1f ? (peak - 1f) / MathF.Max(peak - compressed, 0.00001f) : 0f;
        return Vector3.Lerp(tinted, new Vector3(compressed), Math.Clamp(desaturate, 0f, 1f));
    }

    private static Vector3 PerChannel(Vector3 radiance)
    {
        return new Vector3(
            radiance.X / (radiance.X + 1f),
            radiance.Y / (radiance.Y + 1f),
            radiance.Z / (radiance.Z + 1f));
    }

    // Distance between two colours' ratios, ignoring overall brightness.
    private static float HueError(Vector3 reference, Vector3 candidate)
    {
        var a = NormaliseToPeak(reference);
        var b = NormaliseToPeak(candidate);
        return MathF.Max(MathF.Abs(a.X - b.X), MathF.Max(MathF.Abs(a.Y - b.Y), MathF.Abs(a.Z - b.Z)));
    }

    private static Vector3 NormaliseToPeak(Vector3 value)
    {
        var peak = MathF.Max(value.X, MathF.Max(value.Y, value.Z));
        return peak <= 0.00001f ? Vector3.Zero : value / peak;
    }

    // The cast shadow's direction comes from this, so a hard failure here is a shadow that never
    // draws at all - which is exactly what a silent `return false` looks like on screen, with nothing
    // to distinguish it from a strength or blending problem.
    [Fact]
    public void TryGetLightDirection_PointsAtTheEmittingCell()
    {
        var layout = new LightingTileGridLayout(new Point(-8, -8), new Point(17, 17));
        var cells = new Color[layout.Width * layout.Height];

        // One emitter four tiles to the right of the origin. B carries emission - see EncodeCell.
        cells[layout.GetIndex(new Point(4, 0))] = new Color(0f, 1f, 0.9f, 1f);

        Assert.True(LightingTileGrid.TryGetLightDirection(
            cells, layout, Vector2.Zero, 10f, out var direction, out var strength, out var distance));

        Assert.Equal(1f, direction.X, 2);
        Assert.Equal(0f, direction.Y, 2);
        // A single source means every contribution agrees, so the light is fully directional.
        Assert.Equal(1f, strength, 2);
        // And the reported distance is that emitter's, which is what sets the shadow's length.
        Assert.Equal(4f * TileConstants.TileSize, distance, 1);
    }

    // Two equal emitters on opposite sides cancel: there is no direction to cast along, and the
    // caller must be told so rather than handed an arbitrary one. This is the case the old
    // "straight down" fallback got wrong.
    [Fact]
    public void TryGetLightDirection_ReportsWeakWhenLightArrivesFromBothSides()
    {
        var layout = new LightingTileGridLayout(new Point(-8, -8), new Point(17, 17));
        var cells = new Color[layout.Width * layout.Height];
        cells[layout.GetIndex(new Point(4, 0))] = new Color(0f, 1f, 0.9f, 1f);
        cells[layout.GetIndex(new Point(-4, 0))] = new Color(0f, 1f, 0.9f, 1f);

        LightingTileGrid.TryGetLightDirection(
            cells, layout, Vector2.Zero, 10f, out _, out var strength, out _);

        Assert.True(strength < 0.05f, $"expected the two sides to cancel, got strength {strength}");
    }

    [Fact]
    public void TryGetLightDirection_ReportsNothingWhenNoCellEmits()
    {
        var layout = new LightingTileGridLayout(new Point(-8, -8), new Point(17, 17));
        var cells = new Color[layout.Width * layout.Height];

        Assert.False(LightingTileGrid.TryGetLightDirection(
            cells, layout, Vector2.Zero, 10f, out _, out _, out _));
    }

    // Shadow length has to come from the geometry, not a constant: a constant is simultaneously too
    // long beside a deposit and too short across the room.
    [Fact]
    public void ShadowLength_ShortensNearTheLightAndGrowsWithDistance()
    {
        var height = OreLightSettings.BuildingShadowHeightTiles;
        var min = OreLightSettings.BuildingShadowMinLengthTiles;
        var max = OreLightSettings.BuildingShadowMaxLengthTiles;
        var tile = TileConstants.TileSize;

        // Directly under the light there is nothing to cast sideways. Measured on a building, which
        // has no length floor - a creature's floor deliberately overrides this, see
        // CreatureShadow_KeepsAContactShadowDirectlyUnderTheLight.
        Assert.Equal(0f, WorldSceneRenderer.GetShadowLengthWorld(height, 0f, min, max), 2);

        var near = WorldSceneRenderer.GetShadowLengthWorld(height, 1f * tile, min, max);
        var far = WorldSceneRenderer.GetShadowLengthWorld(height, 2f * tile, min, max);
        Assert.True(near < far, $"expected the shadow to lengthen with distance, got {near} then {far}");

        // A taller caster throws further from the same spot. Measured below both ceilings so this
        // reads the height model rather than the caps.
        Assert.True(
            WorldSceneRenderer.GetShadowLengthWorld(height, 1f * tile, min, max) >
            WorldSceneRenderer.GetShadowLengthWorld(
                OreLightSettings.CreatureShadowHeightTiles,
                1f * tile,
                0f,
                OreLightSettings.CreatureShadowMaxLengthTiles),
            "a taller caster should cast a longer shadow");

        // And it is capped, because the model diverges as the caster approaches the light's height.
        Assert.True(
            WorldSceneRenderer.GetShadowLengthWorld(height, 500f * tile, min, max) <= (max * tile) + 0.01f,
            "shadow length must stay capped however far the light is");
    }

    // A creature's shadow lives inside a band rather than running from zero to the cap: it is the one
    // caster small enough that a full-length shadow detaches, and light enough that no shadow at all
    // reads as hovering.
    [Fact]
    public void CreatureShadow_StaysWithinItsLengthBand()
    {
        var tile = TileConstants.TileSize;
        var height = OreLightSettings.CreatureShadowHeightTiles;
        var min = OreLightSettings.CreatureShadowMinLengthTiles;
        var max = OreLightSettings.CreatureShadowMaxLengthTiles;

        Assert.True(min < max, "the creature shadow band must not be inverted");
        Assert.True(
            max < OreLightSettings.BuildingShadowMaxLengthTiles,
            "a creature's shadow must cap shorter than a building's");

        foreach (var lightDistanceTiles in new[] { 0f, 0.5f, 1f, 2f, 5f, 10f, 500f })
        {
            var length = WorldSceneRenderer.GetShadowLengthWorld(
                height, lightDistanceTiles * tile, min, max);
            Assert.InRange(length, (min * tile) - 0.01f, (max * tile) + 0.01f);
        }
    }

    // The distance response lives in the shadow's DARKNESS. Length is pinned into a narrow band for
    // a caster this short, so if strength did not carry it, nothing would - which is exactly what
    // "shadows no longer change with distance from a light" describes.
    [Fact]
    public void ShadowStrength_DarkensTowardTheLightAndFadesAwayFromIt()
    {
        var tile = TileConstants.TileSize;

        var onTopOfIt = WorldSceneRenderer.GetShadowProximityStrength(0.5f * tile);
        var nearby = WorldSceneRenderer.GetShadowProximityStrength(3f * tile);
        var acrossTheRoom = WorldSceneRenderer.GetShadowProximityStrength(6f * tile);

        Assert.True(
            onTopOfIt > nearby && nearby > acrossTheRoom,
            $"shadow strength must fall with distance, got {onTopOfIt}, {nearby}, {acrossTheRoom}");

        // Full strength within the near band, so a creature sitting on a deposit is fully shadowed.
        Assert.Equal(
            1f,
            WorldSceneRenderer.GetShadowProximityStrength(
                OreLightSettings.ShadowFullStrengthDistanceTiles * tile),
            2);

        // And gone by the fade distance, which lands inside the search radius so it tapers out
        // rather than switching off when the last emitter leaves range.
        Assert.Equal(
            0f,
            WorldSceneRenderer.GetShadowProximityStrength(
                OreLightSettings.ShadowFadeDistanceTiles * tile),
            2);
        Assert.True(
            OreLightSettings.ShadowFadeDistanceTiles < OreLightSettings.CreatureShadowLightRadiusTiles,
            "the fade must finish inside the search radius or shadows pop out instead of fading");
    }

    // Light from a second angle fills a shadow in. This has to be a RAMP - as a clamped threshold it
    // saturated at 0.25 anisotropy, so every ordinarily lit creature cast the same full-strength
    // shadow and competing light did nothing until it had almost exactly cancelled.
    [Fact]
    public void ShadowStrength_BrightensAsLightArrivesFromMoreAngles()
    {
        var oneSided = WorldSceneRenderer.GetShadowDirectionalStrength(1f);
        var mixed = WorldSceneRenderer.GetShadowDirectionalStrength(0.6f);
        var nearlyCancelled = WorldSceneRenderer.GetShadowDirectionalStrength(0.3f);

        Assert.Equal(1f, oneSided, 2);
        Assert.True(
            oneSided > mixed && mixed > nearlyCancelled,
            $"opposing light must progressively brighten the shadow, got {oneSided}, {mixed}, {nearlyCancelled}");

        // Below the threshold light is effectively ambient, so there is no direction to cast along.
        Assert.Equal(
            0f,
            WorldSceneRenderer.GetShadowDirectionalStrength(OreLightSettings.CreatureShadowDirectionality),
            2);
        Assert.Equal(0f, WorldSceneRenderer.GetShadowDirectionalStrength(0f), 2);
    }

    // The floor is what keeps a creature planted: the similar-triangles model goes to zero directly
    // beneath a light, and a creature with no shadow at all reads as hovering.
    [Fact]
    public void CreatureShadow_KeepsAContactShadowDirectlyUnderTheLight()
    {
        var tile = TileConstants.TileSize;

        Assert.Equal(
            OreLightSettings.CreatureShadowMinLengthTiles * tile,
            WorldSceneRenderer.GetShadowLengthWorld(
                OreLightSettings.CreatureShadowHeightTiles,
                0f,
                OreLightSettings.CreatureShadowMinLengthTiles,
                OreLightSettings.CreatureShadowMaxLengthTiles),
            2);
    }

    // The extrusion reads as one shape only while its steps overlap. Spacing is what decides that,
    // and it must not grow with the shadow - a fixed step COUNT stretches the gaps until thin
    // features land as separate dashes, which is exactly what the repeats were.
    [Theory]
    [InlineData(0.2f)]
    [InlineData(0.8f)]
    [InlineData(1.6f)]
    public void ShadowExtrusion_KeepsItsStepSpacingWhateverTheLength(float lengthTiles)
    {
        var length = lengthTiles * TileConstants.TileSize;

        // Mirrors DrawProjectedShadow.
        var steps = Math.Clamp((int)MathF.Ceiling(length / OreLightSettings.ShadowStepWorld) + 1, 3, 40);
        var spacing = length / (steps - 1);

        Assert.True(
            spacing <= OreLightSettings.ShadowStepWorld + 0.01f,
            $"length {lengthTiles} tiles gave {steps} steps spaced {spacing} apart, over the " +
            $"{OreLightSettings.ShadowStepWorld} budget - thin features will band");
    }

    // Unlit pixels land on exactly Ambient, so it is the only knob that sets how dark the darkest
    // areas get. Ambient + LitContribution is what a fully lit surface reaches, and driving that
    // past 1 would clip highlights into flat white.
    [Fact]
    public void AmbientAndLitContribution_LeaveHeadroomForAFullyLitSurface()
    {
        Assert.InRange(OreLightSettings.Ambient, 0f, 0.25f);
        Assert.InRange(OreLightSettings.Ambient + OreLightSettings.LitContribution, 0.9f, 1f);
    }

    [Fact]
    public void LightingCascadeLayout_CascadeCountIsClampedToSupportedRange()
    {
        Assert.Equal(
            OreLightSettings.MinCascadeCount,
            LightingCascadeLayout.CalculateCascadeCount(1f, OreLightSettings.CascadeIntervalTexels));
        Assert.Equal(
            OreLightSettings.MaxCascadeCount,
            LightingCascadeLayout.CalculateCascadeCount(100000f, OreLightSettings.CascadeIntervalTexels));
    }

    [Fact]
    public void CollectOreEmitters_CarriesEachOresOwnLightColour()
    {
        var sandstone = CreateOreTile(0, OreType.SANDSTONE.Name);
        var lumenite = CreateOreTile(1, OreType.LUMENITE.Name);
        var palette = new OreLightColorPalette();
        var emitters = new List<OreLightEmitter>();

        new LightingSourceCollector().CollectOreEmitters(
            [sandstone, lumenite],
            new WorldSpriteEffectSystem(),
            emitters,
            palette);

        // Unregistered ores fall back to white rather than to black, so an ore whose sprite is
        // missing still emits light instead of silently going dark.
        Assert.Equal(2, emitters.Count);
        Assert.All(emitters, emitter => Assert.Equal(Color.White, emitter.LightColor));
    }

    // Exercises the real extraction path (the Color[] overload) so the tests cannot drift from
    // the implementation the game actually runs.
    [Fact]
    public void OreLightColorPalette_RecoversTheOreHueFromMostlyGreyRock()
    {
        // 56 grey rock pixels, 8 strong green vein pixels. A plain average lands on grey; only
        // isolating the above-mean-saturation pixels recovers the green.
        var pixels = new Color[64];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(120, 120, 120, 255);
        }

        for (var i = 0; i < 8; i++)
        {
            pixels[i] = new Color(20, 220, 40, 255);
        }

        var color = OreLightColorPalette.ExtractLightColor(pixels);

        Assert.True(color.G > color.R, $"expected green-dominant, got {color}");
        Assert.True(color.G > color.B, $"expected green-dominant, got {color}");
    }

    [Fact]
    public void OreLightColorPalette_DistinguishesOresWithDifferentVeinHues()
    {
        var red = BuildOreSprite(new Color(210, 40, 30, 255));
        var blue = BuildOreSprite(new Color(30, 60, 220, 255));

        var redLight = OreLightColorPalette.ExtractLightColor(red);
        var blueLight = OreLightColorPalette.ExtractLightColor(blue);

        Assert.True(redLight.R > redLight.B, $"expected red-dominant, got {redLight}");
        Assert.True(blueLight.B > blueLight.R, $"expected blue-dominant, got {blueLight}");
    }

    [Fact]
    public void OreLightColorPalette_FullyTransparentSpriteFallsBackToWhite()
    {
        Assert.Equal(Color.White, OreLightColorPalette.ExtractLightColor(new Color[16]));
    }

    [Fact]
    public void OreLightColorPalette_GreyscaleSpriteFallsBackToNeutralLight()
    {
        var pixels = new Color[16];
        Array.Fill(pixels, new Color(90, 90, 90, 255));

        var color = OreLightColorPalette.ExtractLightColor(pixels);

        // No hue to recover, so it must stay neutral rather than pick an arbitrary tint.
        Assert.Equal(color.R, color.G);
        Assert.Equal(color.G, color.B);
    }

    private static Color[] BuildOreSprite(Color veinColor)
    {
        var pixels = new Color[64];
        Array.Fill(pixels, new Color(118, 122, 130, 255));
        for (var i = 0; i < 10; i++)
        {
            pixels[i] = veinColor;
        }

        return pixels;
    }

    // (LightingFieldUvScale and its two tests are gone. The composite no longer rescales UVs to
    // compensate for the packed overhang - it inverts GetProbeWorld directly, so the overhang is
    // simply extra texels past the screen rather than a stretch that has to be divided back out.)

    // GetLightingFieldUv assumes one field texel per cascade-0 probe. If that ever stopped holding,
    // the composite would read the field at the wrong scale with nothing to signal it.
    [Fact]
    public void LightingField_HasExactlyOneTexelPerCascadeZeroProbe()
    {
        foreach (var lightSize in new[] { new Point(720, 450), new Point(960, 540), new Point(512, 512) })
        {
            var layout = LightingCascadeLayout.Create(lightSize);

            Assert.Equal(layout.GetProbeCount(0), layout.LightingFieldSize);
            Assert.Equal(layout.PackedSize.X / OreLightSettings.BaseProbeSpacing, layout.LightingFieldSize.X);
            Assert.Equal(layout.PackedSize.Y / OreLightSettings.BaseProbeSpacing, layout.LightingFieldSize.Y);
        }
    }

    // The composite's screen -> field lookup must be the exact inverse of the ray march's probe ->
    // world placement. These are two expressions in two places (GetLightingFieldUv and GetProbeWorld
    // in RadianceCascade.fx) and the whole point of item 1 is that they are now inverses of each
    // other rather than two independent routes through a source-pixel space.
    [Fact]
    public void LightingFieldLookup_InvertsProbePlacementExactly()
    {
        var layout = LightingCascadeLayout.Create(new Point(720, 450));
        var origin = new Vector2(-1536.5f, 4096.25f);
        var spacing = new Vector2(102.4f, 102.4f);

        for (var i = 0; i < layout.LightingFieldSize.X; i += 7)
        {
            // GetProbeWorld
            var probeWorld = origin.X + (i * spacing.X);
            // GetLightingFieldUv
            var probeIndex = (probeWorld - origin.X) / spacing.X;
            var uv = (probeIndex + 0.5f) / layout.LightingFieldSize.X;

            // Probe i must land on texel i's centre, which is what makes the linear sampler
            // interpolate between neighbouring probes rather than smear across a half-texel offset.
            Assert.Equal((i + 0.5f) / layout.LightingFieldSize.X, uv, 5);
        }
    }

    // Cascade 0's probes must BRACKET the screen - one at or before the top-left, one at or after the
    // bottom-right - at every zoom rung and for every snap residual.
    //
    // Covering the screen with the lattice's ORIGIN is not the same thing and was the bug: probe 0
    // sits half a spacing past the origin, so a plain floor left up to half a spacing of the top and
    // left edges with no probe outside them. GetLightingFieldUv then returns a negative index and the
    // clamped sampler smears one probe across the strip, whose width breathes with the pan.
    [Theory]
    [InlineData(-5)]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void ProbeLattice_BracketsTheScreenAtEveryZoomAndSnapResidual(int zoomStep)
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);
        var viewCenter = new Vector2(viewport.X * 0.5f, viewport.Y * 0.5f);
        var scale = CameraController.GetScaleForZoomStep(zoomStep);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);
        var count = layout.GetProbeCount(0);

        // Sweep sub-probe camera positions so every possible snap residual is exercised.
        for (var sample = 0; sample < 64; sample++)
        {
            var cameraOrigin = new Vector2(
                7000f + (sample * spacing.X / 64f),
                -3000f - (sample * spacing.Y / 64f));
            var origin = SnapLattice(cameraOrigin, scale, viewport, lightSize);

            // Probe i sits at origin + i * spacing - no half-probe phase.
            var firstProbe = origin;
            var lastProbe = origin + (spacing * (count.X - 1));
            var lastProbeY = origin.Y + (spacing.Y * (count.Y - 1));

            var screenTopLeft = cameraOrigin + ((-viewCenter) / scale);
            var screenBottomRight = cameraOrigin + ((new Vector2(viewport.X, viewport.Y) - viewCenter) / scale);

            Assert.True(
                firstProbe.X <= screenTopLeft.X && firstProbe.Y <= screenTopLeft.Y,
                $"zoom {zoomStep} sample {sample}: first probe {firstProbe} is inside the screen corner {screenTopLeft}");
            Assert.True(
                lastProbe.X >= screenBottomRight.X && lastProbeY >= screenBottomRight.Y,
                $"zoom {zoomStep} sample {sample}: last probe stops short of {screenBottomRight}");
        }
    }

    // The occluder mask must reach at least ShortShadowTiles past every edge of the view, at every
    // zoom rung. That margin is what makes the ray march's mask bounds test unreachable for any
    // caster that could still contribute: shortFade takes a short caster's contribution to exactly
    // zero past ShortShadowTiles, so a creature further out than this cannot affect an on-screen
    // pixel no matter where the camera points.
    //
    // Before this, the mask WAS the screen, so whether a creature occluded a ray depended on the
    // camera's aim and the boundary swept across the world as you panned.
    [Theory]
    [InlineData(-5)]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void OccluderMask_ReachesShortShadowRangeBeyondTheView(int zoomStep)
    {
        var viewport = new Point(1920, 1080);
        var viewCenter = new Vector2(viewport.X * 0.5f, viewport.Y * 0.5f);
        var scale = CameraController.GetScaleForZoomStep(zoomStep);
        var maskSize = RadianceCascadeRenderer.GetOccluderMaskSize(viewport);
        var shake = new Vector2(4f, -3f);
        var cameraOrigin = new Vector2(9001.5f, -4242.25f);

        var layout = RadianceCascadeRenderer.GetOccluderMaskLayout(
            maskSize, cameraOrigin, viewCenter, shake, scale, viewport);

        var required = OreLightSettings.ShortShadowTiles * TileConstants.TileSize;
        var screenWorldSpan = new Vector2(viewport.X / scale, viewport.Y / scale);
        Assert.True(
            layout.GetWorldMargin(screenWorldSpan) >= required - 1f,
            $"zoom {zoomStep}: mask reaches only {layout.GetWorldMargin(screenWorldSpan)} world units " +
            $"past the view, needs {required}");

        // And the stronger statement the margin exists for: a point exactly ShortShadowTiles beyond
        // the corner of the screen still resolves to a texel inside the mask.
        var topLeftWorld = cameraOrigin + ((-viewCenter - shake) / scale);
        var bottomRightWorld = cameraOrigin + ((new Vector2(viewport.X, viewport.Y) - viewCenter - shake) / scale);
        foreach (var probe in new[]
        {
            topLeftWorld - new Vector2(required, required),
            bottomRightWorld + new Vector2(required, required),
        })
        {
            var texel = (probe - layout.WorldOrigin) * layout.TexelsPerWorld;
            Assert.True(
                texel.X >= 0f && texel.Y >= 0f && texel.X < maskSize.X && texel.Y < maskSize.Y,
                $"zoom {zoomStep}: world {probe} maps to mask texel {texel}, outside {maskSize}");
        }
    }

    // The mask origin must land on a whole mask texel. Point-sampled silhouettes flip between
    // blocking and clear if the address slides by a fraction of a texel as the camera moves.
    [Fact]
    public void OccluderMaskOrigin_LandsOnAWholeTexel()
    {
        var viewport = new Point(1920, 1080);
        var viewCenter = new Vector2(viewport.X * 0.5f, viewport.Y * 0.5f);
        var scale = CameraController.GetScaleForZoomStep(2);
        var maskSize = RadianceCascadeRenderer.GetOccluderMaskSize(viewport);

        for (var frame = 0; frame < 200; frame++)
        {
            var cameraOrigin = new Vector2(1000f + (frame * 7.31f), -500f - (frame * 3.17f));
            var layout = RadianceCascadeRenderer.GetOccluderMaskLayout(
                maskSize, cameraOrigin, viewCenter, Vector2.Zero, scale, viewport);

            var texels = layout.WorldOrigin * layout.TexelsPerWorld;
            Assert.True(
                MathF.Abs(texels.X - MathF.Round(texels.X)) < 0.01f,
                $"frame {frame}: mask origin is {texels.X} texels, not a whole number");
            Assert.True(
                MathF.Abs(texels.Y - MathF.Round(texels.Y)) < 0.01f,
                $"frame {frame}: mask origin is {texels.Y} texels, not a whole number");
        }
    }

    // The mask size must not depend on the live camera scale, or every frame of a zoom glide
    // reallocates it. Sized for the tightest zoom instead, so the margin can only be too large.
    [Fact]
    public void OccluderMaskSize_IsIndependentOfZoom()
    {
        var viewport = new Point(1920, 1080);
        var size = RadianceCascadeRenderer.GetOccluderMaskSize(viewport);

        Assert.True(size.X > viewport.X * 0.5f, "mask should be wider than the light buffer");
        Assert.True(size.Y > viewport.Y * 0.5f, "mask should be taller than the light buffer");
        Assert.Equal(size, RadianceCascadeRenderer.GetOccluderMaskSize(viewport));
    }

    // The packed target must leave a FULL coarsest-cascade spacing of slack beyond the light buffer.
    //
    // SnapToLattice floors, so a cascade's origin lands between zero and one full spacing BEFORE the
    // camera's own - never after it. That one-sidedness is what guarantees the top-left of the screen
    // has probes; the whole spacing then has to be absorbed at the far edge instead. Half a block was
    // sized for the old symmetric snap, whose positive half is exactly the defect being fixed: it put
    // the lattice origin past the screen origin, leaving a band down the left/top of the screen with
    // no probes outside it, which the clamped sampler filled with a single smeared edge texel.
    [Fact]
    public void PackedSize_LeavesRoomForTheLatticeSnapOffset()
    {
        foreach (var lightSize in new[] { new Point(720, 450), new Point(960, 540), new Point(512, 512) })
        {
            var layout = LightingCascadeLayout.Create(lightSize);
            var coarsestSpacing = OreLightSettings.BaseProbeSpacing * (1 << (layout.CascadeCount - 1));

            Assert.True(
                layout.PackedSize.X >= lightSize.X + coarsestSpacing,
                $"{lightSize}: packed width {layout.PackedSize.X} leaves no room for a {coarsestSpacing}px snap offset");
            Assert.True(
                layout.PackedSize.Y >= lightSize.Y + coarsestSpacing,
                $"{lightSize}: packed height {layout.PackedSize.Y} leaves no room for a {coarsestSpacing}px snap offset");
        }
    }

    // The snap must never place a cascade's origin AFTER the point it is derived from. See
    // PackedSize_LeavesRoomForTheLatticeSnapOffset for what the positive side of a symmetric snap
    // did to the left and top edges of the screen.
    [Fact]
    public void SnapToLattice_NeverPlacesTheOriginAheadOfTheCamera()
    {
        var spacing = new Vector2(102.4f, 102.4f);
        for (var step = 0; step < 500; step++)
        {
            var point = new Vector2(-2000f + (step * 9.37f), 1500f - (step * 4.11f));
            var snapped = RadianceCascadeRenderer.SnapToLattice(point, spacing);
            var offsetX = snapped.X - point.X;
            var offsetY = snapped.Y - point.Y;

            Assert.True(offsetX <= 1e-3f && offsetX >= -spacing.X - 1e-3f, $"x offset {offsetX} outside [-spacing, 0]");
            Assert.True(offsetY <= 1e-3f && offsetY >= -spacing.Y - 1e-3f, $"y offset {offsetY} outside [-spacing, 0]");
        }
    }

    // Cascade k's own snap grid must be exactly its own probe spacing - base spacing doubled per
    // level. This is the property that lets each cascade step independently and still land back on
    // positions it already occupied, which is what replaced the single shared origin quantised to the
    // COARSEST cascade (16x cascade 0's spacing = 3.2 tiles at this viewport, and therefore a 3.2
    // tile teleport of the whole lattice on every step).
    [Fact]
    public void CascadeSnapGrid_IsThatCascadeOwnProbeSpacing()
    {
        var viewport = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, GameConstants.DefaultCameraScale);

        for (var cascade = 0; cascade < layout.CascadeCount; cascade++)
        {
            var expected = spacing * (layout.GetProbeSpacing(cascade) / (float)layout.BaseProbeSpacing);
            var actual = RadianceCascadeRenderer.GetCascadeProbeWorldSpacing(spacing, cascade);

            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Y, actual.Y, 3);
        }
    }

    // Every cascade spans the SAME world extent - probe count halves exactly as spacing doubles - so
    // a probe of cascade k is inside cascade k+1's lattice everywhere but the last spacing of the
    // edge. That is what lets CascadeMergePixel look the higher cascade up at the lower probe's own
    // position, and it is why the coverage floor that briefly widened the coarse cascades is gone:
    // the merge no longer reaches a look-ahead outside the lattice to ask its question.
    [Theory]
    [InlineData(-5)]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void EveryCascadeSpansTheSameWorldExtent(int zoomStep)
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);
        var scale = CameraController.GetScaleForZoomStep(zoomStep);
        var baseSpacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);
        var screenWorldSpan = new Vector2(viewport.X / scale, viewport.Y / scale);

        var referenceSpan = Vector2.Zero;
        for (var cascade = 0; cascade < layout.CascadeCount; cascade++)
        {
            var spacing = RadianceCascadeRenderer.GetCascadeProbeWorldSpacing(baseSpacing, cascade);
            var count = layout.GetProbeCount(cascade);
            var span = new Vector2(count.X * spacing.X, count.Y * spacing.Y);

            if (cascade == 0)
            {
                referenceSpan = span;
            }

            Assert.Equal(referenceSpan.X, span.X, 1);
            Assert.Equal(referenceSpan.Y, span.Y, 1);

            // Every cascade must span the screen. Only cascade 0 has to BRACKET it - one probe past
            // each edge, with the three spacings of slack the floor snap, half-probe bias and
            // centring consume between them - because cascade 0 is what the composite interpolates
            // across the whole screen. The coarse cascades are read at probe positions rather than
            // across the screen, and their last spacing of edge is covered by the merge's fade;
            // demanding three spacings of slack from a 16-probe lattice would mean 19% of it.
            var required = cascade == 0 ? 3 : 0;
            Assert.True(
                (count.X - required) * spacing.X >= screenWorldSpan.X,
                $"zoom {zoomStep} cascade {cascade}: spans {(count.X - required) * spacing.X}, screen needs {screenWorldSpan.X}");
            Assert.True(
                (count.Y - required) * spacing.Y >= screenWorldSpan.Y,
                $"zoom {zoomStep} cascade {cascade}: spans {(count.Y - required) * spacing.Y} vertically, screen needs {screenWorldSpan.Y}");
        }
    }

    // The merge locates a world point among the HIGHER cascade's probes. With per-cascade origins the
    // two lattices sit on different world grids, and this used to need an explicit reconciliation
    // term in the lower cascade's source-pixel space (GetHigherOriginSourceOffset). Indexing the
    // higher cascade in its own world terms removes the need for one - which is what this pins:
    // inverting the higher cascade's placement recovers the probe index, whatever its origin is.
    [Fact]
    public void CascadeMerge_LocatesAWorldPointInTheHigherCascadeRegardlessOfItsOrigin()
    {
        var spacing = new Vector2(204.8f, 204.8f);

        foreach (var higherOrigin in new[]
        {
            new Vector2(0f, 0f),
            new Vector2(-4096f, 2048f),
            new Vector2(1e5f, -1e5f),
        })
        {
            for (var probe = 0; probe < 8; probe++)
            {
                // Where the higher cascade actually puts probe `probe` (GetProbeWorld).
                var world = higherOrigin + ((new Vector2(probe, probe) + new Vector2(0.5f)) * spacing);
                // What CascadeMergePixel computes for that world point.
                var coordinate = ((world - higherOrigin) / spacing) - new Vector2(0.5f);

                Assert.Equal(probe, coordinate.X, 3);
                Assert.Equal(probe, coordinate.Y, 3);
            }
        }
    }

    // End-to-end version of the test above, and the one that actually constrains the renderer.
    //
    // LatticeSnapQuantum_IsAWholeMultipleOfEveryCascadeProbeSpacing checks the quantum algebraically,
    // in isolation. It cannot catch a lattice that is pinned correctly in principle but re-seated in
    // practice, because it never runs a pan through UpdateProbeLattice's actual inputs - camera
    // origin, view centre, shake and scale all feed GetUnsnappedLatticeOrigin, and only their
    // combination decides where a probe lands.
    //
    // So: sweep the camera across many SUB-PROBE increments, rebuild the lattice exactly as
    // UpdateProbeLattice does, and assert every cascade's probes stay on one fixed world lattice for
    // the whole sweep. A cascade that drifts by a fraction of its own spacing is re-marching its rays
    // from new world points every frame, which is invisible to any pixel comparison but is precisely
    // what makes light and shadow churn while panning.
    [Theory]
    [InlineData(-5)]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void ProbeLattice_StaysOnAFixedWorldGridForEveryCascadeWhilePanning(int zoomStep)
    {
        var viewport = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);
        var scale = CameraController.GetScaleForZoomStep(zoomStep);
        var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);
        var viewCenter = new Vector2(viewport.X * 0.5f, viewport.Y * 0.5f);

        var screenWorldSpan = new Vector2(viewport.X / scale, viewport.Y / scale);

        // Deliberately an irrational-ish fraction of a probe, so the sweep never lands on the snap
        // boundary the same way twice and cannot accidentally agree by landing on whole steps.
        var stepWorld = spacing.X * 0.137f;
        var reference = new Vector2?[layout.CascadeCount];
        var previousOrigin = new Vector2?[layout.CascadeCount];
        var maxStepProbes = new float[layout.CascadeCount];

        for (var frame = 0; frame < 400; frame++)
        {
            // Shake is included because it enters the anchor divided by scale, so it perturbs the
            // lattice differently at every zoom rung - the exact term that made snapping CameraOrigin
            // alone insufficient.
            var shake = new Vector2(
                MathF.Sin(frame * 0.31f) * 3f,
                MathF.Cos(frame * 0.17f) * 3f);
            var cameraOrigin = new Vector2(1234.5f + (frame * stepWorld), -876.25f + (frame * stepWorld * 0.61f));
            var anchor = RadianceCascadeRenderer.GetLatticeAnchorWorld(cameraOrigin, shake, scale);

            for (var cascade = 0; cascade < layout.CascadeCount; cascade++)
            {
                // Mirrors UpdateProbeLattice: half the lattice plus half a probe back from the anchor.
                var cascadeSpacing = RadianceCascadeRenderer.GetCascadeProbeWorldSpacing(spacing, cascade);
                var count = layout.GetProbeCount(cascade);
                var latticeSpan = new Vector2(count.X * cascadeSpacing.X, count.Y * cascadeSpacing.Y);
                var latticeOrigin = RadianceCascadeRenderer.SnapToLattice(
                    anchor - (latticeSpan * 0.5f), cascadeSpacing);
                var previous = previousOrigin[cascade] ?? latticeOrigin;
                previousOrigin[cascade] = latticeOrigin;
                reference[cascade] ??= latticeOrigin;

                // Probe j of cascade k sits at latticeOrigin + (j + 0.5) * cascadeSpacing, so the
                // lattice is unchanged for that cascade exactly when the origin has moved a whole
                // number of ITS spacings since the reference frame.
                var stepsX = (latticeOrigin.X - reference[cascade]!.Value.X) / cascadeSpacing.X;
                var stepsY = (latticeOrigin.Y - reference[cascade]!.Value.Y) / cascadeSpacing.Y;

                Assert.True(
                    MathF.Abs(stepsX - MathF.Round(stepsX)) < 0.001f,
                    $"zoom {zoomStep}, frame {frame}, cascade {cascade}: lattice moved {stepsX} probe " +
                    "spacings in x - a fractional step re-seats every probe onto a new world position");
                Assert.True(
                    MathF.Abs(stepsY - MathF.Round(stepsY)) < 0.001f,
                    $"zoom {zoomStep}, frame {frame}, cascade {cascade}: lattice moved {stepsY} probe " +
                    "spacings in y - a fractional step re-seats every probe onto a new world position");

                // How far a single frame moves this cascade's lattice, in units of its OWN probes.
                // A shared origin snapped to the coarsest cascade moved cascade 0 by 16 probes at a
                // time; per-cascade origins must never move any cascade by more than one.
                var frameStep = MathF.Abs(latticeOrigin.X - previous.X) / cascadeSpacing.X;
                maxStepProbes[cascade] = MathF.Max(maxStepProbes[cascade], frameStep);
            }
        }

        for (var cascade = 0; cascade < layout.CascadeCount; cascade++)
        {
            Assert.True(
                maxStepProbes[cascade] <= 1.001f,
                $"zoom {zoomStep}, cascade {cascade}: lattice stepped {maxStepProbes[cascade]} of its own " +
                "probe spacings in one frame - the screen jumps that far inside the lattice");
        }
    }

    // Probe spacing tracks the screen, but only to within a power of two: it is quantised so that
    // several zoom rungs share one world grid. Nominally 16 screen pixels, and never finer than that
    // (finer would shrink the lattice below the screen, since the probe count is fixed), and never
    // more than one doubling coarser.
    [Fact]
    public void ProbeWorldSpacing_IsQuantisedToAPowerOfTwoOfTheScreenDistance()
    {
        var viewport = new Point(1440, 900);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale);
            var screenPixels = spacing.X * scale;

            Assert.InRange(screenPixels, 16f - 0.01f, 32f + 0.01f);

            // And it is an exact power-of-two multiple of the spacing at the reference zoom, which is
            // what makes two rungs that land on the same value land on the SAME world grid rather
            // than merely a similar one.
            var reference = RadianceCascadeRenderer.GetProbeWorldSpacing(
                viewport, lightSize, GameConstants.DefaultCameraScale);
            var ratio = MathF.Log2(spacing.X / reference.X);
            Assert.Equal(MathF.Round(ratio), ratio, 3);
        }
    }

    // When a zoom step changes the spacing, the coarser grid's probes must land exactly ON positions
    // the finer grid already occupied - the grids must NEST, not merely be similar.
    //
    // Spacing is quantised to powers of two, so multiples of 2^n*s are a subset of multiples of s.
    // That only holds because probe i sits at origin + i*spacing with no half-probe phase: with the
    // phase, coarse probes land on half-integer multiples and share nothing with the fine grid, so
    // every spacing change moved every probe.
    [Fact]
    public void CoarserZoomGrids_NestInsideFinerOnes()
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var layout = LightingCascadeLayout.Create(lightSize);
        var cameraOrigin = new Vector2(6144f, -2048f);
        var count = layout.GetProbeCount(0);

        for (var step = -GameConstants.MaxZoomSteps; step < GameConstants.MaxZoomSteps; step++)
        {
            var fineScale = CameraController.GetScaleForZoomStep(step + 1);
            var coarseScale = CameraController.GetScaleForZoomStep(step);
            var fine = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, fineScale);
            var coarse = RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, coarseScale);
            if (MathF.Abs(fine.X - coarse.X) < 0.001f)
            {
                continue;
            }

            var fineOrigin = SnapLattice(cameraOrigin, fineScale, viewport, lightSize);
            var coarseOrigin = SnapLattice(cameraOrigin, coarseScale, viewport, lightSize);

            // Every coarse probe must coincide with some fine probe: the offset between the grids has
            // to be a whole number of FINE spacings, and the coarse spacing a whole multiple of it.
            var originStepsX = (coarseOrigin.X - fineOrigin.X) / fine.X;
            var spacingRatio = coarse.X / fine.X;

            Assert.True(
                MathF.Abs(originStepsX - MathF.Round(originStepsX)) < 0.01f,
                $"step {step}: grids are offset by {originStepsX} fine spacings, not a whole number");
            Assert.True(
                MathF.Abs(spacingRatio - MathF.Round(spacingRatio)) < 0.001f,
                $"step {step}: coarse spacing is {spacingRatio}x the fine one, not an integer multiple");

            // And spot-check an actual coarse probe against the fine grid.
            for (var probe = 0; probe < count.X; probe += 5)
            {
                var coarseProbe = coarseOrigin.X + (probe * coarse.X);
                var asFineIndex = (coarseProbe - fineOrigin.X) / fine.X;
                Assert.True(
                    MathF.Abs(asFineIndex - MathF.Round(asFineIndex)) < 0.01f,
                    $"step {step} probe {probe}: coarse probe sits at fine index {asFineIndex}, off-grid");
            }
        }
    }

    // Two zoom rungs that share a probe spacing must produce the IDENTICAL lattice origin for a fixed
    // camera position - not merely a similar one.
    //
    // Sharing a spacing is worth nothing on its own. The origin was previously derived by stepping
    // back from the screen's top-left by (latticeSpan - screenWorldSpan)/2, and screenWorldSpan
    // changes at every rung: at 1080p, rungs 0 and +1 both use a 102.4 spacing but their margins were
    // 409.6 and 1945.6, so the snapped origin moved about fifteen probes on a zoom step that was
    // supposed to move nothing. Visible directly in the LightingField debug view, which is drawn in
    // the field's own space and so should sit perfectly still through a zoom.
    [Fact]
    public void ZoomRungsSharingASpacing_ProduceTheIdenticalLatticeOrigin()
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var cameraOrigin = new Vector2(8123.75f, -4271.5f);
        var bySpacing = new Dictionary<float, Vector2>();

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            var spacing = MathF.Round(
                RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale).X, 3);
            var origin = SnapLattice(cameraOrigin, scale, viewport, lightSize);

            if (bySpacing.TryGetValue(spacing, out var previous))
            {
                Assert.True(
                    MathF.Abs(origin.X - previous.X) < 0.01f && MathF.Abs(origin.Y - previous.Y) < 0.01f,
                    $"zoom step {step} shares spacing {spacing} with an earlier rung but its lattice " +
                    $"origin is {origin} rather than {previous} - zooming between them re-seats every probe");
            }
            else
            {
                bySpacing[spacing] = origin;
            }
        }

        Assert.True(bySpacing.Count < 11, "expected rungs to share grids");
    }

    // The zoom fix, stated as a property: the eleven zoom rungs must collapse onto far fewer distinct
    // world grids, so most zoom steps move no probe at all. Screen-relative spacing on its own gives
    // every rung its own grid, which means every zoom re-seats every probe.
    [Fact]
    public void ZoomRungs_ShareWorldGridsRatherThanHavingOneEach()
    {
        var viewport = new Point(1920, 1080);
        var lightSize = LightingRenderTargets.CalculateLightSize(viewport);
        var distinct = new HashSet<float>();
        var rungs = 0;

        for (var step = -GameConstants.MaxZoomSteps; step <= GameConstants.MaxZoomSteps; step++)
        {
            var scale = CameraController.GetScaleForZoomStep(step);
            distinct.Add(MathF.Round(
                RadianceCascadeRenderer.GetProbeWorldSpacing(viewport, lightSize, scale).X, 3));
            rungs++;
        }

        Assert.Equal(11, rungs);
        Assert.True(
            distinct.Count <= 7,
            $"{rungs} zoom rungs produced {distinct.Count} distinct probe grids - the quantisation is " +
            "not collapsing them, so zooming re-seats the lattice on most steps");
    }

    // The merge step pulls each coarse ray's radiance from the four finer rays that subdivide
    // its angular range. If that mapping is wrong the shader still renders - every probe just
    // samples the wrong direction by the same amount, which reads as one global light direction
    // instead of light radiating from each emitter. These tests pin the mapping numerically.
    [Fact]
    public void HigherRayIndices_SubdivideTheParentRaysAngularRange()
    {
        var layout = LightingCascadeLayout.Create(new Point(720, 450));
        var parentCount = layout.GetRayCount(0);
        var childCount = layout.GetRayCount(1);
        Assert.Equal(parentCount * 4, childCount);

        var binWidth = MathF.Tau / parentCount;
        for (var parentIndex = 0; parentIndex < parentCount; parentIndex++)
        {
            var parentAngle = LightingCascadeLayout.GetRayAngle(parentIndex, parentCount);
            var binStart = parentAngle - (binWidth * 0.5f);
            var binEnd = parentAngle + (binWidth * 0.5f);
            for (var childOffset = 0; childOffset < 4; childOffset++)
            {
                var childIndex = LightingCascadeLayout.GetHigherRayIndex(parentIndex, childOffset);
                var childAngle = LightingCascadeLayout.GetRayAngle(childIndex, childCount);
                Assert.InRange(childAngle, binStart, binEnd);
            }
        }
    }

    [Fact]
    public void HigherRayIndices_AreDistinctAndCoverEveryFinerRayExactlyOnce()
    {
        var layout = LightingCascadeLayout.Create(new Point(720, 450));
        var parentCount = layout.GetRayCount(0);
        var seen = new HashSet<int>();

        for (var parentIndex = 0; parentIndex < parentCount; parentIndex++)
        {
            for (var childOffset = 0; childOffset < 4; childOffset++)
            {
                Assert.True(seen.Add(LightingCascadeLayout.GetHigherRayIndex(parentIndex, childOffset)));
            }
        }

        Assert.Equal(layout.GetRayCount(1), seen.Count);
        Assert.Equal(Enumerable.Range(0, layout.GetRayCount(1)), seen.OrderBy(index => index));
    }

    [Fact]
    public void HigherRayOffset_PacksIndicesUsingTheHigherCascadeRowWidth()
    {
        var layout = LightingCascadeLayout.Create(new Point(720, 450));
        var higherRayDimension = layout.GetRayDimension(1);

        // Parent ray 2 has children 8..11; with a 16-wide higher cascade they sit in row 0.
        Assert.Equal(new Point(8, 0), LightingCascadeLayout.GetHigherRayOffset(8, higherRayDimension));
        Assert.Equal(new Point(11, 0), LightingCascadeLayout.GetHigherRayOffset(11, higherRayDimension));
        // Parent ray 4 has children 16..19, which wrap onto the next row.
        Assert.Equal(new Point(0, 1), LightingCascadeLayout.GetHigherRayOffset(16, higherRayDimension));
        Assert.Equal(new Point(3, 1), LightingCascadeLayout.GetHigherRayOffset(19, higherRayDimension));
    }

    // Guards the specific regression: doubling the packed 2D offset per axis looks plausible
    // (ray dimension does double per axis) but scrambles the angular correspondence for every
    // ray outside the first row, because the 2D layout is storage rather than an angle basis.
    [Fact]
    public void PerAxisDoubledOffset_DoesNotPreserveAngularCorrespondence()
    {
        var layout = LightingCascadeLayout.Create(new Point(720, 450));
        var rayDimension = layout.GetRayDimension(0);
        var higherRayDimension = layout.GetRayDimension(1);
        var parentIndex = (1 * rayDimension) + 3;
        var parentOffset = new Point(3, 1);

        var perAxisDoubled = new Point(parentOffset.X * 2, parentOffset.Y * 2);
        var perAxisIndex = (perAxisDoubled.Y * higherRayDimension) + perAxisDoubled.X;
        var correctIndex = LightingCascadeLayout.GetHigherRayIndex(parentIndex, 0);

        Assert.NotEqual(correctIndex, perAxisIndex);
    }

    private static Tile CreateOreTile(int id, string oreName, GridPoint? coordinates = null)
    {
        var point = coordinates ?? new GridPoint(id, 0);
        var tile = new Tile(id, point.ToString());
        tile.SetBase(oreName);
        tile.ConfigureOre(2, 1);
        return tile;
    }
}
