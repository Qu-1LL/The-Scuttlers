using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class GameOverStateSystem
{
    public bool IsGameOver { get; set; }

    public void Reset()
    {
        IsGameOver = false;
    }

    public bool HasLostQueen(GameSession session)
    {
        var cave = session.Cave;
        return cave is not null && cave.GetQueenBuilding() is null;
    }

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
