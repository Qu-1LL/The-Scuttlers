using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace TriloGame.Game.Audio;

public sealed class MusicService
{
    private readonly Dictionary<MusicTrack, SoundEffectInstance> _layers = [];
    private MusicTrack _targetTrack = MusicTrack.AdaptiveTest1;
    private float _fadeSecondsRemaining;
    private float _fadeDurationSeconds;

    public int VolumePercent { get; private set; } = 100;
    public float NormalizedVolume => VolumePercent / 100f;

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
        if (_fadeSecondsRemaining <= 0f)
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
        _fadeSecondsRemaining = 0f;
        _fadeDurationSeconds = 0f;

        foreach (var layer in _layers.Values)
        {
            layer.Volume = 0f;
            layer.Stop();
        }
    }

    public bool SetVolumePercent(int volumePercent)
    {
        var clamped = Math.Clamp(volumePercent, 0, 100);
        if (clamped == VolumePercent)
        {
            return false;
        }

        VolumePercent = clamped;
        foreach (var pair in _layers)
        {
            pair.Value.Volume = pair.Key == _targetTrack ? NormalizedVolume : 0f;
        }

        return true;
    }
}
