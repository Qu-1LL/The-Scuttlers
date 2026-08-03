using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class TurretTests
{
    [Fact]
    public void OnBuilt_RegistersProjectedTilesAndTileBackReferences()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var center = turret.GetCenter();
        var insideTile = cave.GetTile(new GridPoint(center.X + turret.ProjectionRadius, center.Y))
            ?? throw new InvalidOperationException("Expected an in-radius tile to exist.");
        var outsideTile = cave.GetTile(new GridPoint(center.X + turret.ProjectionRadius + 1, center.Y))
            ?? throw new InvalidOperationException("Expected an out-of-radius tile to exist.");

        Assert.Contains(insideTile, turret.ProjectedTiles);
        Assert.Contains(turret, insideTile.Projections);
        Assert.DoesNotContain(outsideTile, turret.ProjectedTiles);
        Assert.DoesNotContain(turret, outsideTile.Projections);
    }

    [Fact]
    public void ProjectionNotifications_SelectCloserEnemy_AndIgnoreFriendlyCreatures()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var friendly = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 19), "Friendly", "fighter");
        var farEnemyTile = cave.GetTile(new GridPoint(28, 19))
            ?? throw new InvalidOperationException("Expected a far enemy tile to exist.");
        var nearEnemyTile = cave.GetTile(new GridPoint(22, 19))
            ?? throw new InvalidOperationException("Expected a near enemy tile to exist.");
        var farEnemy = new Enemy("Far Enemy", farEnemyTile.Coordinates, session);
        var nearEnemy = new Enemy("Near Enemy", nearEnemyTile.Coordinates, session);

        Assert.Null(turret.Target);
        Assert.Empty(friendly.TrackedBy);

        Assert.True(cave.Spawn(farEnemy, farEnemyTile));
        Assert.Same(farEnemy, turret.Target);
        Assert.Contains(turret, farEnemy.TrackedBy);

        Assert.True(cave.Spawn(nearEnemy, nearEnemyTile));
        Assert.Same(nearEnemy, turret.Target);
        Assert.DoesNotContain(turret, farEnemy.TrackedBy);
        Assert.Contains(turret, nearEnemy.TrackedBy);
    }

    [Fact]
    public void ProjectionNotifications_KeepTargetsInsideRadius_AndClearThemWhenTheyLeaveOrDie()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var enemyTile = cave.GetTile(new GridPoint(25, 19))
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Enemy", enemyTile.Coordinates, session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.Same(enemy, turret.Target);

        Assert.True(cave.PlaceCreatureOnTile(enemy, new GridPoint(27, 19)));
        Assert.Same(enemy, turret.Target);

        Assert.True(cave.PlaceCreatureOnTile(enemy, new GridPoint(31, 19)));
        Assert.Null(turret.Target);
        Assert.DoesNotContain(turret, enemy.TrackedBy);

        Assert.True(cave.PlaceCreatureOnTile(enemy, new GridPoint(24, 19)));
        Assert.Same(enemy, turret.Target);

        enemy.TakeDamage(enemy.Health, "test");

        Assert.Null(turret.Target);
        Assert.Empty(enemy.TrackedBy);
    }

    [Fact]
    public void TargetDeath_RetargetsAnotherEnemyAlreadyInsideTheProjectionRadius()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var farEnemy = new Enemy("Far Enemy", new GridPoint(28, 19), session);
        var nearEnemy = new Enemy("Near Enemy", new GridPoint(22, 19), session);

        Assert.True(cave.Spawn(farEnemy, cave.GetTile(farEnemy.Location)!));
        Assert.True(cave.Spawn(nearEnemy, cave.GetTile(nearEnemy.Location)!));
        Assert.Same(nearEnemy, turret.Target);

        nearEnemy.TakeDamage(nearEnemy.Health, "test");

        Assert.Same(farEnemy, turret.Target);
        Assert.Contains(turret, farEnemy.TrackedBy);
    }

    [Fact]
    public void StationedFighters_FireRockProjectilesEveryFiveTicks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var firstFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(18, 17), "Fighter A", "fighter");
        var secondFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(20, 21), "Fighter B", "fighter");
        firstFighter.SetTraits(Array.Empty<TrilobiteTrait>());
        secondFighter.SetTraits(Array.Empty<TrilobiteTrait>());
        firstFighter.SetAssignedBuilding(turret);
        secondFighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(firstFighter));
        Assert.True(turret.Assign(secondFighter));
        Assert.False(firstFighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));
        Assert.False(secondFighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));

        var enemyTile = cave.GetTile(new GridPoint(25, 19))
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Enemy", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));

        var clock = new GameSimulationClockSystem();
        clock.ResetToDefaults();

        for (var index = 0; index < 5; index++)
        {
            clock.RunSingleTick(session);
        }

        Assert.Equal(2, session.Runtime.ActiveProjectileFlights.Count);
        Assert.Equal(enemy.MaxHealth, enemy.Health);

        clock.Advance(session, 500d);

        Assert.Empty(session.Runtime.ActiveProjectileFlights);
        Assert.DoesNotContain(enemy, cave.Enemies);
        Assert.Null(turret.Target);
    }

    [Fact]
    public void StationedFighter_ProjectilesUseAuthoritativeWorldPositionInsteadOfAccessTileCenter()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(18, 17), "Fighter", "fighter");
        fighter.SetTraits(Array.Empty<TrilobiteTrait>());
        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));
        Assert.False(fighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));

        var enemyTile = cave.GetTile(new GridPoint(25, 19))
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Enemy", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));

        for (var index = 0; index < 5; index++)
        {
            turret.Tick(cave);
        }

        var projectile = Assert.Single(session.Runtime.ActiveProjectileFlights);
        Assert.Equal(fighter.GetWorldPosition(), projectile.SourceWorldPosition);
        Assert.NotEqual(new System.Numerics.Vector2(fighter.Location.X * TileConstants.TileSize, fighter.Location.Y * TileConstants.TileSize), projectile.SourceWorldPosition);
    }

    [Fact]
    public void StationedFighter_RotatesToMatchProjectileLaunchAngle()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(18, 17), "Fighter", "fighter");
        fighter.SetTraits(Array.Empty<TrilobiteTrait>());
        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));
        Assert.False(fighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));

        var enemyTile = cave.GetTile(new GridPoint(25, 19))
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Enemy", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));

        for (var index = 0; index < 5; index++)
        {
            turret.Tick(cave);
        }

        var projectile = Assert.Single(session.Runtime.ActiveProjectileFlights);
        var expectedRotation = Microsoft.Xna.Framework.MathHelper.ToRadians(projectile.AngleDegrees) + (MathF.PI / 2f);
        Assert.Equal(expectedRotation, fighter.RotationRadians, 5);
    }

    [Fact]
    public void TryRestoreCreatureLocomotion_ReturnsHostedFighterToLastTrackedAccessTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(1, 1));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 18));
        var accessTile = new GridPoint(18, 17);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, accessTile, "Fighter", "fighter");

        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));
        Assert.False(fighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));
        Assert.False(fighter.IsLocomotionEnabled);

        Assert.True(turret.TryRestoreCreatureLocomotion(fighter));

        Assert.True(fighter.IsLocomotionEnabled);
        Assert.Null(fighter.HostedBuilding);
        Assert.Equal(accessTile, fighter.Location);
        Assert.Same(fighter, cave.GetTrilobiteAtTileKey(accessTile.ToString()));
    }
}
