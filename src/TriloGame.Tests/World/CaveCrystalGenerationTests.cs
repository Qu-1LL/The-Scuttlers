using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;

namespace TriloGame.Tests.World;

public sealed class CaveCrystalGenerationTests
{
    [Fact]
    public void GeneratedCave_PlacesCrystalsOnEmptyTraversableTiles()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var crystalTiles = cave.GetTiles()
            .Where(tile => tile.Decoration == TileDecoration.CaveCrystal)
            .ToArray();

        Assert.NotEmpty(crystalTiles);
        Assert.InRange(crystalTiles.Length, 1, GameConstants.CaveCrystalMaxCount);
        Assert.All(crystalTiles, tile =>
        {
            Assert.Equal("empty", tile.Base);
            Assert.True(tile.CreatureFits());
            Assert.False(tile.IsOreTile());
        });
    }

    [Fact]
    public void GeneratedCave_SpreadsCrystalsSoTheyDoNotTouch()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var crystalTiles = cave.GetTiles()
            .Where(tile => tile.Decoration == TileDecoration.CaveCrystal)
            .ToArray();

        Assert.NotEmpty(crystalTiles);
        Assert.All(crystalTiles, tile =>
            Assert.DoesNotContain(tile.Neighbors, neighbor => neighbor.Decoration == TileDecoration.CaveCrystal));
    }
}
