using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;

namespace TriloGame.Game.Audio;

public sealed class FocusAudioSystem
{
    private const float PlayThreshold = 0.001f;

    private sealed record FocusAudioProfile(
        GameAudioCue Cue,
        Func<Cave, IReadOnlyList<Building>> GetBuildings);

    internal readonly record struct FocusAudioTuning(float PlayThreshold);

    private static readonly FocusAudioProfile[] Profiles =
    [
        new(GameAudioCue.AlgaeFarmFocus, static cave => cave.GetAlgaeFarms()),
        new(GameAudioCue.RadarFocus, static cave => cave.GetRadars())
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
        var bestGain = 0f;

        for (var index = 0; index < buildings.Count; index++)
        {
            var gain = CalculateBuildingGain(buildings[index], camera);
            if (gain > bestGain)
            {
                bestGain = gain;
            }
        }

        if (bestGain < PlayThreshold)
        {
            _audio.StopLoop(profile.Cue);
            return;
        }

        _audio.StartLoop(profile.Cue, bestGain);
    }

    internal static FocusAudioTuning GetTuning(GameAudioCue cue)
    {
        _ = GetProfile(cue);
        return new FocusAudioTuning(PlayThreshold);
    }

    internal static float CalculateSquareCoverageForTesting(float footprintTiles, float scale, int viewportWidth, int viewportHeight)
    {
        return ScreenSpaceAudio.CalculateSquareCoverageForTesting(footprintTiles, scale, viewportWidth, viewportHeight);
    }

    private static float CalculateBuildingGain(Building building, CameraController camera)
    {
        if (building.Location is null)
        {
            return 0f;
        }

        var center = GetPlacedBuildingWorldCenter(building);
        return ScreenSpaceAudio.CalculateVisibleCoverage(
            camera.WorldToScreen(center),
            Math.Max(1, building.Size.X),
            Math.Max(1, building.Size.Y),
            camera);
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
}
