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
}
