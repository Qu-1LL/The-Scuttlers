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
            .FirstOrDefault(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString() &&
                                    !cave.HasCreatureInCell(tile.Coordinates))
            ?? throw new InvalidOperationException("No reachable enemy spawn tile was available for the danger-state test.");
        var enemy = new Enemy("Test Enemy", GridPoint.Parse(enemyTile.Key), session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.True(session.Danger);
        Assert.Single(cave.Enemies);

        enemy.TakeDamage(enemy.Health);

        Assert.False(session.Danger);
        Assert.Empty(cave.Enemies);
    }

    [Fact(Skip = "Breach resolution is now a typed combat command and is covered by CombatWorld tests.")]
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
        session.Combat.ResolveTick(session);
        session.TickCount++;
        session.Combat.ResolveTick(session);
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
        while (enemy.HasActiveMovement)
        {
            cave.AdvanceCreatureMovement();
        }

        Assert.Equal(startingHealth, soilPatch.Health);
        Assert.NotEqual(enemyLocation, enemy.Location);
    }

    [Fact]
    public void EnemyStep3_QueuesContinuousRouteChunkTowardColony()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 10, new GridPoint(1, 1));
        var enemyLocation = new GridPoint(25, 5);
        var enemyTile = cave.GetTile(enemyLocation)
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Runner", enemyLocation, session);
        Assert.True(cave.Spawn(enemy, enemyTile));
        cave.RefreshBfsField("colony");

        Assert.True(enemy.EnemyStep3());

        Assert.True(enemy.HasActiveMovement);
        Assert.NotEmpty(enemy.DesiredRoute);
        Assert.True(GridPoint.ManhattanDistance(enemyLocation, enemy.DesiredRoute[^1].ToGridPoint()) > 1);
        Assert.Equal(RouteContinuationKind.SharedBfsField, enemy.ActiveRouteContinuationKind);
    }

    [Fact]
    public void EnemyStep1_AdjacentHostileNeverBecomesIdleDuringDanger()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, new GridPoint(1, 1));
        var enemy = new Enemy("Biter", new GridPoint(7, 5), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 6), "Target");
        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep1());

        Assert.NotEqual(CreatureActivity.Idle, enemy.Activity);
        Assert.Equal(trilobite.Id, ((Creature)enemy.EnemyTarget!.Value.Target).Id);
        Assert.True(enemy.HasActiveMovement);
    }

    [Fact]
    public void EnemyStep3_ColonyFieldTargetZeroDoesNotReturnSilentIdle()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, new GridPoint(1, 1));
        cave.RefreshBfsField("colony");
        var field = cave.GetBfsFieldObject("colony")
            ?? throw new InvalidOperationException("Expected colony field.");
        var targetTile = cave.GetReachableTiles().First(tile =>
            tile.CreatureFits() &&
            !cave.HasCreatureInCell(tile.Coordinates) &&
            field.GetFieldValue(tile.Coordinates, refresh: false) == 0);
        var enemy = new Enemy("Arrived", targetTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, targetTile));
        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep3());

        Assert.NotEqual(CreatureActivity.Idle, enemy.Activity);
    }

    [Fact]
    public void EnemyStep1_ActivePendingMeleeStaysFightingInsteadOfIdle()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, new GridPoint(1, 1));
        var enemy = new Enemy("Biter", new GridPoint(7, 5), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));
        TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 6), "Target");
        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep1());
        Assert.True(enemy.EnemyStep1());

        Assert.Equal(CreatureActivity.Moving, enemy.Activity);
        Assert.True(enemy.HasActiveMovement);
    }

    [Fact]
    public void EnemyStep1_ContinuesAfterCurrentTargetDies()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var enemy = new Enemy("Retargeter", new GridPoint(12, 6), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));
        var target = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(12, 7), "Target");
        cave.RefreshBfsField("colony");
        session.Combat.BeginTick(cave);
        Assert.True(enemy.EnemyStep1());

        target.TakeDamage(target.Health, enemy);
        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep1());
        Assert.NotEqual(CreatureActivity.Idle, enemy.Activity);
    }

    [Fact]
    public void EnemyMove_InterruptsActiveRouteForAdjacentHostile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 10, new GridPoint(1, 1));
        var enemyLocation = new GridPoint(25, 5);
        var enemy = new Enemy("Ambusher", enemyLocation, session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemyLocation)!));
        cave.RefreshBfsField("colony");
        Assert.True(enemy.EnemyStep3());
        Assert.True(enemy.HasActiveMovement);

        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(25, 6), "Target");

        Assert.True(enemy.Move() is true);

        Assert.True(enemy.HasActiveMovement);
        Assert.Equal(trilobite.Id, ((Creature)enemy.EnemyTarget!.Value.Target).Id);
    }
}
