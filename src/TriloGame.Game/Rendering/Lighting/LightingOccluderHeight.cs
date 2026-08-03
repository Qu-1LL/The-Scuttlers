namespace TriloGame.Game.Rendering.Lighting;

// How much of the light column an object physically stands in, which is what decides the length
// of the shadow it casts. Casters are rasterised into one of two occluder masks according to this,
// so the shader can vary occlusion by caster height instead of treating every blocker as an
// infinite wall. (Height cannot be packed into a colour channel of the mask: the sprite tint is
// multiplied by the sprite's own art colour, so the channel would carry the artwork, not the
// height - which silently made every wall cast a short shadow.)
public enum LightingOccluderHeight
{
    // Floor tiles, soil, and passable buildings. Nothing to cast a shadow with.
    Flat = 0,

    // Creatures. Low to the ground, so they only shade what is close behind them relative to
    // the light; a receiver further away sees over the top of them.
    Short = 1,

    // Solid terrain that stops movement - intact ore deposits and cave crystals. Full-height
    // rock, so it blocks light at any distance the way a wall does.
    Impassable = 2,

    // Walls and radars: full-height structures that cast the longest shadows.
    Tall = 3
}

public static class LightingOccluderHeightExtensions
{
    // Anything at or above Impassable blocks regardless of how far away the receiver is.
    public static bool IsFullHeight(this LightingOccluderHeight height)
    {
        return height >= LightingOccluderHeight.Impassable;
    }

    public static bool CastsShadow(this LightingOccluderHeight height)
    {
        return height != LightingOccluderHeight.Flat;
    }

    // Fraction of light this material passes per tile of thickness. 1 is clear.
    //
    // This is what makes Tall and Impassable different classes rather than two names for the same
    // thing. They block at the same distances - both are full height - but a built wall is a panel
    // with gaps and solid rock is metres of stone, so they should not attenuate identically.
    public static float GetLightTransmission(this LightingOccluderHeight height)
    {
        return height switch
        {
            LightingOccluderHeight.Tall => OreLightSettings.WallTransmission,
            LightingOccluderHeight.Impassable => OreLightSettings.RockTransmission,
            // Flat and Short are not full-height blockers, so they never attenuate a marched ray.
            // Short casters are resolved in the scene layer instead - see DrawCastShadowLayer.
            _ => 1f
        };
    }
}
