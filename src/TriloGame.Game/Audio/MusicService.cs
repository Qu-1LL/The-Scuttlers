using Microsoft.Xna.Framework.Media;

namespace TriloGame.Game.Audio;

public sealed class MusicService
{
    private readonly Dictionary<MusicTrack, Song> _tracks = [];
    private MusicTrack? _currentTrack;

    public int VolumePercent { get; private set; } = 100;

    public float NormalizedVolume => VolumePercent / 100f;

    public void Register(MusicTrack track, Song song)
    {
        _tracks[track] = song;
    }

    // Switch background music only when the requested track changes.
    public bool Play(MusicTrack track, bool repeat = true)
    {
        if (_currentTrack == track && MediaPlayer.State == MediaState.Playing)
        {
            return true;
        }

        if (!_tracks.TryGetValue(track, out var song))
        {
            return false;
        }

        _currentTrack = track;
        MediaPlayer.IsRepeating = repeat;
        MediaPlayer.Volume = NormalizedVolume;
        MediaPlayer.Play(song);
        return true;
    }

    public void Stop()
    {
        _currentTrack = null;
        MediaPlayer.Stop();
    }

    public bool SetVolumePercent(int volumePercent)
    {
        var clamped = Math.Clamp(volumePercent, 0, 100);
        if (clamped == VolumePercent)
        {
            return false;
        }

        VolumePercent = clamped;
        MediaPlayer.Volume = NormalizedVolume;
        return true;
    }
}