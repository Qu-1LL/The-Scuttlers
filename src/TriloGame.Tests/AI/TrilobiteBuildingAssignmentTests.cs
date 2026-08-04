using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Runtime.Systems;
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
        Assert.False(waitingFarmer.RunRoleState(FarmerState.SelectFarm));
        Assert.Null(waitingFarmer.GetAssignedAlgaeFarm());

        Assert.True(farm.RemoveAssignment(firstFarmer));
        Assert.True(cave.HasOpenAlgaeFarms);
        Assert.Same(farm, waitingFarmer.SelectAlgaeFarm());
    }

    [Fact]
    public void FarmerMoveToFarmSlot_AdvancesAlongFarmTraversalRing_WhenHarvestDoesNotSucceed()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 16, new GridPoint(0, 0));
        var farm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(6, 6));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 6), "Farmer", "farmer");

        farmer.SetAssignedBuilding(farm);
        Assert.True(farm.Assign(farmer));
        Assert.Equal(1, farmer.AddToInventory(ResourceName.Sandstone, 1));

        Assert.True(farmer.NavigateToInteractionZone(
            farm,
            TriloGame.Game.Core.Interaction.InteractionZonePurpose.Work));
        while (farmer.HasActiveMovement)
        {
            cave.AdvanceCreatureMovement();
        }

        var nextLocation = farm.GetNextTraversalLocation(farmer.Location);
        Assert.NotNull(nextLocation);

        Assert.True(farmer.RunRoleState(FarmerState.MoveToFarmSlot));
        while (farmer.HasActiveMovement)
        {
            cave.AdvanceCreatureMovement();
        }
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
    public void NewFighterStation_WakesIdleFightersToSelectAndAssign()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        var firstFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Fighter A", "fighter");
        var secondFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 8), "Fighter B", "fighter");

        Assert.Equal(FighterState.Idle, firstFighter.FighterState);
        Assert.Equal(FighterState.Idle, secondFighter.FighterState);

        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));

        Assert.Equal(FighterState.SelectStation, firstFighter.FighterState);
        Assert.Equal(FighterState.SelectStation, secondFighter.FighterState);
        Assert.True(firstFighter.RunRole());
        Assert.Same(turret, firstFighter.GetAssignedFighterStation());

        var guard = 120;
        while (!firstFighter.IsHostedOnBuilding(turret) && guard-- > 0)
        {
            firstFighter.Move();
            cave.AdvanceCreatureMovement();
        }

        Assert.True(guard > 0);
        Assert.True(firstFighter.IsHostedOnBuilding(turret));
    }

    [Fact]
    public void FighterStartingAfterTurretExists_SelectsAndStationsAtIt()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Fighter", "fighter");

        Assert.Equal(FighterState.Idle, fighter.FighterState);
        Assert.True(fighter.Move() is true);
        Assert.Equal(FighterState.SelectStation, fighter.FighterState);
        Assert.Same(turret, fighter.GetAssignedFighterStation());

        var guard = 120;
        while (fighter.HasActiveMovement && guard-- > 0)
        {
            cave.AdvanceCreatureMovement();
        }

        Assert.True(guard > 0);
        fighter.Move();
        Assert.True(fighter.IsHostedOnBuilding(turret));
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
    public void FighterSelection_FillsTurretSlotsBeforeAssigningBarracks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(48, 18, new GridPoint(22, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(38, 8));
        var firstTurret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(16, 8));
        var secondTurret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(26, 8));
        var fighters = new List<Trilobite>();

        for (var index = 0; index < 5; index++)
        {
            var fighter = TestWorldFactory.SpawnTrilobite(
                cave,
                session,
                new GridPoint(4 + (index * 2), 14),
                $"Fighter {index}",
                "fighter");
            fighters.Add(fighter);
            Assert.True(fighter.RunRoleState(FighterState.SelectStation));
        }

        Assert.Equal(2, cave.GetTurretAssignmentCounts()[firstTurret]);
        Assert.Equal(2, cave.GetTurretAssignmentCounts()[secondTurret]);
        Assert.Equal(1, cave.GetBarracksAssignmentCounts()[barracks]);
        Assert.Same(barracks, fighters[4].GetAssignedFighterStation());
    }

    [Fact]
    public void BarracksAssignments_AreUnlimitedAndSplitEvenly()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(64, 24, new GridPoint(30, 0));
        var leftBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(8, 8));
        var rightBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(48, 8));
        var fighters = new List<Trilobite>();

        for (var index = 0; index < 21; index++)
        {
            var fighter = TestWorldFactory.SpawnTrilobite(
                cave,
                session,
                new GridPoint(2 + ((index % 16) * 3), 18 + (index / 16)),
                $"Fighter {index}",
                "fighter");
            fighters.Add(fighter);
            Assert.True(fighter.RunRoleState(FighterState.SelectStation));
        }

        var leftCount = cave.GetBarracksAssignmentCounts()[leftBarracks];
        var rightCount = cave.GetBarracksAssignmentCounts()[rightBarracks];
        Assert.Equal(int.MaxValue, leftBarracks.Capacity);
        Assert.Equal(21, leftCount + rightCount);
        Assert.True(Math.Abs(leftCount - rightCount) <= 1);
        Assert.All(fighters, fighter => Assert.NotNull(fighter.GetAssignedBarracks()));
    }

    [Fact]
    public void FighterSelection_KeepsTurretAssignmentWhileAsyncFieldIsPending()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        turret.ClearPublishedNavigationField();
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Fighter", "fighter");

        Assert.False(fighter.RunRoleState(FighterState.SelectStation));

        Assert.Equal(FighterState.SelectStation, fighter.FighterState);
        Assert.Same(turret, fighter.GetAssignedFighterStation());
        Assert.True(turret.IsAssigned(fighter));
        Assert.False(barracks.IsAssigned(fighter));
    }

    [Fact]
    public void BarracksAssignments_SpreadAcrossLeastOccupiedStationSlots()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 16, new GridPoint(14, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(14, 6));
        var slotCounts = new int[barracks.Stations.Count];

        for (var index = 0; index < 17; index++)
        {
            var fighter = TestWorldFactory.SpawnTrilobite(
                cave,
                session,
                new GridPoint(2 + (index % 12), 13),
                $"Fighter {index}",
                "fighter");
            fighter.SetAssignedBuilding(barracks);
            Assert.True(barracks.Assign(fighter));
            slotCounts[barracks.GetAssignedStationIndex(fighter)!.Value]++;
        }

        Assert.Equal(3, slotCounts.Max());
        Assert.Equal(2, slotCounts.Min());
    }

    [Fact]
    public void ArrivingBarracksFighter_WalksToItsLeastOccupiedAssignedStationSlot()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 16, new GridPoint(14, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(14, 6));
        var targetSlot = new GridPoint(14, 6);

        for (var index = 0; index < barracks.Stations.Count; index++)
        {
            var fighter = TestWorldFactory.SpawnTrilobite(
                cave,
                session,
                new GridPoint(2 + index, 13),
                $"Reserved {index}",
                "fighter");
            fighter.SetAssignedBuilding(barracks);
            Assert.True(barracks.Assign(fighter));
        }

        var arrivingFighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(16, 8), "Arriving", "fighter");
        arrivingFighter.SetAssignedBuilding(barracks);
        Assert.True(barracks.Assign(arrivingFighter));
        Assert.Equal(0, barracks.GetAssignedStationIndex(arrivingFighter));

        Assert.False(arrivingFighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));
        var guard = 20;
        while (arrivingFighter.Activity != CreatureActivity.Stationed && guard-- > 0)
        {
            cave.AdvanceCreatureMovement();
            arrivingFighter.Move();
        }

        Assert.True(guard > 0);
        Assert.Equal(targetSlot, arrivingFighter.Location);
        Assert.Equal(CreatureActivity.Stationed, arrivingFighter.Activity);
    }

    [Fact]
    public void FighterAtTurretApproach_DocksEvenWhenDangerIsActive()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 14, new GridPoint(14, 0));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(18, 5), "Fighter", "fighter");
        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));
        session.Danger = true;

        fighter.Move();

        Assert.True(fighter.IsHostedOnBuilding(turret));
        Assert.False(fighter.IsLocomotionEnabled);
    }

    [Fact]
    public void Fighters_FillAndDockEachTurretWhenMultipleTurretsExist()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(56, 20, new GridPoint(26, 0));
        var firstTurret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(12, 6));
        var secondTurret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(26, 6));
        var thirdTurret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(40, 6));
        var fighters = new List<Trilobite>();

        for (var index = 0; index < 6; index++)
        {
            fighters.Add(TestWorldFactory.SpawnTrilobite(
                cave,
                session,
                new GridPoint(2 + (index * 2), 14),
                $"Fighter {index}",
                "fighter"));
        }

        var guard = 180;
        while (fighters.Any(fighter => !fighter.IsHostedOnBuilding()) && guard-- > 0)
        {
            for (var index = 0; index < fighters.Count; index++)
            {
                fighters[index].Move();
            }

            cave.AdvanceCreatureMovement();
        }

        Assert.True(guard > 0);
        Assert.All(fighters, fighter => Assert.True(fighter.IsHostedOnBuilding()));
        Assert.Equal(2, cave.GetTurretAssignmentCounts()[firstTurret]);
        Assert.Equal(2, cave.GetTurretAssignmentCounts()[secondTurret]);
        Assert.Equal(2, cave.GetTurretAssignmentCounts()[thirdTurret]);
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

        var rebalanced = stationedFighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true);

        Assert.True(rebalanced);
        Assert.Same(turret, stationedFighter.GetAssignedFighterStation());
        Assert.Equal(0, cave.GetBarracksAssignmentCounts()[barracks]);
        Assert.Equal(1, cave.GetTurretAssignmentCounts()[turret]);
        Assert.False(cave.BarracksBuildingsAdded);
    }

    [Fact]
    public void FighterReturnToBarracks_DocksIntoTurretSlots_AndDisablesLocomotion()
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

        Assert.False(firstFighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));
        Assert.False(secondFighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));

        var topLeftWorldX = (18 * TileConstants.TileSize) - TileConstants.TileHalfSize;
        var topLeftWorldY = (6 * TileConstants.TileSize) - TileConstants.TileHalfSize;

        Assert.False(firstFighter.IsLocomotionEnabled);
        Assert.False(secondFighter.IsLocomotionEnabled);
        Assert.Same(turret, firstFighter.HostedBuilding);
        Assert.Same(turret, secondFighter.HostedBuilding);
        Assert.Equal(firstAccessTile, firstFighter.LocomotionRestoreCell);
        Assert.Equal(secondAccessTile, secondFighter.LocomotionRestoreCell);
        Assert.Null(cave.GetTile(firstAccessTile.ToString())!.Built);
        Assert.Null(cave.GetTile(secondAccessTile.ToString())!.Built);
        Assert.Equal(new System.Numerics.Vector2(topLeftWorldX + TileConstants.TileSize, topLeftWorldY + TileConstants.TileSize), firstFighter.GetWorldPosition());
        Assert.Equal(new System.Numerics.Vector2(topLeftWorldX + (TileConstants.TileSize * 2f), topLeftWorldY + (TileConstants.TileSize * 2f)), secondFighter.GetWorldPosition());
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

        Assert.False(fighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));

        var topLeftWorldX = (18 * TileConstants.TileSize) - TileConstants.TileHalfSize;
        var topLeftWorldY = (6 * TileConstants.TileSize) - TileConstants.TileHalfSize;

        Assert.False(fighter.IsLocomotionEnabled);
        Assert.Same(turret, fighter.HostedBuilding);
        Assert.Equal(accessTile, fighter.LocomotionRestoreCell);
        Assert.Null(cave.GetTile(accessTile.ToString())!.Built);
        Assert.Equal(new System.Numerics.Vector2(topLeftWorldX + (TileConstants.TileSize * 2f), topLeftWorldY + TileConstants.TileSize), fighter.GetWorldPosition());
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
        Assert.False(fighter.RunRoleState(FighterState.ReturnToStation, preferAssignedStation: true));

        Assert.True(fighter.ChangeAssignment("builder"));

        Assert.Equal("builder", fighter.Assignment);
        Assert.True(fighter.IsLocomotionEnabled);
        Assert.Null(fighter.HostedBuilding);
        Assert.Null(fighter.GetAssignedFighterStation());
        Assert.Equal(accessTile, fighter.Location);
        Assert.Same(fighter, cave.GetTrilobiteAtTileKey(accessTile.ToString()));
        Assert.False(turret.IsAssigned(fighter));
    }

    [Fact]
    public void BuilderAssignment_SkipsScaffoldWithNoUnreservedMaterials()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 14, new GridPoint(1, 1));
        var reservedScaffold = new Scaffolding(session, new SoilPatch(session));
        var availableScaffold = new Scaffolding(session, new SoilPatch(session));
        Assert.True(cave.Build(reservedScaffold, new GridPoint(8, 6)));
        Assert.True(cave.Build(availableScaffold, new GridPoint(18, 6)));

        var reservationHolder = TestWorldFactory.SpawnTrilobite(
            cave,
            session,
            new GridPoint(5, 6),
            "Reservation Holder");
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 10), "Builder", "builder");

        Assert.Equal(5, reservedScaffold.ReserveMaterial(reservationHolder, ResourceName.Chitinstone, 5));
        Assert.Null(reservedScaffold.GetMaterialReservation(builder));
        Assert.False(reservedScaffold.NeedsAnyResource(includeReservations: true, excludeCreature: builder));
        Assert.True(availableScaffold.NeedsAnyResource(includeReservations: true, excludeCreature: builder));

        Assert.Same(availableScaffold, builder.EnsureBuilderAssignment());
        Assert.DoesNotContain(builder, reservedScaffold.GetAssignments());
        Assert.Contains(builder, availableScaffold.GetAssignments());
    }

    [Fact]
    public void BuilderAssignment_RespectsSoilPatchSingleCarryCapacityBeforeReservation()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        var scaffold = new Scaffolding(session, new SoilPatch(session));
        Assert.True(cave.Build(scaffold, new GridPoint(12, 6)));

        var firstBuilder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 8), "Builder A", "builder");
        var secondBuilder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 8), "Builder B", "builder");

        Assert.Equal(1, scaffold.GetRequiredBuilderCount(firstBuilder.InventoryCapacity));
        Assert.Same(scaffold, firstBuilder.EnsureBuilderAssignment());
        Assert.False(scaffold.CanAssignBuilder(secondBuilder, secondBuilder.InventoryCapacity));

        Assert.Null(secondBuilder.EnsureBuilderAssignment());
        Assert.Single(scaffold.GetAssignments());
        Assert.Contains(firstBuilder, scaffold.GetAssignments());
    }

    [Fact]
    public void BuilderAssignment_ReassignsWhenCurrentScaffoldBecomesUnreachable()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(22, 14, new GridPoint(1, 1));
        var firstScaffold = new Scaffolding(session, new Storage(session));
        var secondScaffold = new Scaffolding(session, new Storage(session));
        Assert.True(cave.Build(firstScaffold, new GridPoint(6, 6)));
        Assert.True(cave.Build(secondScaffold, new GridPoint(14, 6)));
        Assert.Equal(20, firstScaffold.Deposit(ResourceName.Sandstone, 20));
        Assert.Equal(20, secondScaffold.Deposit(ResourceName.Sandstone, 20));

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
    public void BuilderAssignment_SkipsCompletionPendingScaffoldUntilItClears()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(22, 14, new GridPoint(1, 1));
        var blockedScaffold = new Scaffolding(session, new Storage(session));
        var availableScaffold = new Scaffolding(session, new Storage(session));
        var blockedLocation = new GridPoint(6, 6);
        var availableLocation = new GridPoint(14, 6);

        Assert.True(cave.Build(blockedScaffold, blockedLocation));
        Assert.True(cave.Build(availableScaffold, availableLocation));

        var blockedRequirement = blockedScaffold.GetRemainingRequirement(ResourceName.Sandstone);
        var availableRequirement = availableScaffold.GetRemainingRequirement(ResourceName.Sandstone);
        Assert.Equal(blockedRequirement, blockedScaffold.Deposit(ResourceName.Sandstone, blockedRequirement));
        Assert.Equal(availableRequirement, availableScaffold.Deposit(ResourceName.Sandstone, availableRequirement));

        var blocker = TestWorldFactory.SpawnTrilobite(cave, session, blockedLocation, "Blocker", "unassigned");
        Assert.Equal(
            blockedScaffold.ConstructionRequired,
            blockedScaffold.ApplyConstructionWork(blockedScaffold.ConstructionRequired));

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 10), "Builder", "builder");

        Assert.True(blockedScaffold.CompletionPending);
        Assert.Equal(blockedLocation, blocker.Location);
        Assert.False(builder.CanActOnScaffold(blockedScaffold));
        Assert.True(builder.CanActOnScaffold(availableScaffold));
        Assert.Same(availableScaffold, builder.EnsureBuilderAssignment(true));
    }

    [Fact]
    public void NewScaffolding_WakesOnlyBuildersWithoutActiveScaffoldWork()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 14, new GridPoint(1, 1));
        var waitingBuilder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 8), "Waiting Builder", "builder");
        var activeScaffold = new Scaffolding(session, new SoilPatch(session));

        Assert.True(cave.Build(activeScaffold, new GridPoint(12, 6)));
        waitingBuilder.RunRoleState(BuilderState.WaitForMaterials);
        Assert.Equal(BuilderState.Idle, waitingBuilder.BuilderState);

        var activeBuilder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 8), "Active Builder", "builder");
        activeBuilder.SetAssignedBuilding(activeScaffold);
        activeScaffold.Assign(activeBuilder);
        Assert.Contains(activeBuilder, activeScaffold.GetAssignments());

        var newScaffold = new Scaffolding(session, new SoilPatch(session));
        Assert.True(cave.Build(newScaffold, new GridPoint(20, 6)));

        Assert.Equal(BuilderState.SelectScaffold, waitingBuilder.BuilderState);
        Assert.Same(activeScaffold, activeBuilder.GetAssignedScaffolding());
        Assert.Equal(BuilderState.Idle, activeBuilder.BuilderState);
    }

    [Fact]
    public void BuilderBuildsSoilPatchUsingOrganicResourceFromReachableMiningPost()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(7, 6));
        Assert.Equal(5, post.Deposit(ResourceName.Chitinstone, 5));
        var scaffold = new Scaffolding(session, new SoilPatch(session));
        var scaffoldLocation = new GridPoint(12, 6);
        Assert.True(cave.Build(scaffold, scaffoldLocation));
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "Builder", "builder");
        builder.RestartBehavior();

        for (var tick = 0; tick < 80 && cave.GetSoilPatches().All(patch => patch.Location != scaffoldLocation); tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.DoesNotContain(scaffold, cave.GetScaffoldingList());
        Assert.Contains(cave.GetSoilPatches(), patch => patch.Location == scaffoldLocation);
        Assert.Equal(0, post.GetInventory().GetValueOrDefault(ResourceName.Chitinstone, 0));
        Assert.False(builder.HasInventory());
    }

    [Fact]
    public void BuilderCompletesSequentialScaffoldsUsingStorageMaterials()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 16, new GridPoint(1, 1));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(6, 6)));
        Assert.Equal(2, storage.Deposit(ResourceName.Chitinstone, 2));

        var recipe = new[] { ResourceRequirement.ForResource(ResourceName.Chitinstone, 1) };
        var firstLocation = new GridPoint(12, 6);
        var secondLocation = new GridPoint(20, 6);
        var firstScaffold = new Scaffolding(session, new SoilPatch(session), recipe);
        var secondScaffold = new Scaffolding(session, new SoilPatch(session), recipe);
        Assert.True(cave.Build(firstScaffold, firstLocation));
        Assert.True(cave.Build(secondScaffold, secondLocation));

        var builder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(9, 6), "Builder", "builder");
        builder.RestartBehavior();

        for (var tick = 0; tick < 240 && cave.GetSoilPatches().Count < 2; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.DoesNotContain(firstScaffold, cave.GetScaffoldingList());
        Assert.DoesNotContain(secondScaffold, cave.GetScaffoldingList());
        Assert.Contains(cave.GetSoilPatches(), patch => patch.Location == firstLocation);
        Assert.Contains(cave.GetSoilPatches(), patch => patch.Location == secondLocation);
        Assert.Equal(0, storage.GetStoredAmount(ResourceName.Chitinstone));
        Assert.Equal(BuilderState.SelectScaffold, builder.BuilderState);
    }

    [Fact]
    public void BuilderKeepsCarriedMaterialsWhileScaffoldFieldIsWaitingToPublish()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        using var maintenance = new BuildingBfsFieldMaintenanceSystem();
        maintenance.Attach(session);

        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(7, 6));
        Assert.Equal(5, post.Deposit(ResourceName.Sandstone, 5));

        var scaffold = new Scaffolding(session, new Storage(session));
        Assert.True(cave.Build(scaffold, new GridPoint(14, 6)));

        var postTile = post.TileArray.First(tile => tile.CreatureFits());
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, postTile.Coordinates, "Builder", "builder");
        builder.SetAssignedBuilding(scaffold);
        scaffold.Assign(builder);
        Assert.Equal(5, builder.AddToInventory(ResourceName.Sandstone, 5));

        scaffold.ClearPublishedNavigationField();

        Assert.False(builder.NavigateToBuilding(scaffold));
        Assert.Equal(5, builder.Inventory.Amount);

        builder.Move();

        Assert.Equal(5, builder.Inventory.Amount);
        Assert.Equal(5, post.GetStoredAmount(ResourceName.Sandstone));
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
}
