using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Entities;

public class Creature
{
    private const float MovementOffsetMinDistance = 1f;
    private const float MovementOffsetMaxDistance = 15f;
    private readonly Queue<Action> _queue = new();

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
        PathPreview = [];
    }

    public string Name { get; }

    public List<GridPoint> PathPreview { get; }

    public int Health { get; protected set; }

    public int MaxHealth { get; protected set; }

    public int Damage { get; protected set; }

    public GridPoint Location { get; set; }

    public float RotationRadians { get; set; }

    public Vector2 MovementOffset { get; private set; }

    public GameSession Session { get; }

    public World.Cave? Cave { get; set; }

    public string Assignment { get; set; }

    public void ClearActionQueue()
    {
        _queue.Clear();
        PathPreview.Clear();
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

    public bool EnqueueAction(Action action)
    {
        _queue.Enqueue(action);
        return true;
    }

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
        return Cave?.BuildPathFromField(Cave.BuildPointBfsField(destination), Location);
    }

    public List<GridPoint>? BuildNavigationPathToBuilding(Building building)
    {
        return Cave?.BuildPathFromField(Cave.EnsureBuildingBfsField(building), Location);
    }

    protected bool RecoverNavigation(GridPoint? destination, Action? fallbackFn)
    {
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

    protected bool RecoverBuildingNavigation(Building? building, Action? fallbackFn)
    {
        ClearActionQueue();
        if (building is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        var reroute = BuildNavigationPathToBuilding(building);
        if (reroute is not null && reroute.Count > 1)
        {
            EnqueueResolvedPath(reroute, () => RecoverBuildingNavigation(building, fallbackFn), false);
            return false;
        }

        return reroute is not null && reroute.Count == 1 || RunNavigationFallback(fallbackFn);
    }

    protected bool ExecuteNavigationStep(GridPoint next, Action? onFailure)
    {
        var result = Cave?.MoveCreature(this, next);
        if (result == false)
        {
            onFailure?.Invoke();
            return false;
        }

        if (PathPreview.Count > 0)
        {
            PathPreview.RemoveAt(0);
        }

        return result ?? false;
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

        return true;
    }

    public bool NavigateTo(GridPoint destination, Action? fallbackFn = null, bool clearExisting = true)
    {
        fallbackFn ??= GetNavigationFallback();
        var path = BuildNavigationPathToPoint(destination);
        if (path is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        return path.Count < 2 || EnqueueResolvedPath(path, () => RecoverNavigation(destination, fallbackFn), clearExisting);
    }

    public bool NavigateToBuilding(Building building, Action? fallbackFn = null, bool clearExisting = true)
    {
        fallbackFn ??= GetNavigationFallback();
        var path = BuildNavigationPathToBuilding(building);
        if (path is null)
        {
            return RunNavigationFallback(fallbackFn);
        }

        return path.Count < 2 || EnqueueResolvedPath(path, () => RecoverBuildingNavigation(building, fallbackFn), clearExisting);
    }

    public bool QueueMovePath(IReadOnlyList<GridPoint> path, Action? fallbackFn = null)
    {
        fallbackFn ??= GetNavigationFallback();
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

        if (_queue.Count == 0)
        {
            return null;
        }

        var action = _queue.Dequeue();
        action();
        return true;
    }

    public void UpdateMovementOffset(bool randomize)
    {
        MovementOffset = randomize
            ? RandomUtil.NextMovementOffset(MovementOffsetMinDistance, MovementOffsetMaxDistance)
            : Vector2.Zero;
    }

    public bool PerformMove(GridPoint next)
    {
        return Cave?.MoveCreature(this, next) ?? false;
    }

    public HashSet<Building> GetActions()
    {
        var currentTile = Cave?.GetTile(Location.ToString());
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
