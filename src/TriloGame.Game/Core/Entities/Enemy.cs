using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed class Enemy : Creature
{
    public Enemy(string name, GridPoint location, GameSession session)
        : base(name, location, session)
    {
        Assignment = "enemy";
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

    public Core.Buildings.Building? GetHostileBuildingAtTileKey(string? tileKey)
    {
        if (Cave is null || string.IsNullOrWhiteSpace(tileKey))
        {
            return null;
        }

        var tile = Cave.GetTile(tileKey);
        var building = tile?.Built;
        return building is not null && building.Cave == Cave && building.Health > 0 ? building : null;
    }

    public object? GetHostileTargetAtTileKey(string? tileKey)
    {
        return (object?)GetHostileAtTileKey(tileKey) ?? GetHostileBuildingAtTileKey(tileKey);
    }

    public bool IsAdjacentToTileKey(string tileKey, GridPoint? location = null)
    {
        return GridPoint.ManhattanDistance(location ?? Location, GridPoint.Parse(tileKey)) == 1;
    }

    public string? GetAdjacentHostileTileKey(GridPoint? location = null)
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

            if (adjacentBuildingTileKey is null && GetHostileBuildingAtTileKey(neighbor.Key) is not null)
            {
                adjacentBuildingTileKey = neighbor.Key;
            }
        }

        return adjacentBuildingTileKey;
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

        var nextLocation = Cave?.GetBfsFieldNextStep("colony", Location);
        if (nextLocation is null)
        {
            ClearEnemyTarget();
            return false;
        }

        ClearActionQueue();
        PathPreview.Add(nextLocation.Value);
        return EnemyStepMove(nextLocation.Value);
    }

    public bool EnemyStepMove(GridPoint nextLocation)
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

        var moved = PerformMove(nextLocation);
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

        return moved;
    }
}
