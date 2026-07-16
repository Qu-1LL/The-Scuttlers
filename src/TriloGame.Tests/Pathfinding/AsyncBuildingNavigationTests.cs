using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class AsyncBuildingNavigationTests
{
    [Fact]
    public void TopologyDelta_CopiesOnlyDirtyTilesAndTheirNeighbors()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        var dirtyTile = cave.GetTile(new GridPoint(12, 6))!;

        var delta = cave.CreateBuildingNavigationTopologyDelta([dirtyTile.Key]);

        Assert.False(delta.HasBuildingChanges);
        Assert.Contains(dirtyTile.Id, delta.DirtyTileIds);
        Assert.True(delta.TileUpdates.Count < cave.GetTiles().Count);
    }

    [Fact]
    public void NavigableBuildingPublishesSnapshot_AndWallDoesNotMaintainOne()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);

        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(18, 6));
        var wall = TestWorldFactory.BuildWall(cave, session, new GridPoint(12, 1));

        var snapshot = WaitForSnapshot(maintenance, barracks);

        Assert.Equal(barracks.RuntimeId, snapshot.BuildingRuntimeId);
        Assert.Equal(BuildingNavigationMaintenanceMode.Asynchronous, barracks.NavigationFieldMaintenanceMode);
        Assert.Null(wall.PublishedNavigationField);
        Assert.NotEqual(int.MaxValue, cave.GetBuildingBfsFieldValue(barracks, new GridPoint(4, 6)));
    }

    [Fact]
    public void TopologyRepairUsesIncrementalPasses_WithoutFallbackRebuild()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(18, 6));
        var initialSnapshot = WaitForSnapshot(maintenance, barracks);

        var fallbackCount = maintenance.FallbackRebuildCount;
        var dirtyTile = cave.GetTile(new GridPoint(12, 6))!;
        dirtyTile.SetBase("wall");
        dirtyTile.CreatureCanFit = false;
        cave.NotifyBuildingNavigationTopologyChanged([dirtyTile.Key]);
        cave.RefreshReachableTiles();
        WaitForNextGeneration(maintenance, barracks, initialSnapshot.Generation);

        dirtyTile.SetBase("empty");
        dirtyTile.CreatureCanFit = true;
        cave.NotifyBuildingNavigationTopologyChanged([dirtyTile.Key]);
        cave.RefreshReachableTiles();
        WaitForNextGeneration(maintenance, barracks, barracks.PublishedNavigationField!.Generation);

        Assert.Equal(fallbackCount, maintenance.FallbackRebuildCount);
        Assert.True(maintenance.IncrementalRepairsProcessed >= 2);
    }

    [Fact]
    public void ScaffoldReplacementCarriesForwardFieldAndRepairsChangedSeeds()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 16, new GridPoint(1, 1));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);

        var target = new Turret(session);
        var scaffold = new Scaffolding(session, target);
        var location = new GridPoint(18, 6);
        Assert.True(cave.Build(scaffold, location));
        WaitForSnapshot(maintenance, scaffold);

        var inheritedBefore = maintenance.InheritedScaffoldFieldCount;
        Assert.True(cave.ReplaceBuilding(scaffold, target, location));
        WaitForSnapshot(maintenance, target);

        Assert.True(maintenance.InheritedScaffoldFieldCount > inheritedBefore);
        Assert.Equal(0, maintenance.FallbackRebuildCount);
        Assert.Equal(BuildingNavigationSeedMode.AdjacentExteriorPassableTiles, target.NavigationSeedMode);
    }

    [Fact]
    public void StaleResultsFromDetachedSessionAreDiscarded()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(14, 5));
        maintenance.Detach();

        Assert.Null(barracks.PublishedNavigationField);
        Assert.False(maintenance.IsAttached);
    }

    private static BuildingNavigationFieldSnapshot WaitForSnapshot(
        BuildingBfsFieldMaintenanceSystem maintenance,
        Building building)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            maintenance.PumpCompleted();
            if (building.PublishedNavigationField is { } snapshot)
            {
                return snapshot;
            }

            maintenance.WaitForPublishedResult(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Timed out waiting for {building.Name} navigation snapshot.");
    }

    private static void WaitForNextGeneration(
        BuildingBfsFieldMaintenanceSystem maintenance,
        Building building,
        long previousGeneration)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            maintenance.PumpCompleted();
            if (building.PublishedNavigationField is { } snapshot && snapshot.Generation > previousGeneration)
            {
                return;
            }

            maintenance.WaitForPublishedResult(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Timed out waiting for {building.Name} navigation generation {previousGeneration + 1}.");
    }
}
