using TriloGame.Game.Core.Buildings;
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
                var cellFailures = required
                    ? EvaluateRequiredPlacementCell(cellLocation, requireReachableTiles)
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
                var allowTrilobiteOccupants = replacementBuilding.OpenMap[y][x] >= 1;
                var cellFailures = required
                    ? EvaluateRequiredPlacementCell(
                        cellLocation,
                        requireReachableTiles: false,
                        ignoredBuilding: existingBuilding,
                        allowTrilobiteOccupants: allowTrilobiteOccupants,
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
        bool allowTrilobiteOccupants = false,
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

        if (tile.EnemyOccupant is not null)
        {
            failures |= BuildPlacementFailureReason.EnemyOccupant;
        }

        if (tile.Trilobites.Count > 0 && !allowTrilobiteOccupants)
        {
            failures |= BuildPlacementFailureReason.TrilobiteOccupant;
        }

        if (requireReachableTiles && !IsTileReachable(tile))
        {
            failures |= BuildPlacementFailureReason.UnreachableTile;
        }

        return failures;
    }

    private BuildPlacementFailureReason EvaluateOptionalPlacementCell(GridPoint location)
    {
        return GetTile(location) is null
            ? BuildPlacementFailureReason.MissingTile
            : BuildPlacementFailureReason.None;
    }
}
