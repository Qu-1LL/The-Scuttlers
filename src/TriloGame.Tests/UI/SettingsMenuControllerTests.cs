using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class SettingsMenuControllerTests
{
    [Fact]
    public void HandlePointerUp_ClickingHudButtonRequestsOpenWhenClosed()
    {
        var controller = new SettingsMenuController();
        var viewport = new Point(1440, 900);

        var result = controller.HandlePointerUp(
            SettingsMenuLayout.GetSettingsButtonBounds(viewport).Center,
            viewport,
            includeTopHudButton: true,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedOpen, result.Outcome);
    }

    private static SettingsMenuController OpenController()
    {
        var controller = new SettingsMenuController();
        controller.Open(pauseSimulationIfNeeded: false, isMainMenuOpen: false, isSimulationPaused: true);
        return controller;
    }

    [Fact]
    public void HandlePointerUp_FullscreenButtonRequestsFullscreen()
    {
        var controller = OpenController();
        var viewport = new Point(1440, 900);
        var panel = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);

        var result = controller.HandlePointerUp(
            SettingsMenuLayout.GetFullscreenButtonBounds(panel).Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.DisplayModeChanged, result.Outcome);
        Assert.Equal(GameDisplayMode.Fullscreen, result.DisplayMode);
    }

    [Fact]
    public void HandlePointerUp_WindowedButtonRequestsWindowed()
    {
        var controller = OpenController();
        var viewport = new Point(1440, 900);
        var panel = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);

        var result = controller.HandlePointerUp(
            SettingsMenuLayout.GetWindowedButtonBounds(panel).Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.DisplayModeChanged, result.Outcome);
        Assert.Equal(GameDisplayMode.Windowed, result.DisplayMode);
    }

    [Fact]
    public void HandlePointerUp_DisplayModeChangePreservesOtherSettings()
    {
        var controller = OpenController();
        var viewport = new Point(1440, 900);
        var panel = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);

        var result = controller.HandlePointerUp(
            SettingsMenuLayout.GetFullscreenButtonBounds(panel).Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: true,
            volumePercent: 35,
            musicEnabled: false);

        // Volume and music ride along untouched, so applying the outcome cannot reset them.
        Assert.Equal(35, result.VolumePercent);
        Assert.False(result.MusicEnabled);
    }

    // The display-mode row was inserted above these, so they must have shifted down rather than
    // overlapping it.
    [Fact]
    public void Layout_DisplayModeRowDoesNotOverlapNeighbouringControls()
    {
        var panel = SettingsMenuLayout.GetPanelBounds(new Point(1440, 900), includeQuitToMainMenu: true);
        var music = SettingsMenuLayout.GetMusicToggleBounds(panel);
        var fullscreen = SettingsMenuLayout.GetFullscreenButtonBounds(panel);
        var windowed = SettingsMenuLayout.GetWindowedButtonBounds(panel);
        var trilodex = SettingsMenuLayout.GetTrilodexButtonBounds(panel);
        var returnToMenu = SettingsMenuLayout.GetReturnToMainMenuButtonBounds(panel);

        Assert.True(fullscreen.Y >= music.Bottom, "display mode must sit below the music toggle");
        Assert.True(trilodex.Y >= fullscreen.Bottom, "trilodex must sit below the display mode row");
        Assert.True(returnToMenu.Y >= trilodex.Bottom, "return to menu must sit below trilodex");
        Assert.False(fullscreen.Intersects(windowed));
        // Everything stays inside the panel.
        Assert.True(returnToMenu.Bottom <= panel.Bottom);
    }

    [Fact]
    public void Close_ResumesSimulationOnlyWhenMenuPausedIt()
    {
        var controller = new SettingsMenuController();

        var pausedSimulation = controller.Open(
            pauseSimulationIfNeeded: true,
            isMainMenuOpen: false,
            isSimulationPaused: false);
        var resumedSimulation = controller.Close();

        Assert.True(pausedSimulation);
        Assert.True(resumedSimulation);
    }

    [Fact]
    public void Close_DoesNotResumeSimulationWhenItWasAlreadyPaused()
    {
        var controller = new SettingsMenuController();

        var pausedSimulation = controller.Open(
            pauseSimulationIfNeeded: true,
            isMainMenuOpen: false,
            isSimulationPaused: true);
        var resumedSimulation = controller.Close();

        Assert.False(pausedSimulation);
        Assert.False(resumedSimulation);
    }

    [Fact]
    public void HandlePointerUp_ClickingVolumeBarRequestsSnappedVolumeChange()
    {
        var controller = new SettingsMenuController();
        var viewport = new Point(1440, 900);
        controller.Open(
            pauseSimulationIfNeeded: false,
            isMainMenuOpen: false,
            isSimulationPaused: false);
        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);
        var volumeBarBounds = SettingsMenuLayout.GetVolumeBarBounds(panelBounds);
        var pointer = new Point(volumeBarBounds.Left + 129, volumeBarBounds.Center.Y);

        var result = controller.HandlePointerUp(
            pointer,
            viewport,
            includeTopHudButton: true,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.VolumeChanged, result.Outcome);
        Assert.Equal(SettingsMenuLayout.GetSnappedVolumeFromBar(volumeBarBounds, pointer.X), result.VolumePercent);
    }

    [Fact]
    public void HandlePointerUp_ClickingMusicToggleRequestsOppositeMusicState()
    {
        var controller = new SettingsMenuController();
        var viewport = new Point(1440, 900);
        controller.Open(
            pauseSimulationIfNeeded: false,
            isMainMenuOpen: false,
            isSimulationPaused: false);
        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);
        var toggleBounds = SettingsMenuLayout.GetMusicToggleBounds(panelBounds);

        var result = controller.HandlePointerUp(
            toggleBounds.Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.MusicToggled, result.Outcome);
        Assert.False(result.MusicEnabled);
        Assert.Equal(50, result.VolumePercent);
    }

    [Fact]
    public void HandlePointerUp_ClickingOutsideOpenPanelRequestsClose()
    {
        var controller = new SettingsMenuController();
        var viewport = new Point(1440, 900);
        controller.Open(
            pauseSimulationIfNeeded: false,
            isMainMenuOpen: false,
            isSimulationPaused: false);

        var result = controller.HandlePointerUp(
            Point.Zero,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedClose, result.Outcome);
    }

    [Fact]
    public void HandlePointerUp_ReturnToMainMenuRequiresGameplayVariant()
    {
        var controller = new SettingsMenuController();
        var viewport = new Point(1440, 900);
        controller.Open(
            pauseSimulationIfNeeded: false,
            isMainMenuOpen: false,
            isSimulationPaused: false);
        var gameplayPanel = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);
        var returnButton = SettingsMenuLayout.GetReturnToMainMenuButtonBounds(gameplayPanel);

        var gameplayResult = controller.HandlePointerUp(
            returnButton.Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: true,
            volumePercent: 50,
            musicEnabled: true);
        var mainMenuResult = controller.HandlePointerUp(
            returnButton.Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: false,
            volumePercent: 50,
            musicEnabled: true);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedReturnToMainMenu, gameplayResult.Outcome);
        Assert.Equal(SettingsMenuInteractionOutcome.Consumed, mainMenuResult.Outcome);
    }
}
