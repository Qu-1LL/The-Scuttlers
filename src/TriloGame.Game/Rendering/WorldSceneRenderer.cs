using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;
using FrameworkVector2 = Microsoft.Xna.Framework.Vector2;
using NumericsVector2 = System.Numerics.Vector2;

namespace TriloGame.Game.Rendering;

public sealed class WorldSceneRenderer
{
    private static readonly FrameworkVector2[] DroppedResourceOffsets =
    [
        new(-18f, -14f),
        new(0f, -16f),
        new(18f, -10f),
        new(-12f, 12f),
        new(14f, 14f)
    ];

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

    public void DrawWorldLayer(RenderingContext context, GameSession session, WorldSpriteEffectSystem spriteEffects)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        DrawFloorTiles(context, cave);
        DrawTileOverlays(context, cave, spriteEffects);
        DrawSurfaceFeatures(context, cave);
        DrawDroppedResources(context, cave);
        DrawBuildings(context, cave);
        DrawCreatures(context, cave);
        DrawProjectiles(context, session);
    }

    private static void DrawFloorTiles(RenderingContext context, Cave cave)
    {
        var (minX, maxX, minY, maxY) = GetCaveTileBounds(cave);
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var coordinates = new GridPoint(x, y);
                var tile = cave.GetTile(coordinates);
                if (tile is null || !cave.IsTileRevealed(tile))
                {
                    DrawTileTexture(context, "empty", coordinates, Color.Black);
                    continue;
                }

                if (ShouldDrawFloorTile(tile))
                {
                    DrawTileTexture(context, "empty", tile.Coordinates);
                }
            }
        }
    }

    private static void DrawTileOverlays(RenderingContext context, Cave cave, WorldSpriteEffectSystem spriteEffects)
    {
        foreach (var tile in cave.GetTiles().Where(cave.IsTileRevealed))
        {
            if (tile.Base == "wall")
            {
                DrawTileTexture(context, "wall", tile.Coordinates);
            }
            else if (tile.IsOreTile())
            {
                DrawTileTexture(
                    context,
                    tile.Base,
                    tile.Coordinates,
                    GetTileOverlayRotationRadians(tile),
                    GetTileDrawColor(spriteEffects, tile, tile.Base, tile.Coordinates));
            }

            if (tile.Decoration == TileDecoration.CaveCrystal)
            {
                DrawTileTexture(context, "CaveCrystal", tile.Coordinates);
            }
        }
    }

    private static void DrawSurfaceFeatures(RenderingContext context, Cave cave)
    {
        foreach (var antHole in cave.GetAntHoles())
        {
            var tile = cave.GetTile(antHole.TileKey);
            if (tile is null || !cave.IsTileRevealed(tile))
            {
                continue;
            }

            var alpha = SmoothStep(antHole.SpawnProgress);
            DrawWorldTextureNative(
                context,
                "AntHole",
                new FrameworkVector2(tile.Coordinates.X * TileConstants.TileSize, tile.Coordinates.Y * TileConstants.TileSize),
                color: Color.White * alpha);
        }
    }

    private static void DrawDroppedResources(RenderingContext context, Cave cave)
    {
        foreach (var tile in cave.GetTiles().Where(cave.IsTileRevealed))
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

    private static void DrawBuildings(RenderingContext context, Cave cave)
    {
        foreach (var building in cave.Buildings)
        {
            if (building is Scaffolding scaffold)
            {
                foreach (var tile in scaffold.TileArray.Where(cave.IsTileRevealed))
                {
                    var tilePoint = GridPoint.Parse(tile.Key);
                    DrawWorldTextureNative(
                        context,
                        "Scaffold",
                        new FrameworkVector2(tilePoint.X * TileConstants.TileSize, tilePoint.Y * TileConstants.TileSize));
                }

                continue;
            }

            if (building.Location is null)
            {
                continue;
            }

            DrawWorldTextureNative(
                context,
                building.TextureKey,
                BuildingPlacementGrid.GetWorldCenter(building),
                building.GetDisplayRotationTurns() * MathF.PI / 2f,
                BuildingPlacementGrid.GetTextureCenterOrigin(building));
        }
    }

    private static void DrawCreatures(RenderingContext context, Cave cave)
    {
        foreach (var trilobite in cave.Trilobites)
        {
            DrawWorldTextureNative(
                context,
                "Trilobite",
                GetCreatureWorldPosition(trilobite),
                trilobite.RotationRadians);
        }

        foreach (var enemy in cave.Enemies)
        {
            DrawWorldTextureNative(
                context,
                "Enemy",
                GetCreatureWorldPosition(enemy),
                enemy.RotationRadians);
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

    private static Color GetTileDrawColor(WorldSpriteEffectSystem spriteEffects, Tile tile, string textureKey, GridPoint coordinates)
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

    private static float SmoothStep(float progress)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        return clamped * clamped * (3f - (2f * clamped));
    }

    internal static float GetTileOverlayRotationRadians(Tile tile)
    {
        if (!tile.IsOreTile())
        {
            return 0f;
        }

        return tile.OreRotationQuarterTurns * MathF.PI / 2f;
    }

    internal static bool ShouldDrawFloorTile(Tile tile)
    {
        return tile.HasFloorCover || tile.Built is not null;
    }

    internal static (int MinX, int MaxX, int MinY, int MaxY) GetCaveTileBounds(Cave cave)
    {
        var first = true;
        var minX = 0;
        var maxX = 0;
        var minY = 0;
        var maxY = 0;

        foreach (var tile in cave.GetTiles())
        {
            var coordinates = tile.Coordinates;
            if (first)
            {
                minX = maxX = coordinates.X;
                minY = maxY = coordinates.Y;
                first = false;
                continue;
            }

            minX = Math.Min(minX, coordinates.X);
            maxX = Math.Max(maxX, coordinates.X);
            minY = Math.Min(minY, coordinates.Y);
            maxY = Math.Max(maxY, coordinates.Y);
        }

        return (minX, maxX, minY, maxY);
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

    private static FrameworkVector2 GetCreatureWorldPosition(Creature creature)
    {
        return ToFrameworkVector(creature.GetWorldPosition());
    }

    private static FrameworkVector2 ToFrameworkVector(NumericsVector2 value)
    {
        return new FrameworkVector2(value.X, value.Y);
    }
}
