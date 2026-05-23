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
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(farmer.FarmerRanchStep2());

        Assert.True(farmer.IsVisible);
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.True(farmer.DrawBelowBuildings);
        Assert.False(farmer.CanBeDirectlySelected());
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
        Assert.Equal(new GridPoint(6, 6), plow.Location);
        Assert.Equal(0, plow.GetDisplayRotationTurns());
        Assert.True(farmer.IsVisible);
        Assert.False(farmer.DrawBelowBuildings);
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
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
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

    [Fact]
    public void AssignedRanchFarmer_PlowCompletesPathDocksAndRespawnsAfterTwentyTicks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var ranch = Assert.Single(cave.GetRanches());

        Assert.True(farmer.FarmerRanchStep2());

        for (var tick = 0; tick < 20; tick++)
        {
            TickRunner.RunTick(session);
        }

        var firstCyclePlow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.Same(firstCyclePlow, ranch.Plow);
        Assert.True(firstCyclePlow.IsCreatureStationed(farmer));

        TickRunner.RunTick(session);
        TickRunner.RunTick(session);

        Assert.Empty(cave.GetVehicles());
        Assert.True(farmer.IsVisible);
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.True(farmer.DrawBelowBuildings);
        Assert.False(farmer.CanBeDirectlySelected());
        Assert.Same(garage, farmer.HostedBuilding);
        Assert.Null(farmer.HostedVehicle);

        for (var tick = 0; tick < 18; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Empty(cave.GetVehicles());

        TickRunner.RunTick(session);

        var secondCyclePlow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.Same(firstCyclePlow, secondCyclePlow);
        Assert.True(secondCyclePlow.IsCreatureStationed(farmer));
    }

    [Fact]
    public void AssignedRanchFarmer_RotatedGarageUsesFrontSideSoilsForSpawn()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var garage = new Garage(session);
        garage.SetDisplayRotationTurns(1);
        Assert.True(cave.Build(garage, new GridPoint(4, 6)));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 8));

        Assert.True(farmer.FarmerRanchStep2());

        for (var tick = 0; tick < 20; tick++)
        {
            TickRunner.RunTick(session);
        }

        var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.Equal(new GridPoint(4, 8), plow.Location);
        Assert.Equal(1, plow.GetDisplayRotationTurns());
        Assert.True(plow.IsCreatureStationed(farmer));
    }

    [Fact]
    public void AssignedRanchFarmer_RemovingStartSoilBeforeSpawnPreventsPlowSpawn()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        var removedSoil = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));

        Assert.True(farmer.FarmerRanchStep2());
        Assert.True(cave.RemoveBuilding(removedSoil, "test"));

        for (var tick = 0; tick < 24; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Empty(cave.GetVehicles());
        Assert.True(farmer.IsVisible);
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.True(farmer.DrawBelowBuildings);
        Assert.False(farmer.CanBeDirectlySelected());
        Assert.Same(garage, farmer.HostedBuilding);
    }

    [Fact]
    public void AssignedRanchFarmer_AddingGarageAdjacentSoilAfterWaitAllowsImmediateSpawn()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));

        Assert.True(farmer.FarmerRanchStep2());

        for (var tick = 0; tick < 20; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Empty(cave.GetVehicles());

        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));

        TickRunner.RunTick(session);

        var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.True(plow.IsCreatureStationed(farmer));
        Assert.Equal(new GridPoint(6, 6), plow.Location);
    }

    [Fact]
    public void AssignedRanchFarmer_StraightFrontSoilStripSpawnsPlow()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(10, 6));

        Assert.True(farmer.FarmerRanchStep2());

        for (var tick = 0; tick < 24; tick++)
        {
            TickRunner.RunTick(session);
        }

        var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.True(plow.IsCreatureStationed(farmer));
        Assert.All(plow.TileArray, tile => Assert.IsType<SoilPatch>(tile.Built));
    }

    [Fact]
    public void AssignedRanchFarmer_FourPatchSquareUsesShortestCoverageRoute()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 8));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 8));

        Assert.True(farmer.FarmerRanchStep2());

        for (var tick = 0; tick < 20; tick++)
        {
            TickRunner.RunTick(session);
        }

        var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
        Assert.Equal(new GridPoint(7, 6), plow.Location);
        Assert.Equal(10, plow.PathPreview.Count);
    }

    [Fact]
    public void AssignedRanchFarmer_PlowOnlyTurnsNinetyDegreesBetweenSteps()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Farmer", "farmer");
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(8, 8));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 8));

        Assert.True(farmer.FarmerRanchStep2());

        for (var tick = 0; tick < 20; tick++)
        {
            TickRunner.RunTick(session);
        }

        var rotations = new List<int>();
        while (cave.GetVehicles().Count > 0)
        {
            var plow = Assert.IsType<Plow>(Assert.Single(cave.GetVehicles()));
            rotations.Add(plow.GetDisplayRotationTurns());
            TickRunner.RunTick(session);
        }

        Assert.True(rotations.Count >= 2);
        Assert.True(rotations.Distinct().Count() > 1);
        for (var index = 1; index < rotations.Count; index++)
        {
            var delta = Math.Abs(rotations[index] - rotations[index - 1]);
            var rotationDistance = Math.Min(delta, 4 - delta);
            Assert.True(rotationDistance <= 1, $"Unexpected plow flip from {rotations[index - 1]} to {rotations[index]}.");
        }
    }
}
