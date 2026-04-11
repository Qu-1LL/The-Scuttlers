using System.Numerics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Entities;

public class Creature
{
    private const float MovementOffsetMinDistance = 1f;
    private const float MovementOffsetMaxDistance = 15f;
    private readonly Queue<Action> _queue = new();
    private readonly HashSet<Building> _trackedBy = [];
    private Pathfinding.BfsField? _activeBfsTraversalField;

    public Creature(string name, GridPoint location, GameSession session)
    {
        Name = name;
        Location = location;
        Session = session;
        Health = 20;
        MaxHealth = 20;
        Damage = 5;
        Assignment = "unassigned";
        MovementOffset = Vector2.Zero;
        RotationRadians = 0f;
        IsTrackedInTileSystem = true;
        PathPreview = [];
    }

    public string Name { get; private set; }

    public List<GridPoint> PathPreview { get; }

    public int Health { get; protected set; }

    public int MaxHealth { get; protected set; }

    public int Damage { get; protected set; }

    public GridPoint Location { get; set; }

    public float RotationRadians { get; set; }

    public Vector2 MovementOffset { get; private set; }

    public bool IsTrackedInTileSystem { get; private set; }

    public Building? HostedBuilding { get; private set; }

    public Vector2? HostedWorldPosition { get; private set; }

    public GameSession Session { get; }

    public World.Cave? Cave { get; set; }

    public string Assignment { get; set; }

    public string? ActiveBfsTraversalFieldName { get; private set; }

    public Building? ActiveBfsTraversalBuilding { get; private set; }

    public IReadOnlyCollection<Building> TrackedBy => _trackedBy;

    public bool IsHostedOnBuilding(Building? building = null)
    {
        return HostedBuilding is not null &&
               (building is null || ReferenceEquals(HostedBuilding, building));
    }

    public void HostOnBuilding(Building building, Vector2 worldPosition)
    {
        // Hosted creatures keep their last tile `Location` as a restoration hint while
        // world-space consumers render and fire from `HostedWorldPosition`/`GetWorldPosition`.
        HostedBuilding = building;
        HostedWorldPosition = worldPosition;
        IsTrackedInTileSystem = false;
        MovementOffset = Vector2.Zero;
        ClearBfsTraversal();
    }

    public void LeaveTileSystem()
    {
        HostedBuilding = null;
        HostedWorldPosition = null;
        IsTrackedInTileSystem = false;
        MovementOffset = Vector2.Zero;
        ClearBfsTraversal();
    }

    public void ReturnToTileSystem()
    {
        HostedBuilding = null;
        HostedWorldPosition = null;
        IsTrackedInTileSystem = true;
        ClearBfsTraversal();
    }

    protected virtual bool EnsureReadyForTileNavigation()
    {
        return IsTrackedInTileSystem;
    }

    public void ClearActionQueue()
    {
        _queue.Clear();
        PathPreview.Clear();
        ClearBfsTraversal();
    }

    public bool RestartBehavior(bool clearQueue = true)
    {
        if (clearQueue)
        {
            ClearActionQueue();
        }

        var behavior = GetBehavior();
        if (behavior is null)
        {
            return false;
        }

        behavior();
        return true;
    }

    public virtual Action? GetBehavior() => null;

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

    public int DealDamage(object? target)
    {
        return target switch
        {
            Creature creature when !ReferenceEquals(creature, this) => creature.TakeDamage(Damage, this),
            Building building => building.TakeDamage(Damage, this),
            _ => 0
        };
    }

    public virtual int TakeDamage(int amount, object? source = null)
    {
        if (amount <= 0 || Health <= 0)
        {
            return 0;
        }

        var applied = System.Math.Min(Health, amount);
        Health -= applied;
        if (Health <= 0)
        {
            Health = 0;
            RemoveFromGame(source);
        }

        return applied;
    }

    public virtual void CleanupBeforeRemoval(object? source = null)
    {
    }

    public virtual bool RemoveFromGame(object? source = null)
    {
        return Cave?.RemoveCreature(this, source) ?? true;
    }

