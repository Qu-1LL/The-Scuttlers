using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.World;

public sealed class AntHole
{
    private readonly HashSet<Enemy> _ants = [];

    public AntHole(string tileKey, int pendingAntCount, int spawnDelayTicks, int? spawnSourceId = null)
    {
        TileKey = tileKey;
        PendingAntCount = Math.Max(0, pendingAntCount);
        RemainingSpawnDelayTicks = Math.Max(0, spawnDelayTicks);
        SpawnSourceId = spawnSourceId;
    }

    public string TileKey { get; }

    public int PendingAntCount { get; }

    public int RemainingSpawnDelayTicks { get; private set; }

    public int? SpawnSourceId { get; }

    public IReadOnlyCollection<Enemy> Ants => _ants;

    public int AntCount => _ants.Count;

    public bool IsReadyToSpawn => RemainingSpawnDelayTicks <= 0;

    public bool IsCleared => PendingAntCount <= 0 && _ants.Count == 0;

    public void Tick()
    {
        if (RemainingSpawnDelayTicks > 0)
        {
            RemainingSpawnDelayTicks--;
        }
    }

    public bool RegisterAnt(Enemy ant)
    {
        return _ants.Add(ant);
    }

    public bool UnregisterAnt(Enemy ant)
    {
        return _ants.Remove(ant);
    }
}
