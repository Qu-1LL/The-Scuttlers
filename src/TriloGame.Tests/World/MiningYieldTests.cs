using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class MiningYieldTests
{
    [Fact]
    public void GeneratedOres_TakeHalfASecondOfSimulationTimePerYield()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var oreTiles = cave.GetTiles().Where(tile => tile.IsOreTile()).ToArray();

        Assert.NotEmpty(oreTiles);
        Assert.Equal(500d, GameConstants.OreHitsPerYield * GameConstants.GameTimePerSimulationTickMs);
        Assert.All(oreTiles, tile =>
        {
            Assert.InRange(tile.ResourceYield, GameConstants.MinOreYield, GameConstants.MaxOreYield);
            Assert.Equal(GameConstants.OreHitsPerYield, tile.HitsPerYield);
            Assert.Equal(GameConstants.OreHitsPerYield, tile.HitsRemaining);
        });
    }

    [Fact]
    public void TrilobiteInventory_IsLimitedToConfiguredCarryCapacity()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var trilobite = new Trilobite("Carrier", new GridPoint(0, 0), session);

        var accepted = trilobite.AddToInventory(OreType.LUMENITE.Name, GameConstants.TrilobiteCarryCapacity + 2);

        Assert.Equal(GameConstants.TrilobiteCarryCapacity, accepted);
        Assert.True(trilobite.HasInventory());
        Assert.Equal(OreType.LUMENITE.Name, trilobite.Inventory.Type);
        Assert.Equal(GameConstants.TrilobiteCarryCapacity, trilobite.Inventory.Amount);
        Assert.Equal(0, trilobite.GetInventorySpace());
    }

    [Fact]
    public void OreMining_RequiresMultipleHitsAndDepletesToEmpty()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var oreTile = cave.GetReachableTiles()
            .First(tile => tile.Base == "empty" && tile.CreatureFits());
        oreTile.SetBase(OreType.LUMENITE.Name);
        oreTile.ConfigureOre(2, 3);

        var tileMinedCount = 0;
        var lumeniteMinedCount = 0;
        session.On(GameEvents.TileMined, _ => tileMinedCount++);
        session.On(GameEvents.LumeniteMined, _ => lumeniteMinedCount++);

        var hit1 = session.MineTile(cave, oreTile.Key, source: "manual");
        var hit2 = session.MineTile(cave, oreTile.Key, source: "manual");
        var hit3 = session.MineTile(cave, oreTile.Key, source: "manual");
        var hit4 = session.MineTile(cave, oreTile.Key, source: "manual");
        var hit5 = session.MineTile(cave, oreTile.Key, source: "manual");
        var hit6 = session.MineTile(cave, oreTile.Key, source: "manual");

        Assert.True(hit1.HitApplied);
        Assert.False(hit1.YieldedResource);
        Assert.Equal(2, hit1.RemainingYield);
        Assert.Equal(2, hit1.RemainingHits);

        Assert.True(hit2.HitApplied);
        Assert.False(hit2.YieldedResource);
        Assert.Equal(2, hit2.RemainingYield);
        Assert.Equal(1, hit2.RemainingHits);

        Assert.True(hit3.HitApplied);
        Assert.True(hit3.YieldedResource);
        Assert.False(hit3.TileDepleted);
        Assert.Equal(OreType.LUMENITE.Name, hit3.ResourceType);
        Assert.Equal(1, hit3.ResourceAmount);
        Assert.Equal(1, hit3.RemainingYield);
        Assert.Equal(3, hit3.RemainingHits);

        Assert.True(hit4.HitApplied);
        Assert.False(hit4.YieldedResource);
        Assert.True(hit5.HitApplied);
        Assert.False(hit5.YieldedResource);

        Assert.True(hit6.HitApplied);
        Assert.True(hit6.YieldedResource);
        Assert.True(hit6.TileDepleted);
        Assert.Equal("empty", cave.GetTile(oreTile.Key)?.Base);
        Assert.Equal(2, tileMinedCount);
        Assert.Equal(2, lumeniteMinedCount);
        Assert.Equal(2, session.Stats.Get(GameEvents.TileMined));
        Assert.Equal(2, session.Stats.Get(GameEvents.LumeniteMined));
    }

    [Fact]
    public void WallMining_DepletesWallWithoutYieldingResources()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var wallTile = cave.GetReachableTiles()
            .SelectMany(tile => tile.Neighbors)
            .First(tile => tile.Base == "wall");
        var collectorTile = wallTile.Neighbors.First(tile => tile.CreatureFits());
        string? minedResourceType = "not-cleared";
        session.On(GameEvents.WallMined, payload => minedResourceType = payload.ResourceType);

        var hit1 = session.MineTile(cave, wallTile.Key, collectorTile.Key, "manual");
        var hit2 = session.MineTile(cave, wallTile.Key, collectorTile.Key, "manual");
        var hit3 = session.MineTile(cave, wallTile.Key, collectorTile.Key, "manual");

        Assert.True(hit1.HitApplied);
        Assert.False(hit1.YieldedResource);
        Assert.False(hit1.TileDepleted);
        Assert.True(hit2.HitApplied);
        Assert.False(hit2.YieldedResource);
        Assert.False(hit2.TileDepleted);
        Assert.True(hit3.HitApplied);
        Assert.False(hit3.YieldedResource);
        Assert.True(hit3.TileDepleted);
        Assert.Null(hit3.ResourceType);
        Assert.Equal(0, hit3.ResourceAmount);
        Assert.Null(hit3.DroppedAtTileKey);
        Assert.Equal(0, hit3.DroppedAmount);
        Assert.Null(minedResourceType);
        Assert.Equal(0, collectorTile.GetDroppedResourceCount(OreType.SANDSTONE.Name));
    }

    [Fact]
    public void TrilobiteWallMining_DoesNotRequireOrModifyInventory()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var wallTile = cave.GetReachableTiles()
            .SelectMany(tile => tile.Neighbors)
            .First(tile => tile.Base == "wall");
        var minerTile = wallTile.Neighbors.First(tile => tile.CreatureFits());
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, minerTile.Coordinates, "Wall Miner", "miner");
        Assert.Equal(
            GameConstants.TrilobiteCarryCapacity,
            miner.AddToInventory(OreType.LUMENITE.Name, GameConstants.TrilobiteCarryCapacity));

        MineTileResult result = default;
        for (var hit = 0; hit < GameConstants.WallHitsRequired; hit++)
        {
            result = miner.MineTile(wallTile.Key);
        }

        Assert.True(result.TileDepleted);
        Assert.False(result.YieldedResource);
        Assert.Equal(OreType.LUMENITE.Name, miner.Inventory.Type);
        Assert.Equal(GameConstants.TrilobiteCarryCapacity, miner.Inventory.Amount);
    }
}
