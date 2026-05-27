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

    [Fact]
    public void Enemy_SeesAdjacentWallWhenColonyTargetsAreUnreachable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(8, 3, new GridPoint(0, 0));
        TestWorldFactory.BuildWall(cave, session, new GridPoint(3, 0));
        var targetWall = TestWorldFactory.BuildWall(cave, session, new GridPoint(3, 1));
        TestWorldFactory.BuildWall(cave, session, new GridPoint(3, 2));

        var enemyLocation = new GridPoint(4, 1);
        var enemyTile = cave.GetTile(enemyLocation)
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Breacher", enemyLocation, session);

        Assert.True(cave.Spawn(enemy, enemyTile));

        var colonyField = cave.GetBfsFieldObject("colony")
            ?? throw new InvalidOperationException("Expected the colony BFS field to exist.");
        colonyField.Rebuild();

        Assert.Equal(int.MaxValue, colonyField.GetFieldValue(enemyLocation, refresh: false));
        Assert.Equal(targetWall.Location!.Value.ToString(), enemy.GetAdjacentWallTileKey());
    }

    [Fact]
    public void Enemy_IgnoresAdjacentSoilPatchTargets()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, new GridPoint(1, 1));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 4));

        var enemyLocation = new GridPoint(5, 4);
        var enemyTile = cave.GetTile(enemyLocation)
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Forager", enemyLocation, session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.Null(enemy.GetAdjacentHostileTileKey());

        var startingHealth = soilPatch.Health;
        Assert.True(enemy.EnemyStep1());

        Assert.Equal(startingHealth, soilPatch.Health);
        Assert.NotEqual(enemyLocation, enemy.Location);
    }
}
