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
            _roundManager.DeferNextRoundStart();
            Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] Preserved the existing research draft instead of overwriting it after round {round.RoundNumber}.");
            OpenResearchDraftMenu();
            return;
        }

        var draft = _researchDraftSystem.CreateDraft(_session, round);
        if (draft is null)
        {
            Trace.WriteLine($"[RoundManager][Tick {_session.TickCount}] No research draft could be generated after round {round.RoundNumber}.");
            return;
        }

        _roundManager.DeferNextRoundStart();
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
        _antHandler.Advance(session, _roundManager.CurrentRound);
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
        if (!_antHandler.CanSkipCurrentRound(_session, currentRound))
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
        var pointer = _input.MousePoint;
        var timerHovered = layout.TimerBounds.Contains(pointer);
        var roundHovered = canSkipRound && layout.RoundBounds.Contains(pointer);

        DrawRoundedScreenFrame(
            layout.TimerBounds,
            timerHovered ? new Color(22, 50, 71) : new Color(16, 38, 54),
            timerHovered ? new Color(125, 179, 196) : new Color(54, 88, 107),
            2,
            14);
        DrawRoundedScreenFrame(
            layout.RoundBounds,
            !canSkipRound ? new Color(33, 40, 44) : roundHovered ? new Color(74, 104, 87) : new Color(48, 74, 61),
            !canSkipRound ? new Color(92, 104, 112) : roundHovered ? new Color(207, 242, 220) : new Color(132, 173, 150),
            2,
            14);

        DrawScreenTextFittedCentered(
            "Next Round",
            layout.TimerLabelBounds,
            Color.White,
            _rendering.SmallFont,
            minScale: 0.7f);
        DrawScreenTextFittedCentered(
            FormatRoundCountdown(GetRoundWidgetCountdownMs(currentRound)),
            layout.TimerValueBounds,
            Color.White,
            _rendering.SmallFont,
            minScale: 0.9f);
        DrawScreenTextFittedCentered(
            GetRoundBadgeLabel(currentRound),
            layout.RoundValueBounds,
            canSkipRound ? Color.White : new Color(183, 191, 196),
            _rendering.SmallFont,
            minScale: 0.66f);
    }

    private static string GetRoundBadgeLabel(RoundInfo round)
    {
        return round.RoundNumber == 0 && round.GracePeriodActive
            ? "Grace Period"
            : $"Round {round.RoundNumber}";
    }

    private static double GetRoundWidgetCountdownMs(RoundInfo round)
    {
        if (round.RoundNumber == 0 && round.GracePeriodActive)
        {
            return Math.Max(0d, round.SpawnWindowStartMs - round.ElapsedGameTimeMs);
        }

        return round.RemainingDurationMs;
    }

    private static string FormatRoundCountdown(double remainingDurationMs)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remainingDurationMs / 1000d));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
    }
}
