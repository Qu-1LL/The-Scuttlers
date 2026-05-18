using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Automation;

public interface IGamePlayHost
{
    GameSession Session { get; }

    bool IsPaused { get; set; }

    double TickSpeedMs { get; set; }

    void RestartGame();

    void RunSingleTick();
}
