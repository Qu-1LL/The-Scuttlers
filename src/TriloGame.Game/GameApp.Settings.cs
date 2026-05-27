using Microsoft.Xna.Framework;
using TriloGame.Game.Audio;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private bool SettingsCoversPoint(Point point)
    {
        return _settingsMenu.CoversScreenPoint(
            point,
            Window.ClientBounds.Size,
            includeTopHudButton: !_mainMenuOpen,
            allowQuitToMainMenu: !_mainMenuOpen);
    }

    private bool HandleSettingsClick(Point point)
    {
        return HandleSettingsInteraction(
            _settingsMenu.HandlePointerUp(
                point,
                Window.ClientBounds.Size,
                includeTopHudButton: true,
                allowQuitToMainMenu: true,
                _audio.VolumePercent));
    }

    private bool HandleSettingsPanelClick(Point point, bool allowQuitToMainMenu)
    {
        return HandleSettingsInteraction(
            _settingsMenu.HandlePointerUp(
                point,
                Window.ClientBounds.Size,
                includeTopHudButton: false,
                allowQuitToMainMenu,
                _audio.VolumePercent));
    }

    private bool HandleSettingsInteraction(SettingsMenuInteractionResult result)
    {
        if (!result.Handled)
        {
            return false;
        }

        switch (result.Outcome)
        {
            case SettingsMenuInteractionOutcome.RequestedOpen:
                PlayUiSelectSound();
                OpenSettingsMenu();
                return true;
            case SettingsMenuInteractionOutcome.RequestedClose:
                PlayUiSelectSound();
                CloseSettingsMenu();
                return true;
            case SettingsMenuInteractionOutcome.VolumeChanged:
                SetVolumeSetting(result.VolumePercent);
                return true;
            case SettingsMenuInteractionOutcome.RequestedOpenTrilodex:
                PlayUiSelectSound();
                OpenTrilodexMenu(pauseSimulationIfNeeded: !_mainMenuOpen);
                return true;
            case SettingsMenuInteractionOutcome.RequestedReturnToMainMenu:
                PlayUiSelectSound();
                ReturnToMainMenu();
                return true;
            case SettingsMenuInteractionOutcome.Consumed:
                return true;
            default:
                return false;
        }
    }

    private void SetVolumeSetting(int volumePercent)
    {
        PlayUiSelectSound();
        if (_audio.SetVolumePercent(volumePercent))
        {
            _audio.Play(GameAudioCue.VolumeSound);
        }
    }

    private void OpenSettingsMenu(bool pauseSimulationIfNeeded = true)
    {
        if (_settingsMenu.IsOpen)
        {
            return;
        }

        _settingsMenu.Open(pauseSimulationIfNeeded, _mainMenuOpen, _gamePaused);
        _roleRadialMenu = null;
        ResetPointerInteractionState();

        if (pauseSimulationIfNeeded && !_mainMenuOpen && !_gamePaused)
        {
            _gamePaused = true;
        }
    }

    private void CloseSettingsMenu()
    {
        if (!_settingsMenu.IsOpen)
        {
            return;
        }

        var shouldResumeSimulation = _settingsMenu.Close();
        if (shouldResumeSimulation)
        {
            _gamePaused = false;
        }
    }
}
