using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class RanchAssignmentTests
{
    [Fact]
    public void RanchFarmerAssignmentPriorityIsHigherThanAlgaeFarm()
    {
        var session = new GameSession();
        var ranch = new Ranch(session);
        var farm = new AlgaeFarm(session);

        Assert.True(ranch.FarmerAssignmentPriority > farm.FarmerAssignmentPriority);
        Assert.Equal(1, ranch.AssignmentCapacity);
    }

    [Fact]
    public void RanchFormationAssignsAvailableFarmerBeforeAlgaeFarmWork()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(14, 6));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");

        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, garage.Ranch);
        Assert.Same(ranch, farmer.GetAssignedRanch());
        Assert.Contains(farmer, ranch.Assignments);
        Assert.DoesNotContain(farmer, farm.Assignments);
    }

    [Fact]
    public void AssignedRanchFarmerWaitsInGarageThenSpawnsStationedPlow()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(farmer.FarmerRanchStep2());

        Assert.False(farmer.IsVisible);
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.Same(garage, farmer.HostedBuilding);
        Assert.Equal(garage.GetCenter(), farmer.Location);
        Assert.Empty(cave.GetVehicles());

        for (var tick = 0; tick < 19; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Empty(cave.GetVehicles());

        TickRunner.RunTick(session);

        var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.Same(plow, ranch.Plow);
        Assert.True(plow.IsCreatureStationed(farmer));
        Assert.True(farmer.IsVisible);
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.Null(farmer.HostedBuilding);
        Assert.Same(plow, farmer.HostedVehicle);
    }

    [Fact]
    public void AssignedRanchFarmer_DelaysPlowSpawnByTwentyTicksWhileDangerIsActive()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(farmer.FarmerRanchStep2());

        session.Danger = true;
        for (var tick = 0; tick < 20; tick++)
        {
            ranch.Tick(cave);
        }

        Assert.Empty(cave.GetVehicles());

        session.Danger = false;
        for (var tick = 0; tick < 19; tick++)
        {
            ranch.Tick(cave);
        }

        Assert.Empty(cave.GetVehicles());

        ranch.Tick(cave);

        var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.Same(plow, ranch.Plow);
        Assert.True(plow.IsCreatureStationed(farmer));
    }
}
