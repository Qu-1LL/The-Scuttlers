using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace TriloGame.Game.Audio;

public sealed class MusicService
{
    private readonly Dictionary<MusicTrack, SoundEffectInstance> _layers = [];
    private MusicTrack _targetTrack = MusicTrack.AdaptiveTest1;
    private float _fadeSecondsRemaining;
    private float _fadeDurationSeconds;
    private bool _isStarted;

    public int VolumePercent { get; private set; } = 100;
    public float NormalizedVolume => VolumePercent / 100f;
    public bool IsMusicEnabled { get; private set; } = true;

    public void Register(MusicTrack track, SoundEffect music)
    {
        var instance = music.CreateInstance();
        instance.IsLooped = true;
        instance.Volume = 0f;
        _layers[track] = instance;
    }

    public void Start(MusicTrack audibleTrack)
    {
        _targetTrack = audibleTrack;
        _isStarted = true;

        if (!IsMusicEnabled)
        {
            StopLayerPlayback();
            return;
        }

        foreach (var pair in _layers)
        {
            pair.Value.Volume = pair.Key == audibleTrack ? NormalizedVolume : 0f;
            pair.Value.Play();
        }
    }

    public void CrossfadeTo(MusicTrack track, TimeSpan duration)
    {
        if (_targetTrack == track)
        {
            return;
        }

        _targetTrack = track;
        if (!IsMusicEnabled)
        {
            _fadeSecondsRemaining = 0f;
            _fadeDurationSeconds = 0f;
            return;
        }

        _fadeDurationSeconds = Math.Max(0.001f, (float)duration.TotalSeconds);
        _fadeSecondsRemaining = _fadeDurationSeconds;

        foreach (var layer in _layers.Values)
        {
            if (layer.State != SoundState.Playing)
            {
                layer.Play();
            }
        }
    }

    public void Update(GameTime gameTime)
    {
        if (!IsMusicEnabled || _fadeSecondsRemaining <= 0f)
        {
            return;
        }

        _fadeSecondsRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        var t = 1f - Math.Clamp(_fadeSecondsRemaining / _fadeDurationSeconds, 0f, 1f);

        foreach (var pair in _layers)
        {
            var target = pair.Key == _targetTrack ? NormalizedVolume : 0f;
            pair.Value.Volume = MathHelper.Lerp(pair.Value.Volume, target, t);
        }

        if (_fadeSecondsRemaining <= 0f)
        {
            foreach (var pair in _layers)
            {
                pair.Value.Volume = pair.Key == _targetTrack ? NormalizedVolume : 0f;
            }
        }
    }

    public void Stop()
    {
        _isStarted = false;
        _fadeSecondsRemaining = 0f;
        _fadeDurationSeconds = 0f;

        StopLayerPlayback();
    }

    public bool SetVolumePercent(int volumePercent)
    {
        var clamped = Math.Clamp(volumePercent, 0, 100);
        if (clamped == VolumePercent)
        {
            return false;
        }

        VolumePercent = clamped;
        ApplyTargetVolumes();

        return true;
    }

    public bool SetMusicEnabled(bool enabled)
    {
        if (enabled == IsMusicEnabled)
        {
            return false;
        }

        IsMusicEnabled = enabled;
        _fadeSecondsRemaining = 0f;
        _fadeDurationSeconds = 0f;

        if (!enabled)
        {
            StopLayerPlayback();
            return true;
        }

        if (_isStarted)
        {
            foreach (var layer in _layers.Values)
            {
                if (layer.State != SoundState.Playing)
                {
                    layer.Play();
                }
            }
        }

        ApplyTargetVolumes();
        return true;
    }

    private void ApplyTargetVolumes()
    {
        foreach (var pair in _layers)
        {
            pair.Value.Volume = IsMusicEnabled && pair.Key == _targetTrack ? NormalizedVolume : 0f;
        }
    }

    private void StopLayerPlayback()
    {
        foreach (var layer in _layers.Values)
        {
            layer.Volume = 0f;
            layer.Stop();
        }
    }
}
