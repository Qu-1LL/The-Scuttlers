using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering;

public sealed class HealthBarRenderer
{
    private static readonly Color BorderColor = new(12, 16, 20);
    private static readonly Color TrackColor = new(65, 29, 33);

    // Draw after world lighting and sprites, before screen UI, without retaining health or entity state.
    public void Draw(RenderingContext context, Cave cave, Point viewportSize, bool showFullMapVisibility, float interpolationAlpha)
    {
        foreach (var building in cave.Buildings)
        {
            if (TryGetBuildingLayout(building, cave, context.Camera, showFullMapVisibility, out var layout))
            {
                DrawBar(context, viewportSize, layout);
            }
        }

        var vehicles = cave.GetVehicles();
        for (var index = 0; index < vehicles.Count; index++)
        {
            var vehicle = vehicles[index];
            if (HealthBarLayout.IsDamaged(vehicle) && context.Sprites.TryGet(vehicle.TextureKey, out var texture) &&
                TryGetVehicleLayout(vehicle, new Vector2(texture.Width, texture.Height), context.Camera, interpolationAlpha, out var layout))
            {
                DrawBar(context, viewportSize, layout);
            }
        }

        foreach (var trilobite in cave.Trilobites)
        {
            if (trilobite.IsVisible && HealthBarLayout.IsDamaged(trilobite))
            {
                DrawCreatureBar(context, viewportSize, trilobite,
                    TrilobiteSpriteCatalog.ResolveTextureKey(context.Sprites, trilobite), interpolationAlpha);
            }
        }

        foreach (var enemy in cave.Enemies)
        {
            if (enemy.IsVisible && HealthBarLayout.IsDamaged(enemy))
            {
                DrawCreatureBar(context, viewportSize, enemy, "Enemy", interpolationAlpha);
            }
        }
    }

    private static void DrawCreatureBar(RenderingContext context, Point viewportSize, Creature creature, string textureKey, float interpolationAlpha)
    {
        if (context.Sprites.TryGet(textureKey, out var texture) &&
            TryGetCreatureLayout(creature, new Vector2(texture.Width, texture.Height), context.Camera, interpolationAlpha, out var layout))
        {
            DrawBar(context, viewportSize, layout);
        }
    }

    // Use the same interpolated pose and native sprite dimensions as the creature's world draw.
    internal static bool TryGetCreatureLayout(Creature creature, Vector2 spriteSize, CameraController camera, float interpolationAlpha, out HealthBarLayout layout)
    {
        layout = default;
        if (!creature.IsVisible || !HealthBarLayout.IsDamaged(creature))
        {
            return false;
        }

        var position = creature.GetInterpolatedWorldPosition(interpolationAlpha);
        var topCenter = GetSpriteTopCenter(new Vector2(position.X, position.Y), spriteSize, spriteSize / 2f,
            creature.GetInterpolatedFacingRadians(interpolationAlpha));
        return HealthBarLayout.TryCreate(creature, topCenter, camera, out layout);
    }

    // Vehicle art pivots around its configured tile footprint, even when the texture extends beyond it.
    internal static bool TryGetVehicleLayout(Vehicle vehicle, Vector2 spriteSize, CameraController camera, float interpolationAlpha, out HealthBarLayout layout)
    {
        layout = default;
        if (vehicle.Location is null || !HealthBarLayout.IsDamaged(vehicle))
        {
            return false;
        }

        var position = vehicle.GetInterpolatedWorldCenter(interpolationAlpha);
        var origin = new Vector2(vehicle.Size.X * TileConstants.TileHalfSize, vehicle.Size.Y * TileConstants.TileHalfSize);
        var topCenter = GetSpriteTopCenter(new Vector2(position.X, position.Y), spriteSize, origin,
            vehicle.GetInterpolatedRotationRadians(interpolationAlpha));
        return HealthBarLayout.TryCreate(vehicle, topCenter, camera, out layout);
    }

    // One bar covers the whole rotated footprint, including soil patches and construction sites.
    internal static bool TryGetBuildingLayout(Building building, Cave cave, CameraController camera, bool showFullMapVisibility, out HealthBarLayout layout)
    {
        layout = default;
        if (building.Location is null || !HealthBarLayout.IsDamaged(building) ||
            (building is Scaffolding && !HasVisibleScaffoldTile(building, cave, showFullMapVisibility)))
        {
            return false;
        }

        var topCenter = BuildingPlacementGrid.GetWorldCenter(building);
        topCenter.Y -= building.Size.Y * TileConstants.TileHalfSize;
        return HealthBarLayout.TryCreate(building, topCenter, camera, out layout);
    }

    // Match the scaffold sprite reveal rule without allocating a footprint iterator each frame.
    private static bool HasVisibleScaffoldTile(Building building, Cave cave, bool showFullMapVisibility)
    {
        var location = building.Location!.Value;
        for (var y = 0; y < building.OpenMap.Length; y++)
        {
            var row = building.OpenMap[y];
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x] <= 1 && WorldSceneRenderer.ShouldRenderTile(cave,
                    cave.GetTile(new GridPoint(location.X + x, location.Y + y)), showFullMapVisibility))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Keep the bar horizontal above the rotated sprite's axis-aligned bounds.
    internal static Vector2 GetSpriteTopCenter(Vector2 position, Vector2 size, Vector2 origin, float rotation)
    {
        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);
        var localCenter = (size / 2f) - origin;
        var center = position + new Vector2(
            (localCenter.X * cos) - (localCenter.Y * sin),
            (localCenter.X * sin) + (localCenter.Y * cos));
        var halfHeight = ((MathF.Abs(sin) * size.X) + (MathF.Abs(cos) * size.Y)) / 2f;
        return new Vector2(center.X, center.Y - halfHeight);
    }

    private static void DrawBar(RenderingContext context, Point viewportSize, HealthBarLayout layout)
    {
        if (!layout.IntersectsViewport(viewportSize))
        {
            return;
        }

        DrawRectangle(context, layout.Position, layout.Size, BorderColor);
        DrawRectangle(context, layout.FillPosition, layout.TrackSize, TrackColor);
        DrawRectangle(context, layout.FillPosition, layout.FillSize, GetFillColor(layout.Fraction));
    }

    internal static Color GetFillColor(float fraction) => fraction switch
    {
        <= 0.25f => new Color(232, 65, 65),
        <= 0.5f => new Color(242, 185, 55),
        _ => new Color(87, 203, 103)
    };

    private static void DrawRectangle(RenderingContext context, Vector2 position, Vector2 size, Color color)
    {
        context.SpriteBatch.Draw(context.WhitePixel, position, null, color, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
    }
}
