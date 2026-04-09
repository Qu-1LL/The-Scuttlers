using Microsoft.Xna.Framework.Audio;

namespace TriloGame.Game.Audio;

public sealed class AudioService
{
    private readonly Dictionary<GameAudioCue, SoundEffect> _effects = [];
    private readonly Dictionary<GameAudioCue, SoundEffectInstance> _loopInstances = [];

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
        foreach (var instance in _loopInstances.Values)
        {
            instance.Volume = NormalizedVolume;
        }

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

    public bool StartLoop(GameAudioCue cue)
    {
        if (_loopInstances.TryGetValue(cue, out var existing))
        {
            if (existing.State != SoundState.Playing)
            {
                existing.Play();
            }

            return true;
        }

        if (!_effects.TryGetValue(cue, out var effect))
        {
            return false;
        }

        var instance = effect.CreateInstance();
        instance.IsLooped = true;
        instance.Volume = NormalizedVolume;
        instance.Pitch = ClickPitchVariation.GetRandomPitch(cue);
        instance.Pan = 0f;
        instance.Play();
        _loopInstances[cue] = instance;
        return true;
    }

    public void StopLoop(GameAudioCue cue)
    {
        if (!_loopInstances.Remove(cue, out var instance))
        {
            return;
        }

        instance.Stop();
        instance.Dispose();
    }

    public void StopAllLoops()
    {
        foreach (var cue in _loopInstances.Keys.ToArray())
        {
            StopLoop(cue);
        }
    }

    public bool IsLoopPlaying(GameAudioCue cue)
    {
        return _loopInstances.TryGetValue(cue, out var instance) && instance.State == SoundState.Playing;
    }

    public TimeSpan GetDuration(GameAudioCue cue)
    {
        return _effects.TryGetValue(cue, out var effect) ? effect.Duration : TimeSpan.Zero;
    }
}
