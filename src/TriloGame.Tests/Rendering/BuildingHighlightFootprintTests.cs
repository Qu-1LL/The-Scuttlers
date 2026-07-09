using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.Rendering;

public sealed class BuildingHighlightFootprintTests
{
    [Fact]
    public void EnumerateTiles_IncludesZeroAndOneCells()
    {
        var building = new Barracks(new GameSession())
        {
            Location = new GridPoint(10, 20)
        };

        var tiles = BuildingHighlightFootprint.EnumerateTiles(building).ToArray();

        Assert.Equal(9, tiles.Length);
        Assert.Contains(new GridPoint(10, 20), tiles);
        Assert.Contains(new GridPoint(11, 20), tiles);
        Assert.Contains(new GridPoint(11, 21), tiles);
        Assert.Contains(new GridPoint(12, 22), tiles);
    }

    [Fact]
    public void EnumerateTiles_ExcludesValueTwoCells()
    {
        var building = new Turret(new GameSession())
        {
            Location = new GridPoint(4, 7)
        };

        var tiles = BuildingHighlightFootprint.EnumerateTiles(building).ToArray();

        Assert.Equal(7, tiles.Length);
        Assert.DoesNotContain(new GridPoint(6, 7), tiles);
        Assert.DoesNotContain(new GridPoint(4, 9), tiles);
        Assert.Contains(new GridPoint(4, 7), tiles);
        Assert.Contains(new GridPoint(5, 8), tiles);
        Assert.Contains(new GridPoint(6, 9), tiles);
    }

    [Fact]
    public void BuildEdges_UsesSingleOutlineForZeroAndOneFootprintCells()
    {
        var building = new Barracks(new GameSession())
        {
            Location = new GridPoint(10, 20)
        };

        var edges = TileSelectionOutline.BuildEdges(BuildingHighlightFootprint.EnumerateTiles(building));

        Assert.Equal(12, edges.Count);
        Assert.Contains(new TileSelectionEdge(new GridPoint(11, 20), TileSelectionEdgeSide.Top), edges);
        Assert.Contains(new TileSelectionEdge(new GridPoint(12, 21), TileSelectionEdgeSide.Right), edges);
        Assert.Contains(new TileSelectionEdge(new GridPoint(11, 22), TileSelectionEdgeSide.Bottom), edges);
        Assert.Contains(new TileSelectionEdge(new GridPoint(10, 21), TileSelectionEdgeSide.Left), edges);
        Assert.DoesNotContain(new TileSelectionEdge(new GridPoint(11, 20), TileSelectionEdgeSide.Bottom), edges);
        Assert.DoesNotContain(new TileSelectionEdge(new GridPoint(12, 21), TileSelectionEdgeSide.Left), edges);
        Assert.DoesNotContain(new TileSelectionEdge(new GridPoint(11, 22), TileSelectionEdgeSide.Top), edges);
        Assert.DoesNotContain(new TileSelectionEdge(new GridPoint(10, 21), TileSelectionEdgeSide.Right), edges);
    }

    [Fact]
    public void EnumerateTiles_IncludesAllZeroStorageFootprint()
    {
        var building = new Storage(new GameSession())
        {
            Location = new GridPoint(3, 5)
        };

        var tiles = BuildingHighlightFootprint.EnumerateTiles(building).ToArray();

        Assert.Equal(
            [
                new GridPoint(3, 5),
                new GridPoint(4, 5),
                new GridPoint(3, 6),
                new GridPoint(4, 6)
            ],
            tiles);
    }
}
