using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class InteractionZoneTests
{
    [Fact]
    public void Queen_ReservesThreeNorthFeedingAndThreeSouthBroodSlots()
    {
        var (_, _, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));

        var feeding = Assert.Single(queen.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.Feeding);
        var brooding = Assert.Single(queen.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.Brooding);

        Assert.Equal(3, feeding.Capacity);
        Assert.Equal(3, brooding.Capacity);
        Assert.All(feeding.SlotPositions, position => Assert.Equal(4, position.ToGridPoint().Y));
        Assert.All(brooding.SlotPositions, position => Assert.Equal(6, position.ToGridPoint().Y));
    }

    [Fact]
    public void ZoneReservation_IsExclusiveAndExpiresAfterLease()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));
        var zone = Assert.Single(queen.InteractionZones, item => item.Purpose == InteractionZonePurpose.Feeding);
        var first = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 1), "First");
        var second = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 3), "Second");

        Assert.True(zone.TryReserve(first, tick: 1, out var firstSlot));
        Assert.True(zone.TryReserve(second, tick: 1, out var secondSlot));
        Assert.NotEqual(firstSlot, secondSlot);

        zone.ExpireReservations(InteractionZone.ReservationLeaseTicks + 2);

        Assert.Equal(0, zone.OccupiedCount);
    }

    [Fact]
    public void CreatureCanInteractWhenNearReservedSlot()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));
        var zone = Assert.Single(queen.InteractionZones, item => item.Purpose == InteractionZonePurpose.Feeding);
        var feeder = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 1), "Feeder");

        Assert.True(feeder.TryReserveInteractionZone(zone));
        Assert.True(feeder.TryGetReservedZonePosition(out var target));

        feeder.SetWorldPosition(target + new WorldVector(WorldUnits.FromPixels(10), 0), snapPrevious: true);

        Assert.True(feeder.IsAtReservedInteractionSlot());
    }

    [Fact]
    public void MiningPost_ProvidesNineResourceTransferSlots()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(14, 14);
        var post = new MiningPost(session);

        Assert.True(cave.Build(post, new GridPoint(5, 5)));

        var transfer = Assert.Single(post.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.ResourceTransfer);

        Assert.Equal(9, transfer.Capacity);
        Assert.Equal(9, transfer.SlotPositions.Select(position => position.ToGridPoint()).Distinct().Count());
        Assert.All(post.TileArray, tile => Assert.True(tile.CreatureFits()));
    }

    [Fact]
    public void ResourceTransferZone_AllowsSharedReservationsBeyondSlotCount()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, new GridPoint(1, 1));
        var post = new MiningPost(session);
        Assert.True(cave.Build(post, new GridPoint(5, 5)));
        var transfer = Assert.Single(post.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.ResourceTransfer);

        var creatures = new List<Trilobite>(transfer.Capacity + 4);
        for (var index = 0; index < transfer.Capacity + 4; index++)
        {
            creatures.Add(TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3 + index % 4, 9 + index / 4), $"Carrier {index}"));
        }

        for (var index = 0; index < creatures.Count; index++)
        {
            Assert.True(transfer.TryReserve(creatures[index], tick: 1, out var slotIndex));
            Assert.True(slotIndex >= 0);
        }

        Assert.Equal(0, transfer.OccupiedCount);
    }

    [Fact]
    public void InteractionZones_RotateWithBuildingLocalNorth()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(14, 14);
        var silo = new Silo(session);
        silo.RotateMap();
        silo.SetDisplayRotationTurns(1);
        Assert.True(cave.Build(silo, new GridPoint(5, 5)));

        var transfer = Assert.Single(silo.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.ResourceTransfer);

        Assert.All(transfer.SlotPositions, position => Assert.Equal(7, position.ToGridPoint().X));
    }
}
