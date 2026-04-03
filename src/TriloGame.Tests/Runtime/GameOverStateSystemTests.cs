using TriloGame.Game.Runtime.Systems;

namespace TriloGame.Tests.Runtime;

public sealed class GameOverStateSystemTests
{
    [Fact]
    public void TryTrigger_WhenQueenWasLastBuildingAndAllTrilobitesAreDead_StillTriggersGameOver()
    {
        var (session, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        var system = new GameOverStateSystem();

        foreach (var trilobite in cave.GetTrilobiteList().ToArray())
        {
            Assert.True(trilobite.RemoveFromGame("test"));
        }

        Assert.Empty(cave.GetTrilobiteList());
        Assert.True(queen.RemoveFromGame("test"));
        Assert.True(system.HasLostQueen(session));
        Assert.True(system.TryTrigger(session));
        Assert.True(system.IsGameOver);
    }
}
