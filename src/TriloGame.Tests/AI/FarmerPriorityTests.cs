using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class FarmerPriorityTests
{
    [Fact]
    public void RanchAssignment_PreemptsAlgaeFarmWorkAndStartsTheRanchCycle()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(14, 0));
        var algaeFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(16, 6));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Farmer", "farmer");
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.Same(ranch, farmer.GetAssignedRanch());
        Assert.True(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.True(ranch.IsHandlingFarmer(farmer));
        Assert.Same(ranch, farmer.GetAssignedRanch());
        Assert.DoesNotContain(farmer, algaeFarm.Assignments);
        Assert.Same(ranch, garage.Ranch);
    }

    [Fact]
    public void RanchAssignment_HidesFarmerInGarageThenRunsAVisiblePlowCycle()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(14, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 9), "Farmer", "farmer");
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 8));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 8));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(farmer.RunRoleState(FarmerState.SelectFarm));
        Assert.Same(garage, farmer.HostedBuilding);
        Assert.True(farmer.DrawBelowBuildings);
        Assert.False(farmer.IsLocomotionEnabled);

        for (var tick = 0; tick < 20; tick++)
        {
            ranch.Tick(cave);
        }

        var plow = Assert.IsType<TriloGame.Game.Core.Vehicles.Plow>(ranch.Plow);
        Assert.Same(cave, plow.Cave);
        Assert.Same(plow, farmer.HostedVehicle);
        Assert.Equal(10, plow.RouteCells.Count);

        var sawTwoTileRowChange = false;
        var safety = 128;
        while (plow.RouteCells.Count > 0 && safety-- > 0)
        {
            var previousLocation = plow.Location!.Value;
            Assert.True((bool)farmer.Move()!);
            var currentLocation = plow.Location!.Value;
            sawTwoTileRowChange |= currentLocation.X == previousLocation.X &&
                                    System.Math.Abs(currentLocation.Y - previousLocation.Y) == 2;
        }

        Assert.True(safety > 0);
        Assert.True(sawTwoTileRowChange);
        Assert.Equal(new GridPoint(4, 6), plow.Location);
        Assert.Equal(1, ranch.Tick(cave));
        Assert.Null(plow.Cave);
        Assert.Same(garage, farmer.HostedBuilding);
        Assert.True(farmer.DrawBelowBuildings);
    }

    [Fact]
    public void StoredAlgaeDelivery_IsUsedOnlyWhenNoRanchOrAlgaeFarmWorkExists()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(8, 6)));
        Assert.Equal(5, storage.Deposit(ResourceName.Algae, 5));
        var transferZone = Assert.Single(storage.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.ResourceTransfer);
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            transferZone.SlotPositions[0].ToGridPoint(),
            "Farmer",
            "farmer");

        Assert.True(farmer.TryReserveInteractionZone(transferZone));
        Assert.True(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.Equal(5, farmer.Inventory.GetAmount(ResourceName.Algae));
        Assert.Equal(0, storage.GetStoredAmount(ResourceName.Algae));
        Assert.Equal(FarmerState.FeedQueen, farmer.FarmerState);
    }

    [Fact]
    public void AlgaeFarmWork_PreemptsStoredAlgaeDelivery()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 14, new GridPoint(14, 0));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(6, 6)));
        var algaeFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(16, 6));
        Assert.Equal(5, storage.Deposit(ResourceName.Algae, 5));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(14, 9), "Farmer", "farmer");

        Assert.True(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.Same(algaeFarm, farmer.GetAssignedAlgaeFarm());
        Assert.Equal(5, storage.GetStoredAmount(ResourceName.Algae));
    }
}
