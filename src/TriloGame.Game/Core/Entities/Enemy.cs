using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed class Enemy : Creature
{
    private const int DirectPursuitMaximumDistance = WorldUnits.UnitsPerTile * 8;
    private GridPoint? _pursuedCreatureCell;

    public Enemy(string name, GridPoint location, GameSession session)
        : base(name, location, session, CreatureMovementProfile.Ant)
    {
        Assignment = "enemy";
        Description = "A hostile ant that tunnels toward the colony and attacks nearby trilobites, vehicles, and buildings.";
    }

    public CombatTargetRef? EnemyTarget { get; private set; }

    public EnemyCombatState CombatState { get; private set; } = EnemyCombatState.Idle;

    protected override bool QueueBehavior() => EnqueueTask(new CreatureTask(CreatureTaskKind.EnemyStep1));

    protected override bool ExecuteTask(CreatureTask task)
    {
        return task.Kind == CreatureTaskKind.EnemyStep1
            ? EnemyStep1()
            : base.ExecuteTask(task);
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        EnemyTarget = null;
        CombatState = EnemyCombatState.Idle;
    }

    public bool EnsureEnemyState()
    {
        if (Role == CreatureRole.Enemy)
        {
            return true;
        }

        EnemyTarget = null;
        QueueBehavior();

        return false;
    }

    public void ClearEnemyTarget()
    {
        EnemyTarget = null;
        _pursuedCreatureCell = null;
    }

    private bool HasValidEnemyTarget()
    {
        return EnemyTarget is { } target && target.Target switch
        {
            Creature creature => creature.Health > 0 && ReferenceEquals(creature.Cave, Cave),
            Building building => building.Health > 0 && ReferenceEquals(building.Cave, Cave),
            IVehicle vehicle => vehicle.Health > 0 && ReferenceEquals(vehicle.Cave, Cave),
            _ => false
        };
    }

    private bool CanReachEnemyTarget()
    {
        return HasValidEnemyTarget() && CombatWorld.CanMeleeReach(this, EnemyTarget!.Value);
    }

    private bool SetEnemyTargetAtTileKey(string? tileKey, bool includeWalls = true)
    {
        var target = GetHostileTargetAtTileKey(tileKey, includeWalls);
        EnemyTarget = target switch
        {
            Creature creature => CombatTargetRef.For(creature),
            Building building => CombatTargetRef.For(building),
            IVehicle vehicle => CombatTargetRef.For(vehicle),
            _ => null
        };
        _pursuedCreatureCell = null;
        return EnemyTarget.HasValue;
    }

    // Prefer a live nearby combat target before falling back to the colony field.
    private bool TryAcquireNearbyTarget()
    {
        if (GetAdjacentHostileTileKey() is not null)
        {
            return false;
        }

        var target = Session.Combat.FindNearestHostileTarget(this);
        if (!target.HasValue)
        {
            return false;
        }

        EnemyTarget = target;
        _pursuedCreatureCell = null;
        return true;
    }

    // Rebuild a pursuit route from the target's current pose instead of following a stale
    // tile chosen several ticks earlier.
    private bool TryPursueMovingTarget()
    {
        if (!HasValidEnemyTarget() || EnemyTarget!.Value.Target is not Creature target)
        {
            return false;
        }

        var targetCell = target.CurrentCell;
        if (_pursuedCreatureCell == targetCell && HasActiveMovement)
        {
            return true;
        }

        if (!TryBeginOrReplaceDirectCombatRoute(target.Position, DirectPursuitMaximumDistance))
        {
            return false;
        }

        _pursuedCreatureCell = targetCell;
        RecordEnemyIntent(CombatActorState.EngageHostile, CombatActorIntentKind.Move, EnemyTarget);
        return true;
    }

    public IReadOnlyList<Trilobite> GetHostileTrilobites()
    {
        return Cave?.GetTrilobiteList() ?? [];
    }

    public Trilobite? GetHostileAtTileKey(string? tileKey)
    {
        return Cave?.GetTrilobiteAtTileKey(tileKey);
    }

    public Building? GetHostileBuildingAtTileKey(string? tileKey, bool includeWalls = true)
    {
        if (Cave is null || string.IsNullOrWhiteSpace(tileKey))
        {
            return null;
        }

        var tile = Cave.GetTile(tileKey);
        var building = tile?.Built;
        if (building is null ||
            building.Cave != Cave ||
            building.Health <= 0 ||
            building.IgnoredByAnts ||
            (!includeWalls && building is Wall))
        {
            return null;
        }

        return building;
    }

    public Vehicle? GetHostileVehicleAtTileKey(string? tileKey)
    {
        if (Cave is null || string.IsNullOrWhiteSpace(tileKey))
        {
            return null;
        }

        var vehicle = Cave.GetVehicleAtTileKey(tileKey);
        return vehicle is not null && vehicle.Health > 0 ? vehicle : null;
    }

    public object? GetHostileTargetAtTileKey(string? tileKey, bool includeWalls = true)
    {
        return (object?)GetHostileAtTileKey(tileKey) ??
               GetHostileVehicleAtTileKey(tileKey) ??
               (object?)GetHostileBuildingAtTileKey(tileKey, includeWalls);
    }

    public string? GetAdjacentHostileTileKey(GridPoint? location = null, bool includeWalls = false)
    {
        var currentTile = Cave?.GetTile((location ?? Location).ToString());
        if (currentTile is null)
        {
            return null;
        }

        var cave = Cave!;
        string? adjacentBuildingTileKey = null;
        foreach (var neighbor in currentTile.Neighbors)
        {
            if (cave.GetTrilobiteAtTileKey(neighbor.Key) is not null)
            {
                return neighbor.Key;
            }

            if (adjacentBuildingTileKey is null &&
                (GetHostileVehicleAtTileKey(neighbor.Key) is not null ||
                 GetHostileBuildingAtTileKey(neighbor.Key, includeWalls) is not null))
            {
                adjacentBuildingTileKey = neighbor.Key;
            }
        }

        return adjacentBuildingTileKey;
    }

    public string? GetAdjacentWallTileKey(GridPoint? location = null)
    {
        var currentTile = Cave?.GetTile((location ?? Location).ToString());
        if (currentTile is null)
        {
            return null;
        }

        string? adjacentWallTileKey = null;
        foreach (var neighbor in currentTile.Neighbors)
        {
            if (adjacentWallTileKey is null && GetHostileBuildingAtTileKey(neighbor.Key) is Wall)
            {
                adjacentWallTileKey = neighbor.Key;
            }
        }

        return adjacentWallTileKey;
    }

    public bool EnemyStep1()
    {
        CombatState = EnemyCombatState.AcquireTarget;
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (!HasValidEnemyTarget())
        {
            ClearEnemyTarget();
        }

        if (!HasValidEnemyTarget())
        {
            TryAcquireNearbyTarget();
        }

        if (CanReachEnemyTarget())
        {
            return EnemyStep2();
        }

        if (TryPursueMovingTarget())
        {
            return true;
        }

        var adjacent = GetAdjacentHostileTileKey();
        if (adjacent is not null)
        {
            SetEnemyTargetAtTileKey(adjacent, includeWalls: false);
            return EnemyStep2();
        }

        return EnemyStep3();
    }

    public bool EnemyStep2()
    {
        CombatState = EnemyCombatState.AttackTarget;
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (!HasValidEnemyTarget())
        {
            ClearEnemyTarget();
            return EnemyStep3();
        }

        if (!CanReachEnemyTarget())
        {
            return EnemyStep3();
        }

        SetActivity(CreatureActivity.Fighting);
        if (Session.Combat.HasActiveOrPending(this))
        {
            RecordEnemyIntent(CombatActorState.EngageHostile, CombatActorIntentKind.Attack, EnemyTarget);
            return true;
        }

        if (Session.Combat.TryQueueMelee(this, EnemyTarget!.Value))
        {
            RecordEnemyIntent(CombatActorState.EngageHostile, CombatActorIntentKind.Attack, EnemyTarget);
            return true;
        }

        return RecoverEnemy(CombatNoOpReason.InvalidState);
    }

    public bool EnemyStep3()
    {
        CombatState = EnemyCombatState.MoveToColony;
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTarget is not null && !HasValidEnemyTarget())
        {
            ClearEnemyTarget();
        }

        if (EnemyTarget is null)
        {
            TryAcquireNearbyTarget();
        }

        if (EnemyTarget?.Target is Wall && !CanReachEnemyTarget())
        {
            ClearEnemyTarget();
        }

        if (TryPursueMovingTarget())
        {
            return true;
        }

        var cave = Cave;
        var field = cave?.GetBfsFieldObject("colony");
        if (field is null || cave is null)
        {
            ClearEnemyTarget();
            return TryDigTowardQueen() || RecoverEnemy(CombatNoOpReason.NoPath);
        }

        var resolvedField = field;
        var currentFieldValue = field.GetFieldValue(Location, refresh: false);
        if (currentFieldValue == 0)
        {
            return ResolveReachedColonyFieldTarget();
        }

        var resolvedNext = field.GetNextStep(Location, refresh: false);
        if (resolvedNext is null ||
            (cave.GetTile(resolvedNext.Value.ToString()) is { } attemptedTile &&
             !cave.CanCreatureTraverseTile(this, attemptedTile)))
        {
            var refreshedField = cave.GetBfsFieldObject("colony");
            refreshedField?.Rebuild();
            if (refreshedField is null)
            {
                ClearEnemyTarget();
                return RecoverEnemy(CombatNoOpReason.NoPath);
            }

            resolvedField = refreshedField;
            currentFieldValue = resolvedField.GetFieldValue(Location, refresh: false);
            resolvedNext = refreshedField.GetNextStep(Location, refresh: false);
            if (currentFieldValue == 0)
            {
                return ResolveReachedColonyFieldTarget();
            }
        }

        if (currentFieldValue == int.MaxValue || resolvedNext is null)
        {
            var adjacentWallTileKey = GetAdjacentWallTileKey();
            if (adjacentWallTileKey is not null)
            {
                SetEnemyTargetAtTileKey(adjacentWallTileKey);
                ClearTaskQueue();
                return EnemyStep2();
            }

            if (cave.GetWalls().Count > 0)
            {
                var wallField = cave.GetBfsFieldObject("wall");
                if (wallField is not null && wallField.GetFieldValue(Location, refresh: false) != int.MaxValue)
                {
                    var wallNext = wallField.GetNextStep(Location, refresh: false);
                    if (wallNext is not null)
                    {
                        return EnemyRouteAlongField(wallField, allowWallRetarget: true) ||
                               EnemyStepMove(wallNext.Value, allowWallRetarget: true);
                    }
                }
            }

            ClearEnemyTarget();
            return TryDigTowardQueen() || RecoverEnemy(CombatNoOpReason.NoReachableBreach);
        }

        return EnemyRouteAlongField(resolvedField, allowWallRetarget: false) ||
               EnemyStepMove(resolvedNext.Value) ||
               RecoverEnemy(CombatNoOpReason.NoPath);
    }

    private bool EnemyRouteAlongField(BfsField field, bool allowWallRetarget)
    {
        ClearTaskQueue();
        var sharedFieldName = allowWallRetarget ? "wall" : "colony";
        if (BeginStreamingSharedFieldRoute(field, sharedFieldName, clearExisting: false))
        {
            RecordEnemyIntent(
                allowWallRetarget ? CombatActorState.BreachWall : CombatActorState.PursueColony,
                CombatActorIntentKind.Move,
                routeMode: sharedFieldName);
            return true;
        }

        var next = field.GetNextStep(Location, refresh: false);
        if (next is null)
        {
            return false;
        }

        ArmBfsTraversal(field, sharedFieldName);
        return EnemyStepMove(next.Value, allowWallRetarget);
    }

    public bool EnemyStepMove(GridPoint nextLocation, bool allowWallRetarget = false)
    {
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTarget is not null && !HasValidEnemyTarget())
        {
            ClearEnemyTarget();
            ClearTaskQueue();
            return EnemyStep3();
        }

        if (EnemyTarget is null)
        {
            TryAcquireNearbyTarget();
        }

        var adjacent = GetAdjacentHostileTileKey();
        if (adjacent is not null)
        {
            SetEnemyTargetAtTileKey(adjacent, includeWalls: false);
            ClearTaskQueue();
            return EnemyStep2();
        }

        ClearBfsTraversal();
        var moved = Cave?.RequestCreatureMove(this, nextLocation) ?? false;
        if (!moved)
        {
            ClearTaskQueue();
            return EnemyStep3();
        }

        RecordEnemyIntent(
            allowWallRetarget ? CombatActorState.BreachWall : CombatActorState.PursueColony,
            CombatActorIntentKind.Move,
            routeMode: allowWallRetarget ? "wall" : "colony");

        if (CanReachEnemyTarget())
        {
            ClearTaskQueue();
            return EnemyStep2();
        }

        var nextAdjacent = GetAdjacentHostileTileKey();
        if (nextAdjacent is not null)
        {
            SetEnemyTargetAtTileKey(nextAdjacent, includeWalls: false);
            ClearTaskQueue();
            return EnemyStep2();
        }

        if (allowWallRetarget)
        {
            var adjacentWallTileKey = GetAdjacentWallTileKey();
            if (adjacentWallTileKey is not null)
            {
                SetEnemyTargetAtTileKey(adjacentWallTileKey);
                ClearTaskQueue();
                return EnemyStep2();
            }
        }

        return moved;
    }

    protected override bool TryInterruptActiveMovement()
    {
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTarget is not null && !HasValidEnemyTarget())
        {
            ClearEnemyTarget();
        }

        if (EnemyTarget is null)
        {
            TryAcquireNearbyTarget();
        }

        if (CanReachEnemyTarget())
        {
            CancelMovement();
            ClearTaskQueue();
            return EnemyStep2();
        }

        if (EnemyTarget?.Target is Creature target &&
            target.Health > 0 &&
            _pursuedCreatureCell != target.CurrentCell)
        {
            CancelMovement();
            ClearTaskQueue();
            return TryPursueMovingTarget();
        }

        var adjacent = GetAdjacentHostileTileKey();
        if (adjacent is null)
        {
            if (IsStreamingSharedFieldRoute("wall") && GetAdjacentWallTileKey() is { } adjacentWall)
            {
                SetEnemyTargetAtTileKey(adjacentWall);
                CancelMovement();
                ClearTaskQueue();
                return EnemyStep2();
            }

            return false;
        }

        SetEnemyTargetAtTileKey(adjacent, includeWalls: false);
        CancelMovement();
        ClearTaskQueue();
        return EnemyStep2();
    }

    private bool TryDigTowardQueen()
    {
        CombatState = EnemyCombatState.BreachTarget;
        var cave = Cave;
        var queenCenter = cave?.GetQueenBuilding()?.GetCenter();
        if (cave is null || queenCenter is null)
        {
            return false;
        }

        var currentTile = cave.GetTile(Location);
        if (currentTile is null)
        {
            return false;
        }

        Tile? bestWall = null;
        var bestDistance = int.MaxValue;
        foreach (var neighbor in currentTile.Neighbors)
        {
            if (!string.Equals(neighbor.Base, "wall", StringComparison.Ordinal))
            {
                continue;
            }

            var distance = GridPoint.ManhattanDistance(neighbor.Coordinates, queenCenter.Value);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestWall = neighbor;
            bestDistance = distance;
        }

        if (bestWall is null)
        {
            return false;
        }

        if (Session.Mining.HasActiveOrPending(this) ||
            Session.Mining.TryQueueMining(this, bestWall.Key))
        {
            var wall = GetHostileBuildingAtTileKey(bestWall.Key);
            RecordEnemyIntent(
                CombatActorState.BreachWall,
                CombatActorIntentKind.Mine,
                wall is not null ? CombatTargetRef.For(wall) : null);
            return true;
        }

        return false;
    }

    private bool ResolveReachedColonyFieldTarget()
    {
        if (Session.Combat.HasActiveOrPending(this) || Session.Mining.HasActiveOrPending(this))
        {
            return RecoverEnemy(CombatNoOpReason.Cooldown);
        }

        var adjacent = GetAdjacentHostileTileKey(includeWalls: false);
        if (adjacent is not null &&
            SetEnemyTargetAtTileKey(adjacent, includeWalls: false) &&
            CanReachEnemyTarget())
        {
            ClearTaskQueue();
            return EnemyStep2();
        }

        var adjacentWallTileKey = GetAdjacentWallTileKey();
        if (adjacentWallTileKey is not null &&
            SetEnemyTargetAtTileKey(adjacentWallTileKey) &&
            CanReachEnemyTarget())
        {
            ClearTaskQueue();
            return EnemyStep2();
        }

        ClearEnemyTarget();
        ClearTaskQueue();
        return TryDigTowardQueen() || RecoverEnemy(CombatNoOpReason.NoValidTarget);
    }

    private bool RecoverEnemy(CombatNoOpReason reason)
    {
        CombatState = EnemyCombatState.Recover;
        return Session.Combat.RecoverEnemy(this, reason);
    }

    private void RecordEnemyIntent(
        CombatActorState state,
        CombatActorIntentKind kind,
        CombatTargetRef? target = null,
        string? routeMode = null)
    {
        Session.Combat.RecordEnemyIntent(
            this,
            new CombatActorIntent(
                state,
                kind,
                target?.Kind ?? CombatTargetKind.Creature,
                target?.Id ?? 0,
                routeMode ?? ActiveBfsTraversalFieldName));
    }
}
