using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Runtime;

public sealed class GameSimulationClockSystemTests
{
    [Fact]
    public void Advance_WaitsForFullTickBudgetBeforeRunningSimulation()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var system = new GameSimulationClockSystem();
        system.ResetToDefaults();

        system.Advance(bootstrap.Session, GameConstants.TickSpeedFast - 1d);

        Assert.Equal(0, bootstrap.Session.TickCount);

        system.Advance(bootstrap.Session, 1d);

        Assert.Equal(1, bootstrap.Session.TickCount);
    }

    [Fact]
    public void Advance_StopsWhenStopConditionReturnsTrue()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var system = new GameSimulationClockSystem();
        system.ResetToDefaults();
        var stopChecks = 0;

        var executed = system.Advance(
            bootstrap.Session,
            GameConstants.TickSpeedFast * 3d,
            () =>
            {
                stopChecks++;
                return stopChecks == 1;
            });

        Assert.Equal(1, executed);
        Assert.Equal(1, bootstrap.Session.TickCount);
    }

    [Fact]
    public void RunSingleTick_RecordsProfilerSnapshotThroughRuntimeClock()
    {
        var bootstrap = TestWorldFactory.CreateBootstrappedGame();
        var system = new GameSimulationClockSystem();

        system.RunSingleTick(bootstrap.Session);

        Assert.Equal(1, bootstrap.Session.Runtime.TickProfiler.SampleCount);
        Assert.True(bootstrap.Session.Runtime.TickProfiler.Last.TotalMs >= 0d);
    }

    [Fact]
    public void Advance_ResolvesProjectileImpactAfterTravelSpeedBudgetIsConsumed()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(1, 1));
        var shooter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "Shooter", "fighter");
        var targetTile = cave.GetTile(new GridPoint(10, 5))
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var target = new Enemy("Target", targetTile.Coordinates, session);
        Assert.True(cave.Spawn(target, targetTile));

        var system = new ProjectileFlightSystem();
        session.Runtime.CurrentTickSpeedMs = GameConstants.TickSpeedFast;

        Assert.True(shooter.ShootProjectile(target, ProjectileCatalog.Rock));
        var projectile = Assert.Single(session.Runtime.ActiveProjectileFlights);
        var travelMs = (projectile.TargetWorldPosition - projectile.SourceWorldPosition).Length()
            * (float)GameConstants.TickSpeedFast
            / ProjectileCatalog.Rock.TravelPixelsPerTick;

        system.Advance(session, travelMs - 1d);

        Assert.Equal(target.MaxHealth, target.Health);
        Assert.Single(session.Runtime.ActiveProjectileFlights);

        system.Advance(session, 1d);

        Assert.Equal(target.MaxHealth - ProjectileCatalog.Rock.Damage, target.Health);
        Assert.Empty(session.Runtime.ActiveProjectileFlights);
    }

    [Fact]
    public void Advance_RetargetsProjectileMidFlight_WhenTargetMoves()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 24, new GridPoint(1, 1));
        var shooter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "Shooter", "fighter");
        var targetTile = cave.GetTile(new GridPoint(10, 5))
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var movedTargetTile = cave.GetTile(new GridPoint(10, 6))
            ?? throw new InvalidOperationException("Expected a moved enemy tile to exist.");
        var target = new Enemy("Target", targetTile.Coordinates, session);
        Assert.True(cave.Spawn(target, targetTile));

        var system = new ProjectileFlightSystem();
        session.Runtime.CurrentTickSpeedMs = GameConstants.TickSpeedFast;

        Assert.True(shooter.ShootProjectile(target, ProjectileCatalog.Rock));
        var projectile = Assert.Single(session.Runtime.ActiveProjectileFlights);

        system.Advance(session, 100d);

        Assert.Equal(projectile.SourceWorldPosition.Y, projectile.CurrentWorldPosition.Y);

        Assert.True(cave.PlaceCreatureOnTile(target, movedTargetTile.Coordinates));

        system.Advance(session, 10d);

        Assert.True(projectile.CurrentWorldPosition.Y > projectile.SourceWorldPosition.Y);
        Assert.Equal(target.GetWorldPosition(), projectile.TargetWorldPosition);
        Assert.Equal(target.MaxHealth, target.Health);
        Assert.Single(session.Runtime.ActiveProjectileFlights);

        system.Advance(session, 140d);

        Assert.Equal(target.MaxHealth, target.Health);
        Assert.Single(session.Runtime.ActiveProjectileFlights);

        system.Advance(session, 10d);

        Assert.Equal(target.MaxHealth - ProjectileCatalog.Rock.Damage, target.Health);
        Assert.Empty(session.Runtime.ActiveProjectileFlights);
    }
}
