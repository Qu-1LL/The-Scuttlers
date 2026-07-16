using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.Core.Pathfinding;

// Shared immutable topology arrays reused by every field in one maintenance batch.
public sealed class BuildingBfsNavigationSnapshot
{
    public BuildingBfsNavigationSnapshot(
        string[] tileKeys,
        int[][] neighbors,
        bool[] traversable)
    {
        TileKeys = tileKeys;
        Neighbors = neighbors;
        Traversable = traversable;
    }

    public string[] TileKeys { get; }

    public int[][] Neighbors { get; }

    public bool[] Traversable { get; }
}

// Immutable navigation state captured on the simulation thread for background building-field work.
public sealed class BuildingBfsFieldSnapshot
{
    public BuildingBfsFieldSnapshot(
        BuildingBfsNavigationSnapshot navigation,
        int[] values,
        int[] seedIds,
        int[] dirtyTileIds,
        bool rebuild)
        : this(
            values,
            navigation.TileKeys,
            navigation.Neighbors,
            navigation.Traversable,
            seedIds,
            dirtyTileIds,
            rebuild)
    {
    }

    public BuildingBfsFieldSnapshot(
        int[] values,
        string[] tileKeys,
        int[][] neighbors,
        bool[] traversable,
        int[] seedIds,
        int[] dirtyTileIds,
        bool rebuild)
    {
        Values = values;
        TileKeys = tileKeys;
        Neighbors = neighbors;
        Traversable = traversable;
        SeedIds = seedIds;
        DirtyTileIds = dirtyTileIds;
        Rebuild = rebuild;
    }

    public int[] Values { get; }

    public string[] TileKeys { get; }

    public int[][] Neighbors { get; }

    public bool[] Traversable { get; }

    public int[] SeedIds { get; }

    public int[] DirtyTileIds { get; }

    public bool Rebuild { get; }
}

// Worker output remains detached from live cave and building state until the simulation thread publishes it.
public sealed class BuildingBfsFieldMaintenanceResult
{
    public BuildingBfsFieldMaintenanceResult(int[] values, bool[] traversable, int[] nextStepIds)
    {
        Values = values;
        Traversable = traversable;
        NextStepIds = nextStepIds;
    }

    public int[] Values { get; }

    public bool[] Traversable { get; }

    public int[] NextStepIds { get; }
}

// One immutable batch lets every building field use the same topology snapshot.
public sealed class BuildingBfsMaintenanceBatch
{
    public BuildingBfsMaintenanceBatch(
        IReadOnlyList<BuildingBfsMaintenanceWork> workItems,
        long topologyVersion,
        long reachabilityVersion)
    {
        WorkItems = workItems;
        TopologyVersion = topologyVersion;
        ReachabilityVersion = reachabilityVersion;
    }

    public IReadOnlyList<BuildingBfsMaintenanceWork> WorkItems { get; }

    public long TopologyVersion { get; }

    public long ReachabilityVersion { get; }
}

public sealed class BuildingBfsMaintenanceWork
{
    public BuildingBfsMaintenanceWork(Building building, BuildingBfsFieldSnapshot snapshot)
    {
        Building = building;
        Snapshot = snapshot;
    }

    public Building Building { get; }

    public BuildingBfsFieldSnapshot Snapshot { get; }
}

public sealed class BuildingBfsMaintenanceBatchResult
{
    public BuildingBfsMaintenanceBatchResult(IReadOnlyList<BuildingBfsFieldMaintenanceResult> results)
    {
        Results = results;
    }

    public IReadOnlyList<BuildingBfsFieldMaintenanceResult> Results { get; }
}

public static class BuildingBfsFieldMaintenance
{
    // Compute every requested building field without reading live simulation state.
    public static BuildingBfsMaintenanceBatchResult ComputeBatch(BuildingBfsMaintenanceBatch batch)
    {
        var results = new List<BuildingBfsFieldMaintenanceResult>(batch.WorkItems.Count);
        for (var index = 0; index < batch.WorkItems.Count; index++)
        {
            results.Add(Compute(batch.WorkItems[index].Snapshot));
        }

        return new BuildingBfsMaintenanceBatchResult(results);
    }