    public Vector2 GetWorldPosition()
    {
        return HostedWorldPosition ?? new Vector2(
            Location.X * TileConstants.TileSize,
            Location.Y * TileConstants.TileSize) + MovementOffset;
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

    public bool EnqueueAction(Action action)
    {
        _queue.Enqueue(action);
        return true;
    }

    protected int QueuedActionCount => _queue.Count;

    public virtual Action? GetNavigationFallback()
    {
        return Assignment switch
        {
            "miner" when this is Trilobite trilobite => () => trilobite.MinerStep1(),
            "farmer" when this is Trilobite trilobite => () => trilobite.FarmerStep1(),
            "builder" when this is Trilobite trilobite => () => trilobite.BuilderStep1(),
            "fighter" when this is Trilobite trilobite => () => trilobite.FighterStep1(),
            "enemy" when this is Enemy enemy => () => enemy.EnemyStep1(),
            _ => null
        };
    }

    protected bool RunNavigationFallback(Action? fallbackFn)
    {
        ClearActionQueue();
        if (fallbackFn is not null)
        {
            EnqueueAction(fallbackFn);
        }

        return false;
    }

    public List<GridPoint>? BuildNavigationPathToPoint(GridPoint destination)
    {
        if (Cave is null)
        {
            NavigationInstrumentation.RecordPointPathRequest(0, 0L);
            return null;
        }

        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var path = Cave.BuildPathFromField(Cave.BuildPointBfsField(destination), Location);
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
            PathPreview.Clear();
            return true;
        }

        var retryNext = refreshedField.GetNextStep(Location, refresh: false);
        if (retryNext is null)
        {
            PathPreview.Clear();
            return false;
        }

        if (PathPreview.Count > 0)
        {
            PathPreview[0] = retryNext.Value;
        }

        return Cave?.MoveCreature(this, retryNext.Value) ?? false;
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

    protected bool RecoverNavigation(GridPoint? destination, Action? fallbackFn)
    {
        NavigationInstrumentation.RecordNavigationReroute();
        ClearActionQueue();
        if (destination is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        var reroute = BuildNavigationPathToPoint(destination.Value);
        if (reroute is not null && reroute.Count > 1)
        {
            EnqueueResolvedPath(reroute, () => RecoverNavigation(destination, fallbackFn), false);
            return false;
        }

        return reroute is not null && reroute.Count == 1 || RunNavigationFallback(fallbackFn);
    }

    protected bool RecoverDirectNavigation(GridPoint? destination, Action? fallbackFn)
    {
        ClearActionQueue();
        if (destination is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        var reroute = BuildDirectNavigationPathToPoint(destination.Value);
        if (reroute is not null && reroute.Count > 1)
        {
            EnqueueResolvedPath(reroute, () => RecoverDirectNavigation(destination, fallbackFn), false);
            return false;
        }

        return reroute is not null && reroute.Count == 1 || RunNavigationFallback(fallbackFn);
    }

    protected bool RecoverBuildingNavigation(Building? building, Action? fallbackFn, GridPoint? failedStep = null)
    {
        NavigationInstrumentation.RecordNavigationReroute();
        ClearActionQueue();
        if (building is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        if (building is MiningPost miningPost &&
            failedStep.HasValue &&
            Cave?.ShouldInvalidateMiningPostMovementCacheOnFailure(miningPost, Location, failedStep.Value) == true)
        {
            Cave.InvalidateMiningPostMovementCache(miningPost, staleFailure: true);
        }

        if (IsAtBuildingNavigationTarget(building))
        {
            return true;
        }

        EnqueueAction(() => NavigateToBuilding(building, fallbackFn, clearExisting: false));
        return false;
    }

    protected bool ExecuteNavigationStep(GridPoint next, Action? onFailure)
    {
        var result = PerformMove(next);
        if (result == false)
        {
            onFailure?.Invoke();
            return false;
        }

        if (PathPreview.Count > 0)
        {
            NavigationInstrumentation.RecordPathPreviewFrontRemoval(PathPreview.Count);
            PathPreview.RemoveAt(0);
        }

        return result;
    }

    protected bool EnqueueResolvedPath(IReadOnlyList<GridPoint> path, Action? onFailure, bool clearExisting)
    {
        if (clearExisting)
        {
            ClearActionQueue();
        }

        if (path.Count < 2)
        {
            return false;
        }

        foreach (var step in path.Skip(1))
        {
            var next = step;
            PathPreview.Add(next);
            EnqueueAction(() => ExecuteNavigationStep(next, onFailure));
        }

        NavigationInstrumentation.RecordQueuedNavigationSteps(path.Count - 1, PathPreview.Count);
        return true;
    }

    protected bool EnqueueResolvedBuildingPath(Building building, IReadOnlyList<GridPoint> path, Action? fallbackFn, bool clearExisting)
    {
        if (clearExisting)
        {
            ClearActionQueue();
        }

        if (path.Count < 2)
        {
            return false;
        }

        foreach (var step in path.Skip(1))
        {
            var next = step;
            PathPreview.Add(next);
            EnqueueAction(() => ExecuteNavigationStep(next, () => RecoverBuildingNavigation(building, fallbackFn, next)));
        }

        return true;
    }

    public bool NavigateTo(GridPoint destination, Action? fallbackFn = null, bool clearExisting = true)
    {
        fallbackFn ??= GetNavigationFallback();
        if (!EnsureReadyForTileNavigation())
        {
            return RunNavigationFallback(fallbackFn);
        }

        var path = BuildNavigationPathToPoint(destination);
        if (path is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        return path.Count < 2 || EnqueueResolvedPath(path, () => RecoverNavigation(destination, fallbackFn), clearExisting);
    }

    public bool NavigateToPointDirect(GridPoint destination, Action? fallbackFn = null, bool clearExisting = true)
    {
        fallbackFn ??= GetNavigationFallback();
        if (!EnsureReadyForTileNavigation())
        {
            return RunNavigationFallback(fallbackFn);
        }

        var path = BuildDirectNavigationPathToPoint(destination);
        if (path is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        return path.Count < 2 || EnqueueResolvedPath(path, () => RecoverDirectNavigation(destination, fallbackFn), clearExisting);
    }

    public bool NavigateToBuilding(Building building, Action? fallbackFn = null, bool clearExisting = true)
    {
        fallbackFn ??= GetNavigationFallback();
        if (!EnsureReadyForTileNavigation())
        {
            return RunNavigationFallback(fallbackFn);
        }

        if (clearExisting)
        {
            ClearActionQueue();
        }

        // Building navigation now advances one BFS-field step per tick instead of
        // prebuilding and queuing an entire route.
        var field = GetBuildingNavigationField(building);
        if (field is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        if (field.GetFieldValue(Location, refresh: false) == 0)
        {
            return true;
        }

        var resolvedField = field;
        var resolvedNext = field.GetNextStep(Location, refresh: false);
        if (resolvedNext is null || IsImpassableTraversalStep(resolvedNext.Value))
        {
            var attemptedStep = resolvedNext ?? Location;
            var refreshedField = RefreshTraversalField(field, null, building, attemptedStep);
            if (refreshedField is null)
            {
                return RunNavigationFallback(fallbackFn);
            }

            resolvedField = refreshedField;
            resolvedNext = refreshedField.GetNextStep(Location, refresh: false);
            if (resolvedField.GetFieldValue(Location, refresh: false) == 0)
            {
                return true;
            }

            if (resolvedNext is null)
            {
                return RunNavigationFallback(fallbackFn);
            }
        }

        ArmBfsTraversal(resolvedField, building: building);
        ClearBfsTraversal();
        var moved = Cave?.MoveCreature(this, resolvedNext.Value) ?? false;
        return moved || RecoverBuildingNavigation(building, fallbackFn, resolvedNext);
    }

    public bool QueueMovePath(IReadOnlyList<GridPoint> path, Action? fallbackFn = null)
    {
        fallbackFn ??= GetNavigationFallback();
        if (!EnsureReadyForTileNavigation())
        {
            return RunNavigationFallback(fallbackFn);
        }

        if (path.Count < 2)
        {
            return path.Count > 0;
        }

        var destination = path[^1];
        return EnqueueResolvedPath(path, () => RecoverNavigation(destination, fallbackFn), true);
    }

    public bool AppendMovePath(IReadOnlyList<GridPoint> path, Action? fallbackFn = null)
    {
        fallbackFn ??= GetNavigationFallback();
        if (!EnsureReadyForTileNavigation())
        {
            return RunNavigationFallback(fallbackFn);
        }

        if (path.Count < 2)
        {
            return path.Count > 0;
        }

        var destination = path[^1];
        return EnqueueResolvedPath(path, () => RecoverNavigation(destination, fallbackFn), false);
    }

    public List<GridPoint> GetQueuedPathPreview()
    {
        return PathPreview.Count == 0 ? [] : [Location, .. PathPreview];
    }

    public object? Move()
    {
        if (_queue.Count == 0)
        {
            GetBehavior()?.Invoke();
        }

        if (TryInterruptQueuedAction())
        {
            return true;
        }

        if (_queue.Count == 0)
        {
            return null;
        }

        var action = _queue.Dequeue();
        action();
        return true;
    }

    protected virtual bool TryInterruptQueuedAction()
    {
        return false;
    }

    public void UpdateMovementOffset(bool randomize)
    {
        if (!IsTrackedInTileSystem)
        {
            MovementOffset = Vector2.Zero;
            return;
        }

        MovementOffset = randomize
            ? RandomUtil.NextMovementOffset(MovementOffsetMinDistance, MovementOffsetMaxDistance)
            : Vector2.Zero;
    }

    public bool PerformMove(GridPoint next)
    {
        if (!EnsureReadyForTileNavigation())
        {
            return false;
        }

        var field = _activeBfsTraversalField;
        var sharedFieldName = ActiveBfsTraversalFieldName;
        var building = ActiveBfsTraversalBuilding;
        ClearBfsTraversal();

        var moved = Cave?.MoveCreature(this, next) ?? false;
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
