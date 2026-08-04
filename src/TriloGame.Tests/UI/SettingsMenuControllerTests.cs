using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class SettingsMenuControllerTests
{
    private static readonly Point Viewport = new(1440, 900);

    private static SettingsMenuController OpenController()
    {
        var controller = new SettingsMenuController();
        controller.Open(pauseSimulationIfNeeded: false, isMainMenuOpen: false, isSimulationPaused: true);
        return controller;
    }

    private static SettingsPanelLayout Layout(bool allowQuitToMainMenu = true)
    {
        return SettingsMenuLayout.BuildPanel(Viewport, allowQuitToMainMenu);
    }

    private static SettingsMenuInteractionResult Click(
        SettingsMenuController controller,
        Point point,
        bool includeTopHudButton = false,
        bool allowQuitToMainMenu = true,
        int volumePercent = 50,
        bool musicEnabled = true)
    {
        return controller.HandlePointerUp(
            point,
            Viewport,
            includeTopHudButton,
            allowQuitToMainMenu,
            volumePercent,
            musicEnabled);
    }

    [Fact]
    public void HandlePointerUp_ClickingHudButtonRequestsOpenWhenClosed()
    {
        var controller = new SettingsMenuController();

        var result = Click(
            controller,
            SettingsMenuLayout.GetSettingsButtonBounds(Viewport).Center,
            includeTopHudButton: true);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedOpen, result.Outcome);
    }

    [Fact]
    public void HandlePointerUp_ClickingHudButtonRequestsCloseWhenOpen()
    {
        var controller = OpenController();

        var result = Click(
            controller,
            SettingsMenuLayout.GetSettingsButtonBounds(Viewport).Center,
            includeTopHudButton: true);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedClose, result.Outcome);
    }

    [Fact]
    public void HandlePointerUp_FullscreenButtonRequestsFullscreen()
    {
        var result = Click(OpenController(), Layout().Fullscreen.Center);

        Assert.Equal(SettingsMenuInteractionOutcome.DisplayModeChanged, result.Outcome);
        Assert.Equal(GameDisplayMode.Fullscreen, result.DisplayMode);
    }

    [Fact]
    public void HandlePointerUp_WindowedButtonRequestsWindowed()
    {
        var result = Click(OpenController(), Layout().Windowed.Center);

        Assert.Equal(SettingsMenuInteractionOutcome.DisplayModeChanged, result.Outcome);
        Assert.Equal(GameDisplayMode.Windowed, result.DisplayMode);
    }

    [Fact]
    public void HandlePointerUp_DisplayModeChangeCarriesTheOtherSettingsUntouched()
    {
        var result = Click(
            OpenController(),
            Layout().Fullscreen.Center,
            volumePercent: 35,
            musicEnabled: false);

        // Volume and music ride along untouched, so applying the outcome cannot reset them.
        Assert.Equal(35, result.VolumePercent);
        Assert.False(result.MusicEnabled);
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
        var bar = Layout().VolumeBar;
        var pointer = new Point(bar.Left + 129, bar.Center.Y);

        var result = Click(OpenController(), pointer, includeTopHudButton: true);

        Assert.Equal(SettingsMenuInteractionOutcome.VolumeChanged, result.Outcome);
        Assert.Equal(SettingsMenuLayout.GetSnappedVolumeFromBar(bar, pointer.X), result.VolumePercent);
    }

    [Fact]
    public void HandlePointerUp_ClickingMusicToggleRequestsOppositeMusicState()
    {
        var result = Click(OpenController(), Layout().MusicToggle.Center);

        Assert.Equal(SettingsMenuInteractionOutcome.MusicToggled, result.Outcome);
        Assert.False(result.MusicEnabled);
        Assert.Equal(50, result.VolumePercent);
    }

    [Fact]
    public void HandlePointerUp_ClickingOutsideOpenPanelRequestsClose()
    {
        var result = Click(OpenController(), Point.Zero);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedClose, result.Outcome);
    }

    [Fact]
    public void HandlePointerUp_ReturnToMainMenuRequiresGameplayVariant()
    {
        var returnButton = Layout().ReturnToMainMenu;

        var gameplayResult = Click(OpenController(), returnButton.Center, allowQuitToMainMenu: true);
        var mainMenuResult = Click(OpenController(), returnButton.Center, allowQuitToMainMenu: false);

        Assert.Equal(SettingsMenuInteractionOutcome.RequestedReturnToMainMenu, gameplayResult.Outcome);
        // Without the quit row the panel is shorter, so that point is no longer a control - but it is
        // still inside the panel, so the click is swallowed rather than dismissing.
        Assert.NotEqual(SettingsMenuInteractionOutcome.RequestedReturnToMainMenu, mainMenuResult.Outcome);
    }

    // The menu reports a DIRECTION, not a resolution: which sizes exist depends on the desktop, and
    // the menu has no way to know that.
    [Theory]
    [InlineData(true, -1)]
    [InlineData(false, 1)]
    public void HandlePointerUp_ResolutionArrowsRequestAStepInTheirDirection(bool isDownArrow, int expectedStep)
    {
        var layout = Layout();
        var arrow = isDownArrow ? layout.ResolutionDown : layout.ResolutionUp;

        var result = Click(OpenController(), arrow.Center);

        Assert.Equal(SettingsMenuInteractionOutcome.ResolutionStepRequested, result.Outcome);
        Assert.Equal(expectedStep, result.ResolutionStep);
    }

    // The value readout between the arrows is not a control - clicking it must not step anything,
    // or a player reading the current size would change it by accident.
    [Fact]
    public void HandlePointerUp_ClickingTheResolutionValueDoesNothingButConsume()
    {
        var result = Click(OpenController(), Layout().ResolutionValue.Center);

        Assert.Equal(SettingsMenuInteractionOutcome.Consumed, result.Outcome);
    }

    // The region list is what makes this checkable at all: with a hand-ordered if-chain there was no
    // way to ask "does any control shadow another" except by replaying every branch.
    [Fact]
    public void Regions_DoNotOverlapEachOther()
    {
        foreach (var allowQuit in new[] { true, false })
        {
            var regions = SettingsMenuController.BuildRegions(
                Layout(allowQuit),
                Viewport,
                includeTopHudButton: true,
                allowQuitToMainMenu: allowQuit,
                isOpen: true);

            var overlaps = regions.FindOverlaps();

            Assert.True(
                overlaps.Count == 0,
                $"overlapping controls (allowQuit: {allowQuit}): " +
                string.Join(", ", overlaps.Select(pair => $"{pair.First}/{pair.Second}")));
        }
    }

    [Fact]
    public void Regions_AreEmptyWhileThePanelIsClosedApartFromTheHudButton()
    {
        var regions = SettingsMenuController.BuildRegions(
            Layout(),
            Viewport,
            includeTopHudButton: true,
            allowQuitToMainMenu: true,
            isOpen: false);

        Assert.Single(regions.Regions);
        Assert.Equal(SettingsControl.TopHudButton, regions.Regions[0].Id);
    }

    // Every control resolves to its own outcome - the check that adding a control has not shadowed
    // an existing one.
    [Fact]
    public void HandlePointerUp_EveryPanelControlResolvesToItsOwnOutcome()
    {
        var layout = Layout();
        var expected = new (Rectangle Bounds, SettingsMenuInteractionOutcome Outcome)[]
        {
            (layout.Close, SettingsMenuInteractionOutcome.RequestedClose),
            (layout.Back, SettingsMenuInteractionOutcome.RequestedClose),
            (layout.VolumeDown, SettingsMenuInteractionOutcome.VolumeChanged),
            (layout.VolumeUp, SettingsMenuInteractionOutcome.VolumeChanged),
            (layout.VolumeBar, SettingsMenuInteractionOutcome.VolumeChanged),
            (layout.MusicToggle, SettingsMenuInteractionOutcome.MusicToggled),
            (layout.Fullscreen, SettingsMenuInteractionOutcome.DisplayModeChanged),
            (layout.Windowed, SettingsMenuInteractionOutcome.DisplayModeChanged),
            (layout.ResolutionDown, SettingsMenuInteractionOutcome.ResolutionStepRequested),
            (layout.ResolutionUp, SettingsMenuInteractionOutcome.ResolutionStepRequested),
            (layout.Trilodex, SettingsMenuInteractionOutcome.RequestedOpenTrilodex),
            (layout.ReturnToMainMenu, SettingsMenuInteractionOutcome.RequestedReturnToMainMenu)
        };

        foreach (var (bounds, outcome) in expected)
        {
            Assert.Equal(outcome, Click(OpenController(), bounds.Center).Outcome);
        }
    }
}
