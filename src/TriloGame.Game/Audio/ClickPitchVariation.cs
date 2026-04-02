using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Audio;

public static class ClickPitchVariation
{
    private static readonly float[] CuePitches = [-0.06f, 0f, 0.06f];

    public static IReadOnlyList<float> GetPitches(GameAudioCue cue)
    {
        return CuePitches;
    }

    public static float GetPitchForIndex(GameAudioCue cue, int index)
    {
        var pitches = GetPitches(cue);
        if (pitches.Count == 0)
        {
            return 0f;
        }

        var normalizedIndex = Math.Abs(index) % pitches.Count;
        return pitches[normalizedIndex];
    }

    public static float GetRandomPitch(GameAudioCue cue)
    {
        var pitches = GetPitches(cue);
        if (pitches.Count == 0)
        {
            return 0f;
        }

        return pitches[RandomUtil.NextInt(pitches.Count)];
    }
}
