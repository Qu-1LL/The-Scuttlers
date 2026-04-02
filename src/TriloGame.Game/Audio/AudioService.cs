using Microsoft.Xna.Framework.Audio;

namespace TriloGame.Game.Audio;

public sealed class AudioService
{
    private readonly Dictionary<GameAudioCue, SoundEffect> _effects = [];

    public int VolumePercent { get; private set; } = 100;

    public float NormalizedVolume => VolumePercent / 100f;

    public void Register(GameAudioCue cue, SoundEffect effect)
    {
        _effects[cue] = effect;
    }

    public bool SetVolumePercent(int volumePercent)
    {
        var clamped = Math.Clamp(volumePercent, 0, 100);
        if (clamped == VolumePercent)
        {
            return false;
        }

        VolumePercent = clamped;
        return true;
    }

    public bool ChangeVolume(int delta)
    {
        return SetVolumePercent(VolumePercent + delta);
    }

    public bool Play(GameAudioCue cue)
    {
        if (!_effects.TryGetValue(cue, out var effect))
        {
            return false;
        }

        effect.Play(NormalizedVolume, ClickPitchVariation.GetRandomPitch(cue), 0f);
        return true;
    }
}
