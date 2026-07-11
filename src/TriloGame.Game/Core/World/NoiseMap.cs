namespace TriloGame.Game.Core.World;

public enum NoiseMapTileType : byte
{
    Empty = 0,
    Floor = 1,
    Wall = 2
}

public sealed class NoiseMap
{
    private readonly NoiseMapTileType[] _tiles;

    public NoiseMap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
        _tiles = new NoiseMapTileType[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public int CellCount => _tiles.Length;

    public NoiseMapTileType this[int x, int y]
    {
        get => _tiles[GetIndex(x, y)];
        set => _tiles[GetIndex(x, y)] = value;
    }

    public bool IsInBounds(int x, int y)
    {
        return (uint)x < (uint)Width && (uint)y < (uint)Height;
    }

    private int GetIndex(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside the map bounds.");
        }

        return (y * Width) + x;
    }
}
