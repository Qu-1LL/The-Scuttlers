using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Turret : StationBuilding
{
    public const int DefaultFighterAssignmentPriority = 1;
    private const int ReloadTicks = 5;

    private static readonly int[][] DefaultOpenMap =
    [
        [0, 0, 2],
        [0, 0, 0],
        [2, 0, 0]
    ];

    private static readonly StationSlot[] DefaultStations =
    [
        new(null, new Vector2(TileConstants.TileSize, TileConstants.TileSize)),
        new(null, new Vector2(TileConstants.TileSize * 2f, TileConstants.TileSize * 2f))
    ];

    private static readonly StationSlot[] RotatedStations =
    [
        new(null, new Vector2(TileConstants.TileSize * 2f, TileConstants.TileSize)),
        new(null, new Vector2(TileConstants.TileSize, TileConstants.TileSize * 2f))
    ];
    private readonly Dictionary<Creature, int> _remainingReloadTicks = [];

    public override int ProjectionRadius => GameConstants.TurretProjectionRadius;

    public Enemy? Target { get; private set; }

    public Turret(GameSession session)
        : base(
            "Turret",
            new GridPoint(3, 3),
            DefaultOpenMap,
            session,
            "Turret",
            "A defensive station for up to 2 fighters. Fighters prioritize turrets over barracks.",
            DefaultFighterAssignmentPriority,
            DefaultStations)
    {
        MaxHealth = GameConstants.TurretMaxHealth;
        Health = MaxHealth;
    }

    public override void OnBuilt(World.Cave cave)
    {
        base.OnBuilt(cave);
        AcquireInitialTarget();
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        _remainingReloadTicks.Clear();
        SetTarget(null);
        base.CleanupBeforeRemoval(source);
    }

    public override bool Assign(Creature creature)
    {
        var wasAssigned = IsAssigned(creature);
        var assigned = base.Assign(creature);
        if (assigned && !wasAssigned)
        {
            _remainingReloadTicks[creature] = ReloadTicks;
        }

        return assigned;
    }

    public override bool RemoveAssignment(Creature creature)
    {
        var removed = base.RemoveAssignment(creature);
        if (removed)
        {
            _remainingReloadTicks.Remove(creature);
        }

        return removed;
    }

    public override GridPoint? GetAssignedNavigationTile(Creature creature, GridPoint preferredLocation)
    {
        return GetPreferredAccessTile(preferredLocation);
    }

    public override GridPoint? GetPreferredAccessTile(GridPoint preferredLocation)
    {
        if (Cave is null)
        {
            return null;
        }

        GridPoint? bestTile = null;
        var bestDistance = int.MaxValue;
        string? bestKey = null;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in EnumerateOwnedTiles())
        {
            foreach (var neighbor in tile.Neighbors)
            {
                if (ReferenceEquals(neighbor.Built, this) ||
                    !neighbor.CreatureFits() ||
                    !Cave.IsTileReachable(neighbor) ||
                    Cave.GetEnemyAtTileKey(neighbor.Key) is not null ||
                    !visited.Add(neighbor.Key))
                {
                    continue;
                }

                var distance = GridPoint.SquaredDistance(preferredLocation, neighbor.Coordinates);
                if (bestTile is null ||
                    distance < bestDistance ||
                    (distance == bestDistance && string.CompareOrdinal(neighbor.Key, bestKey) < 0))
                {
                    bestTile = neighbor.Coordinates;
                    bestDistance = distance;
                    bestKey = neighbor.Key;
                }
            }
        }

        return bestTile;
    }

    public override bool IsCreatureAtNavigationTarget(Creature creature)
    {
        if (!creature.IsLocomotionEnabled || Cave is null)
        {
            return false;
        }

        var currentTile = Cave.GetTile(creature.Location);
        return currentTile is not null &&
               currentTile.Neighbors.Any(neighbor => ReferenceEquals(neighbor.Built, this));
    }

    public override bool IsCreatureStationed(Creature creature)
    {
        return creature.IsHostedOnBuilding(this) && GetAssignedStationIndex(creature).HasValue;
    }

    public override bool TryStationCreature(Creature creature)
    {
        if (IsCreatureStationed(creature) && TryGetAssignedWorldPosition(creature, out var existingWorldPosition))
        {
            creature.HostOnBuilding(this, existingWorldPosition);
            return true;
        }

        if (!IsCreatureAtNavigationTarget(creature) ||
            !TryGetAssignedWorldPosition(creature, out var worldPosition) ||
            !(Cave?.DisableCreatureLocomotion(creature) ?? false))
        {
            return false;
        }

        creature.HostOnBuilding(this, worldPosition);
        return true;
    }

    public override bool TryRestoreCreatureLocomotion(Creature creature)
    {
        if (creature.IsLocomotionEnabled)
        {
            return true;
        }

        if (!creature.IsHostedOnBuilding(this))
        {
            return false;
        }

        if (Cave?.PlaceCreatureOnTile(creature, creature.LocomotionRestoreCell) == true)
        {
            return true;
        }

        var preferredAccessTile = GetPreferredAccessTile(creature.LocomotionRestoreCell);
        return preferredAccessTile.HasValue &&
               (Cave?.PlaceCreatureOnTile(creature, preferredAccessTile.Value) ?? false);
    }

    public override bool TryGetAssignedWorldPosition(Creature creature, out Vector2 worldPosition)
    {
        if (GetAssignedStationIndex(creature) is not { } stationIndex ||
            !TryGetLocalPixelOffsetForRotation(stationIndex, out var localPixelOffset))
        {
            worldPosition = Vector2.Zero;
            return false;
        }

        worldPosition = ResolveWorldPosition(localPixelOffset);
        return true;
    }

    public override void TargetInRadius(Creature creature)
    {
        if (creature is not Enemy enemy)
        {
            return;
        }

        if (Target is not null && (Target.Health <= 0 || !ReferenceEquals(Target.Cave, Cave)))
        {
            SetTarget(null);
        }

        if (Target is null)
        {
            SetTarget(enemy);
            return;
        }

        var center = GetCenter();
        var currentDistance = GridPoint.ManhattanDistance(center, Target.Location);
        var candidateDistance = GridPoint.ManhattanDistance(center, enemy.Location);
        if (candidateDistance < currentDistance)
        {
            SetTarget(enemy);
        }
    }

    public override void TargetNoLongerInRadius(Creature creature)
    {
        if (ReferenceEquals(Target, creature))
        {
            SetTarget(null);
            AcquireInitialTarget();
        }
    }

    public override void TrackedCreatureDied(Creature creature)
    {
        if (ReferenceEquals(Target, creature))
        {
            SetTarget(null);
            AcquireInitialTarget();
        }
    }

    public override int Tick(World.Cave cave)
    {
        if (Target is not null && (Target.Health <= 0 || !ReferenceEquals(Target.Cave, cave)))
        {
            SetTarget(null);
        }

        if (Target is null)
        {
            AcquireInitialTarget();
        }

        var shotsFired = 0;
        foreach (var creature in Assignments)
        {
            if (!_remainingReloadTicks.TryGetValue(creature, out var remainingTicks))
            {
                remainingTicks = ReloadTicks;
            }

            if (!IsCreatureStationed(creature))
            {
                _remainingReloadTicks[creature] = remainingTicks;
                continue;
            }

            if (remainingTicks > 0)
            {
                remainingTicks--;
            }

            if (remainingTicks <= 0 && Target is not null)
            {
                var launchAngleRadians = GetLaunchAngleRadians(creature, Target);
                if (creature.ShootProjectile(Target, ProjectileCatalog.Rock))
                {
                    creature.RotationRadians = launchAngleRadians;
                    remainingTicks = ReloadTicks;
                    shotsFired++;
                }
            }

            _remainingReloadTicks[creature] = remainingTicks;
        }

        return shotsFired;
    }

    private bool TryGetLocalPixelOffsetForRotation(int stationIndex, out Vector2 localPixelOffset)
    {
        var stations = GetDisplayRotationTurns() is 1 or 3
            ? RotatedStations
            : DefaultStations;
        if (stationIndex < 0 || stationIndex >= stations.Length || !stations[stationIndex].LocalPixelOffset.HasValue)
        {
            localPixelOffset = Vector2.Zero;
            return false;
        }

        localPixelOffset = stations[stationIndex].LocalPixelOffset!.Value;
        return true;
    }

    private void AcquireInitialTarget()
    {
        for (var index = 0; index < ProjectedTiles.Count; index++)
        {
            var enemy = Cave?.GetEnemyAtTileKey(ProjectedTiles[index].Key);
            if (enemy is not null)
            {
                TargetInRadius(enemy);
            }
        }
    }

    private void SetTarget(Enemy? target)
    {
        if (ReferenceEquals(Target, target))
        {
            return;
        }

        UntrackCreature(Target);
        Target = target;
        TrackCreature(Target);
    }

    private static float GetLaunchAngleRadians(Creature source, Creature target)
    {
        var delta = target.GetWorldPosition() - source.GetWorldPosition();
        return delta.LengthSquared() <= 0f
            ? MathF.PI / 2f
            : MathF.Atan2(delta.Y, delta.X) + (MathF.PI / 2f);
    }
}
