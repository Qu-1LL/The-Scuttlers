using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriloGame.Game.UI.Research;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private bool HandleResearchDraftButtonClick(Point point)
    {
        var outcome = _researchDraft.HandleClosedButtonClick(point, Window.ClientBounds.Size);
        if (outcome != ResearchDraftInteractionOutcome.RequestedOpen)
        {
            return false;
        }

        PlayUiSelectSound();
        OpenResearchDraftMenu();
        return true;
    }

    private void HandleResearchDraftMenuInput()
    {
        if (_input.KeyPressed(Keys.Escape))
        {
            if (_researchDraft.HandleEscape(_researchDraftSystem) == ResearchDraftInteractionOutcome.RequestedClose)
            {
                PlayUiSelectSound();
                CloseResearchDraftMenu();
            }

            return;
        }

        if (_input.WheelDelta != 0)
        {
            _researchDraft.HandleWheel(
                _input.MousePoint,
                System.Math.Clamp(-_input.WheelDelta, -90, 90),
                Window.ClientBounds.Size,
                _session,
                _researchDraftSystem);
        }

        if (_input.LeftPressed)
        {
            _researchDraft.HandlePointerDown(_input.MousePoint, Window.ClientBounds.Size, _session, _researchDraftSystem);
        }

        if (_input.LeftHeld)
        {
            _researchDraft.HandlePointerDrag(_input.MousePoint, Window.ClientBounds.Size, _session, _researchDraftSystem);
        }

        if (!_input.LeftReleased)
        {
            return;
        }

        var outcome = _researchDraft.HandlePointerUp(_input.MousePoint, Window.ClientBounds.Size, _session, _researchDraftSystem);
        switch (outcome)
        {
            case ResearchDraftInteractionOutcome.RequestedClose:
                PlayUiSelectSound();
                CloseResearchDraftMenu();
                break;
            case ResearchDraftInteractionOutcome.BranchPlaced:
                _roundManager.TryStartDeferredNextRound(_session);
                if (_infiniteDraft)
                {
                    _researchDraftSystem.CreateDraft(_session, _roundManager.CurrentRound);
                }

                PlayUiSelectSound();
                break;
        }
    }

    private void OpenResearchDraftMenu(bool pauseSimulationIfNeeded = true)
    {
        CloseSettingsMenu();
        _debugMenuOpen = false;
        _roleRadialMenu = null;
        _leftPanActive = false;
        _selectionDragActive = false;
        _selectionDragMode = null;
        _selectionBoxBounds = null;
        _input.EndDrag();
        _researchDraft.Open(_researchDraftSystem);

        if (pauseSimulationIfNeeded && !_mainMenuOpen && !_gamePaused)
        {
            _gamePaused = true;
            _resumeSimulationAfterClosingResearchDraft = true;
        }
    }

    private void CloseResearchDraftMenu()
    {
        if (!_researchDraft.IsOpen)
        {
            return;
        }

        var shouldResumeSimulation = _resumeSimulationAfterClosingResearchDraft;
        _researchDraft.Close(_researchDraftSystem);
        if (shouldResumeSimulation)
        {
            _gamePaused = false;
        }

        _resumeSimulationAfterClosingResearchDraft = false;
    }

    private void ForceCloseResearchDraftMenu()
    {
        if (!_researchDraft.IsOpen)
        {
            _resumeSimulationAfterClosingResearchDraft = false;
            return;
        }

        _researchDraft.Close(_researchDraftSystem);
        _resumeSimulationAfterClosingResearchDraft = false;
    }

    private bool ResearchDraftCoversPoint(Point point)
    {
        return _researchDraft.CoversScreenPoint(point, Window.ClientBounds.Size);
    }
}
