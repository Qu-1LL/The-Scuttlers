using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Combat;

public sealed class CombatObserverTests
{
    [Fact]
    public void DealDamage_RecordsAttackerAndTargetEnteringCombatOnce()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 8, new GridPoint(1, 1));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Fighter", "fighter");
        var enemy = SpawnEnemy(cave, session, new GridPoint(5, 4));

        Assert.True(fighter.DealDamage(enemy) > 0);
        Assert.True(fighter.DealDamage(enemy) > 0);

        var enteredEvents = session.Combat.Events
            .Where(combatEvent => combatEvent.Kind == CombatEventKind.EnteredCombat)
            .ToArray();

        Assert.Equal(2, enteredEvents.Length);
        Assert.Contains(enteredEvents, combatEvent => ReferenceEquals(combatEvent.Creature, fighter) &&
                                                      ReferenceEquals(combatEvent.Opponent, enemy));
        Assert.Contains(enteredEvents, combatEvent => ReferenceEquals(combatEvent.Creature, enemy) &&
                                                      ReferenceEquals(combatEvent.Opponent, fighter));
        Assert.True(session.Combat.IsInCombat(fighter));
        Assert.True(session.Combat.IsInCombat(enemy));
    }

    [Fact]
    public void CreatureDeath_RecordsSourceAndLastLocation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 8, new GridPoint(1, 1));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Fighter", "fighter");
        var enemyLocation = new GridPoint(5, 4);
        var enemy = SpawnEnemy(cave, session, enemyLocation);

        enemy.TakeDamage(enemy.Health, fighter);

        var deathEvent = Assert.Single(
            session.Combat.Events,
            combatEvent => combatEvent.Kind == CombatEventKind.CreatureDied);
        Assert.Same(enemy, deathEvent.Creature);
        Assert.Same(fighter, deathEvent.Source);
        Assert.Same(fighter, deathEvent.Opponent);
        Assert.Equal(enemyLocation, deathEvent.Location);
        Assert.False(session.Combat.IsInCombat(enemy));
    }

    [Fact]
    public void LaunchProjectile_RecordsCombatEntryBeforeImpact()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 8, new GridPoint(1, 1));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Fighter", "fighter");
        var enemy = SpawnEnemy(cave, session, new GridPoint(8, 4));

        Assert.NotNull(session.LaunchProjectile(fighter, enemy, ProjectileCatalog.Rock));

        var enteredEvents = session.Combat.Events
            .Where(combatEvent => combatEvent.Kind == CombatEventKind.EnteredCombat)
            .ToArray();

        Assert.Equal(2, enteredEvents.Length);
        Assert.True(session.Combat.IsInCombat(fighter));
        Assert.True(session.Combat.IsInCombat(enemy));
        Assert.Equal(enemy.MaxHealth, enemy.Health);
    }

    private static Enemy SpawnEnemy(Cave cave, GameSession session, GridPoint location)
    {
        var tile = cave.GetTile(location)
            ?? throw new InvalidOperationException($"No tile exists at {location}.");
        var enemy = new Enemy("Enemy", location, session);
        if (!cave.Spawn(enemy, tile))
        {
            throw new InvalidOperationException($"Failed to spawn enemy at {location}.");
        }

        return enemy;
    }
}
