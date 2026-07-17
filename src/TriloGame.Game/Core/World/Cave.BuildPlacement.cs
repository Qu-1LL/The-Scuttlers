using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

[Flags]
public enum BuildPlacementFailureReason
{
    None = 0,
    MissingTile = 1 << 0,
    ExistingBuilding = 1 << 1,
    BlockingSurfaceFeature = 1 << 2,
    NonEmptyBase = 1 << 3,
    ImpassableTile = 1 << 4,
    EnemyOccupant = 1 << 5,
    TrilobiteOccupant = 1 << 6,
    UnreachableTile = 1 << 7,
    BreaksReachability = 1 << 8,
    BlocksExistingBuildingAccess = 1 << 9
}

public readonly record struct BuildPlacementCell(
    GridPoint Location,
    bool Required,
    BuildPlacementFailureReason FailureReasons)
{
    public bool CanBuild => FailureReasons == BuildPlacementFailureReason.None;
}

public sealed class BuildPlacementResult
{
    public BuildPlacementResult(
        GridPoint location,
        IReadOnlyList<BuildPlacementCell> cells,
        BuildPlacementFailureReason failureReasons)
    {
        Location = location;
        Cells = cells;
        FailureReasons = failureReasons;
    }

    public GridPoint Location { get; }

    public IReadOnlyList<BuildPlacementCell> Cells { get; }

    public BuildPlacementFailureReason FailureReasons { get; }

    public bool CanBuild => FailureReasons == BuildPlacementFailureReason.None;
}

public sealed partial class Cave
{
    public BuildPlacementResult EvaluateBuildPlacement(Building building, GridPoint location, bool preserveReachability = false)
    {
        ArgumentNullException.ThrowIfNull(building);

        var hasQueen = GetQueenBuilding() is not null;
        var buildingIsQueen = building is Queen;
        var requireReachableTiles = hasQueen && !buildingIsQueen;
        var cells = new List<BuildPlacementCell>(building.Size.X * building.Size.Y);
        var failureReasons = BuildPlacementFailureReason.None;

        for (var y = 0; y < building.Size.Y; y++)
        {
            for (var x = 0; x < building.Size.X; x++)
            {
                var cellLocation = new GridPoint(location.X + x, location.Y + y);
                var required = building.OpenMap[y][x] <= 1;
                var allowCreatureOccupants = building.OpenMap[y][x] >= 1;
                var cellFailures = required
                    ? EvaluateRequiredPlacementCell(
                        cellLocation,
                        requireReachableTiles,
                        allowCreatureOccupants: allowCreatureOccupants)
                    : EvaluateOptionalPlacementCell(cellLocation);

                failureReasons |= cellFailures;
                cells.Add(new BuildPlacementCell(cellLocation, required, cellFailures));
            }
        }

        if (failureReasons == BuildPlacementFailureReason.None &&
            preserveReachability &&
            requireReachableTiles &&
            !SimulatedBuildPreservesReachability(building, location))
        {
            failureReasons |= BuildPlacementFailureReason.BreaksReachability;
        }

        if (failureReasons == BuildPlacementFailureReason.None &&
            preserveReachability &&
            requireReachableTiles &&
            !ShouldSkipSimulatedBuildingAccessCheck(building) &&
            !SimulatedBuildPreservesBuildingAccess(building, location))
        {
            failureReasons |= BuildPlacementFailureReason.BlocksExistingBuildingAccess;
        }

        return new BuildPlacementResult(location, cells, failureReasons);
    }

    public BuildPlacementResult EvaluateBuildReplacement(Building existingBuilding, Building replacementBuilding, GridPoint location)
    {
        ArgumentNullException.ThrowIfNull(existingBuilding);
        ArgumentNullException.ThrowIfNull(replacementBuilding);

        var cells = new List<BuildPlacementCell>(replacementBuilding.Size.X * replacementBuilding.Size.Y);
        var failureReasons = BuildPlacementFailureReason.None;

        if (!Buildings.Contains(existingBuilding) || existingBuilding.Location is null)
        {
            failureReasons |= BuildPlacementFailureReason.ExistingBuilding;
        }

        for (var y = 0; y < replacementBuilding.Size.Y; y++)
        {
            for (var x = 0; x < replacementBuilding.Size.X; x++)
            {
                var cellLocation = new GridPoint(location.X + x, location.Y + y);
                var required = replacementBuilding.OpenMap[y][x] <= 1;
                var allowCreatureOccupants = replacementBuilding.OpenMap[y][x] >= 1;
                var cellFailures = required
                    ? EvaluateRequiredPlacementCell(
                        cellLocation,
                        requireReachableTiles: false,
                        ignoredBuilding: existingBuilding,
                        allowCreatureOccupants: allowCreatureOccupants,
                        allowImpassableIgnoredBuildingTile: true)
                    : EvaluateOptionalPlacementCell(cellLocation);

                failureReasons |= cellFailures;
                cells.Add(new BuildPlacementCell(cellLocation, required, cellFailures));
            }
        }

        return new BuildPlacementResult(location, cells, failureReasons);
    }

