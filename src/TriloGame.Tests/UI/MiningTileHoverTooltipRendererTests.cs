using Microsoft.Xna.Framework;
using TriloGame.Game.Core.World;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class MiningTileHoverTooltipRendererTests
{
    [Fact]
    public void BuildModel_UsesFriendlyWallName()
    {
        var tile = new Tile(1, "0,0");
        tile.SetBase("wall");

        var model = MiningTileHoverTooltipRenderer.BuildModel(tile, new Point(100, 100), new Rectangle(0, 0, 800, 600));

        Assert.Equal(["Wall"], model.Lines);
    }

    [Fact]
    public void BuildModel_IncludesOreYieldLine()
    {
        var tile = new Tile(1, "0,0");
        tile.SetBase("lumenite");
        tile.ConfigureOre(3, 2);

        var model = MiningTileHoverTooltipRenderer.BuildModel(tile, new Point(100, 100), new Rectangle(0, 0, 800, 600));

        Assert.Equal(["lumenite", "Yield: 3"], model.Lines);
    }

    [Fact]
    public void BuildModel_KeepsTooltipInsideGameplayRightEdge()
    {
        var tile = new Tile(1, "0,0");
        tile.SetBase("wall");
        var gameplayBounds = new Rectangle(0, 0, 320, 240);

        var model = MiningTileHoverTooltipRenderer.BuildModel(tile, new Point(310, 120), gameplayBounds);

        Assert.True(model.Bounds.Right <= gameplayBounds.Right);
    }
}
