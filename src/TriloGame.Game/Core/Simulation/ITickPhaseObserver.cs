namespace TriloGame.Game.Core.Simulation;

public interface ITickPhaseObserver
{
    void OnTickStarted(GameSession session);

    void OnPhaseStarted(TickPhase phase);

    void OnPhaseCompleted(TickPhase phase);

    void OnTrilobiteMoveStarted(string assignment);

    void OnTrilobiteMoveCompleted(string assignment);

    void OnTickCompleted(GameSession session);
}
