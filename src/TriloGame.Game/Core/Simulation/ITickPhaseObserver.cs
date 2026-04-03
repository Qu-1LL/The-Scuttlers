namespace TriloGame.Game.Core.Simulation;

public interface ITickPhaseObserver
{
    void OnTickStarted(GameSession session);

    void OnPhaseCompleted(TickPhase phase);

    void OnTickCompleted(GameSession session);
}
