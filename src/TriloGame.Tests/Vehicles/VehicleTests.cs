using TriloGame.Game.Core.Entities;
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
        Assert.False(farmer.IsTrackedInTileSystem);
        Assert.True(farmer.IsVisible);
        Assert.Same(plow, farmer.HostedVehicle);
        Assert.Null(farmer.HostedBuilding);
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
