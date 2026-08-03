using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

// Deterministic fixed-size cache for point-route fields shared by same-destination requests.
internal sealed class PointRouteFieldCache
{
    private const int MaximumEntries = 64;
    private readonly Cave _cave;
    private readonly List<Entry> _entries = new(MaximumEntries);
    private long _nextAccessStamp;

    public PointRouteFieldCache(Cave cave)
    {
        _cave = cave;
    }

    public int Count => _entries.Count;

    public bool TryBuildPath(
        GridPoint startLocation,
        GridPoint destination,
        out List<GridPoint>? path,
        out bool deferred)
    {
        return TryBuildPath(startLocation, destination, int.MaxValue, out path, out deferred, out _);
    }

    public bool TryBuildPathChunk(
        GridPoint startLocation,
        GridPoint destination,
        int maximumSteps,
        out List<GridPoint>? path,
        out bool deferred,
        out bool reachedDestination)
    {
        return TryBuildPath(startLocation, destination, maximumSteps, out path, out deferred, out reachedDestination);
    }

    private bool TryBuildPath(
        GridPoint startLocation,
        GridPoint destination,
        int maximumSteps,
        out List<GridPoint>? path,
        out bool deferred,
        out bool reachedDestination)
    {
        path = null;
        deferred = false;
        reachedDestination = false;

        var destinationTile = _cave.GetTile(destination);
        if (destinationTile is null || !destinationTile.CreatureFits() || !_cave.IsTileReachable(destinationTile))
        {
            return true;
        }

        if (TryGet(destinationTile.Id, out var entry))
        {
            path = maximumSteps == int.MaxValue
                ? entry.Field.BuildPathFrom(startLocation)
                : entry.Field.BuildPathChunkFrom(startLocation, maximumSteps, out reachedDestination);
            if (maximumSteps == int.MaxValue)
            {
                reachedDestination = path is not null;
            }

            return true;
        }

        if (!_cave.TryConsumePointRouteBuildBudget())
        {
            deferred = true;
            return false;
        }

        var field = PointRouteField.BuildFrom(_cave, destinationTile);
        Add(destinationTile.Id, field);
        path = maximumSteps == int.MaxValue
            ? field.BuildPathFrom(startLocation)
            : field.BuildPathChunkFrom(startLocation, maximumSteps, out reachedDestination);
        if (maximumSteps == int.MaxValue)
        {
            reachedDestination = path is not null;
        }

        return true;
    }

    private bool TryGet(int destinationId, out Entry entry)
    {
        var topologyVersion = _cave.TopologyVersion;
        var reachabilityVersion = _cave.ReachabilityVersion;
        for (var index = 0; index < _entries.Count; index++)
        {
            entry = _entries[index];
            if (entry.DestinationId != destinationId ||
                entry.TopologyVersion != topologyVersion ||
                entry.ReachabilityVersion != reachabilityVersion)
            {
                continue;
            }

            entry.AccessStamp = NextAccessStamp();
            _entries[index] = entry;
            return true;
        }

        entry = default;
        return false;
    }

    private void Add(int destinationId, PointRouteField field)
    {
        var entry = new Entry(
            destinationId,
            field.TopologyVersion,
            field.ReachabilityVersion,
            NextAccessStamp(),
            field);

        if (_entries.Count < MaximumEntries)
        {
            _entries.Add(entry);
            return;
        }

        var evictionIndex = 0;
        var oldestStamp = _entries[0].AccessStamp;
        for (var index = 1; index < _entries.Count; index++)
        {
            if (_entries[index].AccessStamp >= oldestStamp)
            {
                continue;
            }

            oldestStamp = _entries[index].AccessStamp;
            evictionIndex = index;
        }

        _entries[evictionIndex] = entry;
    }

    private long NextAccessStamp() => ++_nextAccessStamp;

    private record struct Entry(
        int DestinationId,
        long TopologyVersion,
        long ReachabilityVersion,
        long AccessStamp,
        PointRouteField Field);
}
