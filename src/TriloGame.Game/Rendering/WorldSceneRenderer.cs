using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
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

    private static readonly Color WaterAlbedoColor = new(
        OreLightSettings.WaterAlbedo,
        OreLightSettings.WaterAlbedo,
        OreLightSettings.WaterAlbedo,
        1f);

    private const float InventoryBackpackIconTileScale = 0.64f;
    private const float InventoryBackpackSlotSpacingPixels = 42f;

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
                    Color.White,
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
        DrawWorldLayer(
            context,
            session,
            spriteEffects,
            showFullMapVisibility,
            showCombatDebug,
            interpolationAlpha,
            visibleTiles);
    }

    // Render all world layers from one camera cull so lighting and scene pixels share tile coverage.
    public void DrawWorldLayer(
        RenderingContext context,
        GameSession session,
        WorldSpriteEffectSystem spriteEffects,
        bool showFullMapVisibility,
        bool showCombatDebug,
        float interpolationAlpha,
        IReadOnlyList<Tile> visibleTiles)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        // Water sits below the floor: it is what shows through gaps in the cave floor, so it must
        // be laid down before the floor tiles that surround (and partially overlap) it.
        DrawWaterTiles(context, visibleTiles, spriteEffects);
        DrawFloorTiles(context, visibleTiles);
        DrawTileOverlays(context, visibleTiles, spriteEffects);
        DrawSurfaceFeatures(context, cave, showFullMapVisibility);
        DrawDroppedResources(context, visibleTiles);
        DrawBuildings(context, cave, showFullMapVisibility);
        if (showCombatDebug)
        {
            DrawCombatDebug(context, session);
        }
        DrawCreatures(context, session, cave, interpolationAlpha);
        DrawProjectiles(context, session);
    }

    // Short casters only (creatures). Their texture alpha becomes the shadow silhouette, so light
    // stops on the drawn sprite rather than on the whole tile square.
    public void DrawEntityOccluderLayer(
        RenderingContext context,
        Cave cave,
        float interpolationAlpha)
    {
        foreach (var trilobite in cave.Trilobites)
        {
            DrawWorldTextureNative(
                context,
                "Trilobite",
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
    }

    // Silhouette of open water for the composite's specular sheen. Uses a flat white quad rather
    // than the animated frame: the sheen only needs to know where water is, and using the frame
    // would make the highlight flicker with the animation.
    public void DrawWaterMaskLayer(RenderingContext context, IReadOnlyList<Tile> visibleTiles)
    {
        if (!context.Sprites.TryGet("empty", out _))
        {
            return;
        }

        foreach (var tile in visibleTiles)
        {
            if (tile.IsWater())
            {
                DrawTileTexture(context, "empty", tile.Coordinates, 0f, Color.White);
            }
        }
    }

    // Full-height casters go in their own mask so the shader can occlude with them at any
    // distance, while the short mask above only shades what is close behind its casters.
    public void DrawTallOccluderLayer(RenderingContext context, IReadOnlyList<Tile> occludingTiles)
    {
        foreach (var tile in occludingTiles)
        {
            if (!LightingTileClassifier.GetOccluderHeight(tile).IsFullHeight())
            {
                continue;
            }

            var textureKey = tile.Base == "wall"
                ? "wall"
                : GetTileOverlayTextureKey(tile) ?? tile.Base;
            if (!context.Sprites.TryGet(textureKey, out _))
            {
                continue;
            }

            DrawTileTexture(
                context,
                textureKey,
                tile.Coordinates,
                tile.Base == "wall" ? 0f : GetTileOverlayRotationRadians(tile),
                Color.White);
        }
    }

    public void DrawEmissiveLayer(
        RenderingContext context,
        IReadOnlyList<OreLightEmitter> emitters,
        Texture2D lightTexture)
    {
        var origin = new FrameworkVector2(lightTexture.Width / 2f, lightTexture.Height / 2f);
        for (var index = 0; index < emitters.Count; index++)
        {
            var emitter = emitters[index];
            var center = context.Camera.WorldToScreen(emitter.WorldPosition);
            // Each deposit's own colour, so the direct glow matches the cascade light it seeds.
            var lightColor = emitter.LightColor.ToVector4();
            lightColor *= Math.Clamp(emitter.Intensity, 0f, 1f);
            lightColor.W = 1f;
            var diameter = TileConstants.TileSize * context.Camera.CurrentScale * OreLightSettings.OreRadiusTiles * 2f;
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
    }

    private static void DrawWaterTiles(
        RenderingContext context,
        IEnumerable<Tile> visibleTiles,
        WorldSpriteEffectSystem spriteEffects)
    {
        foreach (var tile in visibleTiles)
        {
            if (!tile.IsWater())
            {
                continue;
            }

            // Per-tile phase offset so the surface does not flip every frame in unison.
            var textureKey = spriteEffects.GetAnimatedTextureKey(
                WaterAnimationKey,
                GetWorldSpritePhaseOffsetSeconds(WaterAnimationKey, tile.Coordinates));
            if (context.Sprites.TryGet(textureKey, out _))
            {
                // Dimmed to its albedo: water is mostly a mirror, so its own texture should only
                // be faintly readable and the reflected light in the composite should dominate.
                DrawTileTexture(context, textureKey, tile.Coordinates, 0f, WaterAlbedoColor);
            }
        }
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

    private static void DrawCreatures(
        RenderingContext context,
        GameSession session,
        Cave cave,
        float interpolationAlpha)
    {
        foreach (var trilobite in cave.Trilobites)
        {
            var worldPosition = GetCreatureWorldPosition(trilobite, interpolationAlpha);
            var facingRadians = trilobite.GetInterpolatedFacingRadians(interpolationAlpha);
            DrawWorldTextureNative(
                context,
                "Trilobite",
                worldPosition,
                facingRadians,
                color: GetCreatureDrawColor(session, trilobite));

            DrawTrilobiteInventoryBackpack(context, trilobite, worldPosition, facingRadians);
        }

        foreach (var enemy in cave.Enemies)
        {
            DrawWorldTextureNative(
                context,
                "Enemy",
                GetCreatureWorldPosition(enemy, interpolationAlpha),
                enemy.GetInterpolatedFacingRadians(interpolationAlpha),
                color: GetCreatureDrawColor(session, enemy));
        }
    }

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
    internal static IEnumerable<Tile> EnumerateVisibleTiles(
        Cave cave,
        CameraController camera,
        Point viewportSize,
        bool showFullMapVisibility)
    {
        camera.GetVisibleWorldBounds(viewportSize, out var topLeft, out var bottomRight);

        var minWorldX = MathF.Min(topLeft.X, bottomRight.X);
        var minWorldY = MathF.Min(topLeft.Y, bottomRight.Y);
        var maxWorldX = MathF.Max(topLeft.X, bottomRight.X);
        var maxWorldY = MathF.Max(topLeft.Y, bottomRight.Y);

        var minTileX = (int)MathF.Floor((minWorldX - TileConstants.TileHalfSize) / TileConstants.TileSize) - 2;
        var minTileY = (int)MathF.Floor((minWorldY - TileConstants.TileHalfSize) / TileConstants.TileSize) - 2;
        var maxTileX = (int)MathF.Ceiling((maxWorldX + TileConstants.TileHalfSize) / TileConstants.TileSize) + 2;
        var maxTileY = (int)MathF.Ceiling((maxWorldY + TileConstants.TileHalfSize) / TileConstants.TileSize) + 2;

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
        List<Tile> destination)
    {
        destination.Clear();
        foreach (var tile in EnumerateVisibleTiles(cave, camera, viewportSize, showFullMapVisibility))
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

    private static FrameworkVector2 GetCreatureWorldPosition(Creature creature, float interpolationAlpha)
    {
        return ToFrameworkVector(creature.GetInterpolatedWorldPosition(interpolationAlpha));
    }

    private static FrameworkVector2 ToFrameworkVector(NumericsVector2 value)
    {
        return new FrameworkVector2(value.X, value.Y);
    }
}
