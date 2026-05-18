using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class GameOverStateSystem
{
    public bool IsGameOver { get; set; }

    // Clear any previously triggered game-over state.
    public void Reset()
    {
        IsGameOver = false;
    }

    // Treat the queen's removal from the cave as the loss condition.
    public bool HasLostQueen(GameSession session)
    {
        var cave = session.Cave;
        return cave is not null && cave.GetQueenBuilding() is null;
    }

    // Trigger game over exactly once when the queen-loss condition becomes true.
    public bool TryTrigger(GameSession session)
    {
        if (IsGameOver || !HasLostQueen(session))
        {
            return false;
        }

        IsGameOver = true;
        return true;
    }
}
