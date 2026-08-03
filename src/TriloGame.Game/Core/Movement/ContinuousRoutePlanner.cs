using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Movement;

// Convert a coarse cell path into the shortest deterministic swept-circle corridor.
public static class ContinuousRoutePlanner
{
    public static List<WorldPoint> Build(
        Cave cave,
        Creature creature,
        IReadOnlyList<GridPoint> cellPath,
        WorldPoint? exactDestination = null,
        WorldPoint? originOverride = null)
    {
        var route = new List<WorldPoint>(Math.Max(1, cellPath.Count));
        if (cellPath.Count == 0)
        {
            return route;
        }

        var candidates = new WorldPoint[cellPath.Count];
        for (var index = 0; index < cellPath.Count; index++)
        {
            candidates[index] = WorldPoint.FromGridPoint(cellPath[index]);
        }

        if (exactDestination.HasValue)
        {
            candidates[^1] = exactDestination.Value;
        }

        var origin = originOverride ?? creature.Position;
        var nextIndex = cellPath.Count > 1 ? 1 : 0;
        while (nextIndex < candidates.Length)
        {
            var furthest = -1;
            for (var candidateIndex = candidates.Length - 1; candidateIndex > nextIndex; candidateIndex--)
            {
                if (!cave.HasClearStaticSweep(creature, origin, candidates[candidateIndex]))
                {
                    continue;
                }

                furthest = candidateIndex;
                break;
            }

            if (furthest < 0 &&
                cave.HasClearStaticSweep(creature, origin, candidates[nextIndex]))
            {
                furthest = nextIndex;
            }

            if (furthest < 0)
            {
                break;
            }

            route.Add(candidates[furthest]);
            origin = candidates[furthest];
            nextIndex = furthest + 1;
        }

        return route;
    }
}
