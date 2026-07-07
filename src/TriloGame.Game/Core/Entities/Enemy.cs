using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed class Enemy : Creature
{
    public Enemy(string name, GridPoint location, GameSession session)
        : base(name, location, session)
    {
        Assignment = "enemy";
        Description = "A hostile ant that tunnels toward the colony and attacks nearby trilobites, vehicles, and buildings.";
    }

    public string? EnemyTargetTileKey { get; private set; }

    public override Action? GetBehavior() => EnemyBehavior;

    public override void CleanupBeforeRemoval(object? source = null)
    {
        EnemyTargetTileKey = null;
    }

    private void EnemyBehavior()
    {
        EnqueueAction(() => EnemyStep1());
    }

    public bool EnsureEnemyState()
    {
        if (Assignment == "enemy")
        {
            return true;
        }

        EnemyTargetTileKey = null;
        var fallback = GetBehavior();
        if (fallback is not null && fallback.Method != ((Action)EnemyBehavior).Method)
        {
            fallback();
        }

        return false;
    }

    public void ClearEnemyTarget()
    {
        EnemyTargetTileKey = null;
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

    public bool IsAdjacentToTileKey(string tileKey, GridPoint? location = null)
    {
        return GridPoint.ManhattanDistance(location ?? Location, GridPoint.Parse(tileKey)) == 1;
    }

    public string? GetAdjacentHostileTileKey(GridPoint? location = null, bool includeWalls = false)
    {
        var currentTile = Cave?.GetTile((location ?? Location).ToString());
        if (currentTile is null)
        {
            return null;
        }

        string? adjacentBuildingTileKey = null;
        foreach (var neighbor in currentTile.Neighbors)
        {
            if (neighbor.Trilobites.Count > 0)
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
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTargetTileKey is not null && IsAdjacentToTileKey(EnemyTargetTileKey))
        {
            return EnemyStep2();
        }

        var adjacent = GetAdjacentHostileTileKey();
        if (adjacent is not null)
        {
            EnemyTargetTileKey = adjacent;
            return EnemyStep2();
        }

        return EnemyStep3();
    }

    public bool EnemyStep2()
    {
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTargetTileKey is null)
        {
            return EnemyStep3();
        }

        var hostile = GetHostileTargetAtTileKey(EnemyTargetTileKey);
        if (hostile is null)
        {
            ClearEnemyTarget();
            return EnemyStep3();
        }

        if (!IsAdjacentToTileKey(EnemyTargetTileKey))
        {
            return EnemyStep3();
        }

        var dealt = DealDamage(hostile);
        if (GetHostileTargetAtTileKey(EnemyTargetTileKey) is null)
        {
            ClearEnemyTarget();
        }

        return dealt > 0;
    }

    public bool EnemyStep3()
    {
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTargetTileKey is not null && GetHostileTargetAtTileKey(EnemyTargetTileKey) is null)
        {
            ClearEnemyTarget();
        }

        if (EnemyTargetTileKey is not null && GetHostileBuildingAtTileKey(EnemyTargetTileKey) is Wall && !IsAdjacentToTileKey(EnemyTargetTileKey))
        {
            ClearEnemyTarget();
        }

        var cave = Cave;
        var field = cave?.GetBfsFieldObject("colony");
        if (field is null || cave is null)
        {
            ClearEnemyTarget();
            return TryDigTowardQueen();
        }

        ClearActionQueue();
        var resolvedField = field;
        var resolvedNext = field.GetNextStep(Location, refresh: false);
        if (resolvedNext is null || (cave.GetTile(resolvedNext.Value.ToString()) is { } attemptedTile && !cave.CanCreatureTraverseTile(this, attemptedTile)))
        {
            var refreshedField = cave.GetBfsFieldObject("colony");
            refreshedField?.Rebuild();
            if (refreshedField is null)
            {
                ClearEnemyTarget();
                return false;
            }

            resolvedField = refreshedField;
            resolvedNext = refreshedField.GetNextStep(Location, refresh: false);
            if (resolvedField.GetFieldValue(Location, refresh: false) == 0)
            {
                ClearActionQueue();
                return false;
            }
        }

        if (resolvedField.GetFieldValue(Location, refresh: false) == int.MaxValue || resolvedNext is null)
        {
            var adjacentWallTileKey = GetAdjacentWallTileKey();
            if (adjacentWallTileKey is not null)
            {
                EnemyTargetTileKey = adjacentWallTileKey;
                ClearActionQueue();
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
                        ArmBfsTraversal(wallField, sharedFieldName: "wall");
                        PathPreview.Add(wallNext.Value);
                        return EnemyStepMove(wallNext.Value, allowWallRetarget: true);
                    }
                }
            }

            ClearEnemyTarget();
            return TryDigTowardQueen();
        }

        ArmBfsTraversal(resolvedField, sharedFieldName: "colony");
        PathPreview.Add(resolvedNext.Value);
        return EnemyStepMove(resolvedNext.Value);
    }

    public bool EnemyStepMove(GridPoint nextLocation, bool allowWallRetarget = false)
    {
        if (!EnsureEnemyState())
        {
            return false;
        }

        if (EnemyTargetTileKey is not null && GetHostileTargetAtTileKey(EnemyTargetTileKey) is null)
        {
            ClearEnemyTarget();
            ClearActionQueue();
            return EnemyStep3();
        }

        var adjacent = GetAdjacentHostileTileKey();
        if (adjacent is not null)
        {
            EnemyTargetTileKey = adjacent;
            ClearActionQueue();
            return EnemyStep2();
        }

        ClearBfsTraversal();
        var moved = Cave?.MoveCreature(this, nextLocation) ?? false;
        if (!moved)
        {
            ClearActionQueue();
            return EnemyStep3();
        }

        if (PathPreview.Count > 0)
        {
            PathPreview.RemoveAt(0);
        }

        if (EnemyTargetTileKey is not null && IsAdjacentToTileKey(EnemyTargetTileKey))
        {
            ClearActionQueue();
            return EnemyStep2();
        }

        var nextAdjacent = GetAdjacentHostileTileKey();
        if (nextAdjacent is not null)
        {
            EnemyTargetTileKey = nextAdjacent;
            ClearActionQueue();
            return EnemyStep2();
        }

        if (allowWallRetarget)
        {
            var adjacentWallTileKey = GetAdjacentWallTileKey();
            if (adjacentWallTileKey is not null)
            {
                EnemyTargetTileKey = adjacentWallTileKey;
                ClearActionQueue();
                return EnemyStep2();
            }
        }

        return moved;
    }

    private bool TryDigTowardQueen()
    {
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

        var result = Session.MineTile(cave, bestWall.Key, Location.ToString(), "enemy");
        return result.HitApplied;
    }
}
