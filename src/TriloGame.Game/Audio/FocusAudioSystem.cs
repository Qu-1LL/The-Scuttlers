using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;

namespace TriloGame.Game.Audio;

public sealed class FocusAudioSystem
{
    private const float MinAudibleZoomRangeStart = 0.2f;
    private const float FullAudibleZoomRangeStart = 0.85f;

    // Camera zoom where mining-post focus audio first begins contributing to the score.
    // Lower values make the loop audible while more zoomed out; higher values require
    // the player to zoom closer before any mining post can be heard.
    private static readonly float MinAudibleScale = MathHelper.Lerp(
        GameConstants.DefaultCameraScale,
        GameConstants.MaxScale,
        MinAudibleZoomRangeStart);

    // Camera zoom where the zoom contribution reaches full strength.
    // Between MinAudibleScale and this value, volume ramps up gradually; above this
    // value, additional zoom does not make the focused post louder by itself.
    private static readonly float FullAudibleScale = MathHelper.Lerp(
        GameConstants.DefaultCameraScale,
        GameConstants.MaxScale,
        FullAudibleZoomRangeStart);

    // Screen-space distance from the viewport center where focus falls to zero.
    // Larger radii make off-center mining posts remain audible farther from the
    // center; smaller radii require tighter camera framing.
    private const float AudibleRadiusPixels = 600f;

    // Minimum combined zoom-and-centering score required before the loop is allowed
    // to play. Raising this creates a stricter dead zone; lowering it makes faint
    // focus audio start sooner.
    private const float PlayThreshold = 0.08f;

    private sealed record FocusAudioProfile(
        GameAudioCue Cue,
        Func<Cave, IReadOnlyList<Building>> GetBuildings,
        float MinAudibleScale,
        float FullAudibleScale,
        float AudibleRadiusPixels,
        float PlayThreshold);

    internal readonly record struct FocusAudioTuning(
        float MinAudibleScale,
        float FullAudibleScale,
        float AudibleRadiusPixels,
        float PlayThreshold);

    private static readonly FocusAudioProfile[] Profiles =
    [
        new(
            GameAudioCue.MiningPostFocus,
            static cave => cave.GetMiningPosts(),
            MinAudibleScale,
            FullAudibleScale,
            AudibleRadiusPixels,
            PlayThreshold),

        new(
            GameAudioCue.AlgaeFarmFocus,
            static cave => cave.GetAlgaeFarms(),
            MinAudibleScale,
            FullAudibleScale,
            AudibleRadiusPixels,
            PlayThreshold)
    ];

    private readonly AudioService _audio;

    public FocusAudioSystem(AudioService audio)
    {
        _audio = audio;
    }

    public void Reset()
    {
        for (var index = 0; index < Profiles.Length; index++)
        {
            _audio.StopLoop(Profiles[index].Cue);
        }
    }

    public void Update(GameSession session, CameraController camera)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            Reset();
            return;
        }

        for (var index = 0; index < Profiles.Length; index++)
        {
            UpdateProfile(cave, camera, Profiles[index]);
        }
    }

    private void UpdateProfile(Cave cave, CameraController camera, FocusAudioProfile profile)
    {
        var buildings = profile.GetBuildings(cave);
        var bestScore = 0f;

        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (building.Location is null)
            {
                continue;
            }

            var center = GetPlacedBuildingWorldCenter(building);
            var screen = camera.WorldToScreen(center);
            var score = CalculateFocusScore(screen, camera.ViewCenter, camera.CurrentScale, profile);

            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        if (bestScore < profile.PlayThreshold)
        {
            _audio.StopLoop(profile.Cue);
            return;
        }

        _audio.StartLoop(profile.Cue, Smooth(bestScore));
    }

    internal static FocusAudioTuning GetTuning(GameAudioCue cue)
    {
        var profile = GetProfile(cue);
        return new FocusAudioTuning(
            profile.MinAudibleScale,
            profile.FullAudibleScale,
            profile.AudibleRadiusPixels,
            profile.PlayThreshold);
    }

    internal static float CalculateFocusScoreForTesting(GameAudioCue cue, float distancePixels, float scale)
    {
        var profile = GetProfile(cue);
        return CalculateFocusScore(new Vector2(distancePixels, 0f), Vector2.Zero, scale, profile);
    }

    private static float CalculateFocusScore(Vector2 screenPosition, Vector2 viewCenter, float scale, FocusAudioProfile profile)
    {
        var zoomFactor = InverseLerp(profile.MinAudibleScale, profile.FullAudibleScale, scale);
        var distance = Vector2.Distance(screenPosition, viewCenter);
        var centerFactor = 1f - Math.Clamp(distance / profile.AudibleRadiusPixels, 0f, 1f);

        return zoomFactor * centerFactor;
    }

    private static FocusAudioProfile GetProfile(GameAudioCue cue)
    {
        for (var index = 0; index < Profiles.Length; index++)
        {
            if (Profiles[index].Cue == cue)
            {
                return Profiles[index];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(cue), cue, "Focus audio is only defined for building focus cues.");
    }

    private static Vector2 GetPlacedBuildingWorldCenter(Building building)
    {
        var location = building.Location!.Value;
        return new Vector2(
            (location.X * TileConstants.TileSize) + ((building.Size.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((building.Size.Y - 1) * TileConstants.TileHalfSize));
    }

    private static float InverseLerp(float min, float max, float value)
    {
        if (max <= min)
        {
            return value >= max ? 1f : 0f;
        }

        return Math.Clamp((value - min) / (max - min), 0f, 1f);
    }

    private static float Smooth(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - (2f * value));
    }
}
