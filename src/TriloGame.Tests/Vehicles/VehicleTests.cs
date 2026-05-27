using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Vehicles;

public sealed class VehicleTests
{
    [Fact]
    public void PlowStationsOneFarmerAtConfiguredLocalSlot()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var plow = new Plow(session);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        Assert.True(plow.StationCreature(farmer));

        Assert.Equal("farmer", plow.AssignmentClassification);
        Assert.Equal(1, plow.MaxStationedCreatures);
        Assert.Equal(40, plow.Health);
        Assert.Equal(4, plow.TileArray.Count);
        Assert.Same(plow, cave.GetVehicleAtTileKey(new GridPoint(5, 6).ToString()));
        Assert.IsAssignableFrom<IDriveable>(plow);
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.True(farmer.IsVisible);
        Assert.Same(plow, farmer.HostedVehicle);
        Assert.Null(farmer.HostedBuilding);
        Assert.Same(farmer, plow.Driver);
        Assert.True(plow.IsCreatureDriving(farmer));
        Assert.Contains(farmer, plow.StationedCreatures);
        Assert.Equal(480f, farmer.HostedWorldPosition!.Value.X, 3);
        Assert.Equal(520f, farmer.HostedWorldPosition.Value.Y, 3);
        Assert.Equal(MathF.PI * 0.5f, farmer.RotationRadians, 3);
    }

    [Fact]
    public void VehicleMoveUpdatesStationedCreatureTransform()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var plow = new Plow(session);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        Assert.True(plow.StationCreature(farmer));

        plow.EnqueueMove(new GridPoint(6, 6));
        Assert.True((bool)plow.Move()!);

        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Equal(560f, farmer.HostedWorldPosition!.Value.X, 3);
        Assert.Equal(520f, farmer.HostedWorldPosition.Value.Y, 3);
    }

    [Fact]
    public void DriveablePlowMovesWhenDriverTakesATurn()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var plow = new Plow(session);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        Assert.True(plow.StationCreature(farmer));

        plow.EnqueueMove(new GridPoint(6, 6));

        cave.TickVehicles();
        Assert.Equal(new GridPoint(5, 6), plow.Location);

        Assert.True((bool)farmer.Move()!);

        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Equal(560f, farmer.HostedWorldPosition!.Value.X, 3);
        Assert.Equal(520f, farmer.HostedWorldPosition.Value.Y, 3);
    }

    [Fact]
    public void VehicleMove_AllowsInPlaceRotationAndUpdatesStationedCreatureTransform()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var plow = new Plow(session);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        Assert.True(plow.StationCreature(farmer));

        plow.EnqueueMove(new GridPoint(5, 6), 2);
        Assert.True((bool)plow.Move()!);

        Assert.Equal(new GridPoint(5, 6), plow.Location);
        Assert.Equal(2, plow.GetDisplayRotationTurns());
        Assert.Equal(400f, farmer.HostedWorldPosition!.Value.X, 3);
        Assert.Equal(520f, farmer.HostedWorldPosition.Value.Y, 3);
        Assert.Equal(MathF.PI * 1.5f, farmer.RotationRadians, 3);
    }

    [Fact]
    public void PlowMove_HarvestsTrailingSoilTilesIntoInventory()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        soilPatch.SetAllPlantedResources(GrowableResourceType.ALGAE);
        soilPatch.SetAllGrowthLevels(3);
        var plow = new Plow(session);

        Assert.Equal(400, plow.Capacity);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));

        plow.EnqueueMove(new GridPoint(6, 6));
        Assert.True((bool)plow.Move()!);

        Assert.Equal(10, plow.GetInventoryTotal());
        Assert.Equal(10, plow.GetInventory()[OreType.ALGAE.Name]);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 0))!.PlantedResource);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 1))!.PlantedResource);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
    }

    [Fact]
    public void PlowTurn_HarvestsTrailingSoilTilesIntoInventory()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        soilPatch.SetAllPlantedResources(GrowableResourceType.ALGAE);
        soilPatch.SetAllGrowthLevels(3);
        var plow = new Plow(session);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));

        plow.EnqueueMove(new GridPoint(5, 6), 2);
        Assert.True((bool)plow.Move()!);

        Assert.Equal(10, plow.GetInventoryTotal());
        Assert.Equal(10, plow.GetInventory()[OreType.ALGAE.Name]);
        Assert.Equal(new GridPoint(5, 6), plow.Location);
        Assert.Equal(2, plow.GetDisplayRotationTurns());
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
    }

    [Fact]
    public void PlowMove_PlantsDormantSoilTilesUsingGarageChosenResource()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        var ranch = Assert.Single(cave.GetRanches());
        var plow = new Plow(session);

        Assert.Same(garage, ranch.Garage);
        Assert.Equal(GrowableResourceType.ALGAE, garage.ChosenResource);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));

        plow.EnqueueMove(new GridPoint(6, 6));
        Assert.True((bool)plow.Move()!);

        Assert.Equal(0, plow.GetInventoryTotal());
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 0))!.PlantedResource);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 1))!.PlantedResource);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 1))!.TextureKey);
    }

    [Fact]
    public void PlowMove_LeavesHarvestableTileUntouchedWhenCapacityWouldBeExceeded()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        soilPatch.SetAllPlantedResources(GrowableResourceType.ALGAE);
        soilPatch.SetAllGrowthLevels(3);
        soilPatch.SetReturnedAlgaeAmount(new GridPoint(0, 0), 250);
        soilPatch.SetReturnedAlgaeAmount(new GridPoint(0, 1), 250);
        var plow = new Plow(session);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));

        plow.EnqueueMove(new GridPoint(6, 6));
        Assert.True((bool)plow.Move()!);

        Assert.Equal(250, plow.GetInventoryTotal());
        Assert.Equal(150, plow.GetInventorySpace());
        Assert.Equal(250, plow.GetInventory()[OreType.ALGAE.Name]);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
    }

    [Fact]
    public void ColonyBfsAndEnemiesCanTargetVehicles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var plow = new Plow(session);
        var enemy = new Enemy("Ant", new GridPoint(4, 6), session);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        Assert.True(cave.Spawn(enemy, cave.GetTile(new GridPoint(4, 6))!));

        cave.RebuildBfsField("colony");

        Assert.Equal(0, cave.GetBfsFieldValue("colony", new GridPoint(4, 6)));
        Assert.Equal(new GridPoint(5, 6).ToString(), enemy.GetAdjacentHostileTileKey());
        Assert.True(enemy.EnemyStep1());
        Assert.Equal(35, plow.Health);
    }

    [Fact]
    public void DestroyedPlowIsRemovedAndReturnsPassengerToPlowLocation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var plow = new Plow(session);
        var enemy = new Enemy("Ant", new GridPoint(4, 6), session);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        Assert.True(plow.StationCreature(farmer));
        Assert.True(cave.Spawn(enemy, cave.GetTile(new GridPoint(4, 6))!));

        while (plow.Health > 0)
        {
            Assert.True(enemy.EnemyStep1());
        }

        Assert.Empty(cave.GetVehicles());
        Assert.Null(plow.Cave);
        Assert.Null(cave.GetVehicleAtTileKey(new GridPoint(5, 6).ToString()));
        Assert.True(farmer.IsTrackedInTileSystem);
        Assert.True(farmer.IsVisible);
        Assert.Null(farmer.HostedVehicle);
        Assert.Equal(new GridPoint(5, 6), farmer.Location);
        Assert.Contains(farmer, cave.GetTile(new GridPoint(5, 6))!.Trilobites);
    }
}
