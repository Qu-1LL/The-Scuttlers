using System.Diagnostics;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

// Array-backed reverse field for many creatures navigating to the same point.
internal sealed class PointRouteField
{
    private readonly Cave _cave;
    private readonly int[] _values;
    private readonly int[] _nextStepIds;
    private readonly bool[] _queued;
    private readonly Queue<int> _queue = [];

    private PointRouteField(Cave cave, Tile destination, long topologyVersion, long reachabilityVersion)
    {
        _cave = cave;
        DestinationId = destination.Id;
        TopologyVersion = topologyVersion;
        ReachabilityVersion = reachabilityVersion;
        _values = new int[cave.TileCapacity];
        _nextStepIds = new int[cave.TileCapacity];
        _queued = new bool[cave.TileCapacity];
        Array.Fill(_values, int.MaxValue);
        Array.Fill(_nextStepIds, -1);
        Build(destination);
    }

    public int DestinationId { get; }

    public long TopologyVersion { get; }

    public long ReachabilityVersion { get; }

    public static PointRouteField BuildFrom(Cave cave, Tile destination)
    {
        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var timerStart = Stopwatch.GetTimestamp();
        try
        {
            return new PointRouteField(cave, destination, cave.TopologyVersion, cave.ReachabilityVersion);
        }
        finally
        {
            NavigationInstrumentation.RecordBuildPointBfsField(
                Stopwatch.GetElapsedTime(timerStart).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        }
    }

    public List<GridPoint>? BuildPathFrom(GridPoint startLocation)
    {
        return BuildPathFrom(startLocation, int.MaxValue, out _);
    }

    public List<GridPoint>? BuildPathChunkFrom(GridPoint startLocation, int maximumSteps, out bool reachedDestination)
    {
        return BuildPathFrom(startLocation, maximumSteps, out reachedDestination);
    }

    private List<GridPoint>? BuildPathFrom(GridPoint startLocation, int maximumSteps, out bool reachedDestination)
    {
        reachedDestination = false;
        var startTile = _cave.GetTile(startLocation);
        if (startTile is null ||
            (uint)startTile.Id >= (uint)_values.Length ||
            !startTile.CreatureFits() ||
            !_cave.IsTileReachable(startTile))
        {
            return null;
        }

        var startValue = _values[startTile.Id];
        if (startValue == int.MaxValue)
        {
            return null;
        }

        var cappedSteps = Math.Max(0, Math.Min(maximumSteps, startValue));
        var path = new List<GridPoint>(cappedSteps + 1) { startLocation };
        var currentId = startTile.Id;
        var currentValue = startValue;
        var guard = 0;

        while (currentValue > 0 && guard < cappedSteps && guard++ < _values.Length)
        {
            var nextId = _nextStepIds[currentId];
            var nextTile = _cave.GetTileById(nextId);
            if (nextTile is null || (uint)nextId >= (uint)_values.Length)
            {
                return null;
            }

            path.Add(nextTile.Coordinates);
            currentId = nextId;
            currentValue = _values[currentId];
        }

        reachedDestination = currentValue == 0;
        return currentValue == 0 || path.Count > 1 ? path : null;
    }

    private void Build(Tile destination)
    {
        _values[destination.Id] = 0;
        Enqueue(destination.Id);

        while (_queue.Count > 0)
        {
            var currentId = _queue.Dequeue();
            _queued[currentId] = false;

            var currentTile = _cave.GetTileById(currentId);
            if (currentTile is null)
            {
                continue;
            }

            var currentValue = _values[currentId];
            if (currentValue == int.MaxValue)
            {
                continue;
            }

            foreach (var neighbor in currentTile.Neighbors)
            {
                if (!CanUseTile(neighbor))
                {
                    continue;
                }

                var nextValue = currentValue + 1;
                if (nextValue >= _values[neighbor.Id])
                {
                    continue;
                }

                _values[neighbor.Id] = nextValue;
                Enqueue(neighbor.Id);
            }
        }

        RebuildNextStepCache();
    }

    private void RebuildNextStepCache()
    {
        foreach (var tile in _cave.GetTiles())
        {
            if ((uint)tile.Id >= (uint)_values.Length || !CanUseTile(tile))
            {
                continue;
            }

            var currentValue = _values[tile.Id];
            if (currentValue == int.MaxValue || currentValue == 0)
            {
                continue;
            }

            Tile? bestNeighbor = null;
            var bestValue = currentValue;
            foreach (var neighbor in tile.Neighbors)
            {
                if (!CanUseTile(neighbor))
                {
                    continue;
                }

                var neighborValue = _values[neighbor.Id];
                if (neighborValue == int.MaxValue || neighborValue >= bestValue)
                {
                    continue;
                }

                if (bestNeighbor is null ||
                    neighborValue < bestValue ||
                    string.CompareOrdinal(neighbor.Key, bestNeighbor.Key) < 0)
                {
                    bestNeighbor = neighbor;
                    bestValue = neighborValue;
                }
            }

            _nextStepIds[tile.Id] = bestNeighbor?.Id ?? -1;
        }
    }

    private bool CanUseTile(Tile tile)
    {
        return (uint)tile.Id < (uint)_values.Length &&
               tile.CreatureFits() &&
               _cave.IsTileReachable(tile);
    }

    private void Enqueue(int tileId)
    {
        if (_queued[tileId])
        {
            return;
        }

        _queued[tileId] = true;
        _queue.Enqueue(tileId);
    }
}
