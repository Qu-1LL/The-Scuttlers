using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class TileSelectionOutlineTests
{
    [Fact]
    public void BuildEdges_OmitsInternalEdgesForAdjacentTiles()
    {
        var edges = TileSelectionOutline.BuildEdges(
            [
                new GridPoint(0, 0),
                new GridPoint(1, 0)
            ]);

        Assert.DoesNotContain(edges, edge => edge.Tile == new GridPoint(0, 0) && edge.Side == TileSelectionEdgeSide.Right);
        Assert.DoesNotContain(edges, edge => edge.Tile == new GridPoint(1, 0) && edge.Side == TileSelectionEdgeSide.Left);
        Assert.Equal(6, edges.Count);
    }

    [Fact]
    public void BuildEdges_LeavesSeparateTilesAsSeparatePerimeters()
    {
        var edges = TileSelectionOutline.BuildEdges(
            [
                new GridPoint(0, 0),
                new GridPoint(2, 0)
            ]);

        Assert.Equal(8, edges.Count);
    }
}
