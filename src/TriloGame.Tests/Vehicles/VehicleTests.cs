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
        Assert.False(farmer.IsLocomotionEnabled);
        Assert.True(farmer.IsVisible);
        Assert.Same(plow, farmer.HostedVehicle);
        Assert.Null(farmer.HostedBuilding);
        Assert.Same(farmer, plow.Driver);
        Assert.True(plow.IsCreatureDriving(farmer));
        Assert.Contains(farmer, plow.StationedCreatures);
        Assert.Equal(plow.GetWorldCenter().X + 256f, farmer.GetWorldPosition().X, 3);
        Assert.Equal(plow.GetWorldCenter().Y, farmer.GetWorldPosition().Y, 3);
        Assert.Equal(MathF.PI * 0.5f, farmer.RotationRadians, 3);
        Assert.Equal(farmer.RotationRadians, farmer.GetInterpolatedFacingRadians(0.5f), 3);
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
        AdvancePlowUntilRouteCompletes(plow);

        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Equal(plow.GetWorldCenter().X + 256f, farmer.GetWorldPosition().X, 3);
        Assert.Equal(plow.GetWorldCenter().Y, farmer.GetWorldPosition().Y, 3);
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
        Assert.True(plow.HasActiveMovement);
        Assert.Equal(new GridPoint(5, 6), plow.Location);
        Assert.Equal(plow.GetInterpolatedWorldCenter(0.5f).X + 256f, farmer.GetInterpolatedWorldPosition(0.5f).X, 3);

        Assert.True((bool)farmer.Move()!);
        Assert.False(plow.HasActiveMovement);
        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Equal(plow.GetWorldCenter().X + 256f, farmer.GetWorldPosition().X, 3);
        Assert.Equal(plow.GetWorldCenter().Y, farmer.GetWorldPosition().Y, 3);
    }

    [Fact]
    public void PlowMovement_StaysContinuousAcrossStraightRouteSegments()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var plow = new Plow(session);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        plow.EnqueueMove(new GridPoint(6, 6), 0);
        plow.EnqueueMove(new GridPoint(7, 6), 0);

        var start = plow.GetWorldCenter();
        var movementPixels = plow.MovementSpeed / (float)WorldUnits.UnitsPerPixel;
        Assert.Equal((WorldUnits.UnitsPerTile * 3) / 4, plow.MovementSpeed);
        Assert.True((bool)plow.Move()!);
        Assert.True(plow.HasActiveMovement);
        Assert.Equal(start.X + movementPixels, plow.GetWorldCenter().X, 3);

        Assert.True((bool)plow.Move()!);
        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Single(plow.RouteCells);

        Assert.True((bool)plow.Move()!);
        Assert.True(plow.HasActiveMovement);
        Assert.Equal(start.X + (WorldUnits.UnitsPerTile / (float)WorldUnits.UnitsPerPixel) + movementPixels, plow.GetWorldCenter().X, 3);
    }

    [Fact]
    public void PlowMovement_StopsBeforeChangingDirection()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var plow = new Plow(session);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        plow.EnqueueMove(new GridPoint(6, 6), 0);
        plow.EnqueueMove(new GridPoint(6, 8), 1);

        Assert.True((bool)plow.Move()!);
        Assert.True((bool)plow.Move()!);
        Assert.Equal(new GridPoint(6, 6), plow.Location);

        Assert.True((bool)plow.Move()!);
        Assert.True(plow.HasActiveRotation);
        Assert.Equal(0, plow.GetDisplayRotationTurns());
        Assert.Equal(MathF.PI / (2f * Plow.QuarterTurnDurationTicks), plow.GetInterpolatedRotationRadians(1f), 3);

        for (var tick = 1; tick < Plow.QuarterTurnDurationTicks; tick++)
        {
            Assert.True((bool)plow.Move()!);
        }

        Assert.False(plow.HasActiveRotation);
        Assert.Equal(1, plow.GetDisplayRotationTurns());
        Assert.Single(plow.RouteCells);

        Assert.True((bool)plow.Move()!);
        Assert.True(plow.HasActiveMovement);
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
        AdvancePlowUntilRouteCompletes(plow);

        Assert.Equal(new GridPoint(5, 6), plow.Location);
        Assert.Equal(2, plow.GetDisplayRotationTurns());
        Assert.Equal(plow.GetWorldCenter().X - 256f, farmer.GetWorldPosition().X, 3);
        Assert.Equal(plow.GetWorldCenter().Y, farmer.GetWorldPosition().Y, 3);
        Assert.Equal(MathF.PI * 1.5f, farmer.RotationRadians, 3);
    }

    [Fact]
    public void PlowMove_HarvestsEveryTileInItsCoveredFootprint()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        soilPatch.SetAllPlantedResources(GrowableResourceType.ALGAE);
        soilPatch.SetAllGrowthLevels(3);
        var plow = new Plow(session);

        Assert.Equal(400, plow.Capacity);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));

        plow.EnqueueMove(new GridPoint(6, 6));
        AdvancePlowUntilRouteCompletes(plow);

        Assert.Equal(20, plow.GetInventoryTotal());
        Assert.Equal(20, plow.GetInventory()[ResourceName.Algae]);
        for (var x = 0; x < 2; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                var soilTile = soilPatch.GetSoilTile(new GridPoint(x, y))!;
                Assert.Equal(1, soilTile.GrowthLevel);
                Assert.Equal(GrowableResourceType.ALGAE, soilTile.PlantedResource);
            }
        }
    }

    [Fact]
    public void PlowTurn_HarvestsEveryTileInItsCoveredFootprint()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        soilPatch.SetAllPlantedResources(GrowableResourceType.ALGAE);
        soilPatch.SetAllGrowthLevels(3);
        var plow = new Plow(session);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(6, 6)));

        plow.EnqueueMove(new GridPoint(6, 6), 2);
        AdvancePlowUntilRouteCompletes(plow);

        Assert.Equal(20, plow.GetInventoryTotal());
        Assert.Equal(20, plow.GetInventory()[ResourceName.Algae]);
        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Equal(2, plow.GetDisplayRotationTurns());
        for (var x = 0; x < 2; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(x, y))!.GrowthLevel);
            }
        }
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
        AdvancePlowUntilRouteCompletes(plow);

        Assert.Equal(0, plow.GetInventoryTotal());
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 0))!.PlantedResource);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 0))!.TextureKey);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 1))!.PlantedResource);
        Assert.Equal("SoilTile_Algae_1", soilPatch.GetSoilTile(new GridPoint(0, 1))!.TextureKey);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(1, 0))!.PlantedResource);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(1, 1))!.PlantedResource);
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
        AdvancePlowUntilRouteCompletes(plow);

        Assert.Equal(260, plow.GetInventoryTotal());
        Assert.Equal(140, plow.GetInventorySpace());
        Assert.Equal(260, plow.GetInventory()[ResourceName.Algae]);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(0, 1))!.GrowthLevel);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(1, 0))!.GrowthLevel);
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
    }

    [Fact]
    public void PlowMove_AllowsATwoTileStraightRowChange()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var plow = new Plow(session);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        plow.EnqueueMove(new GridPoint(5, 8), 1);

        AdvancePlowUntilRouteCompletes(plow);
        Assert.Equal(new GridPoint(5, 8), plow.Location);
    }

    [Fact]
    public void PlowCoverageMove_DoesNotAbandonItsRouteForAnotherCreature()    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, new GridPoint(0, 0));
        var plow = new Plow(session);
        TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "Worker", "builder");

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        plow.EnqueueMove(new GridPoint(6, 6));

        AdvancePlowUntilRouteCompletes(plow);
        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Empty(plow.RouteCells);
    }

    private static void AdvancePlowUntilRouteCompletes(Plow plow)
    {
        var safety = 32;
        while (plow.RouteCells.Count > 0 && safety-- > 0)
        {
            Assert.True(plow.Move() is true);
        }

        Assert.True(safety > 0);
        Assert.False(plow.HasActiveMovement);
        Assert.False(plow.HasActiveRotation);
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
        session.Combat.ResolveTick(session);
        session.TickCount++;
        session.Combat.ResolveTick(session);
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
            session.Combat.ResolveTick(session);
            session.TickCount++;
            session.Combat.ResolveTick(session);
        }

        Assert.Empty(cave.GetVehicles());
        Assert.Null(plow.Cave);
        Assert.Null(cave.GetVehicleAtTileKey(new GridPoint(5, 6).ToString()));
        Assert.True(farmer.IsLocomotionEnabled);
        Assert.True(farmer.IsVisible);
        Assert.Null(farmer.HostedVehicle);
        Assert.Equal(new GridPoint(5, 6), farmer.Location);
        Assert.Same(farmer, cave.GetTrilobiteAtTileKey(new GridPoint(5, 6).ToString()));
    }
}
