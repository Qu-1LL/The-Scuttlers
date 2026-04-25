using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Simulation;

public static class MineOrderPlanner
{
    public static IReadOnlyDictionary<Trilobite, IReadOnlyList<string>> BuildPlans(
        Cave cave,
        IReadOnlyList<Trilobite> miners,
        IReadOnlyList<string> selectedTileKeys)
    {
        var activeMiners = miners
            .Where(miner => miner.Cave == cave && string.Equals(miner.Assignment, "miner", StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        if (activeMiners.Length == 0)
        {
            return new Dictionary<Trilobite, IReadOnlyList<string>>();
        }

        var resolvedSelections = ResolveSelections(cave, selectedTileKeys);
        if (resolvedSelections.Count == 0)
        {
            return new Dictionary<Trilobite, IReadOnlyList<string>>();
        }

        var plans = activeMiners.ToDictionary(
            miner => miner,
            _ => new List<string>(),
            ReferenceEqualityComparer.Instance);

        if (activeMiners.Length == 1)
        {
            var miner = activeMiners[0];
            foreach (var selection in resolvedSelections
                         .OrderBy(selection => GridPoint.SquaredDistance(miner.Location, selection.TargetTile.Coordinates))
                         .ThenBy(selection => selection.RequestedKey, StringComparer.Ordinal))
            {
                plans[miner].Add(selection.RequestedKey);
            }

            return FinalizePlans(plans);
        }

        var remaining = new List<ResolvedMineSelection>(resolvedSelections);
        var cursors = activeMiners.ToDictionary(miner => miner, miner => miner.Location, ReferenceEqualityComparer.Instance);
        while (remaining.Count > 0)
        {
            var assignedThisRound = false;
            foreach (var miner in activeMiners)
            {
                if (remaining.Count == 0)
                {
                    break;
                }

                var cursor = cursors[miner];
                var bestIndex = -1;
                var bestDistance = int.MaxValue;
                string? bestKey = null;

                for (var index = 0; index < remaining.Count; index++)
                {
                    var candidate = remaining[index];
                    var distance = GridPoint.SquaredDistance(cursor, candidate.TargetTile.Coordinates);
                    if (bestIndex >= 0 &&
                        (distance > bestDistance ||
                         (distance == bestDistance && string.CompareOrdinal(candidate.RequestedKey, bestKey) >= 0)))
                    {
                        continue;
                    }

                    bestIndex = index;
                    bestDistance = distance;
                    bestKey = candidate.RequestedKey;
                }

                if (bestIndex < 0)
                {
                    continue;
                }

                var selected = remaining[bestIndex];
                remaining.RemoveAt(bestIndex);
                plans[miner].Add(selected.RequestedKey);
                cursors[miner] = selected.TargetTile.Coordinates;
                assignedThisRound = true;
            }

            if (!assignedThisRound)
            {
                break;
            }
        }

        return FinalizePlans(plans);
    }

    public static IReadOnlyList<ResolvedMineSelection> ResolveSelections(Cave cave, IReadOnlyList<string> selectedTileKeys)
    {
        var resolved = new List<ResolvedMineSelection>(selectedTileKeys.Count);
        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tileKey in selectedTileKeys)
        {
            var tile = cave.GetTile(tileKey);
            if (tile is null)
            {
                continue;
            }

            var resolvedTile = ResolveTarget(cave, tile);
            if (resolvedTile is null || !seenTargets.Add(resolvedTile.Key))
            {
                continue;
            }

            resolved.Add(new ResolvedMineSelection(tileKey, resolvedTile));
        }

        return resolved;
    }

    public static IReadOnlyList<Tile> ResolveTargets(Cave cave, IReadOnlyList<string> selectedTileKeys)
    {
        return ResolveSelections(cave, selectedTileKeys)
            .Select(selection => selection.TargetTile)
            .ToArray();
    }

    public static Tile? ResolveTarget(Cave cave, Tile tile)
    {
        if (cave.IsTileRevealed(tile) &&
            Building.IsMineableType(tile.Base) &&
            GetNavigationTarget(cave, tile) is not null)
        {
            return tile;
        }

        Tile? bestTile = null;
        var bestDistance = int.MaxValue;
        var requireRevealedTarget = !cave.IsTileRevealed(tile);
        foreach (var candidate in cave.GetTiles())
        {
            if (!Building.IsMineableType(candidate.Base) ||
                GetNavigationTarget(cave, candidate) is null ||
                (requireRevealedTarget && !cave.IsTileRevealed(candidate)))
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(tile.Coordinates, candidate.Coordinates);
            if (bestTile is not null &&
                (distance > bestDistance ||
                 (distance == bestDistance && string.CompareOrdinal(candidate.Key, bestTile.Key) >= 0)))
            {
                continue;
            }

            bestTile = candidate;
            bestDistance = distance;
        }

        return bestTile;
    }

    public static GridPoint? GetNavigationTarget(Cave cave, Tile tile)
    {
        if (!string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            return tile.CreatureFits() ? tile.Coordinates : null;
        }

        GridPoint? bestTarget = null;
        var bestDistance = int.MaxValue;
        foreach (var neighbor in tile.Neighbors)
        {
            if (!neighbor.CreatureFits())
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(tile.Coordinates, neighbor.Coordinates);
            if (bestTarget is not null &&
                (distance > bestDistance ||
                 (distance == bestDistance && string.CompareOrdinal(neighbor.Key, bestTarget.Value.ToString()) >= 0)))
            {
                continue;
            }

            bestTarget = neighbor.Coordinates;
            bestDistance = distance;
        }

        return bestTarget;
    }

    private static IReadOnlyDictionary<Trilobite, IReadOnlyList<string>> FinalizePlans(Dictionary<Trilobite, List<string>> plans)
    {
        return plans
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value,
                ReferenceEqualityComparer.Instance);
    }

    public readonly record struct ResolvedMineSelection(string RequestedKey, Tile TargetTile);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<Trilobite>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(Trilobite? x, Trilobite? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(Trilobite obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
