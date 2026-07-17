using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class EnemyTargetTrackingTests
{
    [Fact]
    public void EnemyAcquiresNearbyTrilobiteFromContinuousCombatDistance()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 16, new GridPoint(2, 2));
        var enemy = new Enemy("Nearby Hunter", new GridPoint(10, 7), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));

        var trilobite = new Trilobite("Moving Target", new GridPoint(14, 7), session);
        var targetPosition = enemy.Position + new WorldVector((WorldUnits.UnitsPerTile * 3) + (WorldUnits.UnitsPerTile / 2), 0);
        Assert.True(cave.SpawnAtWorldPosition(trilobite, targetPosition));

        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep1());
        Assert.Equal(trilobite.Id, enemy.EnemyTarget?.Id);
        Assert.True(enemy.HasActiveMovement);
    }

    [Fact]
    public void EnemyAdvancesToBuildingAndAcquiresItAsTarget()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 16, new GridPoint(2, 2));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(12, 7));
        var enemyTile = cave.GetTile(new GridPoint(25, 7))!;
        var enemy = new Enemy("Building Hunter", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));
        cave.RefreshBfsField("colony");

        for (var tick = 0; tick < 40; tick++)
        {
            enemy.Move();
            cave.AdvanceCreatureMovement();
            if (enemy.EnemyTarget?.Target is Building)
            {
                break;
            }
        }

        Assert.Same(barracks, enemy.EnemyTarget?.Target);
        var startingHealth = barracks.Health;
        session.Combat.ResolveTick(session);
        session.TickCount++;
        session.Combat.ResolveTick(session);
        Assert.True(barracks.Health < startingHealth);
    }

    [Fact]
    public void EnemyRetargetsWhenTrackedCreatureMovesAway()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(2, 2));
        var enemyTile = cave.GetTile(new GridPoint(14, 7))!;
        var enemy = new Enemy("Retargeter", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));
        var first = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(14, 8), "First");
        var second = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(15, 7), "Second");
        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep1());
        first.TakeDamage(first.Health, enemy);
        enemy.ClearEnemyTarget();
        session.Combat.BeginTick(cave);

        Assert.True(enemy.EnemyStep1());
        Assert.Equal(second.Id, enemy.EnemyTarget?.Id);
    }
}
