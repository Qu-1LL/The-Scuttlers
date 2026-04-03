using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public class Graph
{
    protected readonly Dictionary<string, Tile> Tiles = new(StringComparer.Ordinal);
    private readonly Dictionary<GridPoint, Tile> _tilesByPoint = [];
    private readonly List<Tile> _tilesInOrder = [];
    private readonly List<Tile?> _tilesById = [];

    public Tile AddTile(string key)
    {
        if (!Tiles.TryGetValue(key, out var tile))
        {
            tile = new Tile(_tilesById.Count, key);
            Tiles[key] = tile;
            _tilesByPoint[tile.Coordinates] = tile;
            _tilesInOrder.Add(tile);
            _tilesById.Add(tile);
        }

        return tile;
    }

    public Tile? RemoveTile(string key)
    {
        if (!Tiles.TryGetValue(key, out var deleted))
        {
            return null;
        }

        foreach (var neighbor in deleted.Neighbors.ToArray())
        {
            neighbor.RemoveNeighbor(deleted);
        }

        Tiles.Remove(key);
        _tilesByPoint.Remove(deleted.Coordinates);
        _tilesInOrder.Remove(deleted);
        _tilesById[deleted.Id] = null;
        return deleted;
    }

    public void AddEdge(string left, string right)
    {
        var a = AddTile(left);
        var b = AddTile(right);
        a.AddNeighbor(b);
    }

    public Tile? GetTile(string key) => Tiles.GetValueOrDefault(key);

    public Tile? GetTile(GridPoint point) => _tilesByPoint.GetValueOrDefault(point);

    public Tile? GetTileById(int id) => id >= 0 && id < _tilesById.Count ? _tilesById[id] : null;

    public int TileCapacity => _tilesById.Count;

    public IReadOnlyList<Tile> GetTiles() => _tilesInOrder;
}
