using Microsoft.Xna.Framework;
using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;

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

    private void PlayBuildingPlacementSound(GridPoint location, Building building)
    {
        var center = new Vector2(
            (location.X * TileConstants.TileSize) + ((building.Size.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((building.Size.Y - 1) * TileConstants.TileHalfSize));
        var gain = ScreenSpaceAudio.CalculateVisibleCoverage(
            _camera.WorldToScreen(center),
            Math.Max(1, building.Size.X),
            Math.Max(1, building.Size.Y),
            _camera);
        _audio.Play(GameAudioCue.BuildingPlace, gain);
    }
}
