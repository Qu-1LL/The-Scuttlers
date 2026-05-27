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
            volumePercent: 50);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedOpen, result.Outcome);
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
            volumePercent: 50);

        Assert.Equal(SettingsMenuInteractionOutcome.VolumeChanged, result.Outcome);
        Assert.Equal(SettingsMenuLayout.GetSnappedVolumeFromBar(volumeBarBounds, pointer.X), result.VolumePercent);
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
            volumePercent: 50);

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
            volumePercent: 50);
        var mainMenuResult = controller.HandlePointerUp(
            returnButton.Center,
            viewport,
            includeTopHudButton: false,
            allowQuitToMainMenu: false,
            volumePercent: 50);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedReturnToMainMenu, gameplayResult.Outcome);
        Assert.Equal(SettingsMenuInteractionOutcome.Consumed, mainMenuResult.Outcome);
    }
}
