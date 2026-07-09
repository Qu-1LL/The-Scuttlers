using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class MiningTileSelectionPolicyTests
{
    [Fact]
    public void CanSelect_ReturnsTrue_ForRevealedMineableTile()
    {
        var (_, cave) = TestWorldFactory.CreateRectangularSession(1, 1);
        var tile = cave.GetTile("0,0")!;
        tile.SetBase("wall");
        cave.RevealTile(tile);

        Assert.True(MiningTileSelectionPolicy.CanSelect(cave, tile));
    }

    [Fact]
    public void CanSelect_ReturnsFalse_ForUnrevealedMineableTile()
    {
        var (_, cave) = TestWorldFactory.CreateRectangularSession(1, 1);
        var tile = cave.GetTile("0,0")!;
        tile.SetBase("wall");

        Assert.False(MiningTileSelectionPolicy.CanSelect(cave, tile));
    }

    [Fact]
    public void CanSelect_ReturnsFalse_ForRevealedNonMineableTile()
    {
        var (_, cave) = TestWorldFactory.CreateRectangularSession(1, 1);
        var tile = cave.GetTile("0,0")!;
        cave.RevealTile(tile);

        Assert.False(MiningTileSelectionPolicy.CanSelect(cave, tile));
    }
}
