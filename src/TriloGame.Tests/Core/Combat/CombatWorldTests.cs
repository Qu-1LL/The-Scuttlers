using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.State;

namespace TriloGame.Tests.Core.Combat;

public sealed class CombatWorldTests
{
    [Fact]
    public void Shapes_IncludeCircleAndAabbTangencies()
    {
        var circle = CombatShape.Circle(new WorldPoint(0, 0), 10);
        Assert.True(circle.Intersects(CombatShape.Circle(new WorldPoint(20, 0), 10)));
        Assert.True(circle.Intersects(CombatShape.Aabb(new TriloGame.Game.Core.Interaction.WorldRectangle(10, -2, 4, 4))));
    }

    [Fact]
    public void Capsule_IntersectsCircleAtBoundary()
    {
        var capsule = CombatShape.Capsule(new WorldPoint(0, 0), new WorldPoint(100, 0), 10);
        Assert.True(capsule.Intersects(CombatShape.Circle(new WorldPoint(50, 20), 10)));
        Assert.False(capsule.Intersects(CombatShape.Circle(new WorldPoint(50, 21), 9)));
    }

    [Fact]
    public void CreatureDeath_RaisesRenderParticleRequestBeforeRemoval()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 16, new GridPoint(4, 4));
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 8), "Victim", "fighter");
        var expectedOrigin = creature.Position;
        CreatureDeathParticleRequest? request = null;
        session.CreatureDeathParticlesRequested += value => request = value;

        creature.TakeDamage(creature.Health);

        Assert.True(request.HasValue);
        Assert.Equal(expectedOrigin, request.Value.Origin);
        Assert.DoesNotContain(creature, cave.Trilobites);
    }

    [Fact]
    public void SpatialGrid_ReturnsEveryOverlappingBucketCandidateInStableIdOrder()
    {
        var grid = new CombatSpatialGrid(100);
        var late = new CombatHurtbox { Id = 9, Target = new object(), Shape = CombatShape.Circle(new WorldPoint(150, 0), 5), Faction = CombatFactionMask.Hostile };
        var early = new CombatHurtbox { Id = 2, Target = new object(), Shape = CombatShape.Circle(new WorldPoint(0, 0), 5), Faction = CombatFactionMask.Hostile };
        grid.Add(late);
        grid.Add(early);
        grid.Query(CombatShape.Aabb(new TriloGame.Game.Core.Interaction.WorldRectangle(-10, -10, 220, 20)));
        Assert.Equal(new[] { 2, 9 }, grid.Results.Select(candidate => candidate.Id));
    }

    [Fact]
    public void Melee_ResolvesOnceAndNeverFriendlyFires()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "Fighter", "fighter");
        var enemy = new Enemy("Ant", new GridPoint(5, 6), session);
        Assert.True(cave.SpawnAtWorldPosition(enemy, fighter.Position));

        Assert.True(session.Combat.TryQueueMelee(fighter, CombatTargetRef.For(enemy)));
        session.Combat.ResolveTick(session);
        session.TickCount++;
        session.Combat.ResolveTick(session);
        Assert.Equal(enemy.MaxHealth - fighter.Damage, enemy.Health);
        session.Combat.ResolveTick(session);
        Assert.Equal(enemy.MaxHealth - fighter.Damage, enemy.Health);
        Assert.False(session.Combat.TryQueueMelee(fighter, CombatTargetRef.For(fighter)));
    }

    [Fact]
    public void CreatureHurtbox_UsesTheCenteredCreatureBodyHitbox()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "Fighter", "fighter");

        session.Combat.BeginTick(cave);

        var hurtbox = session.Combat.Hurtboxes.First(box => ReferenceEquals(box.Target, fighter));
        Assert.Equal(CombatShapeKind.Circle, hurtbox.Shape.Kind);
        Assert.Equal(fighter.Position, hurtbox.Shape.First);
        Assert.Equal(fighter.CollisionRadius + fighter.SeparationPadding, hurtbox.Shape.Radius);
    }

    [Fact]
    public void ProjectileImpact_ProducesImmutableCombatHitEvent()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "Fighter", "fighter");
        var enemy = new Enemy("Ant", new GridPoint(5, 6), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));

        session.Combat.SubmitProjectileImpact(fighter, enemy, 3);
        session.Combat.ResolveTick(session);

        var hit = Assert.Single(session.Combat.RecentHitEvents);
        Assert.Equal(fighter.Id, hit.SourceId);
        Assert.Equal(enemy.Id, hit.Target.Id);
        Assert.Equal(3, hit.Damage);
        Assert.Equal(enemy.MaxHealth - 3, enemy.Health);
    }

    [Fact]
    public void FighterPursuit_ReachesLiveEnemyPoseAndEngages()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 8), "Fighter", "fighter");
        var enemy = new Enemy("Pursuit Target", new GridPoint(7, 8), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));

        for (var tick = 0; tick < 80 && enemy.Health == enemy.MaxHealth; tick++)
        {
            session.Combat.BeginTick(cave);
            fighter.Move();
            cave.AdvanceCreatureMovement();
            session.Combat.ResolveTick(session);
            session.TickCount++;
        }

        Assert.True(enemy.Health < enemy.MaxHealth);
        Assert.Same(enemy, fighter.FighterTarget);
        var bodyDistance = (fighter.Position - enemy.Position).Length;
        Assert.True(bodyDistance >= fighter.CollisionRadius + enemy.CollisionRadius);
    }

    [Fact]
    public void FighterPursuit_ReplansMovingTargetWithoutSkippingMovementTick()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 8), "Fighter", "fighter");
        var enemy = new Enemy("Moving Target", new GridPoint(9, 8), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));

        session.Combat.BeginTick(cave);
        fighter.Move();
        cave.AdvanceCreatureMovement();

        enemy.SetWorldPosition(enemy.Position + new WorldVector(WorldUnits.UnitsPerHalfTile, 0), snapPrevious: true);
        session.TickCount++;
        session.Combat.BeginTick(cave);
        var positionBeforeRefresh = fighter.Position;
        var velocityBeforeRefresh = fighter.Velocity;
        Assert.NotEqual(WorldVector.Zero, velocityBeforeRefresh);

        fighter.Move();
        Assert.Equal(velocityBeforeRefresh, fighter.Velocity);
        cave.AdvanceCreatureMovement();

        Assert.Same(enemy, fighter.FighterTarget);
        Assert.True((fighter.Position - positionBeforeRefresh).LengthSquared > 0);
    }

    [Fact]
    public void FighterPursuit_CancelsRouteWhenAssignedEnemyIsRemoved()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 8), "Fighter", "fighter");
        var enemy = new Enemy("Removed Target", new GridPoint(9, 8), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));

        session.Combat.BeginTick(cave);
        fighter.Move();
        Assert.Same(enemy, fighter.FighterTarget);
        Assert.True(fighter.HasActiveMovement);

        Assert.True(enemy.TakeDamage(enemy.Health, fighter) > 0);
        Assert.Null(enemy.Cave);

        session.TickCount++;
        session.Combat.BeginTick(cave);
        fighter.Move();

        Assert.Null(fighter.FighterTarget);
        Assert.False(fighter.HasActiveMovement);
    }

    [Fact]
    public void FighterPursuit_RetargetsAnotherLiveAntAfterAssignedAntIsRemoved()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, GridPoint.Zero);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 8), "Fighter", "fighter");
        var firstEnemy = new Enemy("First Target", new GridPoint(9, 8), session);
        var secondEnemy = new Enemy("Second Target", new GridPoint(10, 8), session);
        Assert.True(cave.Spawn(firstEnemy, cave.GetTile(firstEnemy.Location)!));
        Assert.True(cave.Spawn(secondEnemy, cave.GetTile(secondEnemy.Location)!));

        session.Combat.BeginTick(cave);
        fighter.Move();
        Assert.Same(firstEnemy, fighter.FighterTarget);

        Assert.True(firstEnemy.TakeDamage(firstEnemy.Health, fighter) > 0);
        session.TickCount++;
        session.Combat.BeginTick(cave);
        fighter.Move();

        Assert.Same(secondEnemy, fighter.FighterTarget);
        fighter.Move();
        Assert.True(fighter.HasActiveMovement);
    }
}
