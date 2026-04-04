using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Core.Pathfinding;

public sealed class MiningPostMovementTelemetry
{
    public int CacheHits { get; private set; }

    public int CacheMisses { get; private set; }

    public int CacheRebuildCount { get; private set; }

    public int StalePathInvalidationCount { get; private set; }

    public int SelectionGraphBfsCount { get; private set; }

    internal void RecordCacheHit() => CacheHits++;

    internal void RecordCacheMiss() => CacheMisses++;

    internal void RecordCacheRebuild() => CacheRebuildCount++;

    internal void RecordStalePathInvalidation() => StalePathInvalidationCount++;

    internal void RecordSelectionGraphBfs() => SelectionGraphBfsCount++;

    public void Reset()
    {
        CacheHits = 0;
        CacheMisses = 0;
        CacheRebuildCount = 0;
        StalePathInvalidationCount = 0;
        SelectionGraphBfsCount = 0;
    }
}

internal sealed class MiningPostMovementCacheEntry
{
    public MiningPostMovementCacheEntry(MiningPost post, Cave cave)
    {
        Field = new BfsField(post.Name, "building", cave, post);
        TopologyVersion = -1;
        ReachabilityVersion = -1;
        ForceRebuild = true;
    }

    public BfsField Field { get; }

    public long TopologyVersion { get; set; }

    public long ReachabilityVersion { get; set; }

    public bool ForceRebuild { get; set; }
}
