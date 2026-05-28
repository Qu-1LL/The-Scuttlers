using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class TrilobiteBuildingAssignmentTests
{
    [Fact]
    public void FarmerSelection_FallsBackToAdjacentOpenFarm_WhenNearestFarmIsFull()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var leftFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(1, 6));
        var rightFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(18, 6));
        var firstFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 9), "Farmer A", "farmer");
        var secondFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 9), "Farmer B", "farmer");
        var fallbackFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 9), "Farmer C", "farmer");

        Assert.True(leftFarm.Assign(firstFarmer));
        Assert.True(leftFarm.Assign(secondFarmer));
        Assert.True(cave.HasOpenAlgaeFarms);

        var selectedFarm = fallbackFarmer.SelectAlgaeFarm();

        Assert.Same(rightFarm, selectedFarm);
        Assert.NotSame(leftFarm, selectedFarm);
    }

    [Fact]
    public void FarmerSelection_WaitsWhileAllFarmsAreFull_AndResumesWhenASlotOpens()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(6, 6));
        var firstFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Farmer A", "farmer");
        var secondFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 9), "Farmer B", "farmer");
        var waitingFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 9), "Farmer C", "farmer");

        Assert.True(farm.Assign(firstFarmer));
        Assert.True(farm.Assign(secondFarmer));
        Assert.False(cave.HasOpenAlgaeFarms);

        Assert.Null(waitingFarmer.SelectAlgaeFarm());
        Assert.False(waitingFarmer.FarmerStep1());
        Assert.Null(waitingFarmer.GetAssignedAlgaeFarm());

        Assert.True(farm.RemoveAssignment(firstFarmer));
        Assert.True(cave.HasOpenAlgaeFarms);
        Assert.Same(farm, waitingFarmer.SelectAlgaeFarm());
    }

    [Fact]
    public void FarmerStep2_AdvancesAlongFarmTraversalRing_WhenHarvestDoesNotSucceed()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 16, new GridPoint(0, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(6, 6));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 6), "Farmer", "farmer");

        farmer.SetAssignedBuilding(farm);
        Assert.True(farm.Assign(farmer));
        Assert.Equal(1, farmer.AddToInventory("Sandstone", 1));

        var nextLocation = farm.GetNextTraversalLocation(farmer.Location);
        Assert.NotNull(nextLocation);

        Assert.True(farmer.FarmerStep2());
        Assert.NotNull(farmer.Move());
        Assert.Equal(nextLocation!.Value, farmer.Location);
    }

    [Fact]
    public void FarmerSelection_UsesNearestOwnershipWhenLocalFarmFieldIsStale()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 16, new GridPoint(0, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(6, 6));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 10), "Farmer", "farmer");
        var seedTile = farm.TileArray.First(tile => tile.CreatureFits());

        farm.BfsField.SetField(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [seedTile.Key] = 0
        });
        farm.BfsField.MarkDirty([seedTile.Key], [], []);

        var selectedFarm = farmer.SelectAlgaeFarm();

        Assert.Same(farm, selectedFarm);
    }

    [Fact]
    public void FarmerStep1_FallsBackToLargestGrowableStorage_WhenAllFarmerSlotsAreFull()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(6, 6));
        var firstFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 9), "Farmer A", "farmer");
        var secondFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 9), "Farmer B", "farmer");
        var ignoredGarage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(9, 10));
        var smallerSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(1, 10));
        var largerSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(14, 10));
        var spawnTile = GetBuildingWorkTile(cave, largerSilo);
        var waitingFarmer = TestWorldFactory.SpawnTrilobite(cave, session, spawnTile.Coordinates, "Farmer C", "farmer");

        Assert.True(farm.Assign(firstFarmer));
        Assert.True(farm.Assign(secondFarmer));
        Assert.False(cave.HasOpenAlgaeFarms);
        Assert.Equal(20, ignoredGarage.Deposit(OreType.SANDSTONE.Name, 20));
        Assert.Equal(4, smallerSilo.Deposit(OreType.ALGAE.Name, 4));
        Assert.Equal(9, largerSilo.Deposit(OreType.ALGAE.Name, 9));

        for (var tick = 0; tick < 10 && !waitingFarmer.HasInventory(); tick++)
        {
            waitingFarmer.Move();
        }

        Assert.Equal(OreType.ALGAE.Name, waitingFarmer.Inventory.Type);
        Assert.Equal(5, waitingFarmer.Inventory.Amount);
        Assert.Equal(4, smallerSilo.GetInventoryTotal());
        Assert.Equal(4, largerSilo.GetInventoryTotal());
        Assert.Equal(20, ignoredGarage.GetInventory().GetValueOrDefault(OreType.SANDSTONE.Name, 0));
        Assert.Equal(0, queen.AlgaeCount);
        Assert.Null(waitingFarmer.GetAssignedBuilding());
    }

    [Fact]
    public void FarmerStep1_WithdrawsOnlyGrowableResourcesFromGarageFallback()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 16, new GridPoint(8, 0));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(8, 10));
        var ranch = Assert.IsType<Ranch>(garage.Ranch);
        var ranchFarmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 10), "Ranch Farmer", "farmer");
        var spawnTile = GetBuildingWorkTile(cave, garage);
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, spawnTile.Coordinates, "Farmer", "farmer");

        Assert.Equal(12, garage.Deposit(OreType.SANDSTONE.Name, 12));
        Assert.Equal(7, garage.Deposit(OreType.ALGAE.Name, 7));
        ranchFarmer.SetAssignedBuilding(ranch);
        Assert.True(ranch.Assign(ranchFarmer));
        Assert.False(ranch.HasAssignmentSlot());

        for (var tick = 0; tick < 10 && !farmer.HasInventory(); tick++)
        {
            farmer.Move();
        }

        Assert.Equal(OreType.ALGAE.Name, farmer.Inventory.Type);
        Assert.Equal(5, farmer.Inventory.Amount);
        Assert.Equal(12, garage.GetInventory().GetValueOrDefault(OreType.SANDSTONE.Name, 0));
        Assert.Equal(2, garage.GetInventory().GetValueOrDefault(OreType.ALGAE.Name, 0));
        Assert.Null(farmer.GetAssignedBuilding());
    }

    [Fact]
    public void IdleFarmer_WithdrawsFromStorageAndFeedsQueen_WhenNoAssignmentsAreOpen()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(22, 20, new GridPoint(9, 2));
        var silo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(9, 14));
        var spawnTile = GetBuildingWorkTile(cave, silo);
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, spawnTile.Coordinates, "Farmer", "farmer");

        Assert.Equal(5, silo.Deposit(OreType.ALGAE.Name, 5));

        for (var tick = 0; tick < 80 && queen.AlgaeCount == 0; tick++)
        {
            farmer.Move();
        }

        Assert.Equal(5, queen.AlgaeCount);
        Assert.False(farmer.HasInventory());
        Assert.Equal(0, silo.GetInventoryTotal());
    }

    [Fact]
    public void FighterSelection_UsesNearestBarracksOwnership_WhenUnassigned()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(1, 6));
        var rightBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 9), "Fighter", "fighter");

        var selectedBarracks = fighter.SelectBarracks();

        Assert.Same(rightBarracks, selectedBarracks);
    }

    [Fact]
    public void FighterSelection_PrioritizesTurretOverBarracks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 9), "Fighter", "fighter");

        var selectedStation = fighter.SelectFighterStation();

        Assert.NotSame(barracks, selectedStation);
        Assert.Same(turret, selectedStation);
    }

    [Fact]
    public void FighterSelection_FallsBackToBarracks_WhenAllTurretsAreFull()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 14, new GridPoint(13, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var firstFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(12, 10), "Fighter A", "fighter");
        var secondFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(13, 10), "Fighter B", "fighter");
        var fallbackFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 9), "Fighter C", "fighter");

        Assert.True(turret.Assign(firstFighter));
        Assert.True(turret.Assign(secondFighter));
        Assert.False(turret.Assign(fallbackFighter));

        var selectedStation = fallbackFighter.SelectFighterStation();

        Assert.Same(barracks, selectedStation);
    }

    [Fact]
    public void FighterSelection_ReusesAssignedBarracks_WhenItRemainsValid()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        var leftBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(1, 6));
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 9), "Fighter", "fighter");
        fighter.SetAssignedBuilding(leftBarracks);
        leftBarracks.Assign(fighter);

        var selectedStation = fighter.SelectFighterStation(fighter.GetAssignedFighterStation());

        Assert.Same(leftBarracks, selectedStation);
    }

    [Fact]
    public void FighterSelection_UsesNearestOwnershipWhenLocalBarracksFieldIsStale()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 9), "Fighter", "fighter");
        var seedTile = barracks.TileArray.First(tile => tile.CreatureFits());

        barracks.BfsField.SetField(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [seedTile.Key] = 0
        });
        barracks.BfsField.MarkDirty([seedTile.Key], [], []);

        var selectedStation = fighter.SelectFighterStation();

        Assert.Same(barracks, selectedStation);
    }

    [Fact]
    public void FighterReturnToBarracks_RebalancesAssignments_WhenNewBarracksIsAdded()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(36, 14, new GridPoint(16, 0));
        var leftBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(1, 6));
        var rightBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(28, 6));
        var leftOne = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 6), "Left One", "fighter");
        var leftTwo = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 7), "Left Two", "fighter");
        var rightOne = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(28, 6), "Right One", "fighter");
        var rightTwo = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(28, 7), "Right Two", "fighter");

        leftOne.SetAssignedBuilding(leftBarracks);
        leftBarracks.Assign(leftOne);
        leftTwo.SetAssignedBuilding(leftBarracks);
        leftBarracks.Assign(leftTwo);
        rightOne.SetAssignedBuilding(rightBarracks);
        rightBarracks.Assign(rightOne);
        rightTwo.SetAssignedBuilding(rightBarracks);
        rightBarracks.Assign(rightTwo);

        var newBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(15, 8));

        Assert.True(cave.BarracksBuildingsAdded);
        Assert.Equal(2, cave.GetBarracksAssignmentCounts()[leftBarracks]);
        Assert.Equal(2, cave.GetBarracksAssignmentCounts()[rightBarracks]);
        Assert.Equal(0, cave.GetBarracksAssignmentCounts()[newBarracks]);

        var rebalanced = leftOne.FighterReturnToBarracks(true);

        Assert.True(rebalanced);
        Assert.Same(newBarracks, leftOne.GetAssignedBarracks());
        Assert.Equal(1, cave.GetBarracksAssignmentCounts()[leftBarracks]);
        Assert.Equal(2, cave.GetBarracksAssignmentCounts()[rightBarracks]);
        Assert.Equal(1, cave.GetBarracksAssignmentCounts()[newBarracks]);
        Assert.False(cave.BarracksBuildingsAdded);
    }

    [Fact]
    public void FighterReturnToBarracks_RebalancesIntoHigherPriorityTurret_WhenSlotOpens()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var stationedFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(24, 6), "Fighter", "fighter");

        stationedFighter.SetAssignedBuilding(barracks);
        Assert.True(barracks.Assign(stationedFighter));

        Assert.True(cave.BarracksBuildingsAdded);
        Assert.Same(turret, stationedFighter.SelectFighterStation(stationedFighter.GetAssignedFighterStation()));

        var pathToTurret = stationedFighter.BuildNavigationPathToBuilding(turret);
        Assert.NotNull(pathToTurret);
        Assert.True(pathToTurret!.Count > 1);

        var rebalanced = stationedFighter.FighterReturnToStation(true);

        Assert.True(rebalanced);
        Assert.Same(turret, stationedFighter.GetAssignedFighterStation());
        Assert.Equal(0, cave.GetBarracksAssignmentCounts()[barracks]);
        Assert.Equal(1, cave.GetTurretAssignmentCounts()[turret]);
        Assert.False(cave.BarracksBuildingsAdded);
    }

    [Fact]
    public void FighterReturnToBarracks_DocksIntoTurretSlots_AndLeavesTileSystem()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var firstAccessTile = new GridPoint(18, 5);
        var secondAccessTile = new GridPoint(20, 9);
        var firstFighter = TestWorldFactory.SpawnTrilobite(cave, session, firstAccessTile, "Fighter A", "fighter");
        var secondFighter = TestWorldFactory.SpawnTrilobite(cave, session, secondAccessTile, "Fighter B", "fighter");

        firstFighter.SetAssignedBuilding(turret);
        secondFighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(firstFighter));
        Assert.True(turret.Assign(secondFighter));

        Assert.False(firstFighter.FighterReturnToStation(true));
        Assert.False(secondFighter.FighterReturnToStation(true));

        var topLeftWorldX = (18 * TriloGame.Game.Core.Constants.TileConstants.TileSize) - TriloGame.Game.Core.Constants.TileConstants.TileHalfSize;
        var topLeftWorldY = (6 * TriloGame.Game.Core.Constants.TileConstants.TileSize) - TriloGame.Game.Core.Constants.TileConstants.TileHalfSize;

        Assert.False(firstFighter.IsTrackedInTileSystem);
        Assert.False(secondFighter.IsTrackedInTileSystem);
        Assert.Same(turret, firstFighter.HostedBuilding);
        Assert.Same(turret, secondFighter.HostedBuilding);
        Assert.Equal(firstAccessTile, firstFighter.Location);
        Assert.Equal(secondAccessTile, secondFighter.Location);
        Assert.Null(cave.GetTile(firstAccessTile.ToString())!.Built);
        Assert.Null(cave.GetTile(secondAccessTile.ToString())!.Built);
        Assert.DoesNotContain(firstFighter, cave.GetTile(firstAccessTile.ToString())!.Trilobites);
        Assert.DoesNotContain(secondFighter, cave.GetTile(secondAccessTile.ToString())!.Trilobites);
        Assert.Equal(new System.Numerics.Vector2(topLeftWorldX + 80f, topLeftWorldY + 80f), firstFighter.HostedWorldPosition!.Value);
        Assert.Equal(new System.Numerics.Vector2(topLeftWorldX + 160f, topLeftWorldY + 160f), secondFighter.HostedWorldPosition!.Value);
    }

    [Fact]
    public void FighterReturnToBarracks_DocksIntoRotatedTurretSlotLayout()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        var turret = new Turret(session);
        turret.RotateMap();
        turret.SetDisplayRotationTurns(1);
        Assert.True(cave.Build(turret, new GridPoint(18, 6)));

        var accessTile = new GridPoint(19, 5);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, accessTile, "Fighter", "fighter");
        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));

        Assert.False(fighter.FighterReturnToStation(true));

        var topLeftWorldX = (18 * TriloGame.Game.Core.Constants.TileConstants.TileSize) - TriloGame.Game.Core.Constants.TileConstants.TileHalfSize;
        var topLeftWorldY = (6 * TriloGame.Game.Core.Constants.TileConstants.TileSize) - TriloGame.Game.Core.Constants.TileConstants.TileHalfSize;

        Assert.False(fighter.IsTrackedInTileSystem);
        Assert.Same(turret, fighter.HostedBuilding);
        Assert.Equal(accessTile, fighter.Location);
        Assert.Null(cave.GetTile(accessTile.ToString())!.Built);
        Assert.Equal(new System.Numerics.Vector2(topLeftWorldX + 160f, topLeftWorldY + 80f), fighter.HostedWorldPosition!.Value);
    }

    [Fact]
    public void ChangeAssignment_RestoresDockedTurretFighterToAdjacentTileImmediately()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var accessTile = new GridPoint(18, 5);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, accessTile, "Fighter", "fighter");
        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));
        Assert.False(fighter.FighterReturnToStation(true));

        Assert.True(fighter.ChangeAssignment("builder"));

        Assert.Equal("builder", fighter.Assignment);
        Assert.True(fighter.IsTrackedInTileSystem);
        Assert.Null(fighter.HostedBuilding);
        Assert.Null(fighter.HostedWorldPosition);
        Assert.Null(fighter.GetAssignedFighterStation());
        Assert.Equal(accessTile, fighter.Location);
        Assert.Contains(fighter, cave.GetTile(accessTile.ToString())!.Trilobites);
        Assert.False(turret.IsAssigned(fighter));
    }

    [Fact]
    public void BuilderAssignment_ReassignsWhenCurrentScaffoldBecomesUnreachable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(22, 14, new GridPoint(1, 1));
        var firstScaffold = new Scaffolding(session, new Storage(session));
        var secondScaffold = new Scaffolding(session, new Storage(session));
        Assert.True(cave.Build(firstScaffold, new GridPoint(6, 6)));
        Assert.True(cave.Build(secondScaffold, new GridPoint(14, 6)));
        Assert.Equal(20, firstScaffold.Deposit("Sandstone", 20));
        Assert.Equal(20, secondScaffold.Deposit("Sandstone", 20));

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 10), "Builder", "builder");

        Assert.Same(firstScaffold, builder.EnsureBuilderAssignment(true));
        Assert.Same(firstScaffold, builder.GetAssignedScaffolding());
        Assert.Contains(builder, firstScaffold.GetAssignments());

        foreach (var location in new[]
                 {
                     new GridPoint(5, 5), new GridPoint(6, 5), new GridPoint(7, 5), new GridPoint(8, 5),
                     new GridPoint(5, 6), new GridPoint(8, 6),
                     new GridPoint(5, 7), new GridPoint(8, 7),
                     new GridPoint(5, 8), new GridPoint(6, 8), new GridPoint(7, 8), new GridPoint(8, 8)
                 })
        {
            SetWallTile(cave, location);
        }

        cave.RefreshReachableTiles();
        firstScaffold.BfsField.Rebuild();
        secondScaffold.BfsField.Rebuild();

        var reassigned = builder.EnsureBuilderAssignment(true);

        Assert.Same(secondScaffold, reassigned);
        Assert.Same(secondScaffold, builder.GetAssignedScaffolding());
        Assert.DoesNotContain(builder, firstScaffold.GetAssignments());
        Assert.Contains(builder, secondScaffold.GetAssignments());
    }

    [Fact]
    public void BarracksAssignment_IsTrackedAndRemovedWhenFighterDies()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(18, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(17, 6), "Fighter", "fighter");
        fighter.SetTraits(Array.Empty<TrilobiteTrait>());

        fighter.SetAssignedBuilding(barracks);
        Assert.True(barracks.Assign(fighter));
        Assert.Contains(barracks, fighter.TrackedBy);
        Assert.Equal(1, cave.GetBarracksAssignmentCounts()[barracks]);

        fighter.TakeDamage(fighter.Health, "test");

        Assert.False(barracks.IsAssigned(fighter));
        Assert.Contains(barracks, cave.GetBarracksList());
        Assert.Empty(fighter.TrackedBy);
    }

    private static void SetWallTile(TriloGame.Game.Core.World.Cave cave, GridPoint location)
    {
        var tile = cave.GetTile(location)
            ?? throw new InvalidOperationException($"Expected tile at {location}.");
        tile.SetBase("wall");
        tile.CreatureCanFit = false;
        tile.ConfigureWall(1);
    }

    private static TriloGame.Game.Core.World.Tile GetBuildingWorkTile(TriloGame.Game.Core.World.Cave cave, Building building)
    {
        var location = building.Location ?? throw new InvalidOperationException($"Expected {building.Name} to have a location.");
        for (var y = location.Y - 1; y <= location.Y + building.Size.Y; y++)
        {
            for (var x = location.X - 1; x <= location.X + building.Size.X; x++)
            {
                var point = new GridPoint(x, y);
                if (point.X >= location.X &&
                    point.X < location.X + building.Size.X &&
                    point.Y >= location.Y &&
                    point.Y < location.Y + building.Size.Y)
                {
                    continue;
                }

                var tile = cave.GetTile(point);
                if (tile?.CreatureFits() == true)
                {
                    return tile;
                }
            }
        }

        throw new InvalidOperationException($"Expected {building.Name} to have an adjacent work tile.");
    }
}
