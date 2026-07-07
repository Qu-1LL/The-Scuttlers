using System.Diagnostics;
using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private readonly RoundManager _roundManager = new();
    private readonly AntHandler _antHandler = new(new CaveAntHoleSpawner());
    private readonly RoundDebugWidgetRenderer _roundDebugWidgetRenderer = new();

    private void ResetRoundSystems()
    {
        _antHandler.Reset();
        _researchDraftSystem.Reset();
        _roundManager.Reset(_session);
    }

    private void HandleRoundStarted(RoundInfo round)
    {
        _antHandler.HandleRoundStarted(round);
    }

    private void HandleRoundEnded(RoundInfo round)
    {
        _antHandler.HandleRoundEnded(round);
    }

    private void HandleRoundDraftRequested(RoundInfo round)
    {
        if (HasLostQueen() || _isGameOver)
        {
            Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] Skipped research draft after round {round.RoundNumber} because the queen is gone.");
            return;
        }

        if (_researchDraftSystem.HasPendingDraft)
        {
            if (!_infiniteDraft)
            {
                _roundManager.DeferNextRoundStart();
            }

            Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] Preserved the existing research draft instead of overwriting it after round {round.RoundNumber}.");
            OpenResearchDraftMenu();
            return;
        }

        var draft = _researchDraftSystem.CreateDraft(
            _session,
            round,
            _infiniteDraft ? ResearchDraftSource.InfiniteDraft : ResearchDraftSource.RoundReward);
        if (draft is null)
        {
            Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] No research draft could be generated after round {round.RoundNumber}.");
            return;
        }

        if (!_infiniteDraft)
        {
            _roundManager.DeferNextRoundStart();
        }

        Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] Generated {draft.Branches.Count} research branches after round {round.RoundNumber}.");
        OpenResearchDraftMenu();
    }

    private void HandleSimulationTickCompleted(GameSession session)
    {
        if (HasLostQueen())
        {
            return;
        }

        _roundManager.Advance(session, GameConstants.GameTimePerSimulationTickMs);
        var currentRound = _roundManager.CurrentRound;
        _antHandler.Advance(session, currentRound);
        if (_antHandler.CanCompleteCurrentRound(session, currentRound))
        {
            _roundManager.CompleteCurrentRound(session);
        }
    }

    private bool HandleRoundDebugWidgetClick(Point point)
    {
        if (_mainMenuOpen || _isGameOver || HasLostQueen())
        {
            return false;
        }

        var layout = RoundDebugWidgetLayout.Build(Window.ClientBounds.Size);
        if (!layout.RoundBounds.Contains(point))
        {
            return false;
        }

        var currentRound = _roundManager.CurrentRound;
        if (!_antHandler.CanCompleteCurrentRound(_session, currentRound))
        {
            var remainingKills = _antHandler.GetRemainingKillsForRound(_session, currentRound);
            Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] Skip ignored for round {currentRound.RoundNumber}; remaining ants to defeat this round: {remainingKills}.");
            return false;
        }

        PlayUiSelectSound();
        _roundManager.SkipCurrentRound(_session);
        return true;
    }

    private void DrawRoundDebugWidget()
    {
        if (_mainMenuOpen || _isGameOver || !HasGumUiRenderer)
        {
            return;
        }

        var layout = RoundDebugWidgetLayout.Build(Window.ClientBounds.Size);
        if (layout.TimerBounds.Width <= 0 || layout.RoundBounds.Width <= 0)
        {
            return;
        }

        var currentRound = _roundManager.CurrentRound;
        var canSkipRound = _antHandler.CanSkipCurrentRound(_session, currentRound);
        _roundDebugWidgetRenderer.Draw(_gumUiRenderer, layout, _input.MousePoint, currentRound, canSkipRound);
    }
}
