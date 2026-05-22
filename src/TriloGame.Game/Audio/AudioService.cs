using Microsoft.Xna.Framework.Audio;

namespace TriloGame.Game.Audio;

public sealed class AudioService
{
    private readonly Dictionary<GameAudioCue, SoundEffect> _effects = [];
    private readonly Dictionary<GameAudioCue, SoundEffectInstance> _loopInstances = [];

    private readonly Dictionary<GameAudioCue, float> _loopGains = [];

    public int VolumePercent { get; private set; } = 100;

    public float NormalizedVolume => VolumePercent / 100f;

    // Register or replace the sound effect used for one logical game cue.
    public void Register(GameAudioCue cue, SoundEffect effect)
    {
        _effects[cue] = effect;
    }

    // Clamp the master volume and push the new level to any active loop instances.
    public bool SetVolumePercent(int volumePercent)
    {
        var clamped = Math.Clamp(volumePercent, 0, 100);
        if (clamped == VolumePercent)
        {
            return false;
        }

        VolumePercent = clamped;
        foreach (var pair in _loopInstances)
        {
            pair.Value.Volume = GetOutputVolume(pair.Key);
        }

        return true;
    }

    // Returns volume that the player hears (golbal volume * cue gain)
    private float GetOutputVolume(GameAudioCue cue)
    {
    return NormalizedVolume * _loopGains.GetValueOrDefault(cue, 1f);
    }

    // Adjust the current master volume by a signed delta.
    public bool ChangeVolume(int delta)
    {
        return SetVolumePercent(VolumePercent + delta);
    }

    // Play a one-shot cue if the sound has been registered.
    public bool Play(GameAudioCue cue)
    {
        if (!_effects.TryGetValue(cue, out var effect))
        {
            return false;
        }

        effect.Play(NormalizedVolume, ClickPitchVariation.GetRandomPitch(cue), 0f);
        return true;
    }

    // Start or resume a looped cue while reusing an existing instance when possible.
    public bool StartLoop(GameAudioCue cue, float gain)
    {
        gain = Math.Clamp(gain, 0f, 1f);
        _loopGains[cue] = gain;

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

    public bool StartLoop(GameAudioCue cue)
    {
    return StartLoop(cue, 1f);
    }   

    // Stop and dispose one loop instance if it is currently tracked.
    public void StopLoop(GameAudioCue cue)
    {
        if (!_loopInstances.Remove(cue, out var instance))
        {
            return;
        }

        _loopGains.Remove(cue);

        instance.Stop();
        instance.Dispose();
    }

    // Stop every active loop without leaving disposed instances in the lookup.
    public void StopAllLoops()
    {
        foreach (var cue in _loopInstances.Keys.ToArray())
        {
            StopLoop(cue);
        }
    }

    // Check whether the requested loop is currently playing.
    public bool IsLoopPlaying(GameAudioCue cue)
    {
        return _loopInstances.TryGetValue(cue, out var instance) && instance.State == SoundState.Playing;
    }

    // Report the registered clip duration for timing-sensitive systems.
    public TimeSpan GetDuration(GameAudioCue cue)
    {
        return _effects.TryGetValue(cue, out var effect) ? effect.Duration : TimeSpan.Zero;
    }
}
