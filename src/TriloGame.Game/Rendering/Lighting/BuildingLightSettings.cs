using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering.Lighting;

// What a light-emitting building contributes this frame: the colour it lights the cave in, how
// strongly, and how wide the glow drawn directly on the sprite reads.
//
// Intensity is resolved per frame rather than stored, because it is not constant for every emitter -
// a campfire flickers. Colour and radius are fixed per building type.
public readonly record struct BuildingLightEmission(Color Color, float Intensity, float RadiusTiles);

// Buildings that are light sources in their own right, alongside the ore deposits.
//
// The intensities here are on the same 0..1 scale as OreLightSettings.OreIntensity and land in the
// same channel of the world tile grid, so a building lights the cave through the ray march exactly
// as a deposit does - respecting walls, steering cast shadows, reaching as far as LightReachTiles.
// The radius is the separate screen-space halo drawn on the sprite itself (see
// OreLightSettings.OreRadiusTiles for why that one has to stay tight).
public static class BuildingLightSettings
{
    // Mining posts are the colony's work lighting: white, steady, and a little brighter than a
    // deposit, so a post reads as somewhere the colony has made habitable rather than as another
    // piece of glowing rock.
    public const float MiningPostIntensity = 0.95f;
    public const float MiningPostRadiusTiles = 2.1f;
    public static readonly Color MiningPostColor = new(255, 252, 244, 255);

    // The barracks sprite is a campfire, so it lights like one: warm orange, and never quite still.
    //
    // The base sits low enough that base * (1 + CampfirePulseAmplitude) stays under 1. Intensity is
    // clamped there, so a base that peaks above it would spend the top of every swing flattened
    // against the ceiling - the flicker would visibly stall at its brightest rather than turn over.
    public const float CampfireIntensity = 0.82f;
    // Tighter than the post's: the post is lit across its whole 3x3, but a campfire is a small flame
    // in the middle of one, and a halo wider than the flame reads as the ground glowing.
    public const float CampfireRadiusTiles = 1.6f;
    public static readonly Color CampfireColor = new(255, 138, 42, 255);

    // How far the flicker swings either side of the base intensity, and the period of its slowest
    // component in seconds.
    //
    // Deliberately a modest swing over a slow period. A fire read from across a cave is a body of
    // light that breathes, not a strobe: pushing the amplitude up turns the whole lit pool around the
    // barracks on and off, because everything the ray march gathers from this cell scales with it.
    public const float CampfirePulseAmplitude = 0.2f;
    public const float CampfirePulseSeconds = 2.3f;

    // Two components rather than one, at a ratio chosen not to be a simple fraction.
    //
    // A single sine has an obvious period, and once several campfires are on screen the eye locks
    // onto it and they read as blinking lights. Summing a second, faster component whose period
    // shares no small common factor with the first gives a beat long enough not to be read as a
    // repeat, which is the same reason the water surface layers non-harmonic waves.
    private const float CampfireFastPeriodRatio = 0.37f;
    private const float CampfireSlowWeight = 0.68f;

    public static bool TryGetEmission(Building building, float elapsedSeconds, out BuildingLightEmission emission)
    {
        switch (building)
        {
            case MiningPost:
                emission = new BuildingLightEmission(
                    MiningPostColor,
                    MiningPostIntensity,
                    MiningPostRadiusTiles);
                return true;

            case Barracks:
                emission = new BuildingLightEmission(
                    CampfireColor,
                    Math.Clamp(
                        CampfireIntensity * GetCampfireFlicker(
                            elapsedSeconds,
                            GetFlickerPhaseSeconds(building.GetCenter())),
                        0f,
                        1f),
                    CampfireRadiusTiles);
                return true;

            default:
                emission = default;
                return false;
        }
    }

    // Multiplier on the base intensity, centred on 1.
    internal static float GetCampfireFlicker(float elapsedSeconds, float phaseSeconds)
    {
        var time = elapsedSeconds + phaseSeconds;
        var slow = MathF.Sin(time * MathF.Tau / MathF.Max(0.001f, CampfirePulseSeconds));
        var fast = MathF.Sin(
            time * MathF.Tau / MathF.Max(0.001f, CampfirePulseSeconds * CampfireFastPeriodRatio));
        var wave = (slow * CampfireSlowWeight) + (fast * (1f - CampfireSlowWeight));
        return 1f + (wave * CampfirePulseAmplitude);
    }

    // Per-fire phase offset, so two campfires in the same room are never in step. Hashed from the
    // world position for the same reason the tile sprite animation is (see
    // WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds): it is stable across frames and across
    // saves without anything having to be stored on the building.
    internal static float GetFlickerPhaseSeconds(GridPoint coordinates)
    {
        var hash = (coordinates.X * 73856093) ^ (coordinates.Y * 19349663);
        hash &= 0x7fffffff;
        return (hash % 1000) / 1000f * CampfirePulseSeconds;
    }
}
