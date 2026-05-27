using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class MiningTileSelectionStateTests
{
    [Fact]
    public void Select_ReplacesSelection_WhenAppendIsFalse()
    {
        var selection = new MiningTileSelectionState();

        selection.Select("0,0", append: true, toggleIfAlreadySelected: false);
        selection.Select("1,0", append: false, toggleIfAlreadySelected: false);

        Assert.Equal(["1,0"], selection.TileKeys);
    }

    [Fact]
    public void Select_TogglesExistingTile_WhenRequested()
    {
        var selection = new MiningTileSelectionState();

        selection.Select("0,0", append: true, toggleIfAlreadySelected: false);
        selection.Select("0,0", append: true, toggleIfAlreadySelected: true);

        Assert.False(selection.HasSelection);
        Assert.Empty(selection.TileKeys);
    }

    [Fact]
    public void SelectMany_AppendsDistinctTilesInInputOrder()
    {
        var selection = new MiningTileSelectionState();

        selection.Select("0,0", append: true, toggleIfAlreadySelected: false);
        selection.SelectMany(["1,0", "0,0", "2,0"], append: true);

        Assert.Equal(["0,0", "1,0", "2,0"], selection.TileKeys);
    }

    [Fact]
    public void Contains_UsesConfiguredComparer()
    {
        var selection = new MiningTileSelectionState(StringComparer.OrdinalIgnoreCase);

        selection.Select("Ore-A", append: true, toggleIfAlreadySelected: false);

        Assert.True(selection.Contains("ore-a"));
    }
}
