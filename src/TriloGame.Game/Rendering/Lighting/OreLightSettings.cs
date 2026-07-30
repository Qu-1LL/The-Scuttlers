using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering.Lighting;

public static class OreLightSettings
{
    public const int BaseProbeSpacing = 8;
    public const int BaseRayDimension = 8;
    public const float CascadeIntervalTexels = 2f;
    public const int CascadeRaySteps = 12;
    public const int MinCascadeCount = 4;
    public const int MaxCascadeCount = 6;
    // A bright ambient floor leaves no dynamic range for dim, long-range light: an added 4%
    // reads as nothing against a 58%-lit floor, so extending the light range has no visible
    // effect. Dropping the floor is what lets far-reaching light actually be seen, and matches
    // the cave reading as innately dark with ore as the real light source.
    public const float Ambient = 0.4f;
    // Keep deposits visibly brighter than the subdued ambient world.
    public const float OreIntensity = 0.90f;
    // Radius of the glow drawn directly on the deposit. This sprite ignores geometry entirely, so
    // it must stay barely wider than the deposit itself: at 5.5 tiles it was acting as room
    // lighting and visibly bleeding across walls (measured: floor tiles behind a wall received 71%
    // of unblocked light, dropping to 14% once this halo was removed). All actual room lighting
    // comes from the ray-marched cascade, which respects walls.
    public const float OreRadiusTiles = 2.5f;
    // How far ore light carries, in tiles. A physical inverse-square falloff dies within a
    // couple of tiles, which leaves nothing for occluders further out to block - creatures
    // standing away from a deposit then cast no visible shadow at all. A bounded linear-ish
    // range keeps light reaching far enough for shadows to exist and read clearly.
    public const float OreLightRangeTiles = 64f;
    // How far a short caster's shadow (a creature's) reaches behind it, in tiles. Full-height
    // casters - walls, radars, solid rock - are not limited this way and shadow the whole range.
    public const float ShortShadowTiles = 2.5f;
    // Fraction of light that passes through a full-height blocker (wall, radar, solid rock) per
    // tile of thickness. Applied multiplicatively as the ray marches, so a one-tile wall passes
    // this much and a three-tile wall passes this cubed - thicker rock is naturally more opaque.
    public const float WallTransmission = 0.15f;
    // Water is treated as a mostly specular surface: its own texture is dimmed to WaterAlbedo so it
    // stays only faintly visible, and what you mainly see is reflected light at WaterSheenStrength.
    // That means unlit water reads as near-black and lit water mirrors the ore colour around it.
    public const float WaterAlbedo = 0.45f;
    public const float WaterSheenStrength = 0.85f;
    public const float LumeniteMinimumPulse = 0.38f;

    public static readonly Color SharedOreLightColor = new(255, 209, 158, 255);
}