    // Rebalance one building field against an immutable cave snapshot without touching simulation state.
    public static BuildingBfsFieldMaintenanceResult Compute(BuildingBfsFieldSnapshot snapshot)
    {
        var capacity = snapshot.Traversable.Length;
        var values = snapshot.Rebuild
            ? CreateEmptyValues(capacity)
            : (int[])snapshot.Values.Clone();
        var seeded = new bool[capacity];
        var queued = new bool[capacity];
        var queue = new Queue<int>();

        for (var tileId = 0; tileId < capacity; tileId++)
        {
            if (!snapshot.Traversable[tileId])
            {
                values[tileId] = int.MaxValue;
            }
        }

        for (var index = 0; index < snapshot.SeedIds.Length; index++)
        {
            var tileId = snapshot.SeedIds[index];
            if (!IsTraversable(snapshot, tileId))
            {
                continue;
            }

            seeded[tileId] = true;
            values[tileId] = 0;
            Enqueue(tileId, snapshot, queued, queue);
        }

        if (snapshot.Rebuild)
        {
            BuildFullField(snapshot, values, queued, queue);
            return new BuildingBfsFieldMaintenanceResult(values, snapshot.Traversable, BuildNextStepIds(snapshot, values));
        }

        for (var index = 0; index < snapshot.DirtyTileIds.Length; index++)
        {
            var tileId = snapshot.DirtyTileIds[index];
            Enqueue(tileId, snapshot, queued, queue);
            foreach (var neighborId in GetNeighbors(snapshot, tileId))
            {
                Enqueue(neighborId, snapshot, queued, queue);
            }
        }

        while (queue.Count > 0)
        {
            var tileId = queue.Dequeue();
            queued[tileId] = false;

            var nextValue = ComputeValue(tileId, snapshot, seeded, values);
            if (values[tileId] == nextValue)
            {
                continue;
            }

            if (values[tileId] != int.MaxValue && nextValue > values[tileId])
            {
                values[tileId] = int.MaxValue;
                Enqueue(tileId, snapshot, queued, queue);
                foreach (var neighborId in GetNeighbors(snapshot, tileId))
                {
                    Enqueue(neighborId, snapshot, queued, queue);
                }

                continue;
            }

            values[tileId] = nextValue;
            foreach (var neighborId in GetNeighbors(snapshot, tileId))
            {
                Enqueue(neighborId, snapshot, queued, queue);
            }
        }

        return new BuildingBfsFieldMaintenanceResult(values, snapshot.Traversable, BuildNextStepIds(snapshot, values));
    }

    private static void BuildFullField(BuildingBfsFieldSnapshot snapshot, int[] values, bool[] queued, Queue<int> queue)
    {
        while (queue.Count > 0)
        {
            var tileId = queue.Dequeue();
            queued[tileId] = false;
            var currentValue = values[tileId];
            if (currentValue == int.MaxValue)
            {
                continue;
            }

            foreach (var neighborId in GetNeighbors(snapshot, tileId))
            {
                if (!IsTraversable(snapshot, neighborId) || currentValue + 1 >= values[neighborId])
                {
                    continue;
                }

                values[neighborId] = currentValue + 1;
                Enqueue(neighborId, snapshot, queued, queue);
            }
        }
    }

    private static int[] CreateEmptyValues(int capacity)
    {
        var values = new int[capacity];
        Array.Fill(values, int.MaxValue);
        return values;
    }

    private static int ComputeValue(int tileId, BuildingBfsFieldSnapshot snapshot, bool[] seeded, int[] values)
    {
        if (!IsTraversable(snapshot, tileId))
        {
            return int.MaxValue;
        }

        if (seeded[tileId])
        {
            return 0;
        }

        var bestNeighbor = int.MaxValue;
        foreach (var neighborId in GetNeighbors(snapshot, tileId))
        {
            if (!IsTraversable(snapshot, neighborId))
            {
                continue;
            }

            var neighborValue = values[neighborId];
            if (neighborValue < bestNeighbor)
            {
                bestNeighbor = neighborValue;
            }
        }

        return bestNeighbor == int.MaxValue ? int.MaxValue : bestNeighbor + 1;
    }

    private static int[] BuildNextStepIds(BuildingBfsFieldSnapshot snapshot, int[] values)
    {
        var nextStepIds = new int[values.Length];
        Array.Fill(nextStepIds, -1);

        for (var tileId = 0; tileId < values.Length; tileId++)
        {
            if (!IsTraversable(snapshot, tileId) || values[tileId] == int.MaxValue || values[tileId] == 0)
            {
                continue;
            }

            var bestNeighborId = -1;
            var bestValue = values[tileId];
            foreach (var neighborId in GetNeighbors(snapshot, tileId))
            {
                if (!IsTraversable(snapshot, neighborId))
                {
                    continue;
                }

                var neighborValue = values[neighborId];
                if (neighborValue == int.MaxValue || neighborValue >= bestValue)
                {
                    continue;
                }

                if (bestNeighborId < 0 ||
                    neighborValue < bestValue ||
                    string.CompareOrdinal(snapshot.TileKeys[neighborId], snapshot.TileKeys[bestNeighborId]) < 0)
                {
                    bestNeighborId = neighborId;
                    bestValue = neighborValue;
                }
            }

            nextStepIds[tileId] = bestNeighborId;
        }

        return nextStepIds;
    }

    private static void Enqueue(int tileId, BuildingBfsFieldSnapshot snapshot, bool[] queued, Queue<int> queue)
    {
        if (!IsTraversable(snapshot, tileId) || queued[tileId])
        {
            return;
        }

        queued[tileId] = true;
        queue.Enqueue(tileId);
    }

    private static bool IsTraversable(BuildingBfsFieldSnapshot snapshot, int tileId)
    {
        return tileId >= 0 && tileId < snapshot.Traversable.Length && snapshot.Traversable[tileId];
    }

    private static int[] GetNeighbors(BuildingBfsFieldSnapshot snapshot, int tileId)
    {
        return tileId >= 0 && tileId < snapshot.Neighbors.Length
            ? snapshot.Neighbors[tileId] ?? []
            : [];
    }
}
