using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering.Lighting;
using TriloGame.Game.Shared.Math;
using FrameworkVector2 = Microsoft.Xna.Framework.Vector2;
using NumericsVector2 = System.Numerics.Vector2;
using Interaction = TriloGame.Game.Core.Interaction;

namespace TriloGame.Game.Rendering;

public sealed class WorldSceneRenderer
{
    // Animation name registered on WorldSpriteEffectSystem; resolves to Water1/Water2/Water3.
    public const string WaterAnimationKey = "Water";

    // How far past the edge of a pool the water surface is drawn, in tiles.
    //
    // Water is a layer UNDERNEATH the floor, not a tile that sits beside it, so it does not have to
    // stop where the floor starts - and it must not. Everything that reads the surface reads it at an
    // OFFSET from the pixel being shaded: the composite refracts what is under the surface by up to
    // WaterDistortionUv, and the mask is half resolution, so both reach past the shoreline. Stopping
    // the water exactly at the last water tile means those reads run off the end of it and pull in
    // whatever is next to the pool, which is what shows up as the rim of a pool tearing as the
    // animation steps from one frame to the next. One tile is far more than any of those reads need
    // (the largest is ~11 screen pixels against a tile of 80 or more), and every pixel of it is
    // covered by opaque floor, so the padding is free visually.
    public const int WaterPaddingTiles = 1;

    // A water frame covers exactly ONE tile, at its full 512x512 resolution - the same mapping every
    // other tile texture in the game uses.
    //
    // (Spreading one frame across a 4x4 block of tiles, so the albedo repeated every four tiles
    // instead of every one, is gone. It did break up the repeat and it stayed seamless, but it also
    // spent three quarters of the frame's resolution to do it: a tile was drawn from 128 source
    // pixels rather than 512, which is soft at the default zoom and blocky at the top of the zoom
    // ladder. Breaking the repeat has to come from the surface itself - the wave field, the albedo
    // band, the noise warp, all of which are continuous across the world and owe nothing to the tile
    // grid - or from art that repeats less, not from throwing away the art that exists.)
    private static readonly Color WaterAlbedoColor = new(
        OreLightSettings.WaterAlbedo,
        OreLightSettings.WaterAlbedo,
        OreLightSettings.WaterAlbedo,
        1f);

    // The parallax backdrop is drawn straight to the backbuffer, so no cascade light ever reaches
    // it - it is by definition an unlit surface and belongs at the ambient floor. Drawn at full
    // brightness it showed through unrevealed tiles as the brightest thing on screen, which put a
    // ceiling on how dark the unlit cave could be made: lowering Ambient only widened the gap
    // between dark explored ground and a glaring backdrop.
    private static readonly Color BackgroundAmbientColor = new(
        OreLightSettings.Ambient,
        OreLightSettings.Ambient,
        OreLightSettings.Ambient,
        1f);

    // Alpha carries the water silhouette; the colour channels are reserved for disturbance.
    private static readonly Color WaterMaskSilhouetteColor = new(0f, 0f, 0f, 1f);

    private const float InventoryBackpackIconTileScale = 0.64f;
    private const float InventoryBackpackSlotSpacingPixels = 42f;

    // Neutral fallback for a creature contact shadow with no nearby light to point it away from.

    private static readonly FrameworkVector2[] DroppedResourceOffsets =
    [
        new(-18f, -14f),
        new(0f, -16f),
        new(18f, -10f),
        new(-12f, 12f),
        new(14f, 14f)
    ];

    internal static Color GetCombatHitboxColor() => new(255, 32, 32, 76);

    internal static Color GetMiningStrikeColor() => new(255, 0, 255, 76);

    public void DrawParallaxBackground(RenderingContext context, Rectangle viewport)
    {
        if (!context.Sprites.TryGet("CaveBackground2", out var texture))
        {
            return;
        }

        var camera = context.Camera;
        var scale = MathF.Max(
            viewport.Width / (float)texture.Width,
            viewport.Height / (float)texture.Height) * GameConstants.CaveBackgroundScaleMultiplier;
        var scaledWidth = texture.Width * scale;
        var scaledHeight = texture.Height * scale;
        var parallaxOffset = CalculateParallaxOffset(camera.ParallaxScreenOffset, scaledWidth, scaledHeight);
        var center = camera.ViewCenter + camera.ShakeOffset + parallaxOffset;
        var origin = new FrameworkVector2(texture.Width / 2f, texture.Height / 2f);

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                context.SpriteBatch.Draw(
                    texture,
                    center + new FrameworkVector2(x * scaledWidth, y * scaledHeight),
                    null,
                    BackgroundAmbientColor,
                    0f,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }

    public void DrawWorldLayer(
        RenderingContext context,
        GameSession session,
        WorldSpriteEffectSystem spriteEffects,
        Point viewportSize,
        bool showFullMapVisibility,
        bool showCombatDebug,
        float interpolationAlpha)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        var visibleTiles = new List<Tile>();
        CollectVisibleTiles(cave, context.Camera, viewportSize, showFullMapVisibility, visibleTiles);
        var oreEmitters = new List<OreLightEmitter>();
        new LightingSourceCollector().CollectOreEmitters(visibleTiles, spriteEffects, oreEmitters);
        DrawWorldLayer(
            context,
            session,
            spriteEffects,
            showFullMapVisibility,
            showCombatDebug,
            interpolationAlpha,
            visibleTiles,
            oreEmitters);
    }

    // Render all world layers from one camera cull so lighting and scene pixels share tile coverage.
    public void DrawWorldLayer(
        RenderingContext context,
        GameSession session,
        WorldSpriteEffectSystem spriteEffects,
        bool showFullMapVisibility,
        bool showCombatDebug,
        float interpolationAlpha,
        IReadOnlyList<Tile> visibleTiles,
        IReadOnlyList<OreLightEmitter> oreEmitters,
        LightingTileGrid? tileGrid = null)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        // No water here. The surface is its own layer, drawn into its own target by
        // DrawWaterSceneLayer and composited underneath this one - see the note there. This layer is
        // the LID over it: the floor, and everything standing on the floor.
        DrawFloorTiles(context, visibleTiles);
        DrawTileOverlays(context, visibleTiles, spriteEffects);
        DrawSurfaceFeatures(context, cave, showFullMapVisibility);
        DrawDroppedResources(context, visibleTiles);
        // Cast shadows go down before anything that casts them, so every caster sits on top of its
        // own shadow rather than beside it.
        if (tileGrid is not null)
        {
            DrawCastShadowLayer(context, cave, tileGrid, interpolationAlpha);
        }

