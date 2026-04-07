using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class AlgaeFarm : Building
{
    private sealed class TraversalNode
    {
        public TraversalNode(GridPoint location)
        {
            Location = location;
        }

        public GridPoint Location { get; }

        public TraversalNode? Next { get; set; }
    }

    private static readonly int[][] DefaultTraversalPath = [[6, 1], [5, 2], [4, 3]];
    private readonly HashSet<Creature> _assignments = [];
    private readonly Dictionary<string, TraversalNode> _traversalNodes = new(StringComparer.Ordinal);
    private TraversalNode? _traversalHead;

    public AlgaeFarm(GameSession session)
        : base("Algae Farm", new GridPoint(2, 3), [[1, 1], [1, 1], [1, 1]], session, false)
    {
        TextureKey = "AlgaeFarm";
        Period = 30;
        Growth = 0;
        HarvestYield = 5;
        MaxTrilobites = 2;
        TraversalPath = CloneOpenMap(DefaultTraversalPath);
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        Description = $"A passable algae farm. Up to {MaxTrilobites} worker trilobites harvest {HarvestYield} algae when random < growth/period.";
    }

    public int Period { get; }

    public int Growth { get; private set; }

    public int HarvestYield { get; }

    public int MaxTrilobites { get; }

    public int[][] TraversalPath { get; private set; }

    public IReadOnlyCollection<Creature> Assignments => _assignments;

    public override int[][] RotateMap()
    {
        TraversalPath = RotateTraversalPath(TraversalPath, Size);
        return base.RotateMap();
    }

    public override void OnBuilt(World.Cave cave)
    {
        RebuildTraversalRing();
    }

    public bool HasAssignmentSlot(Creature? creature = null)
    {
        return (creature is not null && _assignments.Contains(creature)) || _assignments.Count < MaxTrilobites;
    }

    public bool Assign(Creature creature)
    {
        if (!HasAssignmentSlot(creature))
        {
            Cave?.RefreshOpenAlgaeFarmAvailability();
            return false;
        }

        var added = _assignments.Add(creature);
        Cave?.RefreshOpenAlgaeFarmAvailability();
        return added || _assignments.Contains(creature);
    }

    public bool RemoveAssignment(Creature creature)
    {
        var removed = _assignments.Remove(creature);
        Cave?.RefreshOpenAlgaeFarmAvailability();
        return removed;
    }

    public int GetVolume() => _assignments.Count;

    public bool IsLocationOnFarm(GridPoint location)
    {
        return _traversalNodes.ContainsKey(location.ToString());
    }

    public GridPoint? GetApproachTile(GridPoint? startLocation)
    {
        var passableTiles = TileArray
            .Where(tile => tile.CreatureFits())
            .Select(tile => tile.Coordinates)
            .ToArray();
        if (passableTiles.Length == 0)
        {
            return null;
        }

        var origin = startLocation ?? passableTiles[0];
        var bestTile = passableTiles[0];
        var bestDistance = GridPoint.SquaredDistance(origin, bestTile);

        foreach (var tile in passableTiles)
        {
            var distance = GridPoint.SquaredDistance(origin, tile);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile;
            }
        }

        return bestTile;
    }

    internal GridPoint? GetNextTraversalLocation(GridPoint currentLocation)
    {
        return _traversalNodes.TryGetValue(currentLocation.ToString(), out var node)
            ? node.Next?.Location
            : null;
    }

    internal GridPoint? GetTraversalStartLocation() => _traversalHead?.Location;

    private void RebuildTraversalRing()
    {
        _traversalNodes.Clear();
        _traversalHead = null;

        if (Location is null || TileArray.Count == 0)
        {
            return;
        }

        var passableTiles = TileArray
            .Where(tile => tile.CreatureFits())
            .ToDictionary(tile => tile.Key, tile => tile.Coordinates, StringComparer.Ordinal);
        if (passableTiles.Count == 0)
        {
            return;
        }

        var orderedLocations = new List<(int Order, GridPoint Location)>();
        for (var y = 0; y < TraversalPath.Length; y++)
        {
            for (var x = 0; x < TraversalPath[y].Length; x++)
            {
                var order = TraversalPath[y][x];
                if (order <= 0)
                {
                    continue;
                }

                var location = new GridPoint(Location.Value.X + x, Location.Value.Y + y);
                if (!passableTiles.ContainsKey(location.ToString()))
                {
                    continue;
                }

                orderedLocations.Add((order, location));
            }
        }

        // Traversal order is defined entirely by the numbered path map, so once the
        // farm is built each farmer can advance to its next tile in O(1).
        var orderedNodes = orderedLocations
            .OrderBy(entry => entry.Order)
            .Select(entry => new TraversalNode(entry.Location))
            .ToArray();
        if (orderedNodes.Length == 0)
        {
            return;
        }

        for (var index = 0; index < orderedNodes.Length; index++)
        {
            var node = orderedNodes[index];
            node.Next = orderedNodes[(index + 1) % orderedNodes.Length];
            _traversalNodes[node.Location.ToString()] = node;
        }

        _traversalHead = orderedNodes[0];
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

    private static int[][] RotateTraversalPath(int[][] traversalPath, GridPoint size)
    {
        var rotated = new int[size.X][];
        for (var column = 0; column < size.X; column++)
        {
            rotated[column] = new int[size.Y];
            var targetIndex = 0;
            for (var row = size.Y - 1; row >= 0; row--)
            {
                rotated[column][targetIndex] = traversalPath[row][column];
                targetIndex++;
            }
        }

        return rotated;
    }
}
