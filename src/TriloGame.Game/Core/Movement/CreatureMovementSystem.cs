using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Movement;

// Resolve all creature locomotion from one tick snapshot before gameplay interactions continue.
public sealed class CreatureMovementSystem
{
    private const int MaximumSubsteps = 8;
    private static readonly WorldVector[] WallProbeDirections =
    [
        new(1000, 0), new(707, 707), new(0, 1000), new(-707, 707),
        new(-1000, 0), new(-707, -707), new(0, -1000), new(707, -707)
    ];
    private readonly List<Creature> _ordered = [];

    public void Advance(Cave cave)
    {
        BuildOrderedSnapshot(cave);
        if (_ordered.Count == 0)
        {
            return;
        }

        var maximumDisplacement = 0;
        var minimumRadius = int.MaxValue;
        for (var index = 0; index < _ordered.Count; index++)
        {
            var creature = _ordered[index];
            creature.BeginMovementTick();
        }

        // Build preferred velocities from the immutable start-of-phase body snapshot.
        for (var index = 0; index < _ordered.Count; index++)
        {
            var creature = _ordered[index];
            creature.SetDesiredVelocity(BuildPreferredVelocity(creature));
            var intended = creature.DesiredVelocity;
            if (creature.HasPendingImpulse)
            {
                intended += creature.PendingImpulse.ClampMagnitude(creature.BaseSpeed * MaximumSubsteps);
            }

            maximumDisplacement = Math.Max(maximumDisplacement, intended.Length);
            minimumRadius = Math.Min(minimumRadius, creature.CollisionRadius);
        }

        var maximumSubstepDistance = Math.Max(1, minimumRadius / 2);
        var substepCount = Math.Clamp(
            (maximumDisplacement + maximumSubstepDistance - 1) / maximumSubstepDistance,
            1,
            MaximumSubsteps);

        for (var substep = 0; substep < substepCount; substep++)
        {
            for (var index = 0; index < _ordered.Count; index++)
            {
                AdvanceCreature(cave, _ordered[index], substepCount);
            }
        }

        for (var index = 0; index < _ordered.Count; index++)
        {
            _ordered[index].CompleteMovementTick();
        }
    }

    private WorldVector BuildPreferredVelocity(Creature creature)
    {
        var routeVelocity = BuildArrivalVelocity(creature);
        var wallAvoidance = creature.Cave is not null &&
                            !routeVelocity.IsZero &&
                            !creature.Cave.HasClearStaticSweep(creature, creature.Position, creature.Position + routeVelocity)
            ? BuildWallAvoidanceVelocity(creature)
            : WorldVector.Zero;
        var preferred = routeVelocity +
                        wallAvoidance.ClampMagnitude(creature.BaseSpeed / 3);
        preferred = preferred.ClampMagnitude(creature.BaseSpeed);
        var accelerationLimit = creature.MovementCohort.GoalKind == MovementGoalKind.Idle
            ? creature.BaseSpeed
            : Math.Max(1, creature.BaseSpeed / 2);
        var acceleration = (preferred - creature.Velocity).ClampMagnitude(
            accelerationLimit);
        return (creature.Velocity + acceleration).ClampMagnitude(creature.BaseSpeed);
    }

    private WorldVector BuildWallAvoidanceVelocity(Creature creature)
    {
        if (creature.Cave is null)
        {
            return WorldVector.Zero;
        }

        var avoidance = WorldVector.Zero;
        var probeDistance = creature.CollisionRadius + creature.SeparationPadding + creature.BaseSpeed;
        for (var index = 0; index < WallProbeDirections.Length; index++)
        {
            var direction = WallProbeDirections[index];
            var probe = creature.Position + new WorldVector(
                (int)(((long)direction.X * probeDistance) / 1000),
                (int)(((long)direction.Y * probeDistance) / 1000));
            if (creature.Cave.CanCreatureOccupyWorldPosition(creature, probe))
            {
                continue;
            }

            avoidance += new WorldVector(-direction.X, -direction.Y).WithMagnitude(creature.BaseSpeed / 4);
        }

        return avoidance;
    }

