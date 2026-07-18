using System.Numerics;
using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Movement;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public class Creature
{
    private const int InteractionArrivalTolerancePixels = 12;
    private const int IdleCandidateAttempts = 8;
    private const int IdleMinimumRestTicks = 10;
    private const int IdleRestTickRange = 31;
    private const int IdleWanderChancePercent = 30;
    private const int IdleWanderRadiusTiles = 2;
    private const int IdleAnchorSnapRadiusTiles = 4;
    private const int IdleMaximumFallbackPathCells = 4;
    internal const int RouteRefillLowWatermark = 3;
    internal const int RouteRefillChunkCells = 10;
    private readonly Queue<CreatureTask> _tasks = new();
    private readonly HashSet<Building> _trackedBy = [];
    private Pathfinding.BfsField? _activeBfsTraversalField;
    private readonly List<WorldPoint> _activeRoute = [];
    private int _activeRouteIndex;
    private string _assignment;
    private WorldVector _pendingImpulse;
    private int _blockedTicks;
    private int _movementBackoffTicks;
    private bool _blockedThisTick;
    private bool _consumedImpulseThisTick;
    private bool _routeBuildDeferred;
    private GridPoint? _locomotionRestoreCell;
    private bool _routeRebuildRequested;
    private RouteContinuationKind _routeContinuationKind;
    private GridPoint _routeContinuationDestination;
    private WorldPoint? _routeContinuationExactDestination;
    private string? _routeContinuationSharedFieldName;
    private Building? _routeContinuationBuilding;
    private WorldPoint? _idleDestination;
    private WorldPoint _idleAnchor;
    private bool _hasIdleAnchor;
    private int _idleRestTicks;
    private int _idleCycle;
    private IdleBehaviorState _idleState;

    public Creature(
        string name,
        GridPoint location,
        GameSession session,
        CreatureMovementProfile? movementProfile = null)
    {
        var profile = movementProfile ?? CreatureMovementProfile.Trilobite;
        Id = session.AllocateCreatureId();
        Name = name;
        Position = WorldPoint.FromGridPoint(location);
        PreviousPosition = Position;
        Velocity = WorldVector.Zero;
        DesiredVelocity = WorldVector.Zero;
        CollisionRadius = profile.CollisionRadius;
        SeparationPadding = profile.SeparationPadding;
        BaseSpeed = profile.BaseSpeed;
        Mass = profile.Mass;
        Session = session;
        Description = string.Empty;
        Health = 20;
        MaxHealth = 20;
        Damage = 5;
        _assignment = "unassigned";
        Role = CreatureRole.Unassigned;
        Activity = CreatureActivity.Idle;
        PreviousFacing = new WorldVector(0, -WorldUnits.UnitsPerPixel);
        FacingDirection = PreviousFacing;
        MovementCohort = MovementCohort.None;
        _idleRestTicks = GetIdleRestTicks(0);
        _idleState = IdleBehaviorState.StationaryIdle;
        RotationRadians = 0f;
        IsLocomotionEnabled = true;
        IsVisible = true;
    }

    public string Name { get; private set; }

    public int Id { get; }

    public string Description { get; protected set; }

    public int Health { get; protected set; }

    public int MaxHealth { get; protected set; }

    public int Damage { get; protected set; }

    public int DamageFlashSequence { get; private set; }

    public GridPoint Location => CurrentCell;

    public GridPoint LocomotionRestoreCell => _locomotionRestoreCell ?? CurrentCell;

    public GridPoint CurrentCell => Position.ToGridPoint();

    public WorldPoint Position { get; private set; }

    public WorldPoint PreviousPosition { get; private set; }

    public WorldVector Velocity { get; private set; }

    public WorldVector DesiredVelocity { get; private set; }

    public int CollisionRadius { get; }

    public int SeparationPadding { get; }

    public int BaseSpeed { get; }

    public int Mass { get; }

    public CreatureRole Role { get; private set; }

    public CreatureActivity Activity { get; private set; }

    public WorldVector PreviousFacing { get; private set; }

    public WorldVector FacingDirection { get; private set; }

    public MovementCohort MovementCohort { get; private set; }

    public WorldPoint? IdleDestination => _idleDestination;

    public WorldPoint? IdleAnchor => _hasIdleAnchor ? _idleAnchor : null;

    public IdleBehaviorState IdleState => _idleState;

    public int IdleRestTicks => _idleRestTicks;

    public WorldPoint? MovementTarget { get; private set; }

    public GridPoint? MovementTargetCell { get; private set; }

    public bool HasActiveMovement => MovementTarget.HasValue;

    public IReadOnlyList<WorldPoint> DesiredRoute => _activeRoute;

    public int DesiredRouteIndex => _activeRouteIndex;

    public InteractionZone? ReservedZone { get; private set; }

    public int? ReservedZoneSlot { get; private set; }

    public float RotationRadians { get; set; }

    public bool IsLocomotionEnabled { get; private set; }

    public bool IsVisible { get; set; }

    public bool DrawBelowBuildings { get; private set; }

    public Building? HostedBuilding { get; private set; }

    public IVehicle? HostedVehicle { get; private set; }

    public GameSession Session { get; }

    public World.Cave? Cave { get; set; }

    public string Assignment
    {
        get => _assignment;
        set
        {
            var assignment = string.IsNullOrWhiteSpace(value) ? "unassigned" : value.Trim().ToLowerInvariant();
            var role = CreatureRoleNames.Parse(assignment);
            if (role != Role)
            {
                _tasks.Clear();
                CancelMovement();
                ReleaseInteractionReservation();
                ClearBfsTraversal();
                ResetIdleState();
            }

            _assignment = assignment;
            Role = role;
        }
    }

    public string? ActiveBfsTraversalFieldName { get; private set; }

    public Building? ActiveBfsTraversalBuilding { get; private set; }

    internal RouteContinuationKind ActiveRouteContinuationKind => _routeContinuationKind;

    internal int RemainingBufferedRoutePoints => Math.Max(0, _activeRoute.Count - _activeRouteIndex - 1);

    public IReadOnlyCollection<Building> TrackedBy => _trackedBy;

    public bool IsHostedOnBuilding(Building? building = null)
    {
        return HostedBuilding is not null &&
               (building is null || ReferenceEquals(HostedBuilding, building));
    }

    public bool IsHostedOnVehicle(IVehicle? vehicle = null)
    {
        return HostedVehicle is not null &&
               (vehicle is null || ReferenceEquals(HostedVehicle, vehicle));
    }

    public void HostOnBuilding(Building building, Vector2 worldPosition, bool drawBelowBuildings = false)
    {
        // Hosted creatures keep their last cell as a restoration hint and use Position as their anchor.
        if (IsLocomotionEnabled)
        {
            _locomotionRestoreCell = CurrentCell;
        }

        HostedBuilding = building;
        HostedVehicle = null;
        IsLocomotionEnabled = false;
        DrawBelowBuildings = drawBelowBuildings;
        SetKinematicWorldPosition(WorldPoint.FromWorldPixels(worldPosition));
        Velocity = WorldVector.Zero;
        DesiredVelocity = WorldVector.Zero;
        MovementTarget = null;
        MovementTargetCell = null;
        _activeRoute.Clear();
        _activeRouteIndex = 0;
        Activity = CreatureActivity.Stationed;
        ClearBfsTraversal();
        ClearRouteContinuation();
    }

    public void HostOnVehicle(IVehicle vehicle, Vector2 worldPosition)
    {
        if (IsLocomotionEnabled)
        {
            _locomotionRestoreCell = CurrentCell;
        }

        HostedBuilding = null;
        HostedVehicle = vehicle;
        IsLocomotionEnabled = false;
        IsVisible = true;
        DrawBelowBuildings = false;
        SetKinematicWorldPosition(WorldPoint.FromWorldPixels(worldPosition));
        Velocity = WorldVector.Zero;
        DesiredVelocity = WorldVector.Zero;
        MovementTarget = null;
        MovementTargetCell = null;
        _activeRoute.Clear();
        _activeRouteIndex = 0;
        Activity = CreatureActivity.Stationed;
        ClearBfsTraversal();
        ClearRouteContinuation();
    }

    public void DisableLocomotion()
    {
        if (IsLocomotionEnabled)
        {
            _locomotionRestoreCell = CurrentCell;
        }

        HostedBuilding = null;
        HostedVehicle = null;
        IsLocomotionEnabled = false;
        IsVisible = true;
        DrawBelowBuildings = false;
        Velocity = WorldVector.Zero;
        DesiredVelocity = WorldVector.Zero;
        MovementTarget = null;
        MovementTargetCell = null;
        _activeRoute.Clear();
        _activeRouteIndex = 0;
        ClearBfsTraversal();
        ClearRouteContinuation();
    }

    public void EnableLocomotion()
    {
        HostedBuilding = null;
        HostedVehicle = null;
        IsLocomotionEnabled = true;
        _locomotionRestoreCell = null;
        IsVisible = true;
        DrawBelowBuildings = false;
        Activity = CreatureActivity.Idle;
        ClearBfsTraversal();
        ClearRouteContinuation();
    }

    public bool CanBeDirectlySelected()
    {
        return IsVisible && !DrawBelowBuildings;
    }

    protected virtual bool EnsureReadyForNavigation()
    {
        return IsLocomotionEnabled;
    }

    public void ClearTaskQueue()
    {
        _tasks.Clear();
        CancelMovement();
        ReleaseInteractionReservation();
        ClearBfsTraversal();
    }

    public bool TryReserveInteractionZone(InteractionZone zone)
    {
        if (ReferenceEquals(ReservedZone, zone) && ReservedZoneSlot.HasValue)
        {
            return zone.TryRenew(this, Session.TickCount, ReservedZoneSlot.Value);
        }

        ReleaseInteractionReservation();
        if (!zone.TryReserve(this, Session.TickCount, out var slotIndex))
        {
            Activity = CreatureActivity.WaitingForSlot;
            return false;
        }

        ReservedZone = zone;
        ReservedZoneSlot = slotIndex;
        return true;
    }

    public void ReleaseInteractionReservation()
    {
        ReservedZone?.Release(this);
        ReservedZone = null;
        ReservedZoneSlot = null;
    }

    public bool TryGetReservedZonePosition(out WorldPoint position)
    {
        if (ReservedZone is not null &&
            ReservedZoneSlot is { } slotIndex &&
            slotIndex >= 0 &&
            slotIndex < ReservedZone.SlotPositions.Count)
        {
            position = ReservedZone.SlotPositions[slotIndex];
            return true;
        }

        position = default;
        return false;
    }

    public bool IsAtReservedInteractionSlot()
    {
        if (!TryGetReservedZonePosition(out var target))
        {
            return false;
        }

        var tolerance = WorldUnits.FromPixels(InteractionArrivalTolerancePixels);
        return (Position - target).LengthSquared <= (long)tolerance * tolerance &&
               Velocity.Length <= BaseSpeed;
    }

    public bool TryMoveInteractionReservation(GridPoint targetCell)
    {
        if (ReservedZone is null || !ReservedZoneSlot.HasValue)
        {
            return false;
        }

        for (var index = 0; index < ReservedZone.SlotPositions.Count; index++)
        {
            if (ReservedZone.SlotPositions[index].ToGridPoint() != targetCell ||
                !ReservedZone.TryMoveReservation(this, Session.TickCount, index))
            {
                continue;
            }

            ReservedZoneSlot = index;
            BeginMovement(ReservedZone.SlotPositions[index]);
            return true;
        }

        Activity = CreatureActivity.WaitingForSlot;
        return false;
    }

    public bool RestartBehavior(bool clearQueue = true)
    {
        if (clearQueue)
        {
            ClearTaskQueue();
        }

        return QueueBehavior();
    }

    protected virtual bool QueueBehavior() => false;

    public bool Rename(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        Name = trimmed;
        return true;
    }

    public int RestoreHealth()
    {
        Health = MaxHealth;
        return Health;
    }

    public virtual int TakeDamage(int amount, object? source = null)
    {
        if (amount <= 0 || Health <= 0)
        {
            return 0;
        }

        var applied = System.Math.Min(Health, amount);
        Health -= applied;
        DamageFlashSequence++;
        Session.NotifyCreatureDamaged(this, applied, source);
        if (Health <= 0)
        {
            Health = 0;
            Session.RequestCreatureDeathParticles(Position);
            Session.RequestAudioCueOncePerTick(
                GameAudioCue.CreatureDeath,
                Position,
                AudioCueRequest.CreatureEffectFootprintTiles);
            RemoveFromGame(source);
        }

        return applied;
    }

    public virtual void CleanupBeforeRemoval(object? source = null)
    {
    }

    public virtual bool RemoveFromGame(object? source = null)
    {
        Session.Combat.RemoveFor(this);
        Session.Mining.RemoveFor(this);
        return Cave?.RemoveCreature(this, source) ?? true;
    }

    public Vector2 GetWorldPosition()
    {
        return Position.ToWorldPixels();
    }

    public Vector2 GetInterpolatedWorldPosition(float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        var previous = PreviousPosition.ToWorldPixels();
        var current = Position.ToWorldPixels();
        return Vector2.Lerp(previous, current, alpha);
    }

    public float GetInterpolatedFacingRadians(float alpha)
    {
        if (!IsLocomotionEnabled)
        {
            return RotationRadians;
        }

        alpha = Math.Clamp(alpha, 0f, 1f);
        var previous = DirectionToRadians(PreviousFacing);
        var current = DirectionToRadians(FacingDirection);
        var delta = MathF.IEEERemainder(current - previous, MathF.Tau);
        return previous + (delta * alpha);
    }

    private static float DirectionToRadians(WorldVector direction)
    {
        return direction.IsZero
            ? 0f
            : MathF.Atan2(direction.Y, direction.X) + (MathF.PI / 2f);
    }

    public void SetMovementCohort(MovementCohort cohort)
    {
        MovementCohort = cohort;
    }

    internal void Face(WorldVector direction)
    {
        if (!direction.IsZero)
        {
            FacingDirection = direction.WithMagnitude(WorldUnits.UnitsPerPixel);
        }
    }

    internal void SnapPresentationPose()
    {
        PreviousPosition = Position;
        PreviousFacing = FacingDirection;
    }

    public void ApplyImpulse(WorldVector impulse, int sourceId)
    {
        if (impulse.IsZero || Health <= 0)
        {
            return;
        }

        _pendingImpulse += impulse;
        Activity = CreatureActivity.KnockedBack;
    }

    internal WorldVector ConsumePendingImpulse(int maximumMagnitude)
    {
        var consumed = _pendingImpulse.ClampMagnitude(maximumMagnitude);
        _pendingImpulse -= consumed;
        if (!consumed.IsZero)
        {
            _consumedImpulseThisTick = true;
        }

        return consumed;
    }

    internal bool HasPendingImpulse => !_pendingImpulse.IsZero;

    internal WorldVector PendingImpulse => _pendingImpulse;

    internal void BeginMovement(GridPoint targetCell)
    {
        MovementTargetCell = targetCell;
        MovementTarget = WorldPoint.FromGridPoint(targetCell);
        DesiredVelocity = (MovementTarget.Value - Position).ClampMagnitude(BaseSpeed);
        Activity = CreatureActivity.Moving;
        EnsureDestinationCohort(targetCell);
    }

    internal void BeginMovement(WorldPoint target)
    {
        MovementTargetCell = target.ToGridPoint();
        MovementTarget = target;
        DesiredVelocity = (target - Position).ClampMagnitude(BaseSpeed);
        Activity = CreatureActivity.Moving;
        EnsureDestinationCohort(target.ToGridPoint());
    }

    internal bool BeginRoute(IReadOnlyList<WorldPoint> route)
    {
        _activeRoute.Clear();
        _activeRouteIndex = 0;
        for (var index = 0; index < route.Count; index++)
        {
            if (route[index] != Position)
            {
                _activeRoute.Add(route[index]);
            }
        }

        if (_activeRoute.Count == 0)
        {
            CancelMovement();
            return false;
        }

        BeginMovement(_activeRoute[0]);
        return true;
    }

    // Replace only a live combat route so target tracking does not reset movement momentum.
    internal bool TryReplaceActiveCombatRoute(WorldPoint exactDestination)
    {
        if (!EnsureReadyForNavigation() || Cave is null)
        {
            return false;
        }

        var path = BuildNavigationPathChunkToPoint(
            exactDestination.ToGridPoint(),
            Location,
            RouteRefillChunkCells,
            out var reachedDestination);
        if (path is null || _routeBuildDeferred)
        {
            return false;
        }

        WorldPoint? exactTarget = reachedDestination ? exactDestination : null;
        if (path.Count < 2 && exactTarget is null)
        {
            return false;
        }

        var route = ContinuousRoutePlanner.Build(Cave, this, path, exactTarget);
        if (route.Count == 0)
        {
            return false;
        }

        var currentVelocity = Velocity;
        _tasks.Clear();
        ReleaseInteractionReservation();
        ClearBfsTraversal();
        if (!BeginRoute(route))
        {
            return false;
        }

        ArmPointRouteContinuation(exactDestination.ToGridPoint(), exactDestination);
        Velocity = currentVelocity;
        return true;
    }

    private bool AppendRoute(IReadOnlyList<WorldPoint> route)
    {
        var appended = false;
        for (var index = 0; index < route.Count; index++)
        {
            var point = route[index];
            if (point == Position ||
                (_activeRoute.Count > 0 && point == _activeRoute[^1]))
            {
                continue;
            }

            _activeRoute.Add(point);
            appended = true;
        }

        return appended;
    }

    private void ClearRouteContinuation()
    {
        _routeContinuationKind = RouteContinuationKind.None;
        _routeContinuationExactDestination = null;
        _routeContinuationSharedFieldName = null;
        _routeContinuationBuilding = null;
    }

    private void ArmPointRouteContinuation(GridPoint destination, WorldPoint? exactDestination)
    {
        _routeContinuationKind = RouteContinuationKind.PointDestination;
        _routeContinuationDestination = destination;
        _routeContinuationExactDestination = exactDestination;
        _routeContinuationSharedFieldName = null;
        _routeContinuationBuilding = null;
    }

    private void ArmFieldRouteContinuation(
        RouteContinuationKind kind,
        Pathfinding.BfsField field,
        string? sharedFieldName = null,
        Building? building = null)
    {
        _routeContinuationKind = kind;
        _routeContinuationExactDestination = null;
        _routeContinuationSharedFieldName = sharedFieldName;
        _routeContinuationBuilding = building;
        ArmBfsTraversal(field, sharedFieldName, building);
    }

    internal void CancelMovement()
    {
        _activeRoute.Clear();
        _activeRouteIndex = 0;
        MovementTarget = null;
        MovementTargetCell = null;
        DesiredVelocity = WorldVector.Zero;
        Velocity = WorldVector.Zero;
        MovementCohort = MovementCohort.None;
        ClearRouteContinuation();
        if (Activity is CreatureActivity.Moving or CreatureActivity.KnockedBack)
        {
            Activity = CreatureActivity.Idle;
        }
    }

    internal void BeginMovementTick()
    {
        if (_blockedThisTick)
        {
            _blockedTicks++;
            if (_blockedTicks == 20)
            {
                _routeRebuildRequested = true;
            }
            else if (_blockedTicks >= 60)
            {
                ReleaseInteractionReservation();
                CancelMovement();
                _movementBackoffTicks = 1 + (Id % 5);
                Activity = CreatureActivity.WaitingForSlot;
                _blockedTicks = 0;
            }
        }
        else
        {
            _blockedTicks = 0;
        }

        _blockedThisTick = false;
        _consumedImpulseThisTick = false;
        PreviousPosition = Position;
        PreviousFacing = FacingDirection;
    }

    internal void SetDesiredVelocity(WorldVector desiredVelocity)
    {
        DesiredVelocity = desiredVelocity.ClampMagnitude(BaseSpeed);
    }

    internal int BlockedTicks => _blockedTicks;

    internal void CompleteMovementTick()
    {
        Velocity = Position - PreviousPosition;
        var facing = _consumedImpulseThisTick ? DesiredVelocity : Velocity;
        if (!facing.IsZero)
        {
            Face(facing);
        }
    }

    internal bool HasRouteContinuation => _activeRouteIndex + 1 < _activeRoute.Count;

    internal bool HasStreamingRouteContinuation => _routeContinuationKind != RouteContinuationKind.None;

    protected bool IsStreamingSharedFieldRoute(string sharedFieldName)
    {
        return _routeContinuationKind == RouteContinuationKind.SharedBfsField &&
               string.Equals(_routeContinuationSharedFieldName, sharedFieldName, StringComparison.Ordinal);
    }

    internal void CommitMovement(WorldPoint position, WorldVector velocity)
    {
        var previous = Position;
        Position = position;
        Velocity = velocity;

        if (MovementTarget is not { } target)
        {
            return;
        }

        var previousRemaining = target - previous;
        var remaining = target - position;
        var toleranceSquared = (long)WorldUnits.FromPixels(1) * WorldUnits.FromPixels(1);
        var reached = remaining.LengthSquared <= toleranceSquared ||
                      ((long)previousRemaining.X * remaining.X + (long)previousRemaining.Y * remaining.Y <= 0 &&
                       !velocity.IsZero);
        if (!reached)
        {
            Activity = CreatureActivity.Moving;
            return;
        }

        Position = target;
        _activeRouteIndex++;
        if (_activeRouteIndex >= _activeRoute.Count)
        {
            TryAppendRouteContinuation(force: true);
        }

        if (_activeRouteIndex < _activeRoute.Count)
        {
            BeginMovement(_activeRoute[_activeRouteIndex]);
        }
        else
        {
            Velocity = WorldVector.Zero;
            DesiredVelocity = WorldVector.Zero;
            _activeRoute.Clear();
            _activeRouteIndex = 0;
            MovementTarget = null;
            MovementTargetCell = null;
            Activity = _routeBuildDeferred ? CreatureActivity.Planning : CreatureActivity.Idle;
            MovementCohort = MovementCohort.None;
            if (!_routeBuildDeferred)
            {
                ClearRouteContinuation();
            }
        }
    }

    private void EnsureDestinationCohort(GridPoint target)
    {
        if (MovementCohort.IsActive)
        {
            return;
        }

        var faction = Role == CreatureRole.Enemy ? CreatureFaction.Ants : CreatureFaction.Colony;
        var goalId = unchecked((target.X * 397) ^ target.Y);
        MovementCohort = new MovementCohort(faction, MovementGoalKind.Destination, goalId);
    }

    protected bool TryAdvanceIdleBehavior()
    {
        if (!CanUseIdleMovement || Cave is null || !IsLocomotionEnabled ||
            ReservedZone is not null || HasPendingImpulse || Session.Danger)
        {
            ResetIdleBehavior();
            return false;
        }

        if (_idleDestination.HasValue)
        {
            if (HasActiveMovement)
            {
                return true;
            }

            _idleDestination = null;
            _idleRestTicks = GetIdleRestTicks(_idleCycle);
            _idleState = IdleBehaviorState.StationaryIdle;
            _hasIdleAnchor = false;
            Activity = CreatureActivity.Idle;
            return true;
        }

        if (_idleRestTicks > 0)
        {
            _idleRestTicks--;
            RefreshIdleAnchor();
            FaceIdleAnchor();
            _idleState = IdleBehaviorState.StationaryIdle;
            Activity = CreatureActivity.Idle;
            return true;
        }

        RefreshIdleAnchor();
        _idleCycle++;
        var maxWanderDistance = (long)IdleWanderRadiusTiles * WorldUnits.UnitsPerTile;

        // Most idle decisions remain stationary; the remainder get one short local move.
        var wanderBucket = PositiveModulo(
            GetIdleSample(_idleCycle, 211) + (_idleCycle * 31) + (Id * 17),
            100);
        if (wanderBucket >= IdleWanderChancePercent)
        {
            _idleRestTicks = GetIdleRestTicks(_idleCycle);
            _idleState = IdleBehaviorState.StationaryIdle;
            Activity = CreatureActivity.Idle;
            return true;
        }

        var directionStart = PositiveModulo(GetIdleSample(_idleCycle, 1), IdleDirections.Length);
        for (var attempt = 0; attempt < IdleCandidateAttempts; attempt++)
        {
            var sample = GetIdleSample(_idleCycle, attempt + 1);
            var distance = (WorldUnits.UnitsPerTile / 2) +
                           (PositiveModulo(sample >> 4, 4) * (WorldUnits.UnitsPerTile / 2));
            // Walk the deterministic direction ring so map edges still get a valid local option.
            var direction = IdleDirections[PositiveModulo(directionStart + attempt, IdleDirections.Length)];
            var candidate = _idleAnchor + new WorldVector(
                (int)(((long)direction.X * distance) / 1000),
                (int)(((long)direction.Y * distance) / 1000));
            if ((candidate - _idleAnchor).LengthSquared > maxWanderDistance * maxWanderDistance ||
                (candidate - Position).LengthSquared < (long)WorldUnits.FromPixels(2) * WorldUnits.FromPixels(2) ||
                !Cave.CanCreatureOccupyWorldPosition(this, candidate))
            {
                continue;
            }

            if (TryBeginIdleLocalMove(candidate))
            {
                return true;
            }
        }

        _idleRestTicks = GetIdleRestTicks(_idleCycle);
        _idleState = IdleBehaviorState.StationaryIdle;
        Activity = CreatureActivity.Idle;
        return true;
    }

    // Allow role controllers to enter the exact same idle-wander path as the base move loop.
    internal bool AdvanceSharedIdleBehavior()
    {
        return TryAdvanceIdleBehavior();
    }

    private bool TryBeginIdleLocalMove(WorldPoint candidate)
    {
        if (Cave is null)
        {
            return false;
        }

        if (Cave.HasClearStaticSweep(this, Position, candidate))
        {
            return BeginIdleDirectMove(candidate);
        }

        var path = Cave.BuildDirectPathToPoint(Location, candidate.ToGridPoint());
        if (path is null || path.Count < 2 || path.Count - 1 > IdleMaximumFallbackPathCells)
        {
            return false;
        }

        var route = ContinuousRoutePlanner.Build(Cave, this, path, candidate);
        if (route.Count == 0 || !BeginRoute(route))
        {
            return false;
        }

        NavigationInstrumentation.RecordQueuedNavigationSteps(route.Count);
        ClearRouteContinuation();
        SetIdleMovementState(candidate);
        return true;
    }

    private void ResetIdleBehavior()
    {
        _idleDestination = null;
        _idleRestTicks = GetIdleRestTicks(_idleCycle);
        _idleState = IdleBehaviorState.StationaryIdle;
        _hasIdleAnchor = false;
    }

    private void ResetIdleState()
    {
        _idleDestination = null;
        _idleAnchor = default;
        _hasIdleAnchor = false;
        _idleState = IdleBehaviorState.StationaryIdle;
        _idleRestTicks = GetIdleRestTicks(_idleCycle);
    }

    private void RefreshIdleAnchor()
    {
        if (!TryGetIdleAnchor(out var anchor))
        {
            anchor = Position;
        }

        var maxAnchorDistance = (long)IdleAnchorSnapRadiusTiles * WorldUnits.UnitsPerTile;
        if ((anchor - Position).LengthSquared > maxAnchorDistance * maxAnchorDistance)
        {
            anchor = Position;
        }

        _idleAnchor = anchor;
        _hasIdleAnchor = true;
    }

    private void FaceIdleAnchor()
    {
        if (_hasIdleAnchor)
        {
            Face(_idleAnchor - Position);
        }
    }

    private bool BeginIdleDirectMove(WorldPoint candidate)
    {
        _activeRoute.Clear();
        _activeRouteIndex = 0;
        _activeRoute.Add(candidate);
        ClearRouteContinuation();
        BeginMovement(candidate);
        SetIdleMovementState(candidate);
        return true;
    }

    private void SetIdleMovementState(WorldPoint candidate)
    {
        SetMovementCohort(new MovementCohort(
            CreatureFaction.Colony,
            MovementGoalKind.Idle,
            unchecked((Id * 397) ^ _idleCycle)));
        _idleDestination = candidate;
        _idleState = IdleBehaviorState.WanderNearAnchor;
    }

    protected virtual bool TryGetIdleAnchor(out WorldPoint anchor)
    {
        anchor = Position;
        return true;
    }

    protected virtual bool CanUseIdleMovement => Role == CreatureRole.Unassigned;

    private int GetIdleRestTicks(int cycle)
    {
        return IdleMinimumRestTicks + PositiveModulo(GetIdleSample(cycle, 97), IdleRestTickRange);
    }

    private int GetIdleSample(int cycle, int salt)
    {
        unchecked
        {
            var sample = Id * 1103515245;
            sample ^= cycle * -1640531527;
            sample += salt * 12345;
            sample ^= sample >> 16;
            return sample & int.MaxValue;
        }
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static readonly WorldVector[] IdleDirections =
    [
        new(1000, 0), new(924, 383), new(707, 707), new(383, 924),
        new(0, 1000), new(-383, 924), new(-707, 707), new(-924, 383),
        new(-1000, 0), new(-924, -383), new(-707, -707), new(-383, -924),
        new(0, -1000), new(383, -924), new(707, -707), new(924, -383)
    ];

    internal void MarkMovementBlocked()
    {
        _blockedThisTick = true;
        if (Activity == CreatureActivity.Moving)
        {
            Activity = CreatureActivity.WaitingForSlot;
        }
    }

    private void QueueActiveRouteRebuild()
    {
        if (Cave is null || _activeRoute.Count == 0)
        {
            return;
        }

        if (_routeContinuationKind != RouteContinuationKind.None && TryAppendRouteContinuation(force: true))
        {
            Activity = CreatureActivity.Moving;
            return;
        }

        var destination = _activeRoute[^1];
        CancelMovement();
        EnqueueTask(CreatureTask.NavigateTo(destination));
        Activity = CreatureActivity.Planning;
    }

    internal void SetWorldPosition(WorldPoint position, bool snapPrevious)
    {
        Position = position;
        if (snapPrevious)
        {
            PreviousPosition = position;
        }
    }

    private void SetKinematicWorldPosition(WorldPoint position)
    {
        Position = position;
        PreviousPosition = position;
    }

    internal void SetActivity(CreatureActivity activity)
    {
        Activity = activity;
    }

    public bool ShootProjectile(Creature target, Projectile projectile)
    {
        return Session.LaunchProjectile(this, target, projectile) is not null;
    }

    public bool AddTrackedBy(Building building)
    {
        return _trackedBy.Add(building);
    }

    public bool RemoveTrackedBy(Building building)
    {
        return _trackedBy.Remove(building);
    }

    public void NotifyTrackedByCreatureDied()
    {
        if (_trackedBy.Count == 0)
        {
            return;
        }

        var trackedBuildings = _trackedBy.ToArray();
        foreach (var building in trackedBuildings)
        {
            building.TrackedCreatureDied(this);
        }

        _trackedBy.Clear();
    }

    public bool EnqueueTask(CreatureTask task)
    {
        _tasks.Enqueue(task);
        return true;
    }

    protected int QueuedTaskCount => _tasks.Count;

    protected virtual bool ExecuteTask(CreatureTask task)
    {
        return task.Kind switch
        {
            CreatureTaskKind.NavigateTo when task.UsesWorldTarget => NavigateTo(task.WorldTarget, clearExisting: false),
            CreatureTaskKind.NavigateTo => NavigateTo(task.Target, clearExisting: false),
            _ => false
        };
    }

    public List<GridPoint>? BuildNavigationPathToPoint(GridPoint destination)
    {
        if (Cave is null)
        {
            NavigationInstrumentation.RecordPointPathRequest(0, 0L);
            return null;
        }

        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var path = Cave.BuildPointPath(Location, destination, out var deferred);
        _routeBuildDeferred = deferred;
        if (deferred)
        {
            Activity = CreatureActivity.Planning;
        }

        NavigationInstrumentation.RecordPointPathRequest(path?.Count ?? 0, GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        return path;
    }

    private List<GridPoint>? BuildNavigationPathChunkToPoint(
        GridPoint destination,
        GridPoint startLocation,
        int maximumSteps,
        out bool reachedDestination)
    {
        reachedDestination = false;
        if (Cave is null)
        {
            NavigationInstrumentation.RecordPointPathRequest(0, 0L);
            return null;
        }

        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var path = Cave.BuildPointPathChunk(startLocation, destination, maximumSteps, out var deferred, out reachedDestination);
        _routeBuildDeferred = deferred;
        if (deferred && !HasActiveMovement)
        {
            Activity = CreatureActivity.Planning;
        }

        NavigationInstrumentation.RecordPointPathRequest(path?.Count ?? 0, GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        return path;
    }

    public List<GridPoint>? BuildDirectNavigationPathToPoint(GridPoint destination)
    {
        return Cave?.BuildDirectPathToPoint(Location, destination);
    }

    public List<GridPoint>? BuildNavigationPathToBuilding(Building building)
    {
        if (Cave is null)
        {
            NavigationInstrumentation.RecordBuildingPathRequest(0, 0L);
            return null;
        }

        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var path = building is MiningPost miningPost
            ? Cave.BuildPathToMiningPost(miningPost, Location)
            : Cave.BuildPathFromField(Cave.EnsureBuildingBfsField(building), Location);
        NavigationInstrumentation.RecordBuildingPathRequest(
            path?.Count ?? 0,
            GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        return path;
    }

    protected Pathfinding.BfsField? GetBuildingNavigationField(Building? building)
    {
        if (building is null || Cave is null)
        {
            return null;
        }

        if (building is MiningPost miningPost)
        {
            return Cave.GetMiningPostMovementFieldObject(miningPost);
        }

        return Cave.GetAccessibleBuildingBfsFieldObject(building, Location, rebuildIfEmpty: true);
    }

    protected void ArmBfsTraversal(Pathfinding.BfsField? field, string? sharedFieldName = null, Building? building = null)
    {
        _activeBfsTraversalField = field;
        ActiveBfsTraversalFieldName = sharedFieldName;
        ActiveBfsTraversalBuilding = building;
    }

    protected void ClearBfsTraversal()
    {
        _activeBfsTraversalField = null;
        ActiveBfsTraversalFieldName = null;
        ActiveBfsTraversalBuilding = null;
    }

    private bool IsImpassableTraversalStep(GridPoint location)
    {
        return Cave is null || !Cave.CanCreatureTraverseTile(this, Cave.GetTile(location.ToString()));
    }

    private Pathfinding.BfsField? RefreshTraversalField(
        Pathfinding.BfsField? field,
        string? sharedFieldName,
        Building? building,
        GridPoint attemptedLocation)
    {
        if (Cave is null)
        {
            return null;
        }

        if (building is MiningPost miningPost)
        {
            if (Cave.ShouldInvalidateMiningPostMovementCacheOnFailure(miningPost, Location, attemptedLocation))
            {
                Cave.InvalidateMiningPostMovementCache(miningPost, staleFailure: true);
            }

            return Cave.GetMiningPostMovementFieldObject(miningPost);
        }

        if (building is not null)
        {
            var buildingField = Cave.GetBuildingBfsFieldObject(building);
            buildingField.Rebuild();
            return buildingField;
        }

        if (!string.IsNullOrWhiteSpace(sharedFieldName))
        {
            var sharedField = Cave.GetBfsFieldObject(sharedFieldName);
            sharedField?.Rebuild();
            return sharedField;
        }

        field?.Rebuild();
        return field;
    }

    private bool RetryTraversalMove(
        Pathfinding.BfsField? field,
        string? sharedFieldName,
        Building? building,
        GridPoint attemptedLocation)
    {
        var refreshedField = RefreshTraversalField(field, sharedFieldName, building, attemptedLocation);
        if (refreshedField is null)
        {
            return false;
        }

        if (refreshedField.GetFieldValue(Location, refresh: false) == 0)
        {
            return true;
        }

        var retryNext = refreshedField.GetNextStep(Location, refresh: false);
        if (retryNext is null)
        {
            return false;
        }

        return Cave?.RequestCreatureMove(this, retryNext.Value) ?? false;
    }

    protected GridPoint? ResolveTraversalStep(
        Pathfinding.BfsField field,
        GridPoint proposedStep,
        string? sharedFieldName = null,
        Building? building = null)
    {
        if (!IsImpassableTraversalStep(proposedStep))
        {
            return proposedStep;
        }

        var refreshedField = RefreshTraversalField(field, sharedFieldName, building, proposedStep);
        if (refreshedField is null)
        {
            return null;
        }

        return refreshedField.GetFieldValue(Location, refresh: false) == 0
            ? Location
            : refreshedField.GetNextStep(Location, refresh: false);
    }

    protected bool IsAtBuildingNavigationTarget(Building? building)
    {
        var field = GetBuildingNavigationField(building);
        return field is not null && field.GetFieldValue(Location, refresh: false) == 0;
    }

    protected bool EnqueueResolvedPath(IReadOnlyList<GridPoint> path, bool clearExisting)
    {
        if (clearExisting)
        {
            ClearTaskQueue();
        }

        if (path.Count < 2)
        {
            return false;
        }

        var route = Cave is null ? [] : ContinuousRoutePlanner.Build(Cave, this, path);
        NavigationInstrumentation.RecordQueuedNavigationSteps(route.Count);
        return BeginRoute(route);
    }

    protected bool EnqueueFieldRouteChunk(Pathfinding.BfsField field, int maximumSteps, bool clearExisting)
    {
        if (!EnsureReadyForNavigation() || Cave is null || maximumSteps <= 0)
        {
            return false;
        }

        var current = Location;
        var currentValue = field.GetFieldValue(current, refresh: false);
        if (currentValue <= 0 || currentValue == int.MaxValue)
        {
            return false;
        }

        var path = new List<GridPoint>(maximumSteps + 1) { current };
        for (var step = 0; step < maximumSteps && currentValue > 0; step++)
        {
            var next = field.GetNextStep(current, refresh: false);
            if (next is null || Cave.GetTile(next.Value) is not { } nextTile ||
                !Cave.CanCreatureTraverseTile(this, nextTile))
            {
                break;
            }

            path.Add(next.Value);
            current = next.Value;
            currentValue = field.GetFieldValue(current, refresh: false);
        }

        return path.Count >= 2 && EnqueueResolvedPath(path, clearExisting);
    }

    protected bool BeginStreamingSharedFieldRoute(Pathfinding.BfsField field, string sharedFieldName, bool clearExisting)
    {
        return TryBeginFieldRoute(
            field,
            RouteContinuationKind.SharedBfsField,
            sharedFieldName,
            building: null,
            clearExisting);
    }

    private List<GridPoint>? BuildFieldPathChunk(
        Pathfinding.BfsField field,
        GridPoint startLocation,
        int maximumSteps,
        out bool reachedFieldTarget)
    {
        reachedFieldTarget = false;
        if (Cave is null || maximumSteps <= 0)
        {
            return null;
        }

        var current = startLocation;
        var currentValue = field.GetFieldValue(current, refresh: false);
        if (currentValue == int.MaxValue)
        {
            return null;
        }

        if (currentValue <= 0)
        {
            reachedFieldTarget = true;
            return [current];
        }

        var path = new List<GridPoint>(Math.Min(maximumSteps, currentValue) + 1) { current };
        for (var step = 0; step < maximumSteps && currentValue > 0; step++)
        {
            var next = field.GetNextStep(current, refresh: false);
            if (next is null || Cave.GetTile(next.Value) is not { } nextTile ||
                !Cave.CanCreatureTraverseTile(this, nextTile))
            {
                break;
            }

            path.Add(next.Value);
            current = next.Value;
            currentValue = field.GetFieldValue(current, refresh: false);
        }

        reachedFieldTarget = currentValue == 0;
        return path.Count >= 2 || reachedFieldTarget ? path : null;
    }

    private bool TryBeginPointRoute(GridPoint destination, WorldPoint? exactDestination, bool clearExisting)
    {
        var path = BuildNavigationPathChunkToPoint(destination, Location, RouteRefillChunkCells, out var reachedDestination);
        if (path is null || Cave is null)
        {
            if (_routeBuildDeferred)
            {
                if (clearExisting)
                {
                    ClearTaskQueue();
                }

                EnqueueTask(exactDestination.HasValue
                    ? CreatureTask.NavigateTo(exactDestination.Value)
                    : CreatureTask.NavigateTo(destination));
                Activity = CreatureActivity.Planning;
                return true;
            }

            return false;
        }

        if (clearExisting)
        {
            ClearTaskQueue();
        }

        var exactTarget = reachedDestination ? exactDestination : null;
        if (path.Count < 2 && exactTarget is null)
        {
            return true;
        }

        var route = ContinuousRoutePlanner.Build(Cave, this, path, exactTarget);
        if (route.Count == 0)
        {
            return true;
        }

        NavigationInstrumentation.RecordQueuedNavigationSteps(route.Count);
        if (!BeginRoute(route))
        {
            return false;
        }

        ArmPointRouteContinuation(destination, exactDestination);
        return true;
    }

    private bool TryBeginFieldRoute(
        Pathfinding.BfsField field,
        RouteContinuationKind kind,
        string? sharedFieldName,
        Building? building,
        bool clearExisting)
    {
        if (clearExisting)
        {
            ClearTaskQueue();
        }

        var path = BuildFieldPathChunk(field, Location, RouteRefillChunkCells, out _);
        if (path is null || Cave is null)
        {
            return false;
        }

        var route = ContinuousRoutePlanner.Build(Cave, this, path);
        if (route.Count == 0)
        {
            return true;
        }

        NavigationInstrumentation.RecordQueuedNavigationSteps(route.Count);
        if (!BeginRoute(route))
        {
            return false;
        }

        ArmFieldRouteContinuation(kind, field, sharedFieldName, building);
        return true;
    }

    internal bool TryAppendRouteContinuation(bool force = false)
    {
        if (!EnsureReadyForNavigation() ||
            Cave is null ||
            _routeContinuationKind == RouteContinuationKind.None ||
            _activeRoute.Count == 0 ||
            (!force && RemainingBufferedRoutePoints > RouteRefillLowWatermark))
        {
            return false;
        }

        var appendOrigin = _activeRoute[^1];
        var appendStart = appendOrigin.ToGridPoint();
        List<GridPoint>? path = null;
        WorldPoint? exactDestination = null;
        var reachedDestination = false;

        switch (_routeContinuationKind)
        {
            case RouteContinuationKind.PointDestination:
                path = BuildNavigationPathChunkToPoint(
                    _routeContinuationDestination,
                    appendStart,
                    RouteRefillChunkCells,
                    out reachedDestination);
                if (_routeBuildDeferred)
                {
                    return false;
                }

                exactDestination = reachedDestination ? _routeContinuationExactDestination : null;
                break;
            case RouteContinuationKind.SharedBfsField:
            {
                var field = string.IsNullOrWhiteSpace(_routeContinuationSharedFieldName)
                    ? _activeBfsTraversalField
                    : Cave.GetBfsFieldObject(_routeContinuationSharedFieldName);
                if (field is null)
                {
                    ClearRouteContinuation();
                    return false;
                }

                path = BuildFieldPathChunk(field, appendStart, RouteRefillChunkCells, out reachedDestination);
                _activeBfsTraversalField = field;
                break;
            }
            case RouteContinuationKind.BuildingField:
            case RouteContinuationKind.MiningPostField:
            {
                var field = GetBuildingNavigationField(_routeContinuationBuilding);
                if (field is null)
                {
                    ClearRouteContinuation();
                    return false;
                }

                path = BuildFieldPathChunk(field, appendStart, RouteRefillChunkCells, out reachedDestination);
                _activeBfsTraversalField = field;
                break;
            }
        }

        if (path is null || path.Count < 2)
        {
            if (reachedDestination)
            {
                ClearRouteContinuation();
            }

            return false;
        }

        var route = ContinuousRoutePlanner.Build(Cave, this, path, exactDestination, appendOrigin);
        var appended = AppendRoute(route);
        if (appended)
        {
            NavigationInstrumentation.RecordQueuedNavigationSteps(route.Count);
        }

        if (appended && reachedDestination)
        {
            ClearRouteContinuation();
        }

        return appended;
    }

    public bool NavigateTo(GridPoint destination, bool clearExisting = true)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        return TryBeginPointRoute(destination, exactDestination: null, clearExisting);
    }

    public bool NavigateTo(WorldPoint destination, bool clearExisting = true)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        return TryBeginPointRoute(destination.ToGridPoint(), destination, clearExisting);
    }

    internal bool NavigateToViaSharedRoute(WorldPoint destination, GridPoint sharedRouteDestination, bool clearExisting = true)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        if (clearExisting)
        {
            ClearTaskQueue();
        }

        return TryBeginPointRoute(sharedRouteDestination, destination, clearExisting) ||
               NavigateTo(destination, clearExisting: false);
    }

    public bool NavigateToPointDirect(GridPoint destination, bool clearExisting = true)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        var path = BuildDirectNavigationPathToPoint(destination);
        if (path is null)
        {
            return false;
        }

        return path.Count < 2 || EnqueueResolvedPath(path, clearExisting);
    }

    public bool NavigateToBuilding(Building building, bool clearExisting = true)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        var field = GetBuildingNavigationField(building);
        if (field is null)
        {
            return false;
        }

        var kind = building is MiningPost ? RouteContinuationKind.MiningPostField : RouteContinuationKind.BuildingField;
        if (TryBeginFieldRoute(field, kind, sharedFieldName: null, building, clearExisting))
        {
            return true;
        }

        field.Rebuild();
        return TryBeginFieldRoute(field, kind, sharedFieldName: null, building, clearExisting: false);
    }

    public bool NavigateToInteractionZone(
        Building building,
        InteractionZonePurpose purpose,
        bool clearExisting = true)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        if (clearExisting)
        {
            ClearTaskQueue();
        }

        if (!building.TryGetInteractionZone(purpose, out var zone) || !TryReserveInteractionZone(zone))
        {
            return false;
        }

        if (!TryGetReservedZonePosition(out var target))
        {
            ReleaseInteractionReservation();
            return false;
        }

        var targetCell = target.ToGridPoint();
        if (CurrentCell == targetCell)
        {
            if (Position != target)
            {
                BeginMovement(target);
            }

            return true;
        }

        if (NavigateTo(targetCell, clearExisting: false))
        {
            return true;
        }

        ReleaseInteractionReservation();
        return false;
    }

    public bool QueueMovePath(IReadOnlyList<GridPoint> path)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        if (path.Count < 2)
        {
            return path.Count > 0;
        }

        return EnqueueResolvedPath(path, true);
    }

    public bool AppendMovePath(IReadOnlyList<GridPoint> path)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        if (path.Count < 2)
        {
            return path.Count > 0;
        }

        return EnqueueResolvedPath(path, false);
    }

    public object? Move()
    {
        if (_routeRebuildRequested)
        {
            _routeRebuildRequested = false;
            QueueActiveRouteRebuild();
        }

        if (_movementBackoffTicks > 0)
        {
            _movementBackoffTicks--;
            return null;
        }

        if (ReservedZone is not null &&
            (!ReservedZone.Owner.OwnsInteractionZone(ReservedZone) ||
             !ReservedZoneSlot.HasValue ||
             !ReservedZone.TryRenew(this, Session.TickCount, ReservedZoneSlot.Value)))
        {
            ReleaseInteractionReservation();
            Activity = CreatureActivity.WaitingForSlot;
        }

        // Driveable vehicles advance on the driver's turn instead of through the creature's own task queue.
        if (HostedVehicle is IDriveable driveable && driveable.IsCreatureDriving(this))
        {
            return HostedVehicle.Move();
        }

        if (HostedVehicle is IDriveable)
        {
            return null;
        }

        if (HasActiveMovement && TryInterruptActiveMovement())
        {
            return true;
        }

        if (HasActiveMovement)
        {
            TryAppendRouteContinuation();
            return null;
        }

        if (HasPendingImpulse)
        {
            return null;
        }

        if (_tasks.Count == 0)
        {
            QueueBehavior();
        }

        if (TryInterruptQueuedTask())
        {
            return true;
        }

        if (_tasks.Count == 0)
        {
            TryAdvanceIdleBehavior();
            return null;
        }

        var executed = ExecuteTask(_tasks.Dequeue());
        if (!executed && _tasks.Count == 0)
        {
            TryAdvanceIdleBehavior();
        }

        return executed;
    }

    protected virtual bool TryInterruptQueuedTask()
    {
        return false;
    }

    protected virtual bool TryInterruptActiveMovement()
    {
        return false;
    }

    public bool PerformMove(GridPoint next)
    {
        if (!EnsureReadyForNavigation())
        {
            return false;
        }

        var field = _activeBfsTraversalField;
        var sharedFieldName = ActiveBfsTraversalFieldName;
        var building = ActiveBfsTraversalBuilding;
        ClearBfsTraversal();

        var moved = Cave?.RequestCreatureMove(this, next) ?? false;
        if (moved || field is null || !IsImpassableTraversalStep(next))
        {
            return moved;
        }

        return RetryTraversalMove(field, sharedFieldName, building, next);
    }

    public HashSet<Building> GetActions()
    {
        var currentTile = Cave?.GetTile(Location);
        if (currentTile is null)
        {
            return [];
        }

        var actions = new HashSet<Building>();
        if (currentTile.Built is { HasStation: true } currentBuilding)
        {
            actions.Add(currentBuilding);
        }

        foreach (var neighbor in currentTile.Neighbors)
        {
            if (neighbor.Built is { HasStation: false } building)
            {
                actions.Add(building);
            }
        }

        return actions;
    }

    public virtual List<Factory> GetBuildable()
    {
        return [.. Session.UnlockedBuildings];
    }
}
