using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.World;

public sealed class AntHole
{
    private readonly HashSet<Enemy> _ants = [];

    public AntHole(string tileKey)
    {
        TileKey = tileKey;
    }

    public string TileKey { get; }

    public IReadOnlyCollection<Enemy> Ants => _ants;

    public int AntCount => _ants.Count;

    public bool IsCleared => _ants.Count == 0;

    public bool RegisterAnt(Enemy ant)
    {
        return _ants.Add(ant);
    }

    public bool UnregisterAnt(Enemy ant)
    {
        return _ants.Remove(ant);
    }
}
