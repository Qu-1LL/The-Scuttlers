using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Movement;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class BfsFieldTests
{
    [Fact]
    public void PointField_BuildsAContiguousPathAcrossReachableTiles()
    {
        var (_, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var destination = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString())
            .OrderByDescending(tile => GridPoint.ManhattanDistance(GridPoint.Parse(tile.Key), trilobite.Location))
            .First();
        var destinationPoint = GridPoint.Parse(destination.Key);

        var field = cave.BuildPointBfsField(destinationPoint);
        var path = cave.BuildPathFromField(field, trilobite.Location);

        Assert.NotNull(field);
        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.Equal(trilobite.Location, path[0]);
        Assert.Equal(destinationPoint, path[^1]);
        for (var index = 1; index < path.Count; index++)
        {
            Assert.Equal(1, GridPoint.ManhattanDistance(path[index - 1], path[index]));
        }
    }

    [Fact]
    public void DirectPointPath_BuildsAShortestContiguousPathAcrossReachableTiles()
    {
        var (_, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var destination = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString())
            .OrderByDescending(tile => GridPoint.ManhattanDistance(GridPoint.Parse(tile.Key), trilobite.Location))
            .First();
        var destinationPoint = GridPoint.Parse(destination.Key);

        var directPath = cave.BuildDirectPathToPoint(trilobite.Location, destinationPoint);
        var fieldPath = cave.BuildPathFromField(cave.BuildPointBfsField(destinationPoint), trilobite.Location);
        var cachedPath = cave.BuildPointPath(trilobite.Location, destinationPoint);

        Assert.NotNull(directPath);
        Assert.NotNull(fieldPath);
        Assert.NotNull(cachedPath);
        Assert.NotEmpty(directPath);
        Assert.Equal(trilobite.Location, directPath[0]);
        Assert.Equal(destinationPoint, directPath[^1]);
        Assert.Equal(fieldPath!.Count, directPath.Count);
        Assert.Equal(fieldPath.Count, cachedPath!.Count);
        for (var index = 1; index < directPath.Count; index++)
        {
            Assert.Equal(1, GridPoint.ManhattanDistance(directPath[index - 1], directPath[index]));
        }
    }

    [Fact]
    public void CachedPointPath_ReusesSingleFieldForSameDestinationBeyondPerTickBudget()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(70, 12, new GridPoint(1, 1));
        var destination = new GridPoint(64, 6);
        var paths = new List<List<GridPoint>?>(Cave.MaximumPointRouteBuildsPerTick + 8);

        NavigationInstrumentation.BeginTick();
        for (var index = 0; index < Cave.MaximumPointRouteBuildsPerTick + 8; index++)
        {
            var start = new GridPoint(4 + index, 6);
            var path = cave.BuildPointPath(start, destination, out var deferred);
            Assert.False(deferred);
            paths.Add(path);
        }

        var navigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, navigation.BuildPointBfsFieldCallCount);
        Assert.Equal(1, cave.PointRouteFieldCacheCount);
        Assert.All(paths, path =>
        {
            Assert.NotNull(path);
            Assert.Equal(destination, path![^1]);
        });
    }

    [Fact]
    public void FormationMove_UsesOneSharedPointFieldForDistinctExactSlots()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(70, 20, new GridPoint(1, 1));
        var creatures = new List<Trilobite>(24);
        for (var index = 0; index < 24; index++)
        {
            var start = new GridPoint(4 + (index % 8), 4 + (index / 8));
            creatures.Add(TestWorldFactory.SpawnTrilobite(cave, session, start, $"Formation {index}"));
        }

        var center = WorldPoint.FromGridPoint(new GridPoint(58, 10)) + new WorldVector(123, 321);
        var centerCell = center.ToGridPoint();
        var assignments = CreatureFormationPlanner.Build(cave, creatures, center);

        NavigationInstrumentation.BeginTick();
        foreach (var assignment in assignments)
        {
            Assert.True(assignment.Creature.NavigateToViaSharedRoute(
                assignment.Destination,
                centerCell,
                clearExisting: true));
            Assert.NotEmpty(assignment.Creature.DesiredRoute);
            Assert.Equal(RouteContinuationKind.PointDestination, assignment.Creature.ActiveRouteContinuationKind);
        }

        var navigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, navigation.BuildPointBfsFieldCallCount);
        Assert.Equal(assignments.Count, navigation.PointPathRequestCount);
    }

    [Fact]
    public void ChunkedPointNavigation_UsesOneFieldAndQueuesOnlyInitialRouteChunks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(96, 12, new GridPoint(1, 1));
        var destination = new GridPoint(88, 6);
        var creatures = new List<Trilobite>(40);
        for (var index = 0; index < 40; index++)
        {
            creatures.Add(TestWorldFactory.SpawnTrilobite(
                cave,
                session,
                new GridPoint(4 + index, 6),
                $"Chunked {index}"));
        }

        NavigationInstrumentation.BeginTick();
        for (var index = 0; index < creatures.Count; index++)
        {
            Assert.True(creatures[index].NavigateTo(destination));
        }

        var navigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, navigation.BuildPointBfsFieldCallCount);
        Assert.Equal(creatures.Count, navigation.PointPathRequestCount);
        Assert.True(navigation.MaxPathLength <= Creature.RouteRefillChunkCells + 1);
        Assert.All(creatures, creature =>
        {
            Assert.True(creature.HasActiveMovement);
            Assert.Equal(RouteContinuationKind.PointDestination, creature.ActiveRouteContinuationKind);
        });
    }

    [Fact]
    public void CachedPointPath_InvalidatesWhenTopologyVersionChanges()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(1, 1));
        var destination = new GridPoint(20, 6);

        NavigationInstrumentation.BeginTick();
        Assert.NotNull(cave.BuildPointPath(new GridPoint(4, 6), destination));
        var initialNavigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, initialNavigation.BuildPointBfsFieldCallCount);

        var wall = new Wall(session);
        Assert.True(cave.Build(wall, new GridPoint(12, 8)));

        NavigationInstrumentation.BeginTick();
        Assert.NotNull(cave.BuildPointPath(new GridPoint(5, 6), destination));
        var rebuiltNavigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, rebuiltNavigation.BuildPointBfsFieldCallCount);
    }

    [Fact]
    public void CachedPointPath_InvalidatesWhenReachabilityVersionChanges()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(1, 1));
        var destination = new GridPoint(20, 6);

        NavigationInstrumentation.BeginTick();
        Assert.NotNull(cave.BuildPointPath(new GridPoint(4, 6), destination));
        var initialNavigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, initialNavigation.BuildPointBfsFieldCallCount);

        var blockedTile = cave.GetTile(new GridPoint(22, 10))
            ?? throw new InvalidOperationException("Expected a reachable tile to exist.");
        blockedTile.SetBase("wall");
        blockedTile.CreatureCanFit = false;
        blockedTile.ConfigureWall(1);
        var reachability = cave.RefreshReachableTiles();
        Assert.NotEmpty(reachability.ChangedKeys);

        NavigationInstrumentation.BeginTick();
        Assert.NotNull(cave.BuildPointPath(new GridPoint(5, 6), destination));
        var rebuiltNavigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(1, rebuiltNavigation.BuildPointBfsFieldCallCount);
    }

    [Fact]
    public void NavigateToBuilding_RefreshesBlockedBfsStepAndRetries()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(12, 5));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Tester", "fighter");
        var field = cave.GetBuildingBfsFieldObject(barracks);
        field.Rebuild();

        var originalStep = field.GetNextStep(trilobite.Location, refresh: false);
        Assert.NotNull(originalStep);

        var blockedTile = cave.GetTile(originalStep!.Value.ToString())!;
        blockedTile.SetBase("wall");
        blockedTile.CreatureCanFit = false;
        field.MarkDirty([blockedTile.Key], [], []);
        var expectedField = new TriloGame.Game.Core.Pathfinding.BfsField(barracks.Name, "building", cave, barracks);
        expectedField.Rebuild();
        var expectedStep = expectedField.GetNextStep(trilobite.Location, refresh: false);

        var startingLocation = trilobite.Location;
        var moved = trilobite.NavigateToBuilding(barracks);

        Assert.NotNull(expectedStep);
        Assert.True(
            moved,
            $"Expected retry step {expectedStep} from {startingLocation}, actual location {trilobite.Location}, blocked {blockedTile.Key}, blockedFit={blockedTile.CreatureFits()}, fieldUpdated={field.IsUpdated()}.");
        Assert.True(field.IsUpdated());
        var startingPosition = trilobite.Position;
        cave.AdvanceCreatureMovement();
        Assert.NotEqual(startingPosition, trilobite.Position);
        Assert.NotEqual(blockedTile.Key, trilobite.CurrentCell.ToString());
    }

    [Fact]
    public void NewBuilding_StartsWithGeneratedDirtyBfsField()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(12, 5));

        var field = cave.GetBuildingBfsFieldObject(farm);

        Assert.True(field.HasCoverage());
        Assert.False(field.IsUpdated());
        Assert.NotEqual(int.MaxValue, field.GetFieldValue(new GridPoint(4, 6), refresh: false));
    }

    [Fact]
    public void DirtyBuildingField_ValueReadKeepsExistingCoverageUntilMoveFailure()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(12, 5));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Tester", "fighter");
        var field = cave.GetBuildingBfsFieldObject(barracks);
        field.Rebuild();

        var staleStep = field.GetNextStep(trilobite.Location, refresh: false);
        Assert.NotNull(staleStep);

        var blockedTile = cave.GetTile(staleStep!.Value.ToString())!;
        blockedTile.SetBase("wall");
        blockedTile.CreatureCanFit = false;
        field.MarkDirty([blockedTile.Key], [], []);

        var staleValue = cave.GetBuildingBfsFieldValue(barracks, trilobite.Location);

        Assert.NotEqual(int.MaxValue, staleValue);
        Assert.False(field.IsUpdated());
    }

    [Fact]
    public void DirtyBuildingField_ValueReadRebuildsWhenReachableTileIsMissingFromCoverage()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(12, 5));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Tester", "fighter");
        var field = cave.GetBuildingBfsFieldObject(barracks);
        var seedTile = barracks.TileArray.First(tile => tile.CreatureFits());
        field.SetField(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [seedTile.Key] = 0
        });
        field.MarkDirty([seedTile.Key], [], []);

        var value = cave.GetBuildingBfsFieldValue(barracks, trilobite.Location);

        Assert.NotEqual(int.MaxValue, value);
        Assert.True(field.IsUpdated());
    }

    [Fact]
    public void MiningWall_AppliesImmediateLocalValueUpdateToExistingBfsFields()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(12, 5));
        var wallLocation = new GridPoint(8, 6);
        var wallTile = cave.GetTile(wallLocation.ToString())!;
        wallTile.SetBase("wall");
        wallTile.CreatureCanFit = false;
        wallTile.ConfigureWall(1);
        cave.RefreshReachableTiles();
        cave.RevealTile(wallTile);

        var field = cave.GetBuildingBfsFieldObject(barracks);
        field.Rebuild();

        Assert.Equal(int.MaxValue, field.GetFieldValue(wallLocation, refresh: false));

        var mined = session.MineTile(cave, wallLocation.ToString(), "test");
        var expectedValue = cave.GetTile(wallLocation.ToString())!.Neighbors
            .Where(neighbor => neighbor.CreatureFits())
            .Select(neighbor => field.GetFieldValue(neighbor.Coordinates, refresh: false))
            .Where(value => value != int.MaxValue)
            .Min();

        Assert.True(mined.TileDepleted);
        Assert.Equal(expectedValue + 1, field.GetFieldValue(wallLocation, refresh: false));
    }

    [Fact]
    public void ColonyField_RebalanceClearsDisconnectedRevealedPocket()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(10, 6, new GridPoint(1, 1));
        var field = cave.GetBfsFieldObject("colony")
            ?? throw new InvalidOperationException("Expected the colony BFS field to exist.");
        field.Rebuild();

        var isolatedLocation = new GridPoint(8, 3);
        Assert.NotEqual(int.MaxValue, field.GetFieldValue(isolatedLocation, refresh: false));

        var dirtyKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var y = 0; y < 6; y++)
        {
            var wallTile = cave.GetTile(new GridPoint(5, y))
                ?? throw new InvalidOperationException("Expected wall-barrier tile to exist.");
            wallTile.SetBase("wall");
            wallTile.CreatureCanFit = false;
            wallTile.ConfigureWall(1);
            dirtyKeys.Add(wallTile.Key);
        }

        var reachability = cave.RefreshReachableTiles();
        foreach (var changedKey in reachability.ChangedKeys)
        {
            dirtyKeys.Add(changedKey);
        }

        field.MarkDirty(dirtyKeys, [], []);
        field.Refresh();

        Assert.True(field.IsUpdated());
        Assert.Equal(int.MaxValue, field.GetFieldValue(isolatedLocation, refresh: false));
    }

    [Fact]
    public void ColonyField_IgnoresAntIgnoredObstacleBuildingsAsTargets()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 10, new GridPoint(1, 1));
        var ignoredBuilding = new IgnoredAntObstacleBuilding(session);

        Assert.True(cave.Build(ignoredBuilding, new GridPoint(6, 4)));

        var field = cave.GetBfsFieldObject("colony")
            ?? throw new InvalidOperationException("Expected the colony BFS field to exist.");
        field.Rebuild();

        Assert.True(field.GetFieldValue(new GridPoint(7, 4), refresh: false) > 1);
    }

    private sealed class IgnoredAntObstacleBuilding : Building
    {
        public IgnoredAntObstacleBuilding(GameSession session)
            : base("Ignored Ant Obstacle", new GridPoint(1, 1), [[0]], session, false)
        {
            IgnoredByAnts = true;
        }
    }
}
