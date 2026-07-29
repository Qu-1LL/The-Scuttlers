using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Entities;

public sealed class CreatureNavigationTests
{
    [Fact]
    public void NavigateToBuilding_FollowsSmoothedContinuousRouteToBuilding()
    {
        var (session, cave, _, post, _) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(0);
        var spawnTile = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && post.TileArray.All(postTile => postTile.Coordinates != tile.Coordinates))
            .OrderByDescending(tile => GridPoint.ManhattanDistance(tile.Coordinates, post.TileArray[0].Coordinates))
            .First();
        var trilobite = new Trilobite("Navigator", spawnTile.Coordinates, session)
        {
            Assignment = "miner"
        };

        Assert.True(cave.Spawn(trilobite, spawnTile));

        var expectedPath = trilobite.BuildNavigationPathToBuilding(post);

        Assert.NotNull(expectedPath);
        Assert.True(expectedPath.Count > 1);

        trilobite.ClearTaskQueue();

        Assert.True(trilobite.NavigateToBuilding(post));
        Assert.NotEmpty(trilobite.DesiredRoute);

        var guard = expectedPath.Count * 4;
        while (trilobite.HasActiveMovement && guard-- > 0)
        {
            cave.AdvanceCreatureMovement();
        }

        Assert.True(trilobite.IsOnPassableBuildingTile(post));
        Assert.False(trilobite.HasActiveMovement);
    }

    [Fact]
    public void NavigateToBuilding_UsesAsyncFieldZeroTileForSmoothRoute()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(1, 1));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);

        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(18, 6)));
        var snapshot = WaitForSnapshot(maintenance, storage);
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Navigator", "builder");

        var path = trilobite.BuildNavigationPathToBuilding(storage);

        Assert.NotNull(path);
        Assert.True(path!.Count > 1);
        var targetTile = cave.GetTile(path[^1]) ?? throw new InvalidOperationException("Expected a navigation target tile.");
        Assert.Equal(0, snapshot.GetDistance(targetTile.Id));
        Assert.DoesNotContain(targetTile, storage.TileArray);

        Assert.True(trilobite.NavigateToBuilding(storage));

        var guard = path.Count * 6;
        while (trilobite.HasActiveMovement && guard-- > 0)
        {
            cave.AdvanceCreatureMovement();
        }

        var finalTile = cave.GetTile(trilobite.Location) ?? throw new InvalidOperationException("Expected a final navigation tile.");
        Assert.True(guard > 0);
        Assert.Equal(0, storage.PublishedNavigationField!.GetDistance(finalTile.Id));
    }

    private static TriloGame.Game.Core.Pathfinding.BuildingNavigationFieldSnapshot WaitForSnapshot(
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
}