        DrawCreatures(context, session, cave, interpolationAlpha, drawBelowBuildings: true);
        DrawBuildings(context, cave, showFullMapVisibility);
        if (showCombatDebug)
        {
            DrawCombatDebug(context, session);
        }
        DrawCreatures(context, session, cave, interpolationAlpha, drawBelowBuildings: false);
        DrawVehicles(context, cave, interpolationAlpha);
        DrawProjectiles(context, session);
    }

    // Every sprite-shaped caster: creatures and buildings. Their texture alpha becomes the shadow
    // silhouette, so the shadow follows the drawn shape rather than a bounding box.
    //
    // Buildings are here as well as in the world tile grid, and the two do different jobs. The grid
    // makes a building block light at any distance, which gives the long soft shadow the ray march
    // resolves at probe spacing. This mask is what the screen-space pass reads, and it is what puts a
    // crisp shadow at the building's base - the detail the probe grid is far too coarse to carry.
    public void DrawEntityOccluderLayer(
        RenderingContext context,
        Cave cave,
        float interpolationAlpha)
    {
        foreach (var trilobite in cave.Trilobites)
        {
            DrawWorldTextureNative(
                context,
                TrilobiteSpriteCatalog.ResolveTextureKey(context.Sprites, trilobite),
                GetCreatureWorldPosition(trilobite, interpolationAlpha),
                trilobite.GetInterpolatedFacingRadians(interpolationAlpha),
                color: Color.White);
        }

        foreach (var enemy in cave.Enemies)
        {
            DrawWorldTextureNative(
                context,
                "Enemy",
                GetCreatureWorldPosition(enemy, interpolationAlpha),
                enemy.GetInterpolatedFacingRadians(interpolationAlpha),
                color: Color.White);
        }

        // Drawn exactly as the scene draws them, so the shadow matches the sprite the player sees -
        // rotation and footprint scaling included.
        foreach (var building in cave.Buildings)
        {
            if (!LightingTileClassifier.GetBuildingOccluderHeight(building).IsFullHeight() ||
                building.Location is null)
            {
                continue;
            }

            DrawBuildingTexture(context, building);
        }
    }

    // Silhouette of open water. Uses a flat quad rather than the animated frame: the surface shader
    // only needs to know WHERE water is, and using the frame would make the whole effect flicker
    // with the tile animation.
    //
    // Drawn black with full alpha, not white. Only the alpha is the silhouette; the colour channels
    // of this target carry local disturbance, added afterwards by DrawWaterDisturbanceLayer, and
    // writing white here would saturate them before that pass ever ran.
    //
    // Padded exactly like the surface itself (WaterPaddingTiles), and for a reason specific to this
    // target: it is HALF resolution. An unpadded silhouette gives the boundary texel partial
    // coverage, so the composite reads the shoreline as "half water" and the surface fades out over
    // the last couple of screen pixels of every pool. Padding puts that ramp a whole tile away,
    // under the floor, so the mask reads solid right up to the edge of the hole. Nothing leaks onto
    // the floor as a result: the composite masks the water terms with the floor layer's own alpha,
    // not with this.
    public void DrawWaterMaskLayer(RenderingContext context, IReadOnlyList<Tile> waterSurfaceTiles)
    {
        if (!context.Sprites.TryGet("empty", out _))
        {
            return;
        }

        foreach (var tile in waterSurfaceTiles)
        {
            DrawTileTexture(context, "empty", tile.Coordinates, 0f, WaterMaskSilhouetteColor);
        }
    }

    // Local disturbance from things moving through the water.
    //
    // Global wave layers alone leave creatures sliding across the surface without touching it, which
    // reads as them hovering over a painting. This marks where the surface is being disturbed.
    //
    // Drawn as a plain radial falloff rather than as rings. The shader derives the rings by feeding
    // the falloff's VALUE to a sine - the value is a monotonic function of distance from the centre,
    // so a sine of it is a set of concentric rings - which means the centre position never has to
    // reach the shader. That is what allows any number of sources instead of however many a uniform
    // array could carry.
    public void DrawWaterDisturbanceLayer(
        RenderingContext context,
        Cave cave,
        Texture2D falloffTexture,
        float interpolationAlpha)
    {
        var origin = new FrameworkVector2(falloffTexture.Width / 2f, falloffTexture.Height / 2f);
        var diameter = TileConstants.TileSize * context.Camera.CurrentScale
            * OreLightSettings.WaterDisturbanceRadiusTiles * 2f;
        var scale = MathF.Max(0.01f, diameter / falloffTexture.Width);
        var color = new Color(
            OreLightSettings.WaterDisturbanceStrength,
            OreLightSettings.WaterDisturbanceStrength,
            OreLightSettings.WaterDisturbanceStrength,
            OreLightSettings.WaterDisturbanceStrength);

        foreach (var trilobite in cave.Trilobites)
        {
            DrawWaterDisturbance(context, cave, falloffTexture, trilobite, interpolationAlpha, origin, scale, color);
        }

        foreach (var enemy in cave.Enemies)
        {
            DrawWaterDisturbance(context, cave, falloffTexture, enemy, interpolationAlpha, origin, scale, color);
        }
    }

    private static void DrawWaterDisturbance(
        RenderingContext context,
        Cave cave,
        Texture2D falloffTexture,
        Creature creature,
        float interpolationAlpha,
        FrameworkVector2 origin,
        float scale,
        Color color)
    {
        var worldPosition = GetCreatureWorldPosition(creature, interpolationAlpha);
        if (!IsOverWater(cave, worldPosition))
        {
            return;
        }

        context.SpriteBatch.Draw(
            falloffTexture,
            context.Camera.WorldToScreen(worldPosition),
            sourceRectangle: null,
            color,
            rotation: 0f,
            origin,
            scale,
            SpriteEffects.None,
            layerDepth: 0f);
    }

    // Resolved through the same world-to-tile rounding the lighting grid and the shader both use, so
    // a creature counts as being over water on exactly the tiles that are drawn as water.
    internal static bool IsOverWater(Cave cave, FrameworkVector2 worldPosition)
    {
        var tile = cave.GetTile(new GridPoint(
            LightingTileGridLayout.ToTileCoordinate(worldPosition.X),
            LightingTileGridLayout.ToTileCoordinate(worldPosition.Y)));
        return tile is not null && tile.IsWater();
    }

    // Full-height casters go in their own mask so the shader can occlude with them at any
    // distance, while the short mask above only shades what is close behind its casters.
    // (DrawTallOccluderLayer is gone. Terrain blockers come from the world-space tile grid, and
    // building silhouettes moved into DrawEntityOccluderLayer above so they cast the same
    // screen-space shadow every other sprite caster does.)

    // Cast shadows: each caster's silhouette drawn ONCE, stretched away from the light.
    //
    // This replaced a per-pixel march that swept the occluder mask along the light direction and took
    // the maximum. That sweep is the right shape in principle - the union of a silhouette dragged
    // along a line IS an elongated silhouette - but sampled at discrete steps it comes out as a row of
    // overlapping copies. Anything thinner than the step, which is every leg and antenna, repeats
    // visibly instead of smearing, and because the direction was sampled per pixel the whole thing
    // curved. Neither is tunable: they are what marching a hard-edged silhouette looks like.
    //
    // Projecting the sprite once gives a single coherent shape, keeps thin features intact, costs one
    // draw per caster instead of sixteen taps per screen pixel, and is what a cast shadow actually is.
    public void DrawCastShadowLayer(
        RenderingContext context,
        Cave cave,
        LightingTileGrid tileGrid,
        float interpolationAlpha)
    {
        var cameraScale = new FrameworkVector2(context.Camera.CurrentScale);

        foreach (var trilobite in cave.Trilobites)
        {
            // The shadow is extruded from the sprite's own alpha, so it has to be the SAME sprite the
            // trilobite is drawn with - a farmer's silhouette is not the unassigned one's.
            if (context.Sprites.TryGet(
                    TrilobiteSpriteCatalog.ResolveTextureKey(context.Sprites, trilobite),
                    out var texture))
            {
                DrawProjectedShadow(
                    context,
                    texture,
                    tileGrid,
                    GetCreatureWorldPosition(trilobite, interpolationAlpha),
                    trilobite.GetInterpolatedFacingRadians(interpolationAlpha),
                    cameraScale,
                    OreLightSettings.CreatureShadowHeightTiles,
                    OreLightSettings.CreatureShadowMinLengthTiles,
                    OreLightSettings.CreatureShadowMaxLengthTiles);
            }
        }

        foreach (var enemy in cave.Enemies)
        {
            if (context.Sprites.TryGet("Enemy", out var texture))
            {
                DrawProjectedShadow(
                    context,
                    texture,
                    tileGrid,
                    GetCreatureWorldPosition(enemy, interpolationAlpha),
                    enemy.GetInterpolatedFacingRadians(interpolationAlpha),
                    cameraScale,
                    OreLightSettings.CreatureShadowHeightTiles,
                    OreLightSettings.CreatureShadowMinLengthTiles,
                    OreLightSettings.CreatureShadowMaxLengthTiles);
            }
        }

        // Buildings cast the same way, keeping their own display rotation and footprint scale so the
        // shadow matches the shape the player sees.
        foreach (var building in cave.Buildings)
        {
            if (!LightingTileClassifier.GetBuildingOccluderHeight(building).IsFullHeight() ||
                building.Location is null ||
                !context.Sprites.TryGet(building.TextureKey, out var texture))
            {
                continue;
            }

            DrawProjectedShadow(
                context,
                texture,
                tileGrid,
                BuildingPlacementGrid.GetWorldCenter(building),
                building.GetDisplayRotationTurns() * MathF.PI / 2f,
                BuildingPlacementGrid.GetTextureFootprintScale(
                    building, texture.Width, texture.Height, context.Camera.CurrentScale),
                OreLightSettings.BuildingShadowHeightTiles,
                OreLightSettings.BuildingShadowMinLengthTiles,
                OreLightSettings.BuildingShadowMaxLengthTiles);
        }
    }

    // One caster's shadow: its silhouette extruded along the light, keeping the caster's own
    // orientation and growing softer with distance.
    //
    // Extruding by repeated draws rather than rotating one stretched copy is what keeps the shape
    // honest. A cast shadow's outline comes from the caster, so it has to stay in the caster's
    // rotation - a trilobite's shadow pointing one way while its body points another reads as an
    // obvious error, and a rectangular building rotated to face the light reads as a different
    // building. Stretching along an arbitrary world axis while holding rotation is a shear, which
    // SpriteBatch cannot express, so the extrusion does it instead: the union of a silhouette dragged
    // along a line IS that silhouette elongated.
    //
    // Each step also grows slightly and fades. That is the penumbra - a shadow really does soften
    // with distance from its caster - and it is also what stops thin features (legs, antennae) from
    // appearing as a row of separate dashes. Discrete steps can only merge if each is wider than the
    // gap to the next, which the growth guarantees and a constant-size extrusion does not.
    // How long a shadow a caster of this height throws from a light this far away.
    //
    // Similar triangles: the light sits at height h, the caster's top at H, so the ray grazing the
    // caster meets the ground at H*d/(h-H) past it. Zero directly beneath the light, growing with
    // distance, and capped because the expression diverges as H approaches h.
    //
    // The bounds come from the caller rather than from shared constants: how short a shadow can get
    // before the caster looks unplaced, and how long before it stops looking attached, both depend on
    // how big the caster is - and a trilobite and a building are not the same size. See
    // CreatureShadowMinLengthTiles.
    internal static float GetShadowLengthWorld(
        float casterHeightTiles,
        float lightDistanceWorld,
        float minLengthTiles,
        float maxLengthTiles)
    {
        var tile = TileConstants.TileSize;
        var casterHeight = casterHeightTiles * tile;
        var rise = MathF.Max((OreLightSettings.ShadowLightHeightTiles * tile) - casterHeight, tile * 0.05f);
        var modelled = casterHeight * lightDistanceWorld / rise;
        // Clamped, not scaled: the light's DIRECTION still comes from the geometry, so a shadow
        // pinned at either bound still points the right way - it has only stopped reporting distance.
        return Math.Clamp(modelled, minLengthTiles * tile, MathF.Max(minLengthTiles, maxLengthTiles) * tile);
    }

    // How dark a caster's shadow is at this distance from the light dominating it: full strength
    // close in, tapering to nothing by ShadowFadeDistanceTiles.
    //
    // lightDistance is already inverse-square weighted across every emitter in range (see
    // LightingTileGrid.TryGetLightDirection), so this tracks whichever deposit is actually doing the
    // lighting rather than the average of everything nearby - which is what makes walking up to one
    // deposit past another darken the shadow instead of averaging the two out.
    internal static float GetShadowProximityStrength(float lightDistanceWorld)
    {
        var tiles = lightDistanceWorld / TileConstants.TileSize;
        var near = OreLightSettings.ShadowFullStrengthDistanceTiles;
        var far = MathF.Max(near + 0.001f, OreLightSettings.ShadowFadeDistanceTiles);
        return Smoothstep(Math.Clamp((far - tiles) / (far - near), 0f, 1f));
    }

    // How much of a shadow survives the light arriving from other angles. 1 when every contribution
    // agrees on a direction, falling to 0 at CreatureShadowDirectionality, below which light is
    // effectively ambient and casts nothing.
    internal static float GetShadowDirectionalStrength(float directionality)
    {
        var threshold = Math.Clamp(OreLightSettings.CreatureShadowDirectionality, 0f, 0.999f);
        return Smoothstep(Math.Clamp((directionality - threshold) / (1f - threshold), 0f, 1f));
    }

    // Matches the tapers in RadianceCascade.fx, so a shadow fades on the same curve its light does.
    private static float Smoothstep(float t) => t * t * (3f - (2f * t));

    private static void DrawProjectedShadow(
        RenderingContext context,
        Texture2D texture,
        LightingTileGrid tileGrid,
        FrameworkVector2 worldPosition,
        float rotation,
        FrameworkVector2 baseScale,
        float casterHeightTiles,
        float minLengthTiles,
        float maxLengthTiles)
    {
        if (!tileGrid.TryGetLightDirection(
                worldPosition,
                OreLightSettings.CreatureShadowLightRadiusTiles,
                out var toLight,
                out var directionality,
                out var lightDistance))
        {
            return;
        }

        // Two independent reasons a shadow is weak, multiplied together.
        //
        // Proximity: a shadow is the light a caster is keeping off the floor, so it darkens as the
        // caster approaches the light and fades as it moves away. This is where the distance response
        // lives - see ShadowFullStrengthDistanceTiles for why it is not in the length.
        //
        // Directionality: anisotropy near zero means light is arriving from every side at once, so
        // there is no direction to cast along and nothing to block. Fade out rather than pick one -
        // which is precisely the failure the old hardcoded "straight down" fallback produced - and
        // fade CONTINUOUSLY, so light from a second angle visibly brightens the shadow instead of
        // having no effect until it has nearly cancelled the first.
        var strength = GetShadowProximityStrength(lightDistance)
            * GetShadowDirectionalStrength(directionality);
        if (strength <= 0.01f)
        {
            return;
        }

        var away = -toLight;
        var length = GetShadowLengthWorld(casterHeightTiles, lightDistance, minLengthTiles, maxLengthTiles);
        var lengthTiles = length / TileConstants.TileSize;

        // Step COUNT follows the length, so the SPACING stays fixed.
        //
        // A fixed count is what produced the repeats: six steps over a 1.1 tile shadow put the copies
        // 113 world units apart, while the spread only widened a leg by about 23, so thin features
        // landed as separate dashes with clear gaps. Holding the spacing at roughly a leg's width
        // instead means a longer shadow spends more draws rather than stretching the gaps, and every
        // feature overlaps its own neighbour whatever the length turns out to be.
        var steps = Math.Clamp((int)MathF.Ceiling(length / OreLightSettings.ShadowStepWorld) + 1, 3, 40);

        // Per-copy alpha is SOLVED, not divided.
        //
        // Alpha compositing accumulates as 1 - (1 - a)^n, so reaching a target opacity S through n
        // overlapping copies needs a = 1 - (1 - S)^(1/n). Dividing the target by the step count
        // instead - which is what this did - makes every copy n times too light, because blended
        // copies at different positions do not average: each simply darkens by its own alpha. The
        // shadow came out at under a tenth of its intended strength, and against a dim cave floor
        // that reads as no shadow at all.
        //
        // It also means the taper is free. Near the caster every copy overlaps and the stack reaches
        // full strength; at the tip only one copy lands, so it fades out on its own.
        var perCopy = 1f - MathF.Pow(1f - OreLightSettings.CreatureShadowStrength, 1f / steps);

        // Back to front, so the darkest part sits nearest the caster once alpha-blended.
        for (var step = steps - 1; step >= 0; step--)
        {
            var t = step / (float)(steps - 1);
            // Penumbra widens with how far this copy has travelled from the caster, in real tiles -
            // so a short shadow stays tight and a long one goes soft, rather than both widening by
            // the same fraction of the sprite.
            var spread = 1f + (OreLightSettings.ShadowSpreadPerTile * lengthTiles * t);
            var alpha = perCopy * strength * (1f - (0.5f * t));

            DrawWorldTextureNative(
                context,
                texture,
                worldPosition + (away * length * t),
                rotation,
                color: Color.Black * alpha,
                scale: baseScale * spread);
        }
    }

    public void DrawEmissiveLayer(
        RenderingContext context,
        IReadOnlyList<OreLightEmitter> emitters,
        Texture2D lightTexture,
        IReadOnlyList<BuildingLightEmitter>? buildingEmitters = null)
    {
        var origin = new FrameworkVector2(lightTexture.Width / 2f, lightTexture.Height / 2f);
        for (var index = 0; index < emitters.Count; index++)
        {
            var emitter = emitters[index];
            // Each deposit's own colour, so the direct glow matches the cascade light it seeds.
            DrawEmissiveHalo(
                context,
                lightTexture,
                origin,
                emitter.WorldPosition,
                emitter.LightColor,
                emitter.Intensity,
                OreLightSettings.OreRadiusTiles);
        }

        if (buildingEmitters is null)
        {
            return;
        }

        for (var index = 0; index < buildingEmitters.Count; index++)
        {
            var emitter = buildingEmitters[index];
            // Radius comes off the emitter rather than a shared constant: a campfire's glow sits on
            // the fire, a mining post's spreads over the whole post.
            DrawEmissiveHalo(
                context,
                lightTexture,
                origin,
                emitter.WorldPosition,
                emitter.LightColor,
                emitter.Intensity,
                emitter.RadiusTiles);
        }
    }

    private static void DrawEmissiveHalo(
        RenderingContext context,
        Texture2D lightTexture,
        FrameworkVector2 origin,
        FrameworkVector2 worldPosition,
        Color color,
        float intensity,
        float radiusTiles)
    {
        var center = context.Camera.WorldToScreen(worldPosition);
        var lightColor = color.ToVector4();
        lightColor *= Math.Clamp(intensity, 0f, 1f);
        lightColor.W = 1f;
        var diameter = TileConstants.TileSize * context.Camera.CurrentScale * radiusTiles * 2f;
        var scale = Math.Max(0.01f, diameter / lightTexture.Width);
        context.SpriteBatch.Draw(
            lightTexture,
            center,
            sourceRectangle: null,
            new Color(lightColor),
            rotation: 0f,
            origin,
            scale,
            SpriteEffects.None,
            layerDepth: 0f);
    }

    // The water surface, as its own layer.
    //
    // It is drawn into its own render target rather than into the scene, and the composite puts it
    // UNDER the scene using the scene's alpha as the lid. That ordering is what the layer is for:
    // water is below the floor, so everything the surface does - its texture, the albedo band, the
    // sheen, the specular, the refraction of what lies under it - has to be finished before the
    // floor is laid over the top, exactly as a real floor with holes in it would show a pool
    // beneath. Drawing water into the scene and then shading the result in screen space cannot do
    // that: the shader has no way to tell a pixel of pool from a pixel of the floor beside it, so
    // the sheen ends up sitting ON the floor around every hole and the refraction drags the floor's
    // own pixels into the water.
    //
    // Padded past the pool edge; see WaterPaddingTiles.
    public void DrawWaterSceneLayer(
        RenderingContext context,
        IReadOnlyList<Tile> waterSurfaceTiles,
        WorldSpriteEffectSystem spriteEffects)
    {
        foreach (var tile in waterSurfaceTiles)
        {
            // Every water tile shares one animation phase, so the whole surface steps through its
            // frames together. GetWorldSpritePhaseOffsetSeconds returns 0 for water deliberately -
            // see the note there.
            var textureKey = spriteEffects.GetAnimatedTextureKey(
                WaterAnimationKey,
                GetWorldSpritePhaseOffsetSeconds(WaterAnimationKey, tile.Coordinates));
            if (context.Sprites.TryGet(textureKey, out _))
            {
                // Dimmed to its albedo: water is partly a mirror, so its own texture should stay
                // subdued and the reflection and albedo banding in the composite should carry it.
                DrawTileTexture(context, textureKey, tile.Coordinates, 0f, WaterAlbedoColor);
            }
        }
    }

    // Which tiles the water surface covers this frame: every visible water tile, plus its padding
    // ring. Collected once and handed to both consumers - the surface layer and the mask - because
    // the two have to agree on where the water is down to the tile, and because deciding it costs a
    // neighbourhood scan per visible tile that there is no reason to pay for twice.
    public static void CollectWaterSurfaceTiles(
        Cave cave,
        IReadOnlyList<Tile> visibleTiles,
        List<Tile> destination)
    {
        destination.Clear();
        foreach (var tile in visibleTiles)
        {
            if (ShouldDrawWaterTile(cave, tile))
            {
                destination.Add(tile);
            }
        }
    }

    // A water tile, or any tile within WaterPaddingTiles of one. The padding tiles are covered by
    // opaque floor (or rock), which is what makes drawing water under them free: a floor hole is
    // carved out of covered floor by definition, so every neighbour of a water tile has a lid.
    //
    // Only ever asked about tiles that are already being rendered, so the padding cannot reach into
    // unexplored ground and show a pool the player has not found.
    internal static bool ShouldDrawWaterTile(Cave cave, Tile tile)
    {
        if (tile.IsWater())
        {
            return true;
        }

        var origin = tile.Coordinates;
        for (var y = -WaterPaddingTiles; y <= WaterPaddingTiles; y++)
        {
            for (var x = -WaterPaddingTiles; x <= WaterPaddingTiles; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                var neighbour = cave.GetTile(new GridPoint(origin.X + x, origin.Y + y));
                if (neighbour is not null && neighbour.IsWater())
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void DrawFloorTiles(RenderingContext context, IEnumerable<Tile> visibleTiles)
    {
        foreach (var tile in visibleTiles)
        {
            if (ShouldDrawFloorTile(tile))
            {
                DrawTileTexture(context, "empty", tile.Coordinates);
            }
        }
    }

    private static void DrawTileOverlays(RenderingContext context, IEnumerable<Tile> visibleTiles, WorldSpriteEffectSystem spriteEffects)
    {
        foreach (var tile in visibleTiles)
        {
            if (tile.Base == "wall")
            {
                DrawTileTexture(context, "wall", tile.Coordinates);
            }
            else if (GetTileOverlayTextureKey(tile) is { } textureKey)
            {
                DrawTileTexture(
                    context,
                    textureKey,
                    tile.Coordinates,
                    GetTileOverlayRotationRadians(tile),
                    GetTileDrawColor(spriteEffects, tile, textureKey, tile.Coordinates));
            }
        }
    }

    private static void DrawSurfaceFeatures(RenderingContext context, Cave cave, bool showFullMapVisibility)
    {
        foreach (var antHole in cave.GetAntHoles())
        {
            var tile = cave.GetTile(antHole.TileKey);
            if (!ShouldRenderTile(cave, tile, showFullMapVisibility))
            {
                continue;
            }

            var visibleTile = tile!;

            DrawWorldTextureNative(
                context,
                "AntHole",
                new FrameworkVector2(visibleTile.Coordinates.X * TileConstants.TileSize, visibleTile.Coordinates.Y * TileConstants.TileSize));
        }
    }

    private static void DrawDroppedResources(RenderingContext context, IEnumerable<Tile> visibleTiles)
    {
        foreach (var tile in visibleTiles)
        {
            var droppedSandstone = tile.GetDroppedResourceCount(OreType.SANDSTONE.Name);
            if (droppedSandstone <= 0)
            {
                continue;
            }

            var worldCenter = new FrameworkVector2(tile.Coordinates.X * TileConstants.TileSize, tile.Coordinates.Y * TileConstants.TileSize);
            var spriteCount = Math.Min(droppedSandstone, DroppedResourceOffsets.Length);
            for (var index = 0; index < spriteCount; index++)
            {
                DrawWorldTextureNative(
                    context,
                    "wall",
                    worldCenter + DroppedResourceOffsets[index],
                    color: new Color(255, 255, 255, 230),
                    scale: new FrameworkVector2(GameConstants.WallDropSpriteScale * context.Camera.CurrentScale));
            }
        }
    }

    private static void DrawBuildings(RenderingContext context, Cave cave, bool showFullMapVisibility)
    {
        foreach (var building in cave.Buildings)
        {
            if (building is Scaffolding scaffold)
            {
                foreach (var tilePoint in BuildingHighlightFootprint.EnumerateTiles(scaffold))
                {
                    var tile = cave.GetTile(tilePoint);
                    if (!ShouldRenderTile(cave, tile, showFullMapVisibility))
                    {
                        continue;
                    }

                    DrawWorldTextureNative(
                        context,
                        "Scaffold",
                        new FrameworkVector2(tilePoint.X * TileConstants.TileSize, tilePoint.Y * TileConstants.TileSize));
                }

                continue;
            }

            if (building is SoilPatch soilPatch)
            {
                DrawSoilPatch(context, soilPatch);
                continue;
            }

            if (building.Location is null)
            {
                continue;
            }

            DrawBuildingTexture(context, building);
        }
    }

    private static void DrawBuildingTexture(RenderingContext context, Building building)
    {
        if (!context.Sprites.TryGet(building.TextureKey, out var texture))
        {
            return;
        }

        DrawWorldTextureNative(
            context,
            texture,
            BuildingPlacementGrid.GetWorldCenter(building),
            building.GetDisplayRotationTurns() * MathF.PI / 2f,
            scale: BuildingPlacementGrid.GetTextureFootprintScale(
                building,
                texture.Width,
                texture.Height,
                context.Camera.CurrentScale));
    }

    private static void DrawSoilPatch(RenderingContext context, SoilPatch soilPatch)
    {
        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            var soilTile = soilPatch.SoilTiles[index];
            if (soilTile.WorldLocation is not { } worldLocation)
            {
                continue;
            }

            DrawWorldTextureNative(
                context,
                soilTile.TextureKey,
                new FrameworkVector2(worldLocation.X * TileConstants.TileSize, worldLocation.Y * TileConstants.TileSize));
        }
    }

    private static void DrawVehicles(RenderingContext context, Cave cave, float interpolationAlpha)
    {
        foreach (var vehicle in cave.GetVehicles())
        {
            if (vehicle.Location is null)
            {
                continue;
            }

            DrawWorldTextureNative(
                context,
                vehicle.TextureKey,
                ToFrameworkVector(vehicle.GetInterpolatedWorldCenter(interpolationAlpha)),
                vehicle.GetInterpolatedRotationRadians(interpolationAlpha),
                GetPlacedVehicleOrigin(vehicle));
        }
    }

    private static void DrawCreatures(
        RenderingContext context,
        GameSession session,
        Cave cave,
        float interpolationAlpha,
        bool drawBelowBuildings)
    {
        foreach (var trilobite in cave.Trilobites)
        {
            if (!trilobite.IsVisible || trilobite.DrawBelowBuildings != drawBelowBuildings)
            {
                continue;
            }

            var worldPosition = GetCreatureWorldPosition(trilobite, interpolationAlpha);
            var facingRadians = trilobite.GetInterpolatedFacingRadians(interpolationAlpha);
            DrawWorldTextureNative(
                context,
                TrilobiteSpriteCatalog.ResolveTextureKey(context.Sprites, trilobite),
                worldPosition,
                facingRadians,
                color: GetCreatureDrawColor(session, trilobite));

            DrawTrilobiteInventoryBackpack(context, trilobite, worldPosition, facingRadians);
        }

        foreach (var enemy in cave.Enemies)
        {
            if (!enemy.IsVisible || enemy.DrawBelowBuildings != drawBelowBuildings)
            {
                continue;
            }

            var worldPosition = GetCreatureWorldPosition(enemy, interpolationAlpha);
            var facingRadians = enemy.GetInterpolatedFacingRadians(interpolationAlpha);
            DrawWorldTextureNative(
                context,
                "Enemy",
                worldPosition,
                facingRadians,
                color: GetCreatureDrawColor(session, enemy));
        }
    }

    // (DrawCreatureContactShadow and GetContactShadowDirection are gone. Creature shadows are a
    // screen-space pass in the composite now - see GetCreatureShadow in RadianceCascade.fx - which
    // takes its direction from the lighting field's gradient rather than from a radius search of the
    // visible emitter list, and finds the silhouette in the occluder mask at screen resolution rather
    // than translating a copy of the sprite. See OreLightSettings.CreatureShadowStrength.)

    private static Color GetCreatureDrawColor(GameSession session, Creature creature)
    {
        var flash = session.Runtime.GetDamageFlashAlpha(creature.Id);
        return GetCreatureDamageColor(flash);
    }

    internal static Color GetCreatureDamageColor(float flash) => flash <= 0f
        ? Color.White
        : Color.Lerp(Color.White, Color.Red, Math.Clamp(flash, 0f, 1f));

    internal static string? GetInventoryBackpackTextureKey(Trilobite trilobite)
    {
        return GetInventoryBackpackTextureKey(trilobite, slotIndex: 0);
    }

    internal static string? GetInventoryBackpackTextureKey(Trilobite trilobite, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= trilobite.Inventory.Amount)
        {
            return null;
        }

        var remainingSlotIndex = slotIndex;
        for (var resourceIndex = 0; resourceIndex < trilobite.Inventory.ResourceTypeCount; resourceIndex++)
        {
            var resourceType = trilobite.Inventory.GetResourceTypeAt(resourceIndex);
            var amount = trilobite.Inventory.GetAmount(resourceType);
            if (remainingSlotIndex < amount)
            {
                return ItemCatalog.GetTextureKey(resourceType);
            }

            remainingSlotIndex -= amount;
        }

        return null;
    }

    // Anchor carried-item slots to a stable top-to-bottom column on the shell.
    internal static FrameworkVector2 GetInventoryBackpackWorldPosition(FrameworkVector2 creatureWorldPosition, float facingRadians)
    {
        _ = facingRadians;
        return creatureWorldPosition;
    }

    internal static float GetInventoryBackpackIconRotationRadians(float facingRadians) => facingRadians;

    internal static FrameworkVector2 GetInventoryBackpackSlotWorldPosition(
        FrameworkVector2 creatureWorldPosition,
        float facingRadians,
        int slotIndex,
        int slotCapacity)
    {
        var center = GetInventoryBackpackWorldPosition(creatureWorldPosition, facingRadians);
        if (slotCapacity <= 1)
        {
            return center;
        }

        var down = new FrameworkVector2(-MathF.Sin(facingRadians), MathF.Cos(facingRadians));
        var topAnchoredIndex = slotIndex - ((slotCapacity - 1) / 2f);
        return center + (down * topAnchoredIndex * InventoryBackpackSlotSpacingPixels);
    }

    private static void DrawTrilobiteInventoryBackpack(
        RenderingContext context,
        Trilobite trilobite,
        FrameworkVector2 creatureWorldPosition,
        float facingRadians)
    {
        var slotCount = trilobite.Inventory.Amount;
        var slotCapacity = trilobite.InventoryCapacity;
        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var textureKey = GetInventoryBackpackTextureKey(trilobite, slotIndex);
            if (textureKey is null || !context.Sprites.TryGet(textureKey, out _))
            {
                continue;
            }

            DrawWorldTextureNative(
                context,
                textureKey,
                GetInventoryBackpackSlotWorldPosition(creatureWorldPosition, facingRadians, slotIndex, slotCapacity),
                GetInventoryBackpackIconRotationRadians(facingRadians),
                scale: new FrameworkVector2(InventoryBackpackIconTileScale * context.Camera.CurrentScale));
        }
    }

    private static void DrawCombatDebug(RenderingContext context, GameSession session)
    {
        var hitboxes = session.Combat.ActiveHitboxes;
        for (var index = 0; index < hitboxes.Count; index++)
        {
            DrawCombatShape(context, hitboxes[index].Shape, new Color(255, 32, 32, 76));
        }

        var hurtboxes = session.Combat.Hurtboxes;
        for (var index = 0; index < hurtboxes.Count; index++)
        {
            DrawCombatShape(context, hurtboxes[index].Shape, new Color(32, 128, 255, 56));
        }

        var mining = session.Mining.Active;
        for (var index = 0; index < mining.Count; index++)
        {
            DrawHurtboxCircle(context, mining[index].Center, mining[index].Radius, new Color(255, 0, 255, 76));
        }
    }

    private static void DrawCombatShape(RenderingContext context, CombatShape shape, Color color)
    {
        if (shape.Kind == CombatShapeKind.Circle)
        {
            DrawHurtboxCircle(context, shape.First, shape.Radius, color);
            return;
        }

        DrawHurtboxRectangle(context, shape.GetBounds(), color);
    }

    private static void DrawHurtboxRectangle(RenderingContext context, Interaction.WorldRectangle bounds, Color color)
    {
        var topLeftWorld = new NumericsVector2(
            bounds.X / (float)WorldUnits.UnitsPerPixel,
            bounds.Y / (float)WorldUnits.UnitsPerPixel);
        var topLeft = context.Camera.WorldToScreen(ToFrameworkVector(topLeftWorld));
        var width = Math.Max(1, (int)MathF.Round(
            (bounds.Width / (float)WorldUnits.UnitsPerPixel) * context.Camera.CurrentScale));
        var height = Math.Max(1, (int)MathF.Round(
            (bounds.Height / (float)WorldUnits.UnitsPerPixel) * context.Camera.CurrentScale));
        context.SpriteBatch.Draw(
            context.WhitePixel,
            new Rectangle((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y), width, height),
            color);
    }

    private static void DrawHurtboxCircle(RenderingContext context, WorldPoint center, int radius, Color color)
    {
        const int strips = 20;
        var centerScreen = context.Camera.WorldToScreen(ToFrameworkVector(center.ToWorldPixels()));
        var screenRadius = Math.Max(1f, (radius / (float)WorldUnits.UnitsPerPixel) * context.Camera.CurrentScale);
        for (var strip = -strips; strip <= strips; strip++)
        {
            var normalizedY = strip / (float)strips;
            var halfWidth = screenRadius * MathF.Sqrt(MathF.Max(0f, 1f - (normalizedY * normalizedY)));
            var y = centerScreen.Y + (normalizedY * screenRadius);
            context.SpriteBatch.Draw(
                context.WhitePixel,
                new Rectangle(
                    (int)MathF.Round(centerScreen.X - halfWidth),
                    (int)MathF.Round(y),
                    Math.Max(1, (int)MathF.Round(halfWidth * 2f)),
                    Math.Max(1, (int)MathF.Ceiling((screenRadius * 2f) / strips))),
                color);
        }
    }

    private static void DrawProjectiles(RenderingContext context, GameSession session)
    {
        foreach (var projectileFlight in session.Runtime.ActiveProjectileFlights)
        {
            var textureKey = context.Sprites.TryGet(projectileFlight.Projectile.SpriteKey, out _)
                ? projectileFlight.Projectile.SpriteKey
                : "wall";
            var worldPosition = ToFrameworkVector(projectileFlight.CurrentWorldPosition);
            var normalizedWorldPosition = worldPosition / TileConstants.TileSize;
            var projectileScale = new FrameworkVector2(projectileFlight.Projectile.SpriteScale);
            DrawWorldTexture(
                context,
                textureKey,
                normalizedWorldPosition,
                MathHelper.ToRadians(projectileFlight.AngleDegrees),
                projectileScale);
        }
    }

    internal static Color GetTileDrawColor(WorldSpriteEffectSystem spriteEffects, Tile tile, string textureKey, GridPoint coordinates)
    {
        if (!tile.IsOreTile())
        {
            return Color.White;
        }

        var clampedYield = Math.Clamp(tile.ResourceYield, GameConstants.DarkestOreYield, GameConstants.MaxOreYield);
        var yieldRange = Math.Max(1, GameConstants.MaxOreYield - GameConstants.DarkestOreYield);
        var normalized = (clampedYield - GameConstants.DarkestOreYield) / (float)yieldRange;
        var brightness = 1f - (GameConstants.MaxOreDarkness * (1f - normalized));
        brightness = Math.Clamp(brightness, 1f - GameConstants.MaxOreDarkness, 1f);
        return spriteEffects.ApplyColor(
            textureKey,
            new Color(brightness, brightness, brightness, 1f),
            GetWorldSpritePhaseOffsetSeconds(textureKey, coordinates));
    }

    // Ore deposits render by their tile base so every legacy ore texture can resolve directly by name.
    internal static string? GetTileOverlayTextureKey(Tile tile)
    {
        if (tile.IsCaveCrystal())
        {
            return Tile.CaveCrystalBase;
        }

        return tile.IsOreTile() ? tile.Base : null;
    }

    // Per-tile animation phase, so a field of animated tiles does not step through its frames in
    // unison. Lumenite only.
    //
    // WATER IS DELIBERATELY EXCLUDED and gets 0 back, so a lake changes frame as one surface rather
    // than as a field of independently flickering tiles. Offsetting it per tile was tried and is
    // worse: the frames differ enough that neighbouring tiles stop lining up, so the tile grid
    // itself becomes visible in the water. A whole lake moving together reads as one body; a lake
    // where every tile animates on its own reads as a checkerboard.
    //
    // Everything else is a static sprite, where a phase offset would do nothing either way.
    internal static float GetWorldSpritePhaseOffsetSeconds(string textureKey, GridPoint coordinates)
    {
        if (!string.Equals(textureKey, OreType.LUMENITE.Name, StringComparison.Ordinal))
        {
            return 0f;
        }

        var hash = (coordinates.X * 73856093) ^ (coordinates.Y * 19349663);
        hash &= 0x7fffffff;
        return (hash % 1000) / 1000f;
    }

    internal static bool IsLightBlockingTile(Tile tile)
    {
        return LightingTileClassifier.BlocksLight(tile);
    }

    internal static float GetTileOverlayRotationRadians(Tile tile)
    {
        if (!tile.IsOreTile() && !tile.IsCaveCrystal())
        {
            return 0f;
        }

        return tile.OreRotationQuarterTurns * MathF.PI / 2f;
    }

    internal static bool ShouldDrawFloorTile(Tile tile)
    {
        return tile.HasFloorCover || tile.Built is not null;
    }

    // Mirror the world-gen-tests culling pass by scanning only tiles inside the camera footprint.
    // paddingTiles is how far past the camera footprint to reach. The default of 2 is the drawing
    // margin - just enough that a tile straddling the screen edge still gets rasterised. Lighting
    // passes a much larger value, because a light source only has to be within the ray march's reach
    // to affect what is on screen, not within the screen itself.
    internal static IEnumerable<Tile> EnumerateVisibleTiles(
        Cave cave,
        CameraController camera,
        Point viewportSize,
        bool showFullMapVisibility,
        int paddingTiles = 2)
    {
        camera.GetVisibleWorldBounds(viewportSize, out var topLeft, out var bottomRight);

        var minWorldX = MathF.Min(topLeft.X, bottomRight.X);
        var minWorldY = MathF.Min(topLeft.Y, bottomRight.Y);
        var maxWorldX = MathF.Max(topLeft.X, bottomRight.X);
        var maxWorldY = MathF.Max(topLeft.Y, bottomRight.Y);

        var minTileX = (int)MathF.Floor((minWorldX - TileConstants.TileHalfSize) / TileConstants.TileSize) - paddingTiles;
        var minTileY = (int)MathF.Floor((minWorldY - TileConstants.TileHalfSize) / TileConstants.TileSize) - paddingTiles;
        var maxTileX = (int)MathF.Ceiling((maxWorldX + TileConstants.TileHalfSize) / TileConstants.TileSize) + paddingTiles;
        var maxTileY = (int)MathF.Ceiling((maxWorldY + TileConstants.TileHalfSize) / TileConstants.TileSize) + paddingTiles;

        for (var y = minTileY; y <= maxTileY; y++)
        {
            for (var x = minTileX; x <= maxTileX; x++)
            {
                var tile = cave.GetTile(new GridPoint(x, y));
                if (!ShouldRenderTile(cave, tile, showFullMapVisibility))
                {
                    continue;
                }

                yield return tile!;
            }
        }
    }

    public static void CollectVisibleTiles(
        Cave cave,
        CameraController camera,
        Point viewportSize,
        bool showFullMapVisibility,
        List<Tile> destination,
        int paddingTiles = 2)
    {
        destination.Clear();
        foreach (var tile in EnumerateVisibleTiles(cave, camera, viewportSize, showFullMapVisibility, paddingTiles))
        {
            destination.Add(tile);
        }
    }

    internal static bool ShouldRenderTile(Cave cave, Tile? tile, bool showFullMapVisibility)
    {
        return tile is not null && (showFullMapVisibility || cave.IsTileRevealed(tile));
    }

    internal static float NormalizeParallaxOffset(float value, float period)
    {
        if (period <= 0f)
        {
            return 0f;
        }

        var wrapped = value % period;
        if (wrapped > period / 2f)
        {
            wrapped -= period;
        }
        else if (wrapped < -(period / 2f))
        {
            wrapped += period;
        }

        return wrapped;
    }

    internal static FrameworkVector2 CalculateParallaxOffset(FrameworkVector2 parallaxScreenOffset, float periodWidth, float periodHeight)
    {
        return new FrameworkVector2(
            NormalizeParallaxOffset(parallaxScreenOffset.X * GameConstants.CaveBackgroundParallaxFactor, periodWidth),
            NormalizeParallaxOffset(parallaxScreenOffset.Y * GameConstants.CaveBackgroundParallaxFactor, periodHeight));
    }

    private static void DrawWorldTexture(RenderingContext context, string textureKey, GridPoint point, float rotation, FrameworkVector2 sizeScale, Color? color = null)
    {
        DrawWorldTexture(context, textureKey, point.ToVector2(), rotation, sizeScale, color);
    }

    private static void DrawWorldTexture(RenderingContext context, string textureKey, FrameworkVector2 gridPoint, float rotation, FrameworkVector2 sizeScale, Color? color = null)
    {
        if (!context.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        var world = new FrameworkVector2(gridPoint.X * TileConstants.TileSize, gridPoint.Y * TileConstants.TileSize);
        var scale = new FrameworkVector2(
            (TileConstants.TileSize * sizeScale.X * context.Camera.CurrentScale) / texture.Width,
            (TileConstants.TileSize * sizeScale.Y * context.Camera.CurrentScale) / texture.Height);

        context.SpriteBatch.Draw(
            texture,
            context.Camera.WorldToScreen(world),
            null,
            color ?? Color.White,
            rotation,
            new FrameworkVector2(texture.Width / 2f, texture.Height / 2f),
            scale,
            SpriteEffects.None,
            0f);
    }

    private static void DrawTileTexture(RenderingContext context, string textureKey, GridPoint point, Color? color = null)
    {
        DrawTileTexture(context, textureKey, point, 0f, color);
    }

    private static void DrawTileTexture(RenderingContext context, string textureKey, GridPoint point, float rotation, Color? color = null)
    {
        DrawWorldTexture(
            context,
            textureKey,
            point,
            rotation,
            FrameworkVector2.One,
            color);
    }

    private static void DrawWorldTextureNative(
        RenderingContext context,
        string textureKey,
        FrameworkVector2 worldPixels,
        float rotation = 0f,
        FrameworkVector2? origin = null,
        Color? color = null,
        FrameworkVector2? scale = null)
    {
        if (!context.Sprites.TryGet(textureKey, out var texture))
        {
            return;
        }

        DrawWorldTextureNative(context, texture, worldPixels, rotation, origin, color, scale);
    }

    private static void DrawWorldTextureNative(
        RenderingContext context,
        Texture2D texture,
        FrameworkVector2 worldPixels,
        float rotation = 0f,
        FrameworkVector2? origin = null,
        Color? color = null,
        FrameworkVector2? scale = null)
    {
        context.SpriteBatch.Draw(
            texture,
            context.Camera.WorldToScreen(worldPixels),
            null,
            color ?? Color.White,
            rotation,
            origin ?? new FrameworkVector2(texture.Width / 2f, texture.Height / 2f),
            scale ?? new FrameworkVector2(context.Camera.CurrentScale),
            SpriteEffects.None,
            0f);
    }

    private static FrameworkVector2 GetPlacedVehicleOrigin(Vehicle vehicle)
    {
        return new FrameworkVector2(
            vehicle.Size.X * TileConstants.TileHalfSize,
            vehicle.Size.Y * TileConstants.TileHalfSize);
    }

    private static FrameworkVector2 GetCreatureWorldPosition(Creature creature, float interpolationAlpha)
    {
        return ToFrameworkVector(creature.GetInterpolatedWorldPosition(interpolationAlpha));
    }

    private static FrameworkVector2 ToFrameworkVector(NumericsVector2 value)
    {
        return new FrameworkVector2(value.X, value.Y);
    }
}
