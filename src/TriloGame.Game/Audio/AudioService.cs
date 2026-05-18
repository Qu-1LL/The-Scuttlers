using Microsoft.Xna.Framework.Audio;

namespace TriloGame.Game.Audio;

public sealed class AudioService
{
    private readonly Dictionary<GameAudioCue, SoundEffect> _effects = [];
    private readonly Dictionary<GameAudioCue, SoundEffectInstance> _loopInstances = [];

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
        foreach (var instance in _loopInstances.Values)
        {
            instance.Volume = NormalizedVolume;
        }

        return true;
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

    // Stop and dispose one loop instance if it is currently tracked.
    public void StopLoop(GameAudioCue cue)
    {
        if (!_loopInstances.Remove(cue, out var instance))
        {
            return;
        }

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
