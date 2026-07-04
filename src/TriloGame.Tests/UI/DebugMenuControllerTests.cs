using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class DebugMenuControllerTests
{
    [Fact]
    public void GetButtons_ReflectsPauseAndSelectedSpeedState()
    {
        var controller = new DebugMenuController();
        var state = new DebugMenuState(
            IsPaused: true,
            TickSpeedMs: GameConstants.TickSpeedFast,
            ActiveBfsDebugField: "enemy",
            DebugMapVisible: true,
            DebugAntHolePlacementMode: false);

        var buttons = controller.GetButtons(new Point(1440, 900), state);

        Assert.Contains(buttons, button => button.Action == DebugMenuAction.TogglePause && button.Label == "Resume");
        Assert.Contains(buttons, button => button.Action == DebugMenuAction.SpeedFast && button.Selected);
        Assert.Contains(buttons, button => button.Action == DebugMenuAction.ShowEnemyField && button.Selected);
        Assert.Contains(buttons, button => button.Action == DebugMenuAction.ToggleMapOverlay && button.Selected);
        Assert.DoesNotContain(buttons, button => button.Action is DebugMenuAction.SpawnEnemy or DebugMenuAction.SpawnTrilobite);
    }

    [Fact]
    public void TryGetButtonAction_ReturnsClickedButtonAction()
    {
        var controller = new DebugMenuController();
        var viewport = new Point(1440, 900);
        var state = new DebugMenuState(
            IsPaused: false,
            TickSpeedMs: GameConstants.TickSpeedNormal,
            ActiveBfsDebugField: null,
            DebugMapVisible: false,
            DebugAntHolePlacementMode: false);
        var closeButton = controller.GetButtons(viewport, state).Single(button => button.Action == DebugMenuAction.Close);

        var handled = controller.TryGetButtonAction(viewport, closeButton.Bounds.Center, state, out var action);

        Assert.True(handled);
        Assert.Equal(DebugMenuAction.Close, action);
    }

    [Fact]
    public void TryGetButtonAction_IgnoresClicksOutsideButtons()
    {
        var controller = new DebugMenuController();

        var handled = controller.TryGetButtonAction(
            new Point(1440, 900),
            Point.Zero,
            new DebugMenuState(false, GameConstants.TickSpeedNormal, null, false, false),
            out _);

        Assert.False(handled);
    }
}
