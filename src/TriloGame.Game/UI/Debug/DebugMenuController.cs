using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;

namespace TriloGame.Game.UI.Debug;

public enum DebugMenuAction
{
    TogglePause,
    SingleTick,
    SpeedSlow,
    SpeedNormal,
    SpeedFast,
    SpeedFastest,
    ToggleMapOverlay,
    ShowQueenField,
    ShowEnemyField,
    ShowColonyField,
    ClearField,
    ToggleRoleLabels,
    RestartGame,
    SpawnEnemy,
    SpawnTrilobite,
    PlaceAntHole,
    Close
}

public readonly record struct DebugMenuButton(
    DebugMenuAction Action,
    string Label,
    Rectangle Bounds,
    bool Enabled,
    bool Selected);

public readonly record struct DebugMenuState(
    bool IsPaused,
    double TickSpeedMs,
    string? ActiveBfsDebugField,
    bool DebugMapVisible,
    bool DebugAntHolePlacementMode);

public sealed class DebugMenuController
{
    public IReadOnlyList<DebugMenuButton> GetButtons(Point viewport, DebugMenuState state)
    {
        var layout = DebugMenuLayout.Build(viewport);
        var quickButtons = DebugMenuLayout.SplitRow(layout.QuickControlsRowBounds, 3, layout.ButtonGap);
        var speedButtons = DebugMenuLayout.SplitRow(layout.SpeedRowBounds, 4, layout.ButtonGap);
        var bfsTopButtons = DebugMenuLayout.SplitRow(layout.BfsTopRowBounds, 2, layout.ButtonGap);
        var bfsBottomButtons = DebugMenuLayout.SplitRow(layout.BfsBottomRowBounds, 3, layout.ButtonGap);
        var actionButtons = DebugMenuLayout.SplitRow(layout.ActionsRowBounds, 2, layout.ButtonGap);

        return
        [
            new DebugMenuButton(DebugMenuAction.TogglePause, state.IsPaused ? "Resume" : "Pause", quickButtons[0], true, false),
            new DebugMenuButton(DebugMenuAction.SingleTick, "Step Tick", quickButtons[1], true, false),
            new DebugMenuButton(DebugMenuAction.Close, "Close", quickButtons[2], true, false),
            new DebugMenuButton(DebugMenuAction.SpeedSlow, "500 ms", speedButtons[0], true, TickSpeedMatches(state, GameConstants.TickSpeedSlow)),
            new DebugMenuButton(DebugMenuAction.SpeedNormal, "250 ms", speedButtons[1], true, TickSpeedMatches(state, GameConstants.TickSpeedNormal)),
            new DebugMenuButton(DebugMenuAction.SpeedFast, "100 ms", speedButtons[2], true, TickSpeedMatches(state, GameConstants.TickSpeedFast)),
            new DebugMenuButton(DebugMenuAction.SpeedFastest, "50 ms", speedButtons[3], true, TickSpeedMatches(state, GameConstants.TickSpeedFastest)),
            new DebugMenuButton(DebugMenuAction.ShowQueenField, "Queen", bfsTopButtons[0], true, string.Equals(state.ActiveBfsDebugField, "queen", StringComparison.Ordinal)),
            new DebugMenuButton(DebugMenuAction.ShowEnemyField, "Enemy", bfsTopButtons[1], true, string.Equals(state.ActiveBfsDebugField, "enemy", StringComparison.Ordinal)),
            new DebugMenuButton(DebugMenuAction.ShowColonyField, "Colony", bfsBottomButtons[0], true, string.Equals(state.ActiveBfsDebugField, "colony", StringComparison.Ordinal)),
            new DebugMenuButton(DebugMenuAction.ToggleMapOverlay, "Show Map", bfsBottomButtons[1], true, state.DebugMapVisible),
            new DebugMenuButton(DebugMenuAction.ClearField, "Clear", bfsBottomButtons[2], true, state.ActiveBfsDebugField is null),
            new DebugMenuButton(DebugMenuAction.RestartGame, "Restart Game", actionButtons[0], true, false),
            new DebugMenuButton(DebugMenuAction.PlaceAntHole, "Place Ant Hole", actionButtons[1], true, state.DebugAntHolePlacementMode)
        ];
    }

    public bool TryGetButtonAction(Point viewport, Point point, DebugMenuState state, out DebugMenuAction action)
    {
        foreach (var button in GetButtons(viewport, state))
        {
            if (!button.Enabled || !button.Bounds.Contains(point))
            {
                continue;
            }

            action = button.Action;
            return true;
        }

        action = default;
        return false;
    }

    private static bool TickSpeedMatches(DebugMenuState state, double tickSpeedMs)
    {
        return Math.Abs(state.TickSpeedMs - tickSpeedMs) < 0.001d;
    }
}
