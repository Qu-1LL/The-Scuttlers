using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Gum;

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
    RequestedReturnToMainMenu,
    DisplayModeChanged,
    ResolutionStepRequested
}

public enum GameDisplayMode
{
    Windowed,
    Fullscreen
}

// The panel's clickable regions, as ids. The map from id to rectangle lives in the layout, and the
// map from id to outcome lives in one switch below - so a control is added by naming it here and
// placing it there, with no hit-test branch to insert in the right position.
internal enum SettingsControl
{
    TopHudButton,
    Close,
    Back,
    VolumeDown,
    VolumeUp,
    VolumeBar,
    MusicToggle,
    Fullscreen,
    Windowed,
    ResolutionDown,
    ResolutionUp,
    Trilodex,
    ReturnToMainMenu
}

public readonly record struct SettingsMenuInteractionResult(
    SettingsMenuInteractionOutcome Outcome,
    int VolumePercent = 0,
    bool MusicEnabled = true,
    GameDisplayMode DisplayMode = GameDisplayMode.Windowed,
    // -1 or +1 for ResolutionStepRequested. A DIRECTION rather than a resolution, because which
    // sizes exist depends on the desktop the game is running on - something the menu has no business
    // knowing. The host steps its own list; see GameApp.StepWindowedResolution.
    int ResolutionStep = 0)
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

        return IsOpen && SettingsMenuLayout.BuildPanel(viewport, allowQuitToMainMenu).Panel.Contains(point);
    }

    // One region list, ordered topmost-first, built from the same layout the renderer draws from.
    // Exposed internally so a test can assert the panel has no overlapping controls without having to
    // replay a chain of branches - see UiRegionMap.FindOverlaps.
    internal static UiRegionMap<SettingsControl> BuildRegions(
        SettingsPanelLayout layout,
        Point viewport,
        bool includeTopHudButton,
        bool allowQuitToMainMenu,
        bool isOpen)
    {
        var regions = new UiRegionMap<SettingsControl>();
        regions.AddIf(
            includeTopHudButton,
            SettingsControl.TopHudButton,
            SettingsMenuLayout.GetSettingsButtonBounds(viewport));

        if (!isOpen)
        {
            return regions;
        }

        return regions
            .Add(SettingsControl.Close, layout.Close)
            .Add(SettingsControl.Back, layout.Back)
            .Add(SettingsControl.VolumeDown, layout.VolumeDown)
            .Add(SettingsControl.VolumeUp, layout.VolumeUp)
            .Add(SettingsControl.VolumeBar, layout.VolumeBar)
            .Add(SettingsControl.MusicToggle, layout.MusicToggle)
            .Add(SettingsControl.Fullscreen, layout.Fullscreen)
            .Add(SettingsControl.Windowed, layout.Windowed)
            .Add(SettingsControl.ResolutionDown, layout.ResolutionDown)
            .Add(SettingsControl.ResolutionUp, layout.ResolutionUp)
            .Add(SettingsControl.Trilodex, layout.Trilodex)
            .AddIf(allowQuitToMainMenu, SettingsControl.ReturnToMainMenu, layout.ReturnToMainMenu);
    }

    public SettingsMenuInteractionResult HandlePointerUp(
        Point point,
        Point viewport,
        bool includeTopHudButton,
        bool allowQuitToMainMenu,
        int volumePercent,
        bool musicEnabled)
    {
        var layout = SettingsMenuLayout.BuildPanel(viewport, allowQuitToMainMenu);
        var regions = BuildRegions(layout, viewport, includeTopHudButton, allowQuitToMainMenu, IsOpen);

        if (!regions.TryHit(point, out var control, out _))
        {
            // Nothing clickable was hit. Inside the panel that is dead space and the click is
            // swallowed; outside it, the click dismisses.
            if (!IsOpen)
            {
                return default;
            }

            return layout.Panel.Contains(point)
                ? new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.Consumed)
                : new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedClose);
        }

        return control switch
        {
            SettingsControl.TopHudButton => new SettingsMenuInteractionResult(
                IsOpen
                    ? SettingsMenuInteractionOutcome.RequestedClose
                    : SettingsMenuInteractionOutcome.RequestedOpen),
            SettingsControl.Close or SettingsControl.Back =>
                new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedClose),
            SettingsControl.VolumeDown => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.VolumeChanged,
                Math.Clamp(volumePercent - SettingsMenuLayout.VolumeStep, 0, 100)),
            SettingsControl.VolumeUp => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.VolumeChanged,
                Math.Clamp(volumePercent + SettingsMenuLayout.VolumeStep, 0, 100)),
            SettingsControl.VolumeBar => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.VolumeChanged,
                SettingsMenuLayout.GetSnappedVolumeFromBar(layout.VolumeBar, point.X)),
            SettingsControl.MusicToggle => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.MusicToggled,
                volumePercent,
                !musicEnabled),
            SettingsControl.Fullscreen => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.DisplayModeChanged,
                volumePercent,
                musicEnabled,
                GameDisplayMode.Fullscreen),
            SettingsControl.Windowed => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.DisplayModeChanged,
                volumePercent,
                musicEnabled,
                GameDisplayMode.Windowed),
            SettingsControl.ResolutionDown => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.ResolutionStepRequested,
                volumePercent,
                musicEnabled,
                ResolutionStep: -1),
            SettingsControl.ResolutionUp => new SettingsMenuInteractionResult(
                SettingsMenuInteractionOutcome.ResolutionStepRequested,
                volumePercent,
                musicEnabled,
                ResolutionStep: 1),
            SettingsControl.Trilodex =>
                new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedOpenTrilodex),
            SettingsControl.ReturnToMainMenu =>
                new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.RequestedReturnToMainMenu),
            _ => new SettingsMenuInteractionResult(SettingsMenuInteractionOutcome.Consumed)
        };
    }
}
