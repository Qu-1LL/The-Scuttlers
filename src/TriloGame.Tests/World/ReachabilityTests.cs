using TriloGame.Game.Core.Events;
using TriloGame.Game.Shared.Math;

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

    [Fact]
    public void MiningWall_AddsOpenedSectionTilesToReachableSetIncrementally()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(10, 6, new GridPoint(1, 1));
        for (var y = 0; y < 6; y++)
        {
            var wallTile = cave.GetTile(new GridPoint(5, y).ToString())
                ?? throw new InvalidOperationException("Expected wall-barrier tile to exist.");
            wallTile.SetBase("wall");
            wallTile.CreatureCanFit = false;
        }

        cave.RefreshReachableTiles();

        var minedWallKey = new GridPoint(5, 3).ToString();
        var isolatedTile = cave.GetTile(new GridPoint(8, 3).ToString())
            ?? throw new InvalidOperationException("Expected isolated cave tile to exist.");

        Assert.False(cave.IsTileReachable(cave.GetTile(minedWallKey)!));
        Assert.False(cave.IsTileReachable(isolatedTile));

        var mined = session.MineTile(cave, minedWallKey, "manual");

        Assert.True(mined);
        Assert.True(cave.IsTileReachable(cave.GetTile(minedWallKey)!));
        Assert.True(cave.IsTileReachable(isolatedTile));
        Assert.Contains(cave.GetTile(minedWallKey)!, cave.GetReachableTiles());
        Assert.Contains(isolatedTile, cave.GetReachableTiles());
    }
}
