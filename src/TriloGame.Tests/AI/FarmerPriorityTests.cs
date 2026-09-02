using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
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
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(storage),
            "Farmer",
            "farmer");

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

    [Fact]
    public void StoredMealDelivery_PreemptsStoredAlgaeDelivery()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var algaeStorage = new Storage(session);
        Assert.True(cave.Build(algaeStorage, new GridPoint(6, 6)));
        Assert.Equal(5, algaeStorage.Deposit(ResourceName.Algae, 5));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(14, 6)));
        Assert.Equal(5, mill.DepositInput(ResourceName.Algae, 5));
        for (var process = 1; process <= 5; process++)
        {
            session.TickCount = process * mill.ProcessingIntervalTicks;
            Assert.Equal(1, mill.Tick(cave));
        }
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(mill),
            "Farmer",
            "farmer");

        Assert.True(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.Equal(5, farmer.Inventory.GetAmount(ResourceName.AlgaeMeal));
        Assert.Equal(0, mill.GetOutputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(5, algaeStorage.GetStoredAmount(ResourceName.Algae));
        Assert.Equal(FarmerState.FeedQueen, farmer.FarmerState);
    }

    [Fact]
    public void StoredMealDelivery_WaitsUntilTheGrindingMillHasAFullCarryLoad()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(8, 6)));
        Assert.Equal(4, mill.DepositInput(ResourceName.Algae, 4));
        for (var batch = 1; batch <= 4; batch++)
        {
            session.TickCount = batch * mill.ProcessingIntervalTicks;
            Assert.Equal(1, mill.Tick(cave));
        }

        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(mill),
            "Farmer",
            "farmer");

        Assert.False(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.Equal(FarmerState.WaitForFarm, farmer.FarmerState);
        Assert.Equal(4, mill.GetOutputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(0, mill.GetOutputCollectorCount(ResourceName.AlgaeMeal));
        Assert.Equal(0, farmer.Inventory.GetAmount(ResourceName.AlgaeMeal));
    }

    [Fact]
    public void FarmerWithAlgae_DepositsIntoReachableGrindingMillBeforeFeedingQueen()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(8, 6)));
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(mill),
            "Farmer",
            "farmer");
        Assert.Equal(5, farmer.AddToInventory(ResourceName.Algae, 5));

        Assert.True(farmer.RunRoleState(FarmerState.MoveToQueen));

        Assert.Equal(0, farmer.Inventory.GetAmount(ResourceName.Algae));
        Assert.Equal(5, mill.GetInputAmount(ResourceName.Algae));
        Assert.Equal(0, queen.NutritionCount);
    }

    [Fact]
    public void FarmerWithAlgae_DepositsIntoLeastFilledCompatibleProcessingBuilding()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 16, new GridPoint(16, 0));
        var firstMill = new GrindingMill(session);
        var bakery = new Bakery(session);
        var secondMill = new GrindingMill(session);
        Assert.True(cave.Build(firstMill, new GridPoint(4, 7)));
        Assert.True(cave.Build(bakery, new GridPoint(14, 7)));
        Assert.True(cave.Build(secondMill, new GridPoint(24, 7)));
        Assert.Equal(150, firstMill.DepositInput(ResourceName.Algae, 150));
        Assert.Equal(100, bakery.DepositInput(ResourceName.Algae, 100));
        Assert.Equal(150, secondMill.DepositInput(ResourceName.Algae, 150));
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(bakery),
            "Farmer",
            "farmer");
        Assert.Equal(5, farmer.AddToInventory(ResourceName.Algae, 5));

        Assert.True(farmer.RunRoleState(FarmerState.MoveToQueen));

        Assert.Equal(150, firstMill.GetInputAmount(ResourceName.Algae));
        Assert.Equal(105, bakery.GetInputAmount(ResourceName.Algae));
        Assert.Equal(150, secondMill.GetInputAmount(ResourceName.Algae));
        Assert.Equal(0, farmer.Inventory.GetAmount(ResourceName.Algae));
        Assert.Equal(0, queen.NutritionCount);
    }

    [Fact]
    public void FarmerWithAlgaeMeal_DepositsIntoBakeryBeforeFeedingQueen()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var bakery = new Bakery(session);
        Assert.True(cave.Build(bakery, new GridPoint(8, 6)));
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(bakery),
            "Farmer",
            "farmer");
        Assert.Equal(5, farmer.AddToInventory(ResourceName.AlgaeMeal, 5));

        Assert.True(farmer.RunRoleState(FarmerState.MoveToQueen));

        Assert.Equal(5, bakery.GetInputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(0, farmer.Inventory.GetAmount(ResourceName.AlgaeMeal));
        Assert.Equal(0, queen.NutritionCount);
    }

    [Fact]
    public void StoredPieDelivery_CollectsBakeryOutputBeforeLowerNutritionFood()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var bakery = new Bakery(session);
        Assert.True(cave.Build(bakery, new GridPoint(8, 6)));
        Assert.Equal(5, bakery.DepositInput(ResourceName.Algae, 5));
        Assert.Equal(5, bakery.DepositInput(ResourceName.AlgaeMeal, 5));
        for (var batch = 1; batch <= 5; batch++)
        {
            session.TickCount = batch * bakery.ProcessingIntervalTicks;
            Assert.Equal(1, bakery.Tick(cave));
        }

        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(bakery),
            "Farmer",
            "farmer");

        Assert.True(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.Equal(5, farmer.Inventory.GetAmount(ResourceName.AlgaePie));
        Assert.Equal(0, bakery.GetOutputAmount(ResourceName.AlgaePie));
        Assert.Equal(FarmerState.FeedQueen, farmer.FarmerState);
    }

    [Fact]
    public void StoredAlgaeSearch_DoesNotWithdrawGrindingMillInput()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(12, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(8, 6)));
        Assert.Equal(5, mill.DepositInput(ResourceName.Algae, 5));
        var farmer = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            GetInteractionLocation(mill),
            "Farmer",
            "farmer");

        Assert.False(farmer.RunRoleState(FarmerState.SelectFarm));

        Assert.Equal(0, farmer.Inventory.GetAmount(ResourceName.Algae));
        Assert.Equal(5, mill.GetInputAmount(ResourceName.Algae));
        Assert.Equal(FarmerState.WaitForFarm, farmer.FarmerState);
    }

    private static GridPoint GetInteractionLocation(Building building)
    {
        foreach (var tile in building.TileArray)
        {
            if (building.IsInteractionTile(tile))
            {
                return tile.Coordinates;
            }

            foreach (var neighbor in tile.Neighbors)
            {
                if (building.IsInteractionTile(neighbor))
                {
                    return neighbor.Coordinates;
                }
            }
        }

        throw new InvalidOperationException($"No interaction tile exists for {building.Name}.");
    }
}
