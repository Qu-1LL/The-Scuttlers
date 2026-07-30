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

    [Fact]
    public void CalculateLightSize_UsesHalfViewportDimensionsWithOddSizeRoundingUp()
    {
        Assert.Equal(new Point(720, 450), LightingRenderTargets.CalculateLightSize(new Point(1440, 900)));
        Assert.Equal(new Point(5, 4), LightingRenderTargets.CalculateLightSize(new Point(9, 7)));
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
    public void LightingTileGrid_EncodesEmissionWithoutChangingBlockerState()
    {
        var ore = CreateOreTile(0, OreType.SANDSTONE.Name);
        var encoded = LightingTileGrid.EncodeCell(ore, known: true, emission: 0.75f);

        Assert.Equal(255, encoded.R);
        Assert.Equal(255, encoded.G);
        Assert.InRange(encoded.B, 190, 195);
    }

    [Fact]
    public void LightingTileGridLayout_TracksCameraFootprintAndZoom()
    {
        var camera = new CameraController();
        camera.SetViewport(801, 601);
        camera.SetOrigin(Vector2.Zero);

        var wide = LightingTileGridLayout.Create(camera, new Point(801, 601));
        camera.CurrentScale *= 2f;
        var zoomed = LightingTileGridLayout.Create(camera, new Point(801, 601));

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

        Assert.Equal(5, layout.CascadeCount);
        Assert.Equal(new Point(768, 512), layout.PackedSize);
        Assert.Equal(new Point(96, 64), layout.LightingFieldSize);
        Assert.Equal(new Point(96, 64), layout.GetProbeCount(0));
        Assert.Equal(new Point(6, 4), layout.GetProbeCount(layout.CascadeCount - 1));
        Assert.Equal(8, layout.GetRayDimension(0));
        Assert.Equal(128, layout.GetRayDimension(layout.CascadeCount - 1));
    }

    [Fact]
    public void LightingCascadeLayout_UsesGeometricIntervalGrowth()
    {
        var layout = LightingCascadeLayout.Create(new Point(320, 240));

        Assert.Equal(2f, layout.GetIntervalLength(0));
        Assert.Equal(8f, layout.GetIntervalLength(1));
        Assert.Equal(32f, layout.GetIntervalLength(2));
        Assert.Equal(0f, layout.GetIntervalOrigin(0));
        Assert.Equal(2f, layout.GetIntervalOrigin(1));
        Assert.Equal(10f, layout.GetIntervalOrigin(2));
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

    // The packed cascade target is power-of-two aligned and therefore LARGER than the light
    // buffer, so the lighting field covers more source pixels than the screen does. Sampling it
    // with raw screen UV stretches the whole radiance field relative to the geometry it lights -
    // invisible when zoomed in, severe when zoomed out. This pins the corrective UV scale.
    [Fact]
    public void LightingFieldUvScale_CompensatesForPackedAlignmentOverhang()
    {
        var lightSize = new Point(720, 450);
        var layout = LightingCascadeLayout.Create(lightSize);

        // Sanity: the packed target really is bigger than the light buffer here.
        Assert.Equal(new Point(768, 512), layout.PackedSize);

        var scale = layout.GetLightingFieldUvScale(lightSize);

        Assert.Equal(720f / 768f, scale.X, 5);
        Assert.Equal(450f / 512f, scale.Y, 5);
        // Must shrink, never expand: the field is the larger of the two spaces.
        Assert.True(scale.X <= 1f && scale.Y <= 1f);
    }

    [Fact]
    public void LightingFieldUvScale_MapsScreenEdgeOntoTheRealLightBufferEdge()
    {
        var lightSize = new Point(720, 450);
        var layout = LightingCascadeLayout.Create(lightSize);
        var scale = layout.GetLightingFieldUvScale(lightSize);

        // Screen UV 1.0 must land on the source pixel the screen actually ends at (720/450),
        // not on the packed extent (768/512).
        Assert.Equal(lightSize.X, scale.X * layout.PackedSize.X, 3);
        Assert.Equal(lightSize.Y, scale.Y * layout.PackedSize.Y, 3);
    }

    [Fact]
    public void LightingFieldUvScale_IsIdentityWhenNoAlignmentPaddingIsNeeded()
    {
        // A light size already on the alignment boundary needs no correction at all.
        var layout = LightingCascadeLayout.Create(new Point(768, 512));
        var scale = layout.GetLightingFieldUvScale(new Point(768, 512));

        Assert.Equal(1f, scale.X, 5);
        Assert.Equal(1f, scale.Y, 5);
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
