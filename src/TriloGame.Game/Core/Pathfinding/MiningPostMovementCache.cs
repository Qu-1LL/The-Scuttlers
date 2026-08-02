namespace TriloGame.Game.Core.Pathfinding;

public sealed class MiningPostMovementTelemetry
{
    private readonly HashSet<int> _accessedBuildingIds = [];
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

    // Compatibility telemetry now tracks the general building field rather than a mining-post cache.
    internal void RecordMovementFieldAccess(int buildingRuntimeId)
    {
        if (_accessedBuildingIds.Add(buildingRuntimeId))
        {
            RecordCacheMiss();
            RecordCacheRebuild();
        }
        else
        {
            RecordCacheHit();
        }
    }

    public void Reset()
    {
        CacheHits = 0;
        CacheMisses = 0;
        CacheRebuildCount = 0;
        StalePathInvalidationCount = 0;
        SelectionGraphBfsCount = 0;
    }
}
