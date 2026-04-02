using TriloGame.Game.Core.Events;

namespace TriloGame.Tests.World;

public sealed class ReachabilityTests
{
    [Fact]
    public void MiningWall_EmitsMineEventsAndUpdatesStats()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var wallTile = cave.GetReachableTiles()
            .SelectMany(tile => tile.Neighbors)
            .FirstOrDefault(tile => tile.Base == "wall")
            ?? throw new InvalidOperationException("No mineable wall tile was found adjacent to reachable tiles.");
        var tileMinedCount = 0;
        var wallMinedCount = 0;
        session.On(GameEvents.TileMined, _ => tileMinedCount++);
        session.On(GameEvents.WallMined, _ => wallMinedCount++);

        var mined = session.MineTile(cave, wallTile.Key, "manual");

        Assert.True(mined);
        Assert.Equal("empty", cave.GetTile(wallTile.Key)?.Base);
        Assert.Equal(1, tileMinedCount);
        Assert.Equal(1, wallMinedCount);
        Assert.Equal(1, session.Stats.Get(GameEvents.TileMined));
        Assert.Equal(1, session.Stats.Get(GameEvents.WallMined));
    }
}
