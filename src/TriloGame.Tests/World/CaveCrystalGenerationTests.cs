using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class CaveCrystalGenerationTests
{
    [Fact]
    public void GeneratedCave_PlacesCrystalsAsBlockingBreakableTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var crystalTiles = cave.GetTiles()
            .Where(tile => tile.IsCaveCrystal())
            .ToArray();

        Assert.NotEmpty(crystalTiles);
        Assert.InRange(crystalTiles.Length, 1, GameConstants.CaveCrystalMaxCount);
        Assert.All(crystalTiles, tile =>
        {
            Assert.Equal(Tile.CaveCrystalBase, tile.Base);
            Assert.False(tile.CreatureFits());
            Assert.True(tile.HasFloorCover);
            Assert.False(tile.IsOreTile());
            Assert.Equal(TileDecoration.None, tile.Decoration);
            Assert.Equal(GameConstants.CaveCrystalHitsRequired, tile.HitsRemaining);
            Assert.InRange(tile.OreRotationQuarterTurns, (byte)0, (byte)3);

            var placement = cave.EvaluateBuildPlacement(new Wall(session), tile.Coordinates);
            Assert.False(placement.CanBuild);
            Assert.True((placement.FailureReasons & BuildPlacementFailureReason.NonEmptyBase) != 0);
            Assert.True((placement.FailureReasons & BuildPlacementFailureReason.ImpassableTile) != 0);
        });
    }

    [Fact]
    public void GeneratedCave_SpreadsCrystalsSoTheyDoNotTouch()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var crystalTiles = cave.GetTiles()
            .Where(tile => tile.IsCaveCrystal())
            .ToArray();

        Assert.NotEmpty(crystalTiles);
        Assert.All(crystalTiles, tile =>
            Assert.DoesNotContain(tile.Neighbors, neighbor => neighbor.IsCaveCrystal()));
    }

    [Fact]
    public void TrilobiteCrystalMining_DepletesToEmptyWithoutYieldingResources()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(8, 8, GridPoint.Zero);
        var crystalTile = cave.GetTile(new GridPoint(5, 5))
            ?? throw new InvalidOperationException("Expected crystal test tile.");
        crystalTile.SetBase(Tile.CaveCrystalBase);
        crystalTile.CreatureCanFit = false;
        crystalTile.ConfigureCaveCrystal(GameConstants.CaveCrystalHitsRequired);
        crystalTile.SetOreRotationQuarterTurns(2);
        cave.RefreshReachableTiles();
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 4), "Crystal Miner", "miner");
        Assert.Equal(
            GameConstants.TrilobiteCarryCapacity,
            miner.AddToInventory(OreType.LUMENITE.Name, GameConstants.TrilobiteCarryCapacity));

        MineTileResult result = default;
        for (var hit = 0; hit < GameConstants.CaveCrystalHitsRequired; hit++)
        {
            result = miner.MineTile(crystalTile.Key);
        }

        Assert.True(result.HitApplied);
        Assert.False(result.YieldedResource);
        Assert.True(result.TileDepleted);
        Assert.Null(result.ResourceType);
        Assert.Equal(0, result.ResourceAmount);
        Assert.Empty(crystalTile.DroppedResources);
        Assert.Equal("empty", crystalTile.Base);
        Assert.True(crystalTile.CreatureFits());
        Assert.Equal(0, crystalTile.OreRotationQuarterTurns);
        Assert.Equal(OreType.LUMENITE.Name, miner.Inventory.Type);
        Assert.Equal(GameConstants.TrilobiteCarryCapacity, miner.Inventory.Amount);
    }
}