    private static WorldVector BuildArrivalVelocity(Creature creature)
    {
        if (creature.MovementTarget is not { } target)
        {
            return WorldVector.Zero;
        }

        var delta = target - creature.Position;
        var distance = delta.Length;
        if (distance <= 0)
        {
            return WorldVector.Zero;
        }

        var speed = creature.HasRouteContinuation || distance >= WorldUnits.UnitsPerTile
            ? creature.BaseSpeed
            : Math.Max(1, (int)(((long)creature.BaseSpeed * distance) / WorldUnits.UnitsPerTile));
        return delta.WithMagnitude(speed);
    }

    private void BuildOrderedSnapshot(Cave cave)
    {
        _ordered.Clear();
        var trilobites = cave.GetTrilobiteList();
        var enemies = cave.GetEnemyList();
        if (_ordered.Capacity < trilobites.Count + enemies.Count)
        {
            _ordered.Capacity = trilobites.Count + enemies.Count;
        }

        var trilobiteIndex = 0;
        var enemyIndex = 0;
        while (trilobiteIndex < trilobites.Count || enemyIndex < enemies.Count)
        {
            Creature creature;
            if (enemyIndex >= enemies.Count ||
                (trilobiteIndex < trilobites.Count && trilobites[trilobiteIndex].Id < enemies[enemyIndex].Id))
            {
                creature = trilobites[trilobiteIndex++];
            }
            else
            {
                creature = enemies[enemyIndex++];
            }

            if (creature.Health > 0 && creature.IsLocomotionEnabled)
            {
                _ordered.Add(creature);
            }
        }
    }

    private void AdvanceCreature(Cave cave, Creature creature, int substepCount)
    {
        var desiredStep = creature.DesiredVelocity / substepCount;
        var impulseStep = creature.HasPendingImpulse
            ? creature.ConsumePendingImpulse(Math.Max(1, creature.BaseSpeed / substepCount))
            : WorldVector.Zero;
        var displacement = desiredStep + impulseStep;
        if (displacement.IsZero)
        {
            return;
        }

        var previous = creature.Position;
        var candidate = previous + displacement;
        if (!cave.CanCreatureOccupyWorldPosition(creature, candidate))
        {
            if (TryCommitStaticSlideFromBlockedMove(cave, creature, previous, displacement))
            {
                return;
            }

            cave.CommitCreaturePosition(creature, previous, WorldVector.Zero);
            creature.MarkMovementBlocked();
            return;
        }

        cave.CommitCreaturePosition(creature, candidate, displacement);
    }

    private bool TryCommitStaticSlideFromBlockedMove(Cave cave, Creature creature, WorldPoint previous, WorldVector displacement)
    {
        if (displacement.X == 0 || displacement.Y == 0)
        {
            return false;
        }

        var horizontal = new WorldVector(displacement.X, 0);
        var vertical = new WorldVector(0, displacement.Y);
        if (Math.Abs(displacement.X) >= Math.Abs(displacement.Y))
        {
            return TryCommitStaticSlide(cave, creature, previous, horizontal) ||
                   TryCommitStaticSlide(cave, creature, previous, vertical);
        }

        return TryCommitStaticSlide(cave, creature, previous, vertical) ||
               TryCommitStaticSlide(cave, creature, previous, horizontal);
    }

    private bool TryCommitStaticSlide(Cave cave, Creature creature, WorldPoint previous, WorldVector slide)
    {
        if (slide.IsZero)
        {
            return false;
        }

        var candidate = previous + slide;
        if (!cave.CanCreatureOccupyWorldPosition(creature, candidate))
        {
            return false;
        }

        cave.CommitCreaturePosition(creature, candidate, slide);
        return true;
    }
}
