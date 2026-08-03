using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering.Lighting;

public readonly record struct OreLightEmitter(
    string OreName,
    GridPoint Coordinates,
    Vector2 WorldPosition,
    float Intensity,
    Color LightColor);

// Carries its own radius, unlike an ore emitter: the halo has to sit on the sprite it belongs to, and
// buildings are not all one tile across.
public readonly record struct BuildingLightEmitter(
    GridPoint Coordinates,
    Vector2 WorldPosition,
    float Intensity,
    Color LightColor,
    float RadiusTiles);

public sealed class LightingSourceCollector
{
    // Collect only intact ore deposits; dropped resources and cave crystals have no light source.
    public int CollectOreEmitters(
        IReadOnlyList<Tile> visibleTiles,
        WorldSpriteEffectSystem spriteEffects,
        List<OreLightEmitter> destination,
        OreLightColorPalette? palette = null)
    {
        destination.Clear();
        foreach (var tile in visibleTiles)
        {
            if (!tile.IsOreTile() || !OreType.TryGet(tile.Base, out var oreType))
            {
                continue;
            }

            var intensity = OreLightSettings.OreIntensity;
            if (string.Equals(oreType.Name, OreType.LUMENITE.Name, StringComparison.Ordinal))
            {
                intensity *= spriteEffects.GetAlphaMultiplier(
                    oreType.Name,
                    WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds(oreType.Name, tile.Coordinates));
            }

            destination.Add(
                new OreLightEmitter(
                    oreType.Name,
                    tile.Coordinates,
                    new Vector2(
                        tile.Coordinates.X * TileConstants.TileSize,
                        tile.Coordinates.Y * TileConstants.TileSize),
                    intensity,
                    palette?.GetLightColor(oreType.Name) ?? Color.White));
        }

        return destination.Count;
    }

    // Buildings that light the cave, for the screen-space glow drawn on the sprite itself. The ray
    // march does NOT read this - LightingTileGrid.ApplyBuildingEmission seeds the world grid for that,
    // over the full lighting footprint, so a post off the edge of the screen still lights what is on
    // it.
    public int CollectBuildingEmitters(
        Cave cave,
        bool showFullMapVisibility,
        float elapsedSeconds,
        List<BuildingLightEmitter> destination)
    {
        destination.Clear();
        foreach (var building in cave.Buildings)
        {
            if (building.Location is null ||
                !BuildingLightSettings.TryGetEmission(building, elapsedSeconds, out var emission))
            {
                continue;
            }

            var centre = building.GetCenter();
            if (!showFullMapVisibility &&
                (cave.GetTile(centre) is not { } tile || !cave.IsTileRevealed(tile)))
            {
                continue;
            }

            destination.Add(
                new BuildingLightEmitter(
                    centre,
                    new Vector2(centre.X * TileConstants.TileSize, centre.Y * TileConstants.TileSize),
                    emission.Intensity,
                    emission.Color,
                    emission.RadiusTiles));
        }

        return destination.Count;
    }
}
