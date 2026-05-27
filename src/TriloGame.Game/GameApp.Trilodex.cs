using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriloGame.Game.UI.Research;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private bool HandleTrilodexButtonClick(Point point)
    {
        var buttonOutcome = _trilodex.HandleClosedButtonClick(point, Window.ClientBounds.Size);
        if (buttonOutcome != TrilodexInteractionOutcome.RequestedOpen)
        {
            return false;
        }

        PlayUiSelectSound();
        OpenTrilodexMenu();
        return true;
    }

    private void HandleTrilodexMenuInput()
    {
        if (_input.KeyPressed(Keys.Escape))
        {
            var escapeOutcome = _trilodex.HandleEscape();
            if (escapeOutcome == TrilodexInteractionOutcome.RequestedClose)
            {
                PlayUiSelectSound();
                CloseTrilodexMenu();
            }
            else if (escapeOutcome == TrilodexInteractionOutcome.Consumed)
            {
                PlayUiSelectSound();
            }

            return;
        }

        if (_input.WheelDelta != 0)
        {
            _trilodex.HandleWheel(
                _input.MousePoint,
                System.Math.Clamp(-_input.WheelDelta, -90, 90),
                Window.ClientBounds.Size);
        }

        if (_input.LeftPressed)
        {
            _trilodex.HandlePointerDown(_input.MousePoint, Window.ClientBounds.Size);
        }

        if (_input.LeftHeld)
        {
            _trilodex.HandlePointerDrag(_input.MousePoint, Window.ClientBounds.Size);
        }

        if (!_input.LeftReleased)
        {
            return;
        }

        var outcome = _trilodex.HandlePointerUp(_input.MousePoint, Window.ClientBounds.Size);
        if (outcome == TrilodexInteractionOutcome.RequestedClose)
        {
            PlayUiSelectSound();
            CloseTrilodexMenu();
        }
        else if (outcome == TrilodexInteractionOutcome.Consumed)
        {
            PlayUiSelectSound();
        }
    }

    private void OpenTrilodexMenu(bool pauseSimulationIfNeeded = true)
    {
        CloseSettingsMenu();
        ForceCloseResearchDraftMenu();
        _debugMenuOpen = false;
        _roleRadialMenu = null;
        ResetPointerInteractionState();
        _trilodex.Open();

        if (pauseSimulationIfNeeded && !_mainMenuOpen && !_gamePaused)
        {
            _gamePaused = true;
            _resumeSimulationAfterClosingTrilodex = true;
        }
    }

    private void CloseTrilodexMenu()
    {
        if (!_trilodex.IsOpen)
        {
            return;
        }

        var shouldResumeSimulation = _resumeSimulationAfterClosingTrilodex;
        _trilodex.Close();
        if (shouldResumeSimulation)
        {
            _gamePaused = false;
        }

        _resumeSimulationAfterClosingTrilodex = false;
    }

    private void ForceCloseTrilodexMenu()
    {
        if (!_trilodex.IsOpen)
        {
            _resumeSimulationAfterClosingTrilodex = false;
            return;
        }

        _trilodex.Close();
        _resumeSimulationAfterClosingTrilodex = false;
    }

    private bool TrilodexCoversPoint(Point point)
    {
        return _trilodex.CoversScreenPoint(point, Window.ClientBounds.Size);
    }
}
