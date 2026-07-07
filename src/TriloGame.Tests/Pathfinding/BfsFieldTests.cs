using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
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

        Assert.NotNull(directPath);
        Assert.NotNull(fieldPath);
        Assert.NotEmpty(directPath);
        Assert.Equal(trilobite.Location, directPath[0]);
        Assert.Equal(destinationPoint, directPath[^1]);
        Assert.Equal(fieldPath!.Count, directPath.Count);
        for (var index = 1; index < directPath.Count; index++)
        {
            Assert.Equal(1, GridPoint.ManhattanDistance(directPath[index - 1], directPath[index]));
        }
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
        Assert.NotEqual(startingLocation, trilobite.Location);
        Assert.Equal(expectedStep!.Value, trilobite.Location);
        Assert.NotEqual(blockedTile.Key, trilobite.Location.ToString());
        Assert.Equal(1, GridPoint.ManhattanDistance(startingLocation, trilobite.Location));
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
