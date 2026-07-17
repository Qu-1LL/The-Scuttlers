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
        Assert.Equal(300d, GameConstants.OreHitsPerYield * GameConstants.GameTimePerSimulationTickMs);
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

        var accepted = trilobite.AddToInventory(ResourceName.Lumenite, GameConstants.TrilobiteCarryCapacity + 2);

        Assert.Equal(GameConstants.TrilobiteCarryCapacity, accepted);
        Assert.True(trilobite.HasInventory());
        Assert.Equal(ResourceName.Lumenite, trilobite.Inventory.Type);
        Assert.Equal(GameConstants.TrilobiteCarryCapacity, trilobite.Inventory.Amount);
        Assert.Equal(0, trilobite.GetInventorySpace());
    }

    [Fact]
    public void TrilobiteInventory_CanCarryMultipleResourceTypes()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var trilobite = new Trilobite("Mixed Carrier", new GridPoint(0, 0), session);

        Assert.Equal(2, trilobite.AddToInventory(ResourceName.Lumenite, 2));
        Assert.Equal(3, trilobite.AddToInventory(ResourceName.Malachite, 3));

        Assert.True(trilobite.HasInventory());
        Assert.Equal(5, trilobite.Inventory.Amount);
        Assert.Equal(2, trilobite.Inventory.GetAmount(ResourceName.Lumenite));
        Assert.Equal(3, trilobite.Inventory.GetAmount(ResourceName.Malachite));
        Assert.Equal(GameConstants.TrilobiteCarryCapacity - 5, trilobite.GetInventorySpace());
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
        Assert.Equal(ResourceName.Lumenite, hit3.ResourceType);
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
    public void SandstoneOreTile_CanBeMinedLikeOtherOreTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var oreTile = cave.GetReachableTiles()
            .First(tile => tile.Base == "empty" && tile.CreatureFits());
        oreTile.SetBase(OreType.SANDSTONE.Name);
        oreTile.ConfigureOre(1, 2);

        var tileMinedCount = 0;
        var sandstoneMinedCount = 0;
        session.On(GameEvents.TileMined, _ => tileMinedCount++);
        session.On(GameEvents.SandstoneMined, _ => sandstoneMinedCount++);

        var hit1 = session.MineTile(cave, oreTile.Key, source: "manual");
        var hit2 = session.MineTile(cave, oreTile.Key, source: "manual");

        Assert.True(hit1.HitApplied);
        Assert.False(hit1.YieldedResource);
        Assert.True(hit2.HitApplied);
        Assert.True(hit2.YieldedResource);
        Assert.True(hit2.TileDepleted);
        Assert.Equal(ResourceName.Sandstone, hit2.ResourceType);
        Assert.Equal("empty", cave.GetTile(oreTile.Key)?.Base);
        Assert.Equal(1, tileMinedCount);
        Assert.Equal(1, sandstoneMinedCount);
        Assert.Equal(1, session.Stats.Get(GameEvents.TileMined));
        Assert.Equal(1, session.Stats.Get(GameEvents.SandstoneMined));
    }

    [Fact]
    public void WallMining_DepletesWallAndReturnsConfiguredOre()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var wallTile = cave.GetReachableTiles()
            .SelectMany(tile => tile.Neighbors)
            .First(tile => tile.Base == "wall");
        var collectorTile = wallTile.Neighbors.First(tile => tile.CreatureFits());
        ResourceName? minedResourceType = null;
        session.On(GameEvents.WallMined, payload => minedResourceType = payload.ResourceType);

        MineTileResult result = default;
        for (var hit = 0; hit < GameConstants.WallHitsRequired; hit++)
        {
            result = session.MineTile(cave, wallTile.Key, collectorTile.Key, "manual");

            if (hit < GameConstants.WallHitsRequired - 1)
            {
                Assert.True(result.HitApplied);
                Assert.False(result.YieldedResource);
                Assert.False(result.TileDepleted);
            }
        }

        Assert.True(result.HitApplied);
        Assert.True(result.YieldedResource);
        Assert.True(result.TileDepleted);
        Assert.Equal(GameConstants.WallMineResourceType, result.ResourceType);
        Assert.Equal(GameConstants.WallMineResourceAmount, result.ResourceAmount);
        Assert.Null(result.DroppedAtTileKey);
        Assert.Equal(0, result.DroppedAmount);
        Assert.Equal(GameConstants.WallMineResourceType, minedResourceType);
        Assert.Equal(0, collectorTile.GetDroppedResourceCount(OreType.SANDSTONE.Name));
    }

    [Fact]
    public void TrilobiteWallMining_AddsConfiguredOreToInventory()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var wallTile = cave.GetReachableTiles()
            .SelectMany(tile => tile.Neighbors)
            .First(tile => tile.Base == "wall");
        var minerTile = wallTile.Neighbors.First(tile => tile.CreatureFits());
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, minerTile.Coordinates, "Wall Miner", "miner");

        MineTileResult result = default;
        for (var hit = 0; hit < GameConstants.WallHitsRequired; hit++)
        {
            result = miner.MineTile(wallTile.Key);
        }

        Assert.True(result.TileDepleted);
        Assert.True(result.YieldedResource);
        Assert.Equal(GameConstants.WallMineResourceType, result.ResourceType);
        Assert.Equal(GameConstants.WallMineResourceAmount, result.ResourceAmount);
        Assert.Equal(GameConstants.WallMineResourceType, miner.Inventory.Type);
        Assert.Equal(GameConstants.WallMineResourceAmount, miner.Inventory.Amount);
    }
}
