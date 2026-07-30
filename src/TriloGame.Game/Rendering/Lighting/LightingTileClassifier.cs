using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Rendering.Lighting;

public static class LightingTileClassifier
{
    // Height of whatever occupies this tile, which decides how long a shadow it casts.
    public static LightingOccluderHeight GetOccluderHeight(Tile tile)
    {
        // Walls are the canonical full-height structure.
        if (string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            return LightingOccluderHeight.Tall;
        }

        // Any other non-empty base is solid terrain the colony cannot walk through - intact ore
        // deposits and cave crystals. Full-height rock, so it blocks at any distance.
        if (!string.Equals(tile.Base, "empty", StringComparison.Ordinal))
        {
            return LightingOccluderHeight.Impassable;
        }

        if (tile.Built is { } building)
        {
            return GetBuildingOccluderHeight(building);
        }

        // Bare floor.
        return LightingOccluderHeight.Flat;
    }

    // Only genuinely tall structures occlude. Everything else - farms, storage, posts, barracks,
    // turrets, soil, scaffolding - is flat as far as light is concerned.
    public static LightingOccluderHeight GetBuildingOccluderHeight(Building building)
    {
        return building is Wall or Radar
            ? LightingOccluderHeight.Tall
            : LightingOccluderHeight.Flat;
    }

    public static bool BlocksLight(Tile tile)
    {
        return GetOccluderHeight(tile).CastsShadow();
    }

    // Used for the off-screen fallback, where only full-height blockers may stop light: short
    // casters need a receiver distance to evaluate against and that is unavailable off-screen.
    public static bool BlocksLightAtAnyDistance(Tile tile)
    {
        return GetOccluderHeight(tile).IsFullHeight();
    }

    public static bool EmitsLight(Tile tile)
    {
        return tile.IsOreTile();
    }

    public static bool IsBuildingOccluder(Building building)
    {
        return GetBuildingOccluderHeight(building).CastsShadow();
    }
}
