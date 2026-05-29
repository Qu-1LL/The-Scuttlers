namespace TriloGame.Game.Core.World;

public sealed class BiomeRegion
{
    private readonly HashSet<Tile> _tiles = [];

    public BiomeRegion(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IReadOnlyCollection<Tile> Tiles => _tiles;

    internal bool AddTile(Tile tile) => _tiles.Add(tile);

    internal bool RemoveTile(Tile tile) => _tiles.Remove(tile);
}
