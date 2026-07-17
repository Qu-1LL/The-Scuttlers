using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Simulation;

public sealed class MiningClaimAllocatorTests
{
    [Fact]
    public void Claims_AssignDifferentOresAndApproachPoints()
    {
        var (_, cave, _, post, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(3);
        cave.RevealCave();

        var first = MiningClaimAllocator.TryClaimNext(miners[0], post);
        var second = MiningClaimAllocator.TryClaimNext(miners[1], post);
        var third = MiningClaimAllocator.TryClaimNext(miners[2], post);

        Assert.True(first.HasValue);
        Assert.True(second.HasValue);
        Assert.True(third.HasValue);
        Assert.Equal(3, new[] { first.Value.TileKey, second.Value.TileKey, third.Value.TileKey }.Distinct().Count());
        Assert.All(new[] { first.Value, second.Value, third.Value }, claim =>
            Assert.True(cave.CanCreatureOccupyWorldPosition(miners[0], claim.ApproachPoint)));
    }

    [Fact]
    public void Claim_AllowsASecondMinerForTheSameOre()
    {
        var (_, cave, _, post, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(2);
        cave.RevealCave();
        var first = MiningClaimAllocator.TryClaimNext(miners[0], post);
        Assert.True(first.HasValue);
        miners[0].AcceptMiningClaim(first.Value);
        var tile = cave.GetTile(first.Value.TileKey)!;

        var second = MiningClaimAllocator.TryClaim(miners[1], post, tile);

        Assert.True(second.HasValue);
        Assert.Equal(first.Value.TileKey, second.Value.TileKey);
        Assert.Equal(first.Value.TileKey, post.GetAssignment(miners[1]));
    }

    [Fact]
    public void Claim_UsesNeighborApproachForWalkableOreTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(7, 0));
        var oreLocation = new GridPoint(5, 8);
        var ore = cave.GetTile(oreLocation)!;
        ore.SetBase(OreType.CHITINSTONE.Name);
        ore.CreatureCanFit = true;
        ore.ConfigureOre(1, 1);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(3, 5));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 8), "Miner", "miner");

        var claim = MiningClaimAllocator.TryClaim(miner, post, ore);

        Assert.True(claim.HasValue);
        Assert.NotEqual(WorldPoint.FromGridPoint(oreLocation), claim.Value.ApproachPoint);
        Assert.Contains(ore.Neighbors, neighbor => WorldPoint.FromGridPoint(neighbor.Coordinates) == claim.Value.ApproachPoint);
    }
}
