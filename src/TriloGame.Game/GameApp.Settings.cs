using Microsoft.Xna.Framework;
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
                _audio.VolumePercent,
                _music.IsMusicEnabled));
    }

    private bool HandleSettingsPanelClick(Point point, bool allowQuitToMainMenu)
    {
        return HandleSettingsInteraction(
            _settingsMenu.HandlePointerUp(
                point,
                Window.ClientBounds.Size,
                includeTopHudButton: false,
                allowQuitToMainMenu,
                _audio.VolumePercent,
                _music.IsMusicEnabled));
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
            case SettingsMenuInteractionOutcome.MusicToggled:
                SetMusicEnabledSetting(result.MusicEnabled);
                return true;
            case SettingsMenuInteractionOutcome.DisplayModeChanged:
                SetDisplayModeSetting(result.DisplayMode);
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
        SetMasterVolume(volumePercent);
    }

    private void SetMusicEnabledSetting(bool musicEnabled)
    {
        PlayUiSelectSound();
        _music.SetMusicEnabled(musicEnabled);
    }

    // Borderless in both modes: fullscreen uses the desktop resolution, windowed returns to the
    // fixed design size. MonoGame's hardware-mode-switch fullscreen is avoided so alt-tabbing and
    // switching back does not renegotiate the display mode.
    private void SetDisplayModeSetting(GameDisplayMode displayMode)
    {
        if (_displayMode == displayMode)
        {
            return;
        }

        PlayUiSelectSound();
        _displayMode = displayMode;
        ApplyDisplayMode();
    }

    // Just the swap chain. Split out of ApplyDisplayMode so startup can establish the mode before the
    // rest of the renderer exists: HandleViewportResize below assumes a constructed camera and UI, and
    // during Initialize neither is ready. Initialize calls this; everything after calls
    // ApplyDisplayMode.
    internal void ConfigureDisplayMode()
    {
        if (_displayMode == GameDisplayMode.Fullscreen)
        {
            _graphics.PreferredBackBufferWidth = GraphicsDevice.Adapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsDevice.Adapter.CurrentDisplayMode.Height;
            _graphics.HardwareModeSwitch = false;
            _graphics.IsFullScreen = true;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = WindowedWidth;
            _graphics.PreferredBackBufferHeight = WindowedHeight;
            _graphics.IsFullScreen = false;
        }

        _graphics.ApplyChanges();
    }

    private void ApplyDisplayMode()
    {
        ConfigureDisplayMode();
        // ApplyChanges is EXPECTED to resize the window synchronously and fire Window.ClientSizeChanged
        // before returning, which is already wired to HandleViewportResize() - but that was found not
        // to hold for every platform on the fullscreen path specifically: the camera's ViewCenter
        // simply never got updated for it, which is a full window's worth of "everything drawn shifted
        // by however much the resolution changed" (a hard cutoff along one edge, past which nothing is
        // drawn at all), not merely cosmetic. So this calls the same idempotent method explicitly
        // rather than trusting the event alone. HandleViewportResize is a no-op if the event already
        // handled this exact resize, so having both triggers cannot double-apply the adjustment - see
        // that method's comment.
        HandleViewportResize();
    }

    private void OpenSettingsMenu(bool pauseSimulationIfNeeded = true)
    {
        if (_settingsMenu.IsOpen)
        {
            return;
        }

        TransitionAudioForSettingsOpen();
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

        TransitionAudioForSettingsClose();
        var shouldResumeSimulation = _settingsMenu.Close();
        if (shouldResumeSimulation)
        {
            _gamePaused = false;
        }
    }
}
