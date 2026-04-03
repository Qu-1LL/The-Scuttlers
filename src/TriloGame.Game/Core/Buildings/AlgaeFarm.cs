using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class AlgaeFarm : Building
{
    private const int DefaultAssignmentCapacity = 1;
    private readonly HashSet<Creature> _assignments = [];
    private Dictionary<string, World.Tile>? _passableTileMapCache;
    private List<string>? _harvestCycleCache;
    private Dictionary<string, string>? _nextHarvestStepByTileKey;

    public AlgaeFarm(GameSession session)
        : base("Algae Farm", new GridPoint(2, 3), [[1, 1], [1, 1], [1, 1]], session, false)
    {
        TextureKey = "AlgaeFarm";
        Period = 30;
        Growth = 0;
        HarvestYield = GameConstants.AlgaeHarvestYield;
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        Description = $"A passable algae farm. Worker trilobites harvest {HarvestYield} algae when random < growth/period.";
        AssignmentCapacity = DefaultAssignmentCapacity;
    }

    public int Period { get; }

    public int Growth { get; private set; }

    public int HarvestYield { get; }

    public int AssignmentCapacity { get; private set; }

    public bool Assign(Creature creature)
    {
        if (!CanAssign(creature))
        {
            return false;
        }

        _assignments.Add(creature);
        return true;
    }

    public void RemoveAssignment(Creature creature) => _assignments.Remove(creature);

    public int GetVolume() => _assignments.Count;

    public int GetAvailableAssignmentSlots()
    {
        return Math.Max(0, AssignmentCapacity - _assignments.Count);
    }

    public bool CanAssign(Creature creature)
    {
        return _assignments.Contains(creature) || _assignments.Count < AssignmentCapacity;
    }

    public void SetAssignmentCapacity(int capacity)
    {
        AssignmentCapacity = Math.Max(DefaultAssignmentCapacity, capacity);
    }

    public void IncreaseAssignmentCapacity(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        AssignmentCapacity += amount;
    }

    public Dictionary<string, World.Tile> GetPassableTileMap()
    {
        _passableTileMapCache ??= TileArray
            .Where(tile => tile.CreatureFits())
            .ToDictionary(tile => tile.Key, StringComparer.Ordinal);
        return _passableTileMapCache;
    }

    public bool IsLocationOnFarm(GridPoint location)
    {
        return GetPassableTileMap().ContainsKey(location.ToString());
    }

    public GridPoint? GetApproachTile(GridPoint? startLocation)
    {
        var passableTileMap = GetPassableTileMap();
        if (passableTileMap.Count == 0)
        {
            return null;
        }

        var passableTiles = passableTileMap.Values;
        var firstTile = passableTiles.First();
        var origin = startLocation ?? firstTile.Coordinates;
        var bestTile = firstTile;
        var bestDistance = GridPoint.SquaredDistance(origin, firstTile.Coordinates);

        foreach (var tile in passableTiles)
        {
            var distance = GridPoint.SquaredDistance(origin, tile.Coordinates);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile;
            }
        }

        return bestTile.Coordinates;
    }

    private List<string>? FindFarmPath(string startKey, string goalKey, Dictionary<string, World.Tile> passableTileMap)
    {
        if (!passableTileMap.ContainsKey(startKey) || !passableTileMap.ContainsKey(goalKey))
        {
            return null;
        }

        if (startKey == goalKey)
        {
            return [startKey];
        }

        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { startKey };
        var cameFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        queue.Enqueue(startKey);

        while (queue.Count > 0)
        {
            var currentKey = queue.Dequeue();
            if (currentKey == goalKey)
            {
                var path = new List<string>();
                string? key = goalKey;
                while (key is not null)
                {
                    path.Add(key);
                    key = cameFrom.GetValueOrDefault(key);
                }

                path.Reverse();
                return path;
            }

            var currentTile = passableTileMap[currentKey];
            foreach (var neighbor in currentTile.Neighbors)
            {
                if (!passableTileMap.ContainsKey(neighbor.Key) || !visited.Add(neighbor.Key))
                {
                    continue;
                }

                cameFrom[neighbor.Key] = currentKey;
                queue.Enqueue(neighbor.Key);
            }
        }

        return null;
    }

    private string? FindNextUnvisitedKey(string currentKey, HashSet<string> unvisitedKeys, Dictionary<string, World.Tile> passableTileMap)
    {
        string? bestKey = null;
        var bestLength = int.MaxValue;
        foreach (var candidateKey in unvisitedKeys)
        {
            var candidatePath = FindFarmPath(currentKey, candidateKey, passableTileMap);
            if (candidatePath is null)
            {
                continue;
            }

            if (candidatePath.Count < bestLength)
            {
                bestLength = candidatePath.Count;
                bestKey = candidateKey;
            }
        }

        return bestKey;
    }

    private List<string> BuildVisitCycle(string originKey, Dictionary<string, World.Tile> passableTileMap)
    {
        var route = new List<string> { originKey };
        var unvisited = new HashSet<string>(passableTileMap.Keys, StringComparer.Ordinal);
        unvisited.Remove(originKey);
        var currentKey = originKey;

        while (unvisited.Count > 0)
        {
            var nextKey = FindNextUnvisitedKey(currentKey, unvisited, passableTileMap);
            if (nextKey is null)
            {
                break;
            }

            var segment = FindFarmPath(currentKey, nextKey, passableTileMap);
            if (segment is null || segment.Count < 2)
            {
                unvisited.Remove(nextKey);
                continue;
            }

            foreach (var key in segment.Skip(1))
            {
                route.Add(key);
                unvisited.Remove(key);
            }

            currentKey = route[^1];
        }

        if (!string.Equals(currentKey, originKey, StringComparison.Ordinal))
        {
            var returnPath = FindFarmPath(currentKey, originKey, passableTileMap);
            if (returnPath is not null && returnPath.Count > 1)
            {
                route.AddRange(returnPath.Skip(1));
            }
        }

        return route;
    }

    private List<string> GetHarvestCycle(Dictionary<string, World.Tile> passableTileMap)
    {
        _harvestCycleCache ??= BuildVisitCycle(
            passableTileMap.Values
                .OrderBy(tile => tile.Coordinates.Y)
                .ThenBy(tile => tile.Coordinates.X)
                .Select(tile => tile.Key)
                .First(),
            passableTileMap);
        return _harvestCycleCache;
    }

    private Dictionary<string, string> GetNextHarvestStepMap(Dictionary<string, World.Tile> passableTileMap)
    {
        _nextHarvestStepByTileKey ??= BuildNextHarvestStepMap(GetHarvestCycle(passableTileMap));
        return _nextHarvestStepByTileKey;
    }

    private static Dictionary<string, string> BuildNextHarvestStepMap(IReadOnlyList<string> cycle)
    {
        var nextStepByTileKey = new Dictionary<string, string>(StringComparer.Ordinal);
        if (cycle.Count == 0)
        {
            return nextStepByTileKey;
        }

        if (cycle.Count == 1)
        {
            nextStepByTileKey[cycle[0]] = cycle[0];
            return nextStepByTileKey;
        }

        for (var index = 0; index < cycle.Count - 1; index++)
        {
            nextStepByTileKey[cycle[index]] = cycle[index + 1];
        }
        return nextStepByTileKey;
    }

    private static List<string> RotateClosedCycleToStart(List<string> cycle, string startKey)
    {
        if (cycle.Count == 0)
        {
            return [];
        }

        var uniqueCount = cycle.Count > 1 && string.Equals(cycle[0], cycle[^1], StringComparison.Ordinal)
            ? cycle.Count - 1
            : cycle.Count;
        var startIndex = cycle.Take(uniqueCount).ToList().FindIndex(key => string.Equals(key, startKey, StringComparison.Ordinal));
        if (startIndex < 0)
        {
            return cycle;
        }

        var rotated = new List<string>(uniqueCount + 1);
        for (var index = 0; index < uniqueCount; index++)
        {
            rotated.Add(cycle[(startIndex + index) % uniqueCount]);
        }

        rotated.Add(startKey);
        return rotated;
    }

    public bool TryGetNextHarvestStep(GridPoint currentPositionOnFarm, out GridPoint nextLocation)
    {
        var passableTileMap = GetPassableTileMap();
        if (passableTileMap.Count == 0)
        {
            nextLocation = default;
            return false;
        }

        var currentKey = currentPositionOnFarm.ToString();
        if (!passableTileMap.ContainsKey(currentKey))
        {
            nextLocation = default;
            return false;
        }

        var nextStepByTileKey = GetNextHarvestStepMap(passableTileMap);
        if (!nextStepByTileKey.TryGetValue(currentKey, out var nextKey) || !passableTileMap.TryGetValue(nextKey, out var nextTile))
        {
            nextLocation = default;
            return false;
        }

        nextLocation = nextTile.Coordinates;
        return true;
    }

    public List<GridPoint> GetPath(GridPoint currentPositionOnFarm, Creature? creature = null)
    {
        var passableTileMap = GetPassableTileMap();
        if (passableTileMap.Count == 0)
        {
            return [];
        }

        var originKey = currentPositionOnFarm.ToString();
        if (!passableTileMap.ContainsKey(originKey))
        {
            originKey = GetApproachTile(currentPositionOnFarm)?.ToString() ?? passableTileMap.Keys.First();
        }

        return RotateClosedCycleToStart(GetHarvestCycle(passableTileMap), originKey)
            .Select(key => passableTileMap[key].Coordinates)
            .ToList();
    }

    public bool TryHarvest(Trilobite creature)
    {
        Growth++;
        if (RandomUtil.NextDouble() >= ((double)Growth / Period))
        {
            return false;
        }

        var harvested = creature.AddToInventory("Algae", HarvestYield);
        if (harvested != HarvestYield)
        {
            return false;
        }

        Growth = 0;
        return true;
    }
}
