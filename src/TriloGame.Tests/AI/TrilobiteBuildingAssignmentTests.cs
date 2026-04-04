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
    public void FighterSelection_ReusesAssignedBarracks_WhenItRemainsValid()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 12, new GridPoint(12, 0));
        var leftBarracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(1, 6));
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(24, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(23, 9), "Fighter", "fighter");
        fighter.SetAssignedBuilding(leftBarracks);
        leftBarracks.Assign(fighter);

        var selectedBarracks = fighter.SelectBarracks(fighter.GetAssignedBarracks());

        Assert.Same(leftBarracks, selectedBarracks);
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
}
