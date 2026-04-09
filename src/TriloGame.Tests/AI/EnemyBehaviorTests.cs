using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class EnemyBehaviorTests
{
    [Fact]
    public void SpawningAndRemovingLastEnemy_TogglesDangerState()
    {
        var (session, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        cave.RevealCave();
        var enemyTile = cave.GetReachableTiles()
            .FirstOrDefault(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString() && tile.Trilobites.Count == 0)
            ?? throw new InvalidOperationException("No reachable enemy spawn tile was available for the danger-state test.");
        var enemy = new Enemy("Test Enemy", GridPoint.Parse(enemyTile.Key), session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.True(session.Danger);
        Assert.Single(cave.Enemies);

        enemy.TakeDamage(enemy.Health);

        Assert.False(session.Danger);
        Assert.Empty(cave.Enemies);
    }

    [Fact]
    public void EnemyStep3_DigsAdjacentWallWhenColonyPathIsBlocked()
    {
        var (session, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        var colonyTile = queen.GetFeedTiles().First(tile => tile.CreatureFits());
        var wallTile = cave.AddTile(new GridPoint(colonyTile.Coordinates.X + 1000, colonyTile.Coordinates.Y).ToString());
        wallTile.SetBase("wall");
        wallTile.CreatureCanFit = false;
        wallTile.ConfigureWall(1);
        wallTile.AddNeighbor(colonyTile);

        var enemyTile = cave.AddTile(new GridPoint(colonyTile.Coordinates.X + 1001, colonyTile.Coordinates.Y).ToString());
        enemyTile.SetBase("empty");
        enemyTile.CreatureCanFit = true;
        enemyTile.AddNeighbor(wallTile);

        var enemy = new Enemy("Tunnel Ant", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));

        Assert.True(enemy.EnemyStep3());
        Assert.Equal("empty", wallTile.Base);
    }
}
