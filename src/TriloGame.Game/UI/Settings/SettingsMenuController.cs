using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Settings;

public enum SettingsMenuInteractionOutcome
{
    None,
    Consumed,
    RequestedOpen,
    RequestedClose,
    VolumeChanged,
    MusicToggled,
    RequestedOpenTrilodex,
    RequestedReturnToMainMenu
}

public readonly record struct SettingsMenuInteractionResult(
    SettingsMenuInteractionOutcome Outcome,
    int VolumePercent = 0,
    bool MusicEnabled = true)
{
    public bool Handled => Outcome != SettingsMenuInteractionOutcome.None;
}

public sealed class SettingsMenuController
{
    private bool _resumeSimulationAfterClose;

    public bool IsOpen { get; private set; }

    public void Reset()
    {
        IsOpen = false;
        _resumeSimulationAfterClose = false;
    }

    public bool Open(bool pauseSimulationIfNeeded, bool isMainMenuOpen, bool isSimulationPaused)
    {
        if (IsOpen)
        {
            return false;
        }

        IsOpen = true;
        _resumeSimulationAfterClose = pauseSimulationIfNeeded && !isMainMenuOpen && !isSimulationPaused;
        return _resumeSimulationAfterClose;
    }

    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsOpen = false;
        var shouldResumeSimulation = _resumeSimulationAfterClose;
        _resumeSimulationAfterClose = false;
        return shouldResumeSimulation;
    }

    public SettingsMenuInteractionOutcome HandleEscape()
    {
        return IsOpen
            ? SettingsMenuInteractionOutcome.RequestedClose
            : SettingsMenuInteractionOutcome.None;
    }

    public bool CoversScreenPoint(Point point, Point viewport, bool includeTopHudButton, bool allowQuitToMainMenu)
    {
        if (includeTopHudButton && SettingsMenuLayout.GetSettingsButtonBounds(viewport).Contains(point))
        {
            return true;
        }

        return IsOpen && SettingsMenuLayout.GetPanelBounds(viewport, allowQuitToMainMenu).Contains(point);
    }

    public SettingsMenuInteractionResult HandlePointerUp(
        Point point,
        Point viewport,
        bool includeTopHudButton,
        bool allowQuitToMainMenu,
        int volumePercent,
        bool musicEnabled)
    {
        if (includeTopHudButton && SettingsMenuLayout.GetSettingsButtonBounds(viewport).Contains(point))
        {
            return new SettingsMenuInteractionResult(
                IsOpen
                    ? SettingsMenuInteractionOutcome.RequestedClose
                    : SettingsMenuInteractionOutcome.RequestedOpen);
        }

        if (!IsOpen)
        {
            return default;
        }

        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport, allowQuitToMainMenu);
        var volumeDownBounds = SettingsMenuLayout.GetVolumeDownButtonBounds(panelBounds);
        var volumeUpBounds = SettingsMenuLayout.GetVolumeUpButtonBounds(panelBounds);
        var volumeBarBounds = SettingsMenuLayout.GetVolumeBarBounds(panelBounds);
        var musicToggleBounds = SettingsMenuLayout.GetMusicToggleBounds(panelBounds);
        var trilodexBounds = SettingsMenuLayout.GetTrilodexButtonBounds(panelBounds);
        if (SettingsMenuLayout.GetCloseButtonBounds(panelBounds).Contains(point) ||
            SettingsMenuLayout.GetBackButtonBounds(panelBounds).Contains(point))
        {
            return new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedClose);
        }

        if (volumeDownBounds.Contains(point))
        {
            return new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.VolumeChanged,
                Math.Clamp(volumePercent - SettingsMenuLayout.VolumeStep, 0, 100));
        }

        if (volumeUpBounds.Contains(point))
        {
            return new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.VolumeChanged,
                Math.Clamp(volumePercent + SettingsMenuLayout.VolumeStep, 0, 100));
        }

        if (volumeBarBounds.Contains(point))
        {
            return new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.VolumeChanged,
                SettingsMenuLayout.GetSnappedVolumeFromBar(volumeBarBounds, point.X));
        }

        if (musicToggleBounds.Contains(point))
        {
            return new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.MusicToggled,
                volumePercent,
                !musicEnabled);
        }

        if (trilodexBounds.Contains(point))
        {
            return new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedOpenTrilodex);
        }

        if (allowQuitToMainMenu && SettingsMenuLayout.GetReturnToMainMenuButtonBounds(panelBounds).Contains(point))
        {
            return new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedReturnToMainMenu);
        }

        if (!panelBounds.Contains(point))
        {
            return new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedClose);
        }

        return new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.Consumed);
    }
}
