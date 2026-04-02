using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class AlgaeFarm : Building
{
    private readonly HashSet<Creature> _assignments = [];

    public AlgaeFarm(GameSession session)
        : base("Algae Farm", new GridPoint(2, 3), [[1, 1], [1, 1], [1, 1]], session, false)
    {
        TextureKey = "AlgaeFarm";
        Period = 30;
        Growth = 0;
        HarvestYield = 5;
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        Description = $"A passable algae farm. Worker trilobites harvest {HarvestYield} algae when random < growth/period.";
    }

    public int Period { get; }

    public int Growth { get; private set; }

    public int HarvestYield { get; }

    public void Assign(Creature creature) => _assignments.Add(creature);

    public void RemoveAssignment(Creature creature) => _assignments.Remove(creature);

    public int GetVolume() => _assignments.Count;

    public Dictionary<string, World.Tile> GetPassableTileMap()
    {
        return TileArray
            .Where(tile => tile.CreatureFits())
            .ToDictionary(tile => tile.Key, StringComparer.Ordinal);
    }

    public bool IsLocationOnFarm(GridPoint location)
    {
        return GetPassableTileMap().ContainsKey(location.ToString());
    }

    public GridPoint? GetApproachTile(GridPoint? startLocation)
    {
        var passableTiles = GetPassableTileMap().Values.ToArray();
        if (passableTiles.Length == 0)
        {
            return null;
        }

        var origin = startLocation ?? GridPoint.Parse(passableTiles[0].Key);
        var bestTile = passableTiles[0];
        var bestDistance = GridPoint.SquaredDistance(origin, GridPoint.Parse(bestTile.Key));

        foreach (var tile in passableTiles)
        {
            var distance = GridPoint.SquaredDistance(origin, GridPoint.Parse(tile.Key));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile;
            }
        }

        return GridPoint.Parse(bestTile.Key);
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

    public List<GridPoint> GetPath(GridPoint currentPositionOnFarm)
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

        return route.Select(GridPoint.Parse).ToList();
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
