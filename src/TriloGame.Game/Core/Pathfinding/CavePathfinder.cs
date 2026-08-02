using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

internal static class CavePathfinder
{
    public static List<GridPoint>? BuildDirectPathToPoint(Cave cave, GridPoint startLocation, GridPoint destination)
    {
        var startTile = cave.GetTile(startLocation);
        var destinationTile = cave.GetTile(destination);
        if (startTile is null || destinationTile is null ||
            !startTile.CreatureFits() || !destinationTile.CreatureFits() ||
            !cave.IsTileReachable(startTile) || !cave.IsTileReachable(destinationTile))
        {
            return null;
        }

        if (startTile.Key == destinationTile.Key)
        {
            return [startLocation];
        }

        var previousIds = CreatePreviousMap(cave);
        var queue = new Queue<int>();
        previousIds[startTile.Id] = startTile.Id;
        queue.Enqueue(startTile.Id);

        while (queue.Count > 0)
        {
            var current = cave.GetTileById(queue.Dequeue());
            if (current is null)
            {
                continue;
            }

            string? lastNeighborKey = null;
            while (TryGetNextNeighborByKey(current, lastNeighborKey, out var neighbor))
            {
                lastNeighborKey = neighbor.Key;
                if (!CanUsePathTile(cave, neighbor) || previousIds[neighbor.Id] >= 0)
                {
                    continue;
                }

                previousIds[neighbor.Id] = current.Id;
                if (neighbor.Id == destinationTile.Id)
                {
                    return ReconstructPath(cave, previousIds, startTile.Id, destinationTile.Id);
                }

                queue.Enqueue(neighbor.Id);
            }
        }

        return null;
    }

    public static List<GridPoint>? BuildPathToNearestEmptyTile(Cave cave, GridPoint startLocation)
    {
        var startTile = cave.GetTile(startLocation);
        if (startTile is null || !startTile.CreatureFits() || !cave.IsTileReachable(startTile))
        {
            return null;
        }

        var previousIds = CreatePreviousMap(cave);
        var queue = new Queue<int>();
        previousIds[startTile.Id] = startTile.Id;
        queue.Enqueue(startTile.Id);

        while (queue.Count > 0)
        {
            var current = cave.GetTileById(queue.Dequeue());
            if (current is null)
            {
                continue;
            }

            if (current.Key != startTile.Key &&
                string.Equals(current.Base, "empty", StringComparison.Ordinal) &&
                current.Built is null &&
                !cave.HasCreatureInCell(current.Coordinates))
            {
                return ReconstructPath(cave, previousIds, startTile.Id, current.Id);
            }

            string? lastNeighborKey = null;
            while (TryGetNextNeighborByKey(current, lastNeighborKey, out var neighbor))
            {
                lastNeighborKey = neighbor.Key;
                if (!CanUsePathTile(cave, neighbor) || previousIds[neighbor.Id] >= 0)
                {
                    continue;
                }

                previousIds[neighbor.Id] = current.Id;
                queue.Enqueue(neighbor.Id);
            }
        }

        return null;
    }

    public static Cave.MineablePathResult? BuildPathToNearestMineableType(
        Cave cave,
        GridPoint startLocation,
        MiningPost post,
        string mineableType,
        ISet<string>? reservedTileKeys = null)
    {
        var startTile = cave.GetTile(startLocation);
        if (startTile is null || !startTile.CreatureFits() || !cave.IsTileReachable(startTile))
        {
            return null;
        }

        reservedTileKeys ??= new HashSet<string>(StringComparer.Ordinal);
        var previousIds = CreatePreviousMap(cave);
        var queue = new Queue<int>();
        previousIds[startTile.Id] = startTile.Id;
        queue.Enqueue(startTile.Id);

        while (queue.Count > 0)
        {
            var current = cave.GetTileById(queue.Dequeue());
            if (current is null)
            {
                continue;
            }

            var currentResult = TryCreateMineablePathResult(
                cave,
                current,
                post,
                mineableType,
                reservedTileKeys,
                previousIds,
                startTile.Id);
            if (currentResult.HasValue)
            {
                return currentResult.Value;
            }

            string? lastNeighborKey = null;
            while (TryGetNextNeighborByKey(current, lastNeighborKey, out var neighbor))
            {
                lastNeighborKey = neighbor.Key;
                if (!CanUsePathTile(cave, neighbor) || previousIds[neighbor.Id] >= 0)
                {
                    continue;
                }

                previousIds[neighbor.Id] = current.Id;
                queue.Enqueue(neighbor.Id);
            }
        }

        return null;
    }

