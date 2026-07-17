using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Movement;

public sealed class SmoothCreatureMovementTests
{
    [Fact]
    public void Movement_ReportsFullTickVelocityAndFacesActualDisplacement()
    {
        var (session, cave, _, creature) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var destination = WorldPoint.FromGridPoint(new GridPoint(creature.Location.X + 2, creature.Location.Y));
        Assert.True(creature.NavigateTo(destination));

        cave.AdvanceCreatureMovement();

        Assert.Equal(creature.Position - creature.PreviousPosition, creature.Velocity);
        Assert.True(creature.Velocity.X > 0);
        Assert.True(creature.FacingDirection.X > 0);
        Assert.Equal(0, creature.FacingDirection.Y);
    }

    [Fact]
    public void Knockback_PreservesDesiredMovementFacing()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, GridPoint.Zero);
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Target");

        Assert.True(creature.NavigateTo(new GridPoint(8, 4)));
        creature.ApplyImpulse(new WorldVector(0, -WorldUnits.UnitsPerTile), sourceId: 99);

        cave.AdvanceCreatureMovement();

        Assert.True(creature.Velocity.X > 0);
        Assert.True(creature.Velocity.Y < 0);
        Assert.Equal(new WorldVector(WorldUnits.UnitsPerPixel, 0), creature.FacingDirection);
    }

    [Fact]
    public void InterpolatedPosition_UsesPreviousAndCurrentTickPoses()
    {
        var (_, _, _, creature) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var start = creature.Position;
        creature.SetWorldPosition(start + new WorldVector(WorldUnits.FromPixels(100), 0), snapPrevious: false);

        var midpoint = creature.GetInterpolatedWorldPosition(0.5f);

        Assert.Equal(start.ToWorldPixels().X + 50f, midpoint.X);
    }

    [Fact]
    public void UnassignedCreature_WaitsThenChoosesNearbyIdleDestination()
    {
        var (_, _, _, creature) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        creature.Assignment = "unassigned";
        var anchor = creature.Position;

        for (var tick = 0; tick < 70 && !creature.IdleDestination.HasValue; tick++)
        {
            creature.Move();
        }

        Assert.True(creature.IdleDestination.HasValue);
        Assert.InRange((creature.IdleDestination.Value - anchor).Length,
            WorldUnits.UnitsPerTile - WorldUnits.UnitsPerPixel,
            WorldUnits.UnitsPerTile * 2);
        Assert.Equal(MovementGoalKind.Idle, creature.MovementCohort.GoalKind);
    }
}
