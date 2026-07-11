using Microsoft.Xna.Framework;
using TriloGame.Game.Audio;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private static readonly TimeSpan SettingsMusicFadeDuration = TimeSpan.FromSeconds(0.5);

    private void UpdateMusic(GameTime gameTime)
    {
        _music.Update(gameTime);
    }

    private void ResetPassiveAudio()
    {
        _focusAudioSystem.Reset();
    }

    // Keep building-focus loops active only during unobstructed gameplay.
    private void SyncGameplayAudio()
    {
        if (_settingsMenuOpen)
        {
            ResetPassiveAudio();
            return;
        }

        _focusAudioSystem.Update(_session, _camera);
    }

    private void StartGameplayMusic()
    {
        ResetPassiveAudio();
        _music.Start(MusicTrack.AdaptiveTest1);
    }

    private void StopGameplayMusic()
    {
        ResetPassiveAudio();
        _music.Stop();
    }

    private void TransitionAudioForSettingsOpen()
    {
        ResetPassiveAudio();
        if (_mainMenuOpen)
        {
            return;
        }

        _music.CrossfadeTo(MusicTrack.AdaptiveTest2, SettingsMusicFadeDuration);
    }

    private void TransitionAudioForSettingsClose()
    {
        if (_mainMenuOpen)
        {
            return;
        }

        _music.CrossfadeTo(MusicTrack.AdaptiveTest1, SettingsMusicFadeDuration);
    }

    private void SetMasterVolume(int volumePercent)
    {
        PlayUiSelectSound();
        if (_audio.SetVolumePercent(volumePercent) | _music.SetVolumePercent(volumePercent))
        {
            _audio.Play(GameAudioCue.VolumeSound);
        }
    }
}
