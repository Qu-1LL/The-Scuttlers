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
            case SettingsMenuInteractionOutcome.ResolutionStepRequested:
                StepWindowedResolution(result.ResolutionStep);
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

    // Fullscreen is borderless at the desktop resolution; windowed is an ordinary OS window - title
    // bar, minimise and close buttons, draggable edges - sized by the Resolution setting.
    // MonoGame's hardware-mode-switch fullscreen is avoided so alt-tabbing and switching back does
    // not renegotiate the display mode.
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
            _graphics.PreferredBackBufferWidth = _windowedResolution.Width;
            _graphics.PreferredBackBufferHeight = _windowedResolution.Height;
            _graphics.IsFullScreen = false;
        }

        // The window chrome follows the mode. Borderless is what makes fullscreen cover the screen
        // without a hardware mode switch; windowed wants the opposite - the OS title bar, with its
        // minimise and close buttons, and edges the player can drag.
        //
        // Applied on BOTH sides of ApplyChanges, and the second call is the load-bearing one.
        //
        // SDL ignores SDL_SetWindowBordered and SDL_SetWindowResizable while a window is fullscreen,
        // but MonoGame's setters cache the value they were handed regardless. Setting the flags only
        // beforehand therefore left the fullscreen -> windowed switch believing it had removed the
        // border when SDL had discarded the call: the window came back with no title bar, and it
        // only appeared on the NEXT ConfigureDisplayMode (a resolution change), because by then the
        // window was no longer fullscreen and the same call finally landed. Repeating the assignment
        // after ApplyChanges has exited fullscreen is what makes the transition itself apply.
        //
        // The call before ApplyChanges is still worth keeping: MonoGame re-asserts the border from
        // its own cached flag while tearing down fullscreen, so leaving it stale there would fight
        // the transition on the way in.
        // Everything from here until the mode has settled is US resizing the window, not the player,
        // so HandleViewportResize must not mistake it for a chosen size - see the flag's declaration.
        // Saved and restored rather than cleared, so this composes when ApplyDisplayMode has already
        // raised it around the whole transition; clearing here would drop the guard while its
        // trailing HandleViewportResize still had to run.
        var wasApplyingDisplayMode = _applyingDisplayMode;
        _applyingDisplayMode = true;
        try
        {
            ApplyWindowChrome();
            _graphics.ApplyChanges();
            ApplyWindowChrome();
        }
        finally
        {
            _applyingDisplayMode = wasApplyingDisplayMode;
        }
    }

    private void ApplyWindowChrome()
    {
        Window.IsBorderless = _displayMode == GameDisplayMode.Fullscreen;
        Window.AllowUserResizing = _displayMode == GameDisplayMode.Windowed;
    }

    // The sizes the Resolution setting can offer on this machine. Bounded by the desktop so the
    // player cannot put the title bar - now their only way to move or close a window - off-screen.
    private IReadOnlyList<GameResolution> GetSelectableResolutions()
    {
        var display = GraphicsDevice.Adapter.CurrentDisplayMode;
        return GameResolutions.GetSelectable(display.Width, display.Height);
    }

    private void StepWindowedResolution(int direction)
    {
        // Inert in fullscreen: the desktop already decides the resolution there, so a step would
        // silently change a size the player cannot see the effect of. The row is drawn greyed to
        // match - see SettingsMenuRenderer.DrawResolutionRow.
        if (_displayMode != GameDisplayMode.Windowed)
        {
            return;
        }

        var next = GameResolutions.Step(GetSelectableResolutions(), _windowedResolution, direction);
        if (next == _windowedResolution)
        {
            return;
        }

        PlayUiSelectSound();
        _windowedResolution = next;
        ApplyDisplayMode();
    }

    private void ApplyDisplayMode()
    {
        _applyingDisplayMode = true;
        try
        {
            ApplyDisplayModeCore();
        }
        finally
        {
            _applyingDisplayMode = false;
        }
    }

    private void ApplyDisplayModeCore()
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
