using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Pathfinding;
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
    private readonly List<GridPoint> _traversalCycle = [];
    private Dictionary<string, World.Tile>? _passableTileMapCache;
    private TraversalNode? _traversalHead;

    public AlgaeFarm(GameSession session)
        : base("Algae Farm", new GridPoint(2, 3), [[1, 1], [1, 1], [1, 1]], session, true)
    {
        TextureKey = "AlgaeFarm";
        Period = 30;
        Growth = 0;
        HarvestYield = GameConstants.AlgaeHarvestYield;
        MaxTrilobites = 2;
        TraversalPath = CloneOpenMap(DefaultTraversalPath);
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Description = $"A passable algae farm. Up to {MaxTrilobites} worker trilobites harvest {HarvestYield} algae when random < growth/period.";
    }

    public int Period { get; }

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Asynchronous;

    public int Growth { get; private set; }

    public int HarvestYield { get; }

    public int MaxTrilobites { get; private set; }

    public int AssignmentCapacity => MaxTrilobites;

    public int FarmerAssignmentPriority => 0;

    public int[][] TraversalPath { get; private set; }

    public IReadOnlyCollection<Creature> Assignments => _assignments;

    public override int[][] RotateMap()
    {
        TraversalPath = RotateTraversalPath(TraversalPath, Size);
        InvalidateTraversalCaches();
        return base.RotateMap();
    }

    public override void OnBuilt(World.Cave cave)
    {
        base.OnBuilt(cave);
        RebuildTraversalRing();
    }

    public bool HasAssignmentSlot(Creature? creature = null)
    {
        return (creature is not null && _assignments.Contains(creature)) || _assignments.Count < MaxTrilobites;
    }

    public bool CanAssign(Creature creature)
    {
        return HasAssignmentSlot(creature);
    }

    public bool Assign(Creature creature)
    {
        if (!HasAssignmentSlot(creature))
        {
            Cave?.RefreshOpenAlgaeFarmAvailability();
            return false;
        }

        var added = _assignments.Add(creature);
        if (added)
        {
            TrackCreature(creature);
        }

        Cave?.RefreshOpenAlgaeFarmAvailability();
        return added || _assignments.Contains(creature);
    }

    public bool RemoveAssignment(Creature creature)
    {
        var removed = _assignments.Remove(creature);
        if (removed)
        {
            UntrackCreature(creature);
        }

        Cave?.RefreshOpenAlgaeFarmAvailability();
        return removed;
    }

    public override void TrackedCreatureDied(Creature creature)
    {
        RemoveAssignment(creature);
    }

    public int GetVolume() => _assignments.Count;

    public int GetAvailableAssignmentSlots()
    {
        return Math.Max(0, MaxTrilobites - _assignments.Count);
    }

    public void SetAssignmentCapacity(int capacity)
    {
        MaxTrilobites = Math.Max(1, capacity);
        Cave?.RefreshOpenAlgaeFarmAvailability();
    }

    public void IncreaseAssignmentCapacity(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        MaxTrilobites += amount;
        Cave?.RefreshOpenAlgaeFarmAvailability();
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
        return _traversalNodes.ContainsKey(location.ToString());
    }

    public GridPoint? GetApproachTile(GridPoint? startLocation)
    {
        var passableTiles = GetPassableTileMap().Values;
        using var enumerator = passableTiles.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var bestTile = enumerator.Current;
        var origin = startLocation ?? bestTile.Coordinates;
        var bestDistance = GridPoint.SquaredDistance(origin, bestTile.Coordinates);

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

    internal GridPoint? GetNextTraversalLocation(GridPoint currentLocation)
    {
        return _traversalNodes.TryGetValue(currentLocation.ToString(), out var node)
            ? node.Next?.Location
            : null;
    }

    internal GridPoint? GetTraversalStartLocation() => _traversalHead?.Location;

    public bool TryGetNextHarvestStep(GridPoint currentPositionOnFarm, out GridPoint nextLocation)
    {
        var next = GetNextTraversalLocation(currentPositionOnFarm);
        if (next is null)
        {
            nextLocation = default;
            return false;
        }

        nextLocation = next.Value;
        return true;
    }

    public List<GridPoint> GetPath(GridPoint currentPositionOnFarm, Creature? creature = null)
    {
        if (_traversalCycle.Count == 0)
        {
            return [];
        }

        var start = IsLocationOnFarm(currentPositionOnFarm)
            ? currentPositionOnFarm
            : GetApproachTile(currentPositionOnFarm) ?? _traversalCycle[0];
        var startIndex = _traversalCycle.FindIndex(location => location == start);
        if (startIndex < 0)
        {
            startIndex = 0;
            start = _traversalCycle[0];
        }

        var path = new List<GridPoint>(_traversalCycle.Count + 1);
        for (var offset = 0; offset < _traversalCycle.Count; offset++)
        {
            path.Add(_traversalCycle[(startIndex + offset) % _traversalCycle.Count]);
        }

        path.Add(start);
        return path;
    }

    private void InvalidateTraversalCaches()
    {
        _passableTileMapCache = null;
        _traversalNodes.Clear();
        _traversalCycle.Clear();
        _traversalHead = null;
    }

    private void RebuildTraversalRing()
    {
        InvalidateTraversalCaches();

        if (Location is null || TileArray.Count == 0)
        {
            return;
        }

        var passableTiles = GetPassableTileMap();
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
            _traversalCycle.Add(node.Location);
        }

        _traversalHead = orderedNodes[0];
    }

    public bool TryHarvest(IInventoryCarrier creature)
    {
        Growth++;
        if (RandomUtil.NextDouble() >= ((double)Growth / Period))
        {
            return false;
        }

        var harvested = creature.AddToInventory(ResourceName.Algae, HarvestYield);
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