    private BuildPlacementFailureReason EvaluateRequiredPlacementCell(
        GridPoint location,
        bool requireReachableTiles,
        Building? ignoredBuilding = null,
        bool allowCreatureOccupants = false,
        bool allowImpassableIgnoredBuildingTile = false)
    {
        var tile = GetTile(location);
        if (tile is null)
        {
            return BuildPlacementFailureReason.MissingTile;
        }

        var failures = BuildPlacementFailureReason.None;
        if (tile.Built is not null && !ReferenceEquals(tile.Built, ignoredBuilding))
        {
            failures |= BuildPlacementFailureReason.ExistingBuilding;
        }

        if (HasBlockingSurfaceFeature(tile))
        {
            failures |= BuildPlacementFailureReason.BlockingSurfaceFeature;
        }

        if (!string.Equals(tile.Base, "empty", StringComparison.Ordinal))
        {
            failures |= BuildPlacementFailureReason.NonEmptyBase;
        }

        if (!tile.CreatureFits() &&
            !(allowImpassableIgnoredBuildingTile && ReferenceEquals(tile.Built, ignoredBuilding)))
        {
            failures |= BuildPlacementFailureReason.ImpassableTile;
        }

        if (!allowCreatureOccupants)
        {
            failures |= GetContinuousBodyPlacementFailures(location);
        }

        if (requireReachableTiles && !IsTileReachable(tile))
        {
            failures |= BuildPlacementFailureReason.UnreachableTile;
        }

        return failures;
    }

    public bool HasCreatureOverlappingSolidCells(Building building, GridPoint location)
    {
        for (var y = 0; y < building.Size.Y; y++)
        {
            for (var x = 0; x < building.Size.X; x++)
            {
                if (building.OpenMap[y][x] >= 1)
                {
                    continue;
                }

                if (GetContinuousBodyPlacementFailures(new GridPoint(location.X + x, location.Y + y)) !=
                    BuildPlacementFailureReason.None)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private BuildPlacementFailureReason GetContinuousBodyPlacementFailures(GridPoint cell)
    {
        var failures = BuildPlacementFailureReason.None;
        var trilobites = GetTrilobiteList();
        for (var index = 0; index < trilobites.Count; index++)
        {
            if (CircleOverlapsCell(trilobites[index], cell))
            {
                failures |= BuildPlacementFailureReason.TrilobiteOccupant;
                break;
            }
        }

        var enemies = GetEnemyList();
        for (var index = 0; index < enemies.Count; index++)
        {
            if (CircleOverlapsCell(enemies[index], cell))
            {
                failures |= BuildPlacementFailureReason.EnemyOccupant;
                break;
            }
        }

        return failures;
    }

    private static bool CircleOverlapsCell(Creature creature, GridPoint cell)
    {
        if (creature.Cave is null || creature.Health <= 0)
        {
            return false;
        }

        var centerX = cell.X * WorldUnits.UnitsPerTile;
        var centerY = cell.Y * WorldUnits.UnitsPerTile;
        var minX = centerX - WorldUnits.UnitsPerHalfTile;
        var maxX = centerX + WorldUnits.UnitsPerHalfTile;
        var minY = centerY - WorldUnits.UnitsPerHalfTile;
        var maxY = centerY + WorldUnits.UnitsPerHalfTile;
        var closestX = Math.Clamp(creature.Position.X, minX, maxX);
        var closestY = Math.Clamp(creature.Position.Y, minY, maxY);
        var dx = (long)creature.Position.X - closestX;
        var dy = (long)creature.Position.Y - closestY;
        return (dx * dx) + (dy * dy) < (long)creature.CollisionRadius * creature.CollisionRadius;
    }

    private BuildPlacementFailureReason EvaluateOptionalPlacementCell(GridPoint location)
    {
        return GetTile(location) is null
            ? BuildPlacementFailureReason.MissingTile
            : BuildPlacementFailureReason.None;
    }
}
