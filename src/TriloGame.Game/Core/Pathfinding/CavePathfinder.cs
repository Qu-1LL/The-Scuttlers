using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

internal static class CavePathfinder
{
    private static readonly Comparison<Tile> TileKeyComparison =
        static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key);

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

        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { startTile.Key };
        var cameFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        queue.Enqueue(startTile);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetNeighborsOrderedByKey(current))
            {
                if (!neighbor.CreatureFits() || !cave.IsTileReachable(neighbor) || !visited.Add(neighbor.Key))
                {
                    continue;
                }

                cameFrom[neighbor.Key] = current.Key;
                if (neighbor.Key == destinationTile.Key)
                {
                    return ReconstructDirectPath(cameFrom, startTile.Key, destinationTile.Key);
                }

                queue.Enqueue(neighbor);
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

        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { startTile.Key };
        var cameFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        queue.Enqueue(startTile);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Key != startTile.Key &&
                string.Equals(current.Base, "empty", StringComparison.Ordinal) &&
                current.Built is null &&
                current.Trilobites.Count == 0 &&
                current.EnemyOccupant is null)
            {
                return ReconstructDirectPath(cameFrom, startTile.Key, current.Key);
            }

            foreach (var neighbor in GetNeighborsOrderedByKey(current))
            {
                if (!neighbor.CreatureFits() || !cave.IsTileReachable(neighbor) || !visited.Add(neighbor.Key))
                {
                    continue;
                }

                cameFrom[neighbor.Key] = current.Key;
                queue.Enqueue(neighbor);
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
        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { startTile.Key };
        var cameFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        queue.Enqueue(startTile);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentResult = TryCreateMineablePathResult(
                current,
                post,
                mineableType,
                reservedTileKeys,
                cameFrom,
                startTile.Key);
            if (currentResult.HasValue)
            {
                return currentResult.Value;
            }

            foreach (var neighbor in GetNeighborsOrderedByKey(current))
            {
                if (!neighbor.CreatureFits() || !cave.IsTileReachable(neighbor) || !visited.Add(neighbor.Key))
                {
                    continue;
                }

                cameFrom[neighbor.Key] = current.Key;
                queue.Enqueue(neighbor);
            }
        }

        return null;
    }

    private static Cave.MineablePathResult? TryCreateMineablePathResult(
        Tile current,
        MiningPost post,
        string mineableType,
        ISet<string> reservedTileKeys,
        IReadOnlyDictionary<string, string> cameFrom,
        string startKey)
    {
        if (string.Equals(mineableType, "wall", StringComparison.Ordinal))
        {
            foreach (var neighbor in GetNeighborsOrderedByKey(current))
            {
                if (!string.Equals(neighbor.Base, "wall", StringComparison.Ordinal) ||
                    reservedTileKeys.Contains(neighbor.Key) ||
                    !post.IsLocationInArea(neighbor.Coordinates))
                {
                    continue;
                }

                return new Cave.MineablePathResult(
                    neighbor.Key,
                    current.Coordinates,
                    ReconstructDirectPath(cameFrom, startKey, current.Key));
            }

            return null;
        }

        if (!string.Equals(current.Base, mineableType, StringComparison.Ordinal) ||
            reservedTileKeys.Contains(current.Key) ||
            !post.IsLocationInArea(current.Coordinates))
        {
            return null;
        }

        return new Cave.MineablePathResult(
            current.Key,
            current.Coordinates,
            ReconstructDirectPath(cameFrom, startKey, current.Key));
    }

    private static Tile[] GetNeighborsOrderedByKey(Tile tile)
    {
        var neighbors = new Tile[tile.Neighbors.Count];
        var index = 0;
        foreach (var neighbor in tile.Neighbors)
        {
            neighbors[index++] = neighbor;
        }

        Array.Sort(neighbors, TileKeyComparison);
        return neighbors;
    }

    private static List<GridPoint> ReconstructDirectPath(
        IReadOnlyDictionary<string, string> cameFrom,
        string startKey,
        string destinationKey)
    {
        var path = new List<GridPoint>();
        string? currentKey = destinationKey;
        while (currentKey is not null)
        {
            path.Add(GridPoint.Parse(currentKey));
            currentKey = string.Equals(currentKey, startKey, StringComparison.Ordinal)
                ? null
                : cameFrom.GetValueOrDefault(currentKey);
        }

        path.Reverse();
        return path;
    }
}
