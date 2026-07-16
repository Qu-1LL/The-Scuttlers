using System.Threading;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Pathfinding;

public sealed class MiningPostNavigationCacheTests
{
    [Fact]
    public void NavigateToBuilding_UsesTheHeldBuildingFieldWithoutQueuedPreview()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Tester", "miner");

        var startingLocation = trilobite.Location;
        Assert.True(trilobite.NavigateToBuilding(post));

        Assert.NotEqual(startingLocation, trilobite.Location);
        Assert.Empty(trilobite.GetQueuedPathPreview());
    }

    [Fact]
    public void BuildingFieldPending_UsesManhattanDistanceUntilWorkerPublishes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Update(session);

        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(18, 6)));

        var probe = new GridPoint(4, 10);
        var expectedFallback = GridPoint.ManhattanDistance(probe, storage.Location!.Value);
        Assert.True(storage.BfsField.IsMaintenancePending);
        Assert.Equal(expectedFallback, cave.GetBuildingBfsFieldValue(storage, probe));
        Assert.False(cave.IsBuildingBfsFieldCurrent(storage));

        maintenance.Update(session);
        session.TickCount++;
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                maintenance.Update(session);
                return cave.IsBuildingBfsFieldCurrent(storage);
            },
            TimeSpan.FromSeconds(2)));

        Assert.NotEqual(int.MaxValue, cave.GetBuildingBfsFieldValue(storage, probe));
        Assert.NotNull(cave.GetBuildingBfsFieldNextStep(storage, probe));
    }

    [Fact]
    public void ExistingBuildingFieldPending_UsesLastPublishedFieldUntilWorkerPublishes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(18, 6));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 10), "Tester", "miner");
        var probe = trilobite.Location;
        var expectedValue = cave.GetBuildingBfsFieldValue(post, probe);
        var expectedNext = cave.GetBuildingBfsFieldNextStep(post, probe);
        var expectedPath = trilobite.BuildNavigationPathToBuilding(post);

        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Update(session);

        Assert.True(cave.Build(new Wall(session), new GridPoint(2, 2)));

        Assert.True(post.BfsField!.IsMaintenancePending);
        Assert.False(cave.IsBuildingBfsFieldCurrent(post));
        Assert.Equal(expectedValue, cave.GetBuildingBfsFieldValue(post, probe));
        Assert.Equal(expectedNext, cave.GetBuildingBfsFieldNextStep(post, probe));

        var pendingPath = trilobite.BuildNavigationPathToBuilding(post);
        Assert.NotNull(expectedPath);
        Assert.NotNull(pendingPath);
        Assert.Equal(expectedPath.Count, pendingPath.Count);
    }

    [Fact]
    public void ClosestMiningPostSelection_UsesPerBuildingFieldDistances()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(27, 12, new GridPoint(12, 0));
        var leftPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var rightPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(20, 6));
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Builder", "builder");

        Assert.Same(leftPost, cave.GetNearestMiningPost(builder.Location));
        leftPost.Deposit(TriloGame.Game.Core.Economy.ResourceName.Sandstone, leftPost.Capacity);

        Assert.Same(rightPost, builder.SelectMiningPostForInventoryDeposit());
    }

    [Fact]
    public void TopologyChange_UpdatesCriticalFieldsImmediatelyAndQueuesOtherBuildings()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(12, 0));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(20, 6));
        var queenField = cave.GetBuildingBfsFieldObject(queen);
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Update(session);

        Assert.True(cave.Build(new Storage(session), new GridPoint(14, 7)));

        Assert.True(cave.IsBuildingBfsFieldCurrent(queen));
        Assert.False(queenField!.IsMaintenancePending);
        Assert.True(post.BfsField.IsMaintenancePending);

        maintenance.Update(session);
        session.TickCount++;
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                maintenance.Update(session);
                return cave.IsBuildingBfsFieldCurrent(post);
            },
            TimeSpan.FromSeconds(2)));

        var expected = new TriloGame.Game.Core.Pathfinding.BfsField(post.Name, "building", cave, post);
        expected.Rebuild();
        foreach (var tile in cave.GetReachableTiles())
        {
            Assert.Equal(
                expected.GetFieldValue(tile.Coordinates, refresh: false),
                post.BfsField.GetFieldValue(tile.Coordinates, refresh: false));
        }
    }
}