    // Search the post target index once and retain the route that selected the mineable.
    public static Cave.MineablePathResult? BuildPathToNearestTrackedMineableApproach(
        Cave cave,
        Trilobite miner,
        MiningPost post,
        ResourceName? requiredResource)
    {
        var startTile = cave.GetTile(miner.Location);
        if (startTile is null ||
            !cave.CanCreatureTraverseTile(miner, startTile) ||
            !cave.IsTileReachable(startTile))
        {
            return null;
        }

        var preferUnassignedTarget = post.HasUnassignedTrackedMineableTarget(cave, miner, requiredResource);
        var search = cave.PathSearchWorkspace;
        search.Begin(cave.TileCapacity);
        search.AddStart(startTile.Id);
        Tile? sharedFallbackTarget = null;
        var sharedFallbackApproachId = -1;

        while (search.TryDequeue(out var currentId))
        {
            var current = cave.GetTileById(currentId);
            if (current is null)
            {
                continue;
            }

            if (TryGetTrackedMineableAtApproach(post, miner, current, requiredResource, preferUnassignedTarget, out var target) &&
                cave.CanCreatureOccupyWorldPosition(miner, WorldPoint.FromGridPoint(current.Coordinates)))
            {
                if (!preferUnassignedTarget || !post.IsTargetAssignedToOther(miner, target.Key))
                {
                    var path = ReconstructPath(cave, search, startTile.Id, current.Id);
                    return path is null
                        ? null
                        : new Cave.MineablePathResult(target.Key, current.Coordinates, path);
                }

                if (sharedFallbackTarget is null)
                {
                    sharedFallbackTarget = target;
                    sharedFallbackApproachId = current.Id;
                }
            }

            string? lastNeighborKey = null;
            while (TryGetNextNeighborByKey(current, lastNeighborKey, out var neighbor))
            {
                lastNeighborKey = neighbor.Key;
                if (!CanUsePathTile(cave, miner, neighbor) || search.WasVisited(neighbor.Id))
                {
                    continue;
                }

                search.Visit(neighbor.Id, current.Id);
            }
        }

        if (sharedFallbackTarget is null)
        {
            return null;
        }

        var sharedPath = ReconstructPath(cave, search, startTile.Id, sharedFallbackApproachId);
        return sharedPath is null
            ? null
            : new Cave.MineablePathResult(sharedFallbackTarget.Key, sharedPath[^1], sharedPath);
    }

    private static bool TryGetTrackedMineableAtApproach(
        MiningPost post,
        Trilobite miner,
        Tile approachTile,
        ResourceName? requiredResource,
        bool preferUnassignedTarget,
        out Tile target)
    {
        Tile? sharedFallbackTarget = null;
        string? lastNeighborKey = null;
        while (TryGetNextNeighborByKey(approachTile, lastNeighborKey, out var neighbor))
        {
            lastNeighborKey = neighbor.Key;
            if (!post.IsTrackedMineableTarget(neighbor, requiredResource))
            {
                continue;
            }

            if (!preferUnassignedTarget || !post.IsTargetAssignedToOther(miner, neighbor.Key))
            {
                target = neighbor;
                return true;
            }

            sharedFallbackTarget ??= neighbor;
        }

        target = sharedFallbackTarget!;
        return sharedFallbackTarget is not null;
    }

    // A mining claim owns the exact route that made its target reachable.
    public static List<GridPoint>? BuildPathToMineableApproach(Cave cave, Trilobite miner, Tile target)
    {
        var startTile = cave.GetTile(miner.Location);
        if (startTile is null ||
            !cave.CanCreatureTraverseTile(miner, startTile) ||
            !cave.IsTileReachable(startTile) ||
            !Building.IsMineableType(target.Base))
        {
            return null;
        }

        var search = cave.PathSearchWorkspace;
        search.Begin(cave.TileCapacity);
        search.AddStart(startTile.Id);

        while (search.TryDequeue(out var currentId))
        {
            var current = cave.GetTileById(currentId);
            if (current is null)
            {
                continue;
            }

            if (IsMineableApproach(cave, miner, target, current))
            {
                return ReconstructPath(cave, search, startTile.Id, current.Id);
            }

            string? lastNeighborKey = null;
            while (TryGetNextNeighborByKey(current, lastNeighborKey, out var neighbor))
            {
                lastNeighborKey = neighbor.Key;
                if (!CanUsePathTile(cave, miner, neighbor) || search.WasVisited(neighbor.Id))
                {
                    continue;
                }

                search.Visit(neighbor.Id, current.Id);
            }
        }

        return null;
    }

