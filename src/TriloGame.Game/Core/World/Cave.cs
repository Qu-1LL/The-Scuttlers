using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave : Graph
{
    private const int SizeMult = 30;
    private const int HoleLimit = 10;
    private const double DegradeLimit = 2.75;
    private const double DegradeDeviation = 0.7;
    private const int CavernCount = 25;
    private const int Radius = 20;
    private const int OreMult = 300;
    private const int OreDist = 8;
    private readonly List<Trilobite> _trilobiteList = [];
    private readonly List<Enemy> _enemyList = [];
    private readonly List<Building> _buildingList = [];
    private readonly List<MiningPost> _miningPosts = [];
    private readonly List<AlgaeFarm> _algaeFarms = [];
    private readonly List<Barracks> _barracks = [];
    private readonly List<Scaffolding> _scaffolds = [];
    private readonly Dictionary<string, Enemy> _enemyOccupancy = new(StringComparer.Ordinal);
    private Queen? _queenBuilding;

    public Cave(GameSession session)
    {
        Session = session;
        GenerateCaveShrink();
        Trilobites = [];
        Enemies = [];
        Buildings = [];
        RevealedTiles = [];
        ReachableTiles = [];
        session.Cave = this;
        ResetBfsFields();
    }

    public GameSession Session { get; }

    public HashSet<Trilobite> Trilobites { get; }

    public HashSet<Enemy> Enemies { get; }

    public HashSet<Building> Buildings { get; }

    public HashSet<Tile> RevealedTiles { get; private set; }

    public HashSet<Tile> ReachableTiles { get; private set; }

    public IReadOnlyList<Trilobite> GetTrilobiteList() => _trilobiteList;

    public IReadOnlyList<Enemy> GetEnemyList() => _enemyList;

    public IReadOnlyList<Building> GetBuildingList() => _buildingList;

    public IReadOnlyList<MiningPost> GetMiningPosts() => _miningPosts;

    public IReadOnlyList<AlgaeFarm> GetAlgaeFarms() => _algaeFarms;

    public IReadOnlyList<Barracks> GetBarracksList() => _barracks;

    public IReadOnlyList<Scaffolding> GetScaffoldingList() => _scaffolds;

    private void GenerateCaveShrink()
    {
        FillCircle(0, 0, Radius);

        var origins = new List<GridPoint> { GridPoint.Zero };
        for (var index = 0; index < CavernCount; index++)
        {
            var randNum = RandomUtil.NextInt(origins.Count);
            var offsetFactor = RandomUtil.NextDouble();
            var xOffset = Radius * 2d * offsetFactor + Radius * RandomUtil.NextDouble();
            var yOffset = Radius * 2d * (1d - offsetFactor) + Radius * RandomUtil.NextDouble();

            var origin = new GridPoint(
                (int)System.Math.Floor(origins[randNum].X + xOffset),
                (int)System.Math.Floor(origins[randNum].Y + yOffset));

            if (RandomUtil.NextDouble() > 0.5d)
            {
                origin = origin with { X = -origin.X };
            }

            if (RandomUtil.NextDouble() > 0.5d)
            {
                origin = origin with { Y = -origin.Y };
            }

            if (origins.Any(existing => IsInCircle(origin.X, origin.Y, existing.X, existing.Y, Radius)))
            {
                index--;
                continue;
            }

            origins.Add(origin);
            var newRadius = (int)System.Math.Floor((0.5d + RandomUtil.NextDouble()) * Radius);
            FillCircle(origin.X, origin.Y, newRadius);
        }

        var values = RandomUtil.Shuffle(Tiles.Keys);
        var count = 0;
        foreach (var value in values)
        {
            var tile = GetTile(value)!;
            var coords = GridPoint.Parse(value);
            if (tile.Neighbors.Count == 4 && !IsInCircle(coords.X, coords.Y, 0, 0, Radius / 2))
            {
                RemoveTile(value);
                count++;
            }

            if (count > (Radius * HoleLimit) + (CavernCount * HoleLimit))
            {
                break;
            }
        }

        for (var index = 0; index < 2 + (Radius / SizeMult) + (Radius / CavernCount); index++)
        {
            DegradeCave();
        }

        foreach (var value in Tiles.Keys.ToArray())
        {
            if (GetTile(value)?.Neighbors.Count == 0)
            {
                RemoveTile(value);
            }
        }

        foreach (var value in Tiles.Keys.ToArray())
        {
            var tile = GetTile(value)!;
            if (tile.Neighbors.Count < 4)
            {
                tile.SetBase("wall");
                tile.CreatureCanFit = false;
            }
        }

        foreach (var value in Tiles.Keys.ToArray())
        {
            var tile = GetTile(value)!;
            if (tile.Base != "wall")
            {
                continue;
            }

            var willDelete = tile.Neighbors.All(neighbor => neighbor.Base != "empty");
            if (willDelete)
            {
                RemoveTile(value);
            }
        }

        FillOres();
    }

    private void DegradeCave()
    {
        foreach (var value in RandomUtil.Shuffle(Tiles.Keys))
        {
            var tile = GetTile(value)!;
            var randNum = RandomUtil.NextNormal(tile.Neighbors.Count, DegradeDeviation);
            if (randNum < DegradeLimit && tile.Neighbors.Count < 4)
            {
                RemoveTile(value);
            }
        }
    }

    private void FillOres()
    {
        static bool TryPlaceGuaranteedOre(Cave cave, int min, int maxExclusive, string ore)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            {
                var x = RandomUtil.NextInt(min, maxExclusive);
                var y = RandomUtil.NextInt(min, maxExclusive);
                var tile = cave.GetTile(new GridPoint(x, y).ToString());
                if (tile is not null && tile.Base == "empty")
                {
                    tile.SetBase(ore);
                    return true;
                }
            }

            return false;
        }

        TryPlaceGuaranteedOre(this, -8, 9, OreType.SANDSTONE.Name);
        TryPlaceGuaranteedOre(this, -6, 7, OreType.ALGAE.Name);
        TryPlaceGuaranteedOre(this, -6, 7, OreType.MAGNETITE.Name);

        var oreCount = 0;
        foreach (var ore in OreType.GetOres())
        {
            var count = 0;
            foreach (var tile in RandomUtil.Shuffle(GetTiles()))
            {
                var lower = System.Math.Abs(RandomUtil.NextNormal(3d * CavernCount * oreCount, CavernCount * (OreType.GetOres().Count - oreCount)) / OreDist);
                var upper = System.Math.Abs(RandomUtil.NextNormal(3d * CavernCount * (oreCount + 3), 2d * CavernCount * (OreType.GetOres().Count - oreCount)) / OreDist);
                var coords = GridPoint.Parse(tile.Key);
                var vector = GetDistance(coords.X, coords.Y, 0, 0);
                if (vector > lower && vector < upper && tile.Base == "empty")
                {
                    tile.SetBase(ore.Name);
                    var veinCount = 0;
                    var roll = RandomUtil.NextDouble();
                    while (roll < 0.85d && veinCount <= 2 + (OreType.GetOres().Count - oreCount))
                    {
                        var neighbor = tile.GetRandomNeighbor();
                        var brokenCount = 0;
                        while (neighbor is not null && neighbor.Base != "empty" && brokenCount < 4)
                        {
                            neighbor = neighbor.GetRandomNeighbor();
                            brokenCount++;
                        }

                        if (neighbor is not null && brokenCount < 4)
                        {
                            neighbor.SetBase(ore.Name);
                        }

                        roll = RandomUtil.NextDouble();
                        veinCount++;
                    }

                    count++;
                }

                if (count >= (CavernCount / 5d) + (CavernCount * Radius * (OreType.GetOres().Count - oreCount)) / (double)OreMult)
                {
                    break;
                }
            }

            oreCount++;
        }
    }

    private void FillCircle(int originX, int originY, int radius)
    {
        for (var x = originX - radius; x <= originX + radius; x++)
        {
            for (var y = originY - radius; y <= originY + radius; y++)
            {
                if (!IsInCircle(x, y, originX, originY, radius))
                {
                    continue;
                }

                AddTile(new GridPoint(x, y).ToString());
                if (Tiles.ContainsKey(new GridPoint(x - 1, y).ToString()))
                {
                    AddEdge(new GridPoint(x, y).ToString(), new GridPoint(x - 1, y).ToString());
                }

                if (Tiles.ContainsKey(new GridPoint(x, y - 1).ToString()))
                {
                    AddEdge(new GridPoint(x, y).ToString(), new GridPoint(x, y - 1).ToString());
                }
            }
        }
    }

    private void RegisterBuilding(Building building)
    {
        switch (building)
        {
            case Queen queen:
                _queenBuilding = queen;
                break;
            case MiningPost post:
                _miningPosts.Add(post);
                break;
            case AlgaeFarm farm:
                _algaeFarms.Add(farm);
                break;
            case Barracks barracks:
                _barracks.Add(barracks);
                break;
            case Scaffolding scaffolding:
                _scaffolds.Add(scaffolding);
                break;
        }
    }

    private void UnregisterBuilding(Building building)
    {
        switch (building)
        {
            case Queen queen when ReferenceEquals(_queenBuilding, queen):
                _queenBuilding = null;
                break;
            case MiningPost post:
                _miningPosts.Remove(post);
                break;
            case AlgaeFarm farm:
                _algaeFarms.Remove(farm);
                break;
            case Barracks barracks:
                _barracks.Remove(barracks);
                break;
            case Scaffolding scaffolding:
                _scaffolds.Remove(scaffolding);
                break;
        }
    }

    public bool CanBuild(Building building, GridPoint location, bool preserveReachability = false)
    {
        var hasQueen = GetQueenBuilding() is not null;
        var buildingIsQueen = building is Queen;
        var requireReachableTiles = hasQueen && !buildingIsQueen;

        for (var x = 0; x < building.Size.X; x++)
        {
            for (var y = 0; y < building.Size.Y; y++)
            {
                if (building.OpenMap[y][x] > 1)
                {
                    continue;
                }

                var tile = GetTile(new GridPoint(location.X + x, location.Y + y).ToString());
                if (tile is null || tile.Built is not null || tile.Base != "empty" || !tile.CreatureFits() || tile.Trilobites.Count > 0)
                {
                    return false;
                }

                if (requireReachableTiles && !IsTileReachable(tile))
                {
                    return false;
                }
            }
        }

        if (preserveReachability && requireReachableTiles && !SimulatedBuildPreservesReachability(building, location))
        {
            return false;
        }

        if (preserveReachability && requireReachableTiles && !SimulatedBuildPreservesBuildingAccess(building, location))
        {
            return false;
        }

        return true;
    }

    public HashSet<string> BuildSimulatedReachableKeySet(Building? building = null, GridPoint? location = null)
    {
        var reachableKeys = ReachableTiles.Select(tile => tile.Key).ToHashSet(StringComparer.Ordinal);
        if (building is null || location is null)
        {
            return reachableKeys;
        }

        for (var x = 0; x < building.Size.X; x++)
        {
            for (var y = 0; y < building.Size.Y; y++)
            {
                if (building.OpenMap[y][x] <= 1)
                {
                    reachableKeys.Remove(new GridPoint(location.Value.X + x, location.Value.Y + y).ToString());
                }
            }
        }

        return reachableKeys;
    }

    public bool IsBuildingAccessibleFromReachableKeys(Building building, HashSet<string> reachableKeys)
    {
        if (building.TileArray.Count == 0)
        {
            return true;
        }

        if (building.TileArray.Any(tile => tile.CreatureFits() && reachableKeys.Contains(tile.Key)))
        {
            return true;
        }

        foreach (var tile in building.TileArray)
        {
            foreach (var neighbor in tile.Neighbors)
            {
                if (neighbor.CreatureFits() && reachableKeys.Contains(neighbor.Key))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool SimulatedBuildPreservesReachability(Building building, GridPoint location)
    {
        var queenBuilding = GetQueenBuilding();
        if (queenBuilding is null)
        {
            return true;
        }

        var simulatedReachableKeys = BuildSimulatedReachableKeySet(building, location);
        if (simulatedReachableKeys.Count == 0)
        {
            return true;
        }

        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tile in queenBuilding.TileArray)
        {
            if (tile.CreatureFits() && simulatedReachableKeys.Contains(tile.Key) && visited.Add(tile.Key))
            {
                queue.Enqueue(tile);
            }
        }

        while (queue.Count > 0)
        {
            var currentTile = queue.Dequeue();
            foreach (var neighbor in currentTile.Neighbors)
            {
                if (simulatedReachableKeys.Contains(neighbor.Key) && visited.Add(neighbor.Key))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited.Count == simulatedReachableKeys.Count;
    }

    public bool SimulatedBuildPreservesBuildingAccess(Building building, GridPoint location)
    {
        var currentReachableKeys = BuildSimulatedReachableKeySet();
        var simulatedReachableKeys = BuildSimulatedReachableKeySet(building, location);

        foreach (var existingBuilding in Buildings)
        {
            if (ReferenceEquals(existingBuilding, building))
            {
                continue;
            }

            if (IsBuildingAccessibleFromReachableKeys(existingBuilding, currentReachableKeys) &&
                !IsBuildingAccessibleFromReachableKeys(existingBuilding, simulatedReachableKeys))
            {
                return false;
            }
        }

        return true;
    }

    public bool Build(Building building, GridPoint location)
    {
        if (!CanBuild(building, location))
        {
            return false;
        }

        Buildings.Add(building);
        _buildingList.Add(building);
        building.Cave = this;
        building.BfsField.SetCave(this);
        building.BfsField.SetOwnerBuilding(building);
        building.TileArray = [];
        building.Location = location;

        for (var x = 0; x < building.Size.X; x++)
        {
            for (var y = 0; y < building.Size.Y; y++)
            {
                var tile = GetTile(new GridPoint(location.X + x, location.Y + y).ToString())!;
                building.TileArray.Add(tile);
                if (building.OpenMap[y][x] > 1)
                {
                    continue;
                }

                tile.SetBuilt(building);
                tile.CreatureCanFit = building.OpenMap[y][x] >= 1;
            }
        }

        building.OnBuilt(this);
        RegisterBuilding(building);

        var dirtyKeys = building.TileArray.Select(tile => tile.Key).ToArray();
        var reachability = RefreshReachableTiles();
        MarkAllBuildingFieldsDirty(dirtyKeys.Concat(reachability.ChangedKeys), [building], []);
        RebalanceAllBfsFields(dirtyKeys, [building], []);
        return true;
    }

    public bool RemoveBuilding(Building building, object? source = null)
    {
        if (!Buildings.Remove(building))
        {
            return false;
        }

        _buildingList.Remove(building);
        UnregisterBuilding(building);

        var dirtyKeys = new List<string>();
        foreach (var tile in building.TileArray)
        {
            dirtyKeys.Add(tile.Key);
            if (ReferenceEquals(tile.Built, building))
            {
                tile.SetBuilt(null);
            }

            tile.CreatureCanFit = true;
        }

        foreach (var creature in GetCreatures())
        {
            var creatureWasAffected = false;
            if (creature is Trilobite trilobite && ReferenceEquals(trilobite.BuilderSourcePost, building))
            {
                trilobite.ClearBuilderSourcePost();
                creatureWasAffected = true;
            }

            switch (building)
            {
                case MiningPost post:
                    post.RemoveAssignment(creature);
                    post.ReleaseMaterialReservation(creature);
                    break;
                case AlgaeFarm farm:
                    farm.RemoveAssignment(creature);
                    break;
                case Barracks barracks:
                    barracks.RemoveAssignment(creature);
                    break;
                case Scaffolding scaffold:
                    scaffold.RemoveAssignment(creature);
                    scaffold.ReleaseMaterialReservation(creature);
                    break;
            }

            if (creature is Trilobite assignedTrilobite && ReferenceEquals(assignedTrilobite.GetAssignedBuilding(), building))
            {
                assignedTrilobite.ClearActionQueue();
                assignedTrilobite.ReleaseAssignedBuilding();
                assignedTrilobite.RestartBehavior(false);
                creatureWasAffected = true;
            }

            if (creatureWasAffected)
            {
                creature.RestartBehavior(false);
            }
        }

        building.CleanupBeforeRemoval(source);
        var reachability = RefreshReachableTiles();
        MarkAllBuildingFieldsDirty(dirtyKeys.Concat(reachability.ChangedKeys), [building], []);
        building.TileArray = [];
        building.Location = null;
        building.Cave = null;
        building.BfsField.SetCave(null);
        RebalanceAllBfsFields(dirtyKeys, [building], []);
        return true;
    }

    public Queen? GetQueenBuilding()
    {
        return _queenBuilding;
    }

    public bool IsTileRevealed(Tile tile) => RevealedTiles.Contains(tile);

    public IReadOnlyCollection<Tile> GetRevealedTiles() => RevealedTiles;

    public bool IsTileReachable(Tile tile) => ReachableTiles.Contains(tile);

    public IReadOnlyCollection<Tile> GetReachableTiles() => ReachableTiles;

    public IReadOnlyList<string> GetReachabilityChangedKeys(HashSet<Tile> previousReachableTiles, HashSet<Tile> nextReachableTiles)
    {
        var changedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in previousReachableTiles)
        {
            if (!nextReachableTiles.Contains(tile))
            {
                changedKeys.Add(tile.Key);
            }
        }

        foreach (var tile in nextReachableTiles)
        {
            if (!previousReachableTiles.Contains(tile))
            {
                changedKeys.Add(tile.Key);
            }
        }

        return changedKeys.ToArray();
    }

    public ReachabilityRefreshResult RefreshReachableTiles()
    {
        var previousReachableTiles = ReachableTiles;
        var queenBuilding = GetQueenBuilding();
        var nextReachableTiles = new HashSet<Tile>();

        if (queenBuilding is null || queenBuilding.TileArray.Count == 0)
        {
            ReachableTiles = nextReachableTiles;
            return new ReachabilityRefreshResult(0, GetReachabilityChangedKeys(previousReachableTiles, nextReachableTiles));
        }

        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in queenBuilding.TileArray)
        {
            if (tile.CreatureFits() && visited.Add(tile.Key))
            {
                queue.Enqueue(tile);
            }
        }

        while (queue.Count > 0)
        {
            var currentTile = queue.Dequeue();
            if (!currentTile.CreatureFits())
            {
                continue;
            }

            nextReachableTiles.Add(currentTile);
            foreach (var neighbor in currentTile.Neighbors)
            {
                if (neighbor.CreatureFits() && visited.Add(neighbor.Key))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        ReachableTiles = nextReachableTiles;
        return new ReachabilityRefreshResult(ReachableTiles.Count, GetReachabilityChangedKeys(previousReachableTiles, nextReachableTiles));
    }
}

public readonly record struct ReachabilityRefreshResult(int Count, IReadOnlyList<string> ChangedKeys);

public sealed partial class Cave
{
    public BfsField GetBuildingBfsFieldObject(Building building)
    {
        building.BfsField ??= new BfsField(building.Name, "building", this, building);
        building.BfsField.SetOwnerBuilding(building);
        building.BfsField.SetCave(building.Cave ?? this);
        return building.BfsField;
    }

    public Dictionary<string, int> EnsureBuildingBfsField(Building building)
    {
        return GetBuildingBfsFieldObject(building).GetField();
    }

    public bool MarkAllBuildingFieldsDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null, IEnumerable<Creature>? dirtyCreatures = null)
    {
        foreach (var building in _buildingList)
        {
            var fieldObject = GetBuildingBfsFieldObject(building);
            fieldObject.MarkDirty(tileKeys, dirtyBuildings, dirtyCreatures);
        }

        return true;
    }

    public GridPoint? GetFieldNextStep(Dictionary<string, int>? field, GridPoint location)
    {
        if (field is null)
        {
            return null;
        }

        var tempField = new BfsField(cave: this);
        tempField.SetField(field);
        return tempField.GetNextStep(location, false);
    }

    public List<GridPoint>? BuildPathFromField(Dictionary<string, int>? field, GridPoint startLocation)
    {
        if (field is null)
        {
            return null;
        }

        var tempField = new BfsField(cave: this);
        tempField.SetField(field);
        return tempField.BuildPathFrom(startLocation, false);
    }

    public Dictionary<string, int>? BuildPointBfsField(GridPoint destination)
    {
        var destinationTile = GetTile(destination.ToString());
        if (destinationTile is null || !destinationTile.CreatureFits() || !IsTileReachable(destinationTile))
        {
            return null;
        }

        var field = ReachableTiles
            .Where(tile => tile.CreatureFits())
            .ToDictionary(tile => tile.Key, _ => int.MaxValue, StringComparer.Ordinal);
        field[destination.ToString()] = 0;

        var queue = new Queue<string>();
        queue.Enqueue(destination.ToString());
        while (queue.Count > 0)
        {
            var currentKey = queue.Dequeue();
            var currentTile = GetTile(currentKey);
            if (currentTile is null)
            {
                continue;
            }

            var currentValue = field.GetValueOrDefault(currentKey, int.MaxValue);
            if (currentValue == int.MaxValue)
            {
                continue;
            }

            foreach (var neighbor in currentTile.Neighbors)
            {
                if (!neighbor.CreatureFits() || !IsTileReachable(neighbor))
                {
                    continue;
                }

                var nextValue = currentValue + 1;
                if (nextValue >= field.GetValueOrDefault(neighbor.Key, int.MaxValue))
                {
                    continue;
                }

                field[neighbor.Key] = nextValue;
                queue.Enqueue(neighbor.Key);
            }
        }

        return field;
    }

    public int GetBuildingBfsFieldValue(Building building, GridPoint location)
    {
        return GetBuildingBfsFieldObject(building).GetFieldValue(location);
    }

    public GridPoint? GetBuildingBfsFieldNextStep(Building building, GridPoint location)
    {
        return GetBuildingBfsFieldObject(building).GetNextStep(location);
    }

    public int RevealTile(Tile tile)
    {
        return RevealedTiles.Add(tile) ? 1 : 0;
    }

    public int RevealTiles(IEnumerable<Tile> tiles)
    {
        var revealedKeys = new HashSet<string>(StringComparer.Ordinal);
        var revealedCount = 0;
        foreach (var tile in tiles)
        {
            revealedCount += RevealTile(tile);
            revealedKeys.Add(tile.Key);
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
        }

        return revealedCount;
    }

    public int RevealTilesInRadius(IEnumerable<GridPoint> centerLocations, int radius)
    {
        var radiusSq = radius * radius;
        var revealedKeys = new HashSet<string>(StringComparer.Ordinal);
        var revealedCount = 0;

        foreach (var tile in GetTiles())
        {
            var tileCoords = GridPoint.Parse(tile.Key);
            foreach (var center in centerLocations)
            {
                var dx = tileCoords.X - center.X;
                var dy = tileCoords.Y - center.Y;
                if ((dx * dx) + (dy * dy) <= radiusSq)
                {
                    revealedCount += RevealTile(tile);
                    revealedKeys.Add(tile.Key);
                    break;
                }
            }
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
        }

        return revealedCount;
    }

    public int RevealTilesBetweenRadii(IReadOnlyList<GridPoint> centerLocations, int innerRadius, int outerRadius)
    {
        if (centerLocations.Count == 0 || outerRadius < 0)
        {
            return 0;
        }

        var minRadius = System.Math.Max(-1, innerRadius);
        if (outerRadius <= minRadius)
        {
            return 0;
        }

        var minX = centerLocations.Min(center => center.X);
        var maxX = centerLocations.Max(center => center.X);
        var minY = centerLocations.Min(center => center.Y);
        var maxY = centerLocations.Max(center => center.Y);
        var innerSq = minRadius * minRadius;
        var outerSq = outerRadius * outerRadius;
        var revealedKeys = new HashSet<string>(StringComparer.Ordinal);
        var revealedCount = 0;

        for (var x = minX - outerRadius; x <= maxX + outerRadius; x++)
        {
            for (var y = minY - outerRadius; y <= maxY + outerRadius; y++)
            {
                var tile = GetTile(new GridPoint(x, y).ToString());
                if (tile is null)
                {
                    continue;
                }

                var insideOuter = false;
                var insideInner = false;
                foreach (var center in centerLocations)
                {
                    var dx = x - center.X;
                    var dy = y - center.Y;
                    var distSq = (dx * dx) + (dy * dy);
                    if (distSq <= outerSq)
                    {
                        insideOuter = true;
                        if (distSq <= innerSq)
                        {
                            insideInner = true;
                            break;
                        }
                    }
                }

                if (insideOuter && !insideInner)
                {
                    revealedCount += RevealTile(tile);
                    revealedKeys.Add(tile.Key);
                }
            }
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
        }

        return revealedCount;
    }

    public int RevealCave()
    {
        var queenBuilding = GetQueenBuilding();
        if (queenBuilding is null)
        {
            return 0;
        }

        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in queenBuilding.TileArray)
        {
            if (visited.Add(tile.Key))
            {
                queue.Enqueue(tile);
            }
        }

        var revealedKeys = new HashSet<string>(StringComparer.Ordinal);
        var revealedCount = 0;

        while (queue.Count > 0)
        {
            var currentTile = queue.Dequeue();
            revealedCount += RevealTile(currentTile);
            revealedKeys.Add(currentTile.Key);

            if (currentTile.Base == "wall")
            {
                continue;
            }

            foreach (var neighbor in currentTile.Neighbors)
            {
                if (!visited.Add(neighbor.Key))
                {
                    continue;
                }

                revealedCount += RevealTile(neighbor);
                revealedKeys.Add(neighbor.Key);
                if (neighbor.Base != "wall")
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
        }

        return revealedCount;
    }

    public void NotifyMineableTilesChanged(IEnumerable<string> tileKeys)
    {
        foreach (var building in _miningPosts)
        {
            building.InvalidateMineableQueuesForKeys(tileKeys);
        }
    }

    public IReadOnlyList<string> GetBfsFieldNames() => ["enemy", "colony"];

    public Dictionary<string, BfsField> ResetBfsFields()
    {
        Session.BfsFields = new Dictionary<string, BfsField>(StringComparer.Ordinal)
        {
            ["enemy"] = new BfsField("enemy", "enemy", this),
            ["colony"] = new BfsField("colony", "colony", this)
        };
        return Session.BfsFields;
    }

    public BfsField? GetBfsFieldObject(string fieldName)
    {
        if (fieldName == "queen")
        {
            var queenBuilding = GetQueenBuilding();
            return queenBuilding is null ? null : GetBuildingBfsFieldObject(queenBuilding);
        }

        if (!Session.BfsFields.TryGetValue(fieldName, out var field))
        {
            field = new BfsField(fieldName, fieldName, this);
            Session.BfsFields[fieldName] = field;
        }

        field.SetCave(this);
        return field;
    }

    public Dictionary<string, int>? GetBfsField(string fieldName) => GetBfsFieldObject(fieldName)?.GetField();

    public Dictionary<string, int>? RefreshBfsField(string fieldName) => GetBfsFieldObject(fieldName)?.Refresh();

    public Dictionary<string, int>? RebuildBfsField(string fieldName) => GetBfsFieldObject(fieldName)?.Rebuild();

    public Dictionary<string, BfsField> MarkSharedBfsFieldsDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null, IEnumerable<Creature>? dirtyCreatures = null)
    {
        foreach (var fieldName in GetBfsFieldNames())
        {
            GetBfsFieldObject(fieldName)?.MarkDirty(tileKeys, dirtyBuildings, dirtyCreatures);
        }

        return Session.BfsFields;
    }

    public Dictionary<string, int>? RebalanceBfsField(string fieldName, IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null, IEnumerable<Creature>? dirtyCreatures = null)
    {
        var fieldObject = GetBfsFieldObject(fieldName);
        if (fieldObject is null)
        {
            return null;
        }

        fieldObject.MarkDirty(dirtyKeys, dirtyBuildings, dirtyCreatures);
        return fieldObject.Refresh();
    }

    public Dictionary<string, BfsField> RebalanceAllBfsFields(IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null, IEnumerable<Creature>? dirtyCreatures = null)
    {
        foreach (var fieldName in GetBfsFieldNames())
        {
            RebalanceBfsField(fieldName, dirtyKeys, dirtyBuildings, dirtyCreatures);
        }

        return Session.BfsFields;
    }

    public int GetBfsFieldValue(string fieldName, GridPoint location)
    {
        return GetBfsFieldObject(fieldName)?.GetFieldValue(location) ?? int.MaxValue;
    }

    public GridPoint? GetBfsFieldNextStep(string fieldName, GridPoint location)
    {
        return GetBfsFieldObject(fieldName)?.GetNextStep(location);
    }

    public IReadOnlyList<string> GetCreatureBfsFieldNames(Creature creature)
    {
        return creature is Enemy ? ["enemy"] : ["colony"];
    }

    public bool MarkCreatureBfsFieldsDirty(Creature creature, IEnumerable<string>? tileKeys = null)
    {
        foreach (var fieldName in GetCreatureBfsFieldNames(creature))
        {
            GetBfsFieldObject(fieldName)?.MarkDirty(tileKeys, [], [creature]);
        }

        return true;
    }

    public IEnumerable<Creature> GetCreatures()
    {
        foreach (var trilobite in _trilobiteList)
        {
            yield return trilobite;
        }

        foreach (var enemy in _enemyList)
        {
            yield return enemy;
        }
    }

    private bool HasEnemies() => Enemies.Count > 0;

    private int RestoreAllCreatureHealth()
    {
        var restoredCount = 0;
        foreach (var creature in Trilobites)
        {
            creature.RestoreHealth();
            restoredCount++;
        }

        return restoredCount;
    }

    public bool SyncTrilobiteTileOccupancy(Creature creature, Tile? fromTile = null, Tile? toTile = null)
    {
        if (creature is Trilobite trilobite)
        {
            fromTile?.RemoveTrilobite(trilobite);
            toTile?.AddTrilobite(trilobite);
            return true;
        }

        if (creature is not Enemy enemy)
        {
            return false;
        }

        if (fromTile is not null && ReferenceEquals(fromTile.EnemyOccupant, enemy))
        {
            fromTile.SetEnemyOccupant(null);
            _enemyOccupancy.Remove(fromTile.Key);
        }

        if (toTile is not null)
        {
            toTile.SetEnemyOccupant(enemy);
            _enemyOccupancy[toTile.Key] = enemy;
        }

        return true;
    }

    public bool RemoveCreature(Creature creature, object? source = null)
    {
        var removedEnemy = creature is Enemy;

        if (removedEnemy)
        {
            if (!Enemies.Remove((Enemy)creature))
            {
                return false;
            }

            _enemyList.Remove((Enemy)creature);
        }
        else if (!Trilobites.Remove((Trilobite)creature))
        {
            return false;
        }
        else
        {
            _trilobiteList.Remove((Trilobite)creature);
        }

        creature.ClearActionQueue();
        creature.CleanupBeforeRemoval(source);
        foreach (var building in _buildingList)
        {
            switch (building)
            {
                case MiningPost post:
                    post.RemoveAssignment(creature);
                    post.ReleaseMaterialReservation(creature);
                    break;
                case AlgaeFarm farm:
                    farm.RemoveAssignment(creature);
                    break;
                case Barracks barracks:
                    barracks.RemoveAssignment(creature);
                    break;
                case Scaffolding scaffold:
                    scaffold.RemoveAssignment(creature);
                    scaffold.ReleaseMaterialReservation(creature);
                    break;
            }
        }

        var currentTile = GetTile(creature.Location.ToString());
        SyncTrilobiteTileOccupancy(creature, currentTile, null);

        if (removedEnemy && !HasEnemies())
        {
            Session.Danger = false;
            RestoreAllCreatureHealth();
        }

        MarkCreatureBfsFieldsDirty(creature, currentTile is null ? [] : [currentTile.Key]);
        creature.Location = GridPoint.Zero;
        creature.Cave = null;
        creature.UpdateMovementOffset(false);
        return true;
    }

    public bool Spawn(Creature creature, Tile tile)
    {
        if (tile.Base == "wall" || !tile.CreatureFits() || !IsTileReachable(tile))
        {
            return false;
        }

        var currentTile = GetTile(creature.Location.ToString());
        creature.Location = GridPoint.Parse(tile.Key);
        SyncTrilobiteTileOccupancy(creature, currentTile, tile);
        creature.UpdateMovementOffset(false);
        creature.Cave = this;

        if (creature is Enemy enemy)
        {
            Enemies.Add(enemy);
            _enemyList.Add(enemy);
            Session.Danger = true;
        }
        else
        {
            var trilobite = (Trilobite)creature;
            Trilobites.Add(trilobite);
            _trilobiteList.Add(trilobite);
        }

        MarkCreatureBfsFieldsDirty(creature, [tile.Key]);
        return true;
    }

    public bool MoveCreature(Creature creature, GridPoint nextLocation)
    {
        var current = creature.Location;
        var nextTile = GetTile(nextLocation.ToString());
        if (nextTile is null || !nextTile.CreatureFits())
        {
            return false;
        }

        if (GridPoint.ManhattanDistance(current, nextLocation) != 1)
        {
            return false;
        }

        var currentTile = GetTile(current.ToString());
        var moveX = current.X - nextLocation.X;
        var moveY = current.Y - nextLocation.Y;

        creature.Location = nextLocation;
        creature.UpdateMovementOffset(true);
        if (moveX == 0)
        {
            creature.RotationRadians = -moveY == 1 ? MathF.PI : 0f;
        }
        else
        {
            creature.RotationRadians = -moveX == 1 ? MathF.PI / 2f : MathF.PI * 1.5f;
        }

        SyncTrilobiteTileOccupancy(creature, currentTile, nextTile);
        MarkCreatureBfsFieldsDirty(creature, new[] { currentTile?.Key, nextTile.Key }.OfType<string>());
        return true;
    }

    public IReadOnlyList<string> GetCoords() => Tiles.Keys.ToArray();

    public Trilobite? GetTrilobiteAtTileKey(string? tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey))
        {
            return null;
        }

        return GetTile(tileKey)?.Trilobites.FirstOrDefault();
    }

    public Enemy? GetEnemyAtTileKey(string? tileKey)
    {
        return string.IsNullOrWhiteSpace(tileKey)
            ? null
            : _enemyOccupancy.GetValueOrDefault(tileKey);
    }

    private static bool IsInCircle(int x, int y, int cx, int cy, int radius)
    {
        var dx = x - cx;
        var dy = y - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static double GetDistance(int x, int y, int cx, int cy)
    {
        var dx = x - cx;
        var dy = y - cy;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }
}
