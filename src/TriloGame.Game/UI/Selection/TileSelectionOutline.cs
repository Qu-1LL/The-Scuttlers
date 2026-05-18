using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class TileSelectionOutline
{
    public static IReadOnlyList<TileSelectionEdge> BuildEdges(IEnumerable<GridPoint> tiles)
    {
        var selected = tiles.ToHashSet();
        var edges = new List<TileSelectionEdge>();

        foreach (var tile in selected)
        {
            if (!selected.Contains(new GridPoint(tile.X, tile.Y - 1)))
            {
                edges.Add(new TileSelectionEdge(tile, TileSelectionEdgeSide.Top));
            }

            if (!selected.Contains(new GridPoint(tile.X + 1, tile.Y)))
            {
                edges.Add(new TileSelectionEdge(tile, TileSelectionEdgeSide.Right));
            }

            if (!selected.Contains(new GridPoint(tile.X, tile.Y + 1)))
            {
                edges.Add(new TileSelectionEdge(tile, TileSelectionEdgeSide.Bottom));
            }

            if (!selected.Contains(new GridPoint(tile.X - 1, tile.Y)))
            {
                edges.Add(new TileSelectionEdge(tile, TileSelectionEdgeSide.Left));
            }
        }

        return edges;
    }
}

public readonly record struct TileSelectionEdge(GridPoint Tile, TileSelectionEdgeSide Side);

public enum TileSelectionEdgeSide
{
    Top,
    Right,
    Bottom,
    Left
}