    private static bool IsMineableApproach(Cave cave, Trilobite miner, Tile target, Tile approachTile)
    {
        foreach (var neighbor in target.Neighbors)
        {
            if (neighbor.Id == approachTile.Id)
            {
                return cave.CanCreatureOccupyWorldPosition(miner, WorldPoint.FromGridPoint(approachTile.Coordinates));
            }
        }

        return false;
    }

    private static bool CanUsePathTile(Cave cave, Creature creature, Tile tile)
    {
        return cave.CanCreatureTraverseTile(creature, tile) && cave.IsTileReachable(tile);
    }

    private static Cave.MineablePathResult? TryCreateMineablePathResult(
        Cave cave,
        Tile current,
        MiningPost post,
        string mineableType,
        ISet<string> reservedTileKeys,
        IReadOnlyList<int> previousIds,
        int startId)
    {
        if (string.Equals(mineableType, "wall", StringComparison.Ordinal))
        {
            string? lastNeighborKey = null;
            while (TryGetNextNeighborByKey(current, lastNeighborKey, out var neighbor))
            {
                lastNeighborKey = neighbor.Key;
                if (!string.Equals(neighbor.Base, "wall", StringComparison.Ordinal) ||
                    reservedTileKeys.Contains(neighbor.Key) ||
                    !post.IsLocationInArea(neighbor.Coordinates))
                {
                    continue;
                }

                var path = ReconstructPath(cave, previousIds, startId, current.Id);
                return path is null
                    ? null
                    : new Cave.MineablePathResult(neighbor.Key, current.Coordinates, path);
            }

            return null;
        }

        if (!string.Equals(current.Base, mineableType, StringComparison.Ordinal) ||
            reservedTileKeys.Contains(current.Key) ||
            !post.IsLocationInArea(current.Coordinates))
        {
            return null;
        }

        var directPath = ReconstructPath(cave, previousIds, startId, current.Id);
        return directPath is null
            ? null
            : new Cave.MineablePathResult(current.Key, current.Coordinates, directPath);
    }

    private static int[] CreatePreviousMap(Cave cave)
    {
        var previousIds = new int[cave.TileCapacity];
        Array.Fill(previousIds, -1);
        return previousIds;
    }

    private static bool CanUsePathTile(Cave cave, Tile tile)
    {
        return tile.CreatureFits() && cave.IsTileReachable(tile);
    }

    private static List<GridPoint>? ReconstructPath(
        Cave cave,
        TraversalSearchWorkspace search,
        int startId,
        int destinationId)
    {
        if ((uint)startId >= (uint)cave.TileCapacity ||
            (uint)destinationId >= (uint)cave.TileCapacity ||
            !search.WasVisited(destinationId))
        {
            return null;
        }

        var path = new List<GridPoint>();
        var currentId = destinationId;
        var guard = 0;
        while (guard++ < cave.TileCapacity)
        {
            var tile = cave.GetTileById(currentId);
            if (tile is null)
            {
                return null;
            }

            path.Add(tile.Coordinates);
            if (currentId == startId)
            {
                path.Reverse();
                return path;
            }

            currentId = search.GetPreviousId(currentId);
        }

        return null;
    }
    private static List<GridPoint>? ReconstructPath(
        Cave cave,
        IReadOnlyList<int> previousIds,
        int startId,
        int destinationId)
    {
        if ((uint)startId >= (uint)previousIds.Count ||
            (uint)destinationId >= (uint)previousIds.Count ||
            previousIds[destinationId] < 0)
        {
            return null;
        }

        var path = new List<GridPoint>();
        var currentId = destinationId;
        var guard = 0;
        while (guard++ < previousIds.Count)
        {
            var tile = cave.GetTileById(currentId);
            if (tile is null)
            {
                return null;
            }

            path.Add(tile.Coordinates);
            if (currentId == startId)
            {
                path.Reverse();
                return path;
            }

            currentId = previousIds[currentId];
            if (currentId < 0)
            {
                return null;
            }
        }

        return null;
    }

    private static bool TryGetNextNeighborByKey(Tile tile, string? lastKey, out Tile neighbor)
    {
        Tile? next = null;
        foreach (var candidate in tile.Neighbors)
        {
            if (lastKey is not null &&
                string.CompareOrdinal(candidate.Key, lastKey) <= 0)
            {
                continue;
            }

            if (next is null ||
                string.CompareOrdinal(candidate.Key, next.Key) < 0)
            {
                next = candidate;
            }
        }

        if (next is null)
        {
            neighbor = null!;
            return false;
        }

        neighbor = next;
        return true;
    }
}
