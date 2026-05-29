using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;
using System.Diagnostics;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave : Graph
{
    public readonly record struct MineablePathResult(string TileKey, GridPoint NavigationTarget, List<GridPoint> Path);

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
    private readonly List<Vehicle> _vehicles = [];
    private readonly List<Building> _buildingList = [];
    private readonly List<MiningPost> _miningPosts = [];
    private readonly List<AlgaeFarm> _algaeFarms = [];
    private readonly List<Barracks> _barracks = [];
    private readonly List<Turret> _turrets = [];
    private readonly List<Wall> _walls = [];
    private readonly List<Scaffolding> _scaffolds = [];
    private readonly Dictionary<string, Enemy> _enemyOccupancy = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vehicle> _vehicleOccupancy = new(StringComparer.Ordinal);
    private readonly Dictionary<MiningPost, MiningPostMovementCacheEntry> _miningPostMovementCache = [];
    private readonly Dictionary<MiningPost, int> _miningPostAssignmentCounts = [];
    private readonly Dictionary<StationBuilding, int> _fighterStationAssignmentCounts = [];
    private readonly MiningPostOwnershipField _miningPostOwnershipField;
    private readonly AlgaeFarmOwnershipField _algaeFarmOwnershipField;
    private readonly BarracksOwnershipField _barracksOwnershipField;
    private readonly TurretOwnershipField _turretOwnershipField;
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
        _miningPostOwnershipField = new MiningPostOwnershipField(this);
        _algaeFarmOwnershipField = new AlgaeFarmOwnershipField(this);
        _barracksOwnershipField = new BarracksOwnershipField(this);
        _turretOwnershipField = new TurretOwnershipField(this);
        session.Cave = this;
        ResetBfsFields();
    }

    public GameSession Session { get; }

    public HashSet<Trilobite> Trilobites { get; }

    public HashSet<Enemy> Enemies { get; }

    public HashSet<Building> Buildings { get; }

    public HashSet<Tile> RevealedTiles { get; private set; }

    public HashSet<Tile> ReachableTiles { get; private set; }

    public bool HasOpenAlgaeFarms { get; private set; }

    public bool HasAvailableMiningPostAssignments { get; private set; }

    public bool MiningPostBuildingsAdded { get; private set; }

    public bool BarracksBuildingsAdded { get; private set; }

    public long TopologyVersion { get; private set; }

    public long ReachabilityVersion { get; private set; }

    public IReadOnlyList<Trilobite> GetTrilobiteList() => _trilobiteList;

    public IReadOnlyList<Enemy> GetEnemyList() => _enemyList;

    public IReadOnlyList<Vehicle> GetVehicles() => _vehicles;

    public IReadOnlyList<Building> GetBuildingList() => _buildingList;

    public IReadOnlyList<MiningPost> GetMiningPosts() => _miningPosts;

    public IReadOnlyList<AlgaeFarm> GetAlgaeFarms() => _algaeFarms;

    public IReadOnlyList<Barracks> GetBarracksList() => _barracks;

    public IReadOnlyList<Turret> GetTurretList() => _turrets;

    public IReadOnlyList<Wall> GetWalls() => _walls;

    public IReadOnlyList<Scaffolding> GetScaffoldingList() => _scaffolds;

    public IReadOnlyDictionary<MiningPost, int> GetMiningPostAssignmentCounts() => _miningPostAssignmentCounts;

    public IReadOnlyDictionary<Barracks, int> GetBarracksAssignmentCounts()
    {
        return _fighterStationAssignmentCounts
            .Where(pair => pair.Key is Barracks)
            .ToDictionary(pair => (Barracks)pair.Key, pair => pair.Value);
    }

    public IReadOnlyDictionary<Turret, int> GetTurretAssignmentCounts()
    {
        return _fighterStationAssignmentCounts
            .Where(pair => pair.Key is Turret)
            .ToDictionary(pair => (Turret)pair.Key, pair => pair.Value);
    }

    public IReadOnlyList<StationBuilding> GetFighterStations()
    {
        return [.. EnumerateFighterStations()];
    }

    private IEnumerable<StationBuilding> EnumerateFighterStations()
    {
        foreach (var turret in _turrets)
        {
            yield return turret;
        }

        foreach (var barracks in _barracks)
        {
            yield return barracks;
        }
    }

    public bool RefreshDangerState()
    {
        var previousDanger = Session.Danger;
        Session.Danger = _enemyList.Any(enemy =>
        {
            var tile = GetTile(enemy.Location);
            return tile is not null && IsTileRevealed(tile);
        });

        if (!Session.Danger)
        {
            DespawnAntHoles();
            if (previousDanger)
            {
                GetBfsFieldObject("enemy")?.ClearField();
            }
        }

        return Session.Danger;
    }

    public int SpawnUndiscoveredAntCluster(int requestedCount)
    {
        if (requestedCount <= 0 || Enemies.Count >= GameConstants.MaxAmbientAntCount)
        {
            return 0;
        }

        var candidates = GetTiles()
            .Where(tile =>
                !IsTileRevealed(tile) &&
                tile.CreatureFits() &&
                tile.Trilobites.Count == 0 &&
                tile.EnemyOccupant is null &&
                !IsEnemySpawnBlockedTile(tile))
            .ToArray();
        if (candidates.Length == 0)
        {
            return 0;
        }

        var targetCount = Math.Min(requestedCount, GameConstants.MaxAmbientAntCount - Enemies.Count);
        var seedTile = candidates[RandomUtil.NextInt(candidates.Length)];
        var selectedTiles = new List<Tile>(targetCount);
        var queue = new Queue<Tile>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue(seedTile);
        visited.Add(seedTile.Key);

        while (queue.Count > 0 && selectedTiles.Count < targetCount)
        {
            var current = queue.Dequeue();
            if (current.CreatureFits() &&
                !IsTileRevealed(current) &&
                current.Trilobites.Count == 0 &&
                current.EnemyOccupant is null &&
                !IsEnemySpawnBlockedTile(current))
            {
                selectedTiles.Add(current);
            }

            foreach (var neighbor in RandomUtil.Shuffle(current.Neighbors))
            {
                if (!visited.Add(neighbor.Key) ||
                    IsTileRevealed(neighbor) ||
                    !neighbor.CreatureFits() ||
                    neighbor.Trilobites.Count > 0 ||
                    neighbor.EnemyOccupant is not null ||
                    IsEnemySpawnBlockedTile(neighbor))
                {
                    continue;
                }

                queue.Enqueue(neighbor);
            }
        }

        var spawned = 0;
        foreach (var tile in selectedTiles)
        {
            if (Spawn(new Enemy($"Ant {Session.Runtime.AllocateDebugEnemyId()}", tile.Coordinates, Session), tile))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private void GenerateCaveShrink()
    {
        FillCircle(0, 0, Radius);

        var origins = new List<GridPoint> { GridPoint.Zero };
        for (var cavernIndex = 0; cavernIndex < CavernCount; cavernIndex++)
        {
            AddGeneratedCavern(origins);
        }

        var sandBiome = AddBiomeCavern(origins, BiomeNames.Sand);
        ApplySandBiomeGeneration(sandBiome);

        var lushBiome = AddBiomeCavern(origins, BiomeNames.Lush);
        ApplyLushBiomeGeneration(lushBiome);

        var greenBiome = AddBiomeCavern(origins, BiomeNames.Green);
        ApplyGreenBiomeGeneration(greenBiome);

        var lavaBiome = AddBiomeCavern(origins, BiomeNames.Lava);
        ApplyLavaBiomeGeneration(lavaBiome);

        var protectedCenterRadius = Radius / 2 ;
        var holeBreakThreshold = (Radius * HoleLimit) + (CavernCount * HoleLimit);
        var tileKeys = RandomUtil.Shuffle(Tiles.Keys);
        var removedHoleCount = 0;
        foreach (var tileKey in tileKeys)
        {
            var tile = GetTile(tileKey)!;
            var coords = GridPoint.Parse(tileKey);
            if (tile.Neighbors.Count == 4 &&
                ((coords.X * coords.X) + (coords.Y * coords.Y) > protectedCenterRadius * protectedCenterRadius))
            {
                RemoveTile(tileKey);
                removedHoleCount++;
            }

            if (removedHoleCount > holeBreakThreshold)
            {
                break;
            }
        }

        for (var index = 0; index < 2d + ((double)Radius / SizeMult) + ((double)Radius / CavernCount); index++)
        {
            DegradeCaveOnce();
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
                tile.ConfigureWall(GameConstants.WallHitsRequired);
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

    private void DegradeCaveOnce()
    {
        var tileKeys = RandomUtil.Shuffle(Tiles.Keys);
        foreach (var tileKey in tileKeys)
        {
            var tile = GetTile(tileKey)!;
            var neighborCount = tile.Neighbors.Count;
            var sample = RandomUtil.NextNormal(neighborCount, DegradeDeviation);
            if (neighborCount < 4 && sample < DegradeLimit)
            {
                RemoveTile(tileKey);
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
                    cave.ConfigureGeneratedOreTile(tile, ore);
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
                    ConfigureGeneratedOreTile(tile, ore.Name);
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
                            ConfigureGeneratedOreTile(neighbor, ore.Name);
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

    private void ConfigureGeneratedOreTile(Tile tile, string oreName)
    {
        tile.SetBase(oreName);
        tile.ConfigureOre(
            RandomUtil.NextInt(GameConstants.MinOreYield, GameConstants.MaxOreYield + 1),
            RandomUtil.NextInt(GameConstants.MinOreHitsPerYield, GameConstants.MaxOreHitsPerYield + 1));
    }

    private void FillCircle(int originX, int originY, int radius, BiomeRegion? biome = null)
    {
        for (var x = originX - radius; x <= originX + radius; x++)
        {
            for (var y = originY - radius; y <= originY + radius; y++)
            {
                if (!IsInCircle(x, y, originX, originY, radius))
                {
                    continue;
                }

                var tile = AddTile(new GridPoint(x, y).ToString());
                if (biome is not null)
                {
                    SetTileBiome(tile, biome);
                }

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
            case Garage garage:
                _garages.Add(garage);
                break;
            case Silo silo:
                _silos.Add(silo);
                break;
            case SoilPatch soilPatch:
                _soilPatches.Add(soilPatch);
                RegisterSoilPatchTiles(soilPatch);
                break;
            case MiningPost post:
                _miningPosts.Add(post);
                _miningPostAssignmentCounts[post] = post.GetVolume();
                MiningPostBuildingsAdded = true;
                SyncMiningPostAssignmentAvailability();
                SyncMiningPostBuildingsAddedState();
                break;
            case AlgaeFarm farm:
                _algaeFarms.Add(farm);
                RefreshOpenAlgaeFarmAvailability();
                break;
            case Turret turret:
                _turrets.Add(turret);
                _fighterStationAssignmentCounts[turret] = turret.GetVolume();
                BarracksBuildingsAdded = true;
                SyncBarracksBuildingsAddedState();
                break;
            case Barracks barracks:
                _barracks.Add(barracks);
                _fighterStationAssignmentCounts[barracks] = barracks.GetVolume();
                BarracksBuildingsAdded = true;
                SyncBarracksBuildingsAddedState();
                break;
            case Wall wall:
                _walls.Add(wall);
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
            case Garage garage:
                _garages.Remove(garage);
                break;
            case Silo silo:
                _silos.Remove(silo);
                break;
            case SoilPatch soilPatch:
                UnregisterSoilPatchTiles(soilPatch);
                _soilPatches.Remove(soilPatch);
                break;
            case MiningPost post:
                _miningPosts.Remove(post);
                _miningPostAssignmentCounts.Remove(post);
                ForgetMiningPostMovementCache(post);
                SyncMiningPostAssignmentAvailability();
                SyncMiningPostBuildingsAddedState();
                break;
            case AlgaeFarm farm:
                _algaeFarms.Remove(farm);
                RefreshOpenAlgaeFarmAvailability();
                break;
            case Turret turret:
                _turrets.Remove(turret);
                _fighterStationAssignmentCounts.Remove(turret);
                SyncBarracksBuildingsAddedState();
                break;
            case Barracks barracks:
                _barracks.Remove(barracks);
                _fighterStationAssignmentCounts.Remove(barracks);
                SyncBarracksBuildingsAddedState();
                break;
            case Wall wall:
                _walls.Remove(wall);
                break;
            case Scaffolding scaffolding:
                _scaffolds.Remove(scaffolding);
                break;
        }
    }

    private void AdvanceTopologyVersion()
    {
        TopologyVersion++;
    }

    private void AdvanceReachabilityVersion()
    {
        ReachabilityVersion++;
    }

    internal void AdvanceTopologyVersionForCache()
    {
        AdvanceTopologyVersion();
    }

    internal void AdvanceReachabilityVersionForIncrementalReachability()
    {
        AdvanceReachabilityVersion();
    }

    private void ForgetMiningPostMovementCache(MiningPost post)
    {
        _miningPostMovementCache.Remove(post);
    }

    public bool RefreshOpenAlgaeFarmAvailability()
    {
        HasOpenAlgaeFarms = _algaeFarms.Any(farm => farm.Location is not null && farm.TileArray.Count > 0 && farm.HasAssignmentSlot());
        return HasOpenAlgaeFarms;
    }

    private static bool IsActiveAssignedBuilding(Building building)
    {
        return building.Location is not null && building.TileArray.Count > 0;
    }

    private bool ShouldBalanceMiningPost(MiningPost post)
    {
        return IsActiveAssignedBuilding(post) && post.AssignmentsAvailable;
    }

    private bool ShouldBalanceStation(StationBuilding station)
    {
        return IsActiveAssignedBuilding(station);
    }

    private static bool AreAssignmentCountsBalanced<TBuilding>(IDictionary<TBuilding, int> counts, Func<TBuilding, bool> includeBuilding)
        where TBuilding : Building
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        var includedCount = 0;

        foreach (var pair in counts)
        {
            if (!includeBuilding(pair.Key))
            {
                continue;
            }

            includedCount++;
            min = System.Math.Min(min, pair.Value);
            max = System.Math.Max(max, pair.Value);
        }

        return includedCount <= 1 || (max - min) <= 1;
    }

    private static int? GetLeastAssignmentCount<TBuilding>(IDictionary<TBuilding, int> counts, Func<TBuilding, bool> includeBuilding)
        where TBuilding : Building
    {
        int? leastCount = null;
        foreach (var pair in counts)
        {
            if (!includeBuilding(pair.Key))
            {
                continue;
            }

            leastCount = !leastCount.HasValue || pair.Value < leastCount.Value
                ? pair.Value
                : leastCount.Value;
        }

        return leastCount;
    }

    internal int GetMiningPostAssignmentCount(MiningPost post)
    {
        return _miningPostAssignmentCounts.GetValueOrDefault(post, post.GetVolume());
    }

    internal int GetStationAssignmentCount(StationBuilding station)
    {
        return _fighterStationAssignmentCounts.GetValueOrDefault(station, station.GetVolume());
    }

    internal void SyncMiningPostAssignmentCount(MiningPost post, int count)
    {
        if (!_miningPostAssignmentCounts.ContainsKey(post))
        {
            return;
        }

        _miningPostAssignmentCounts[post] = count;
        SyncMiningPostBuildingsAddedState();
    }

    internal void SyncStationAssignmentCount(StationBuilding station, int count)
    {
        if (!_fighterStationAssignmentCounts.ContainsKey(station))
        {
            return;
        }

        _fighterStationAssignmentCounts[station] = count;
        SyncBarracksBuildingsAddedState();
    }

    private void SyncMiningPostBuildingsAddedState()
    {
        if (MiningPostBuildingsAdded && AreAssignmentCountsBalanced(_miningPostAssignmentCounts, ShouldBalanceMiningPost))
        {
            MiningPostBuildingsAdded = false;
        }
    }

    private void SyncBarracksBuildingsAddedState()
    {
        BarracksBuildingsAdded = EnumerateFighterStations()
            .Where(ShouldBalanceStation)
            .Any(ShouldRebalanceFighterAssignments);
    }

    internal void OnMiningPostAssignmentsAvailableChanged(MiningPost post, bool previousValue, bool currentValue)
    {
        SyncMiningPostAssignmentAvailability();

        if (!previousValue && currentValue && _miningPostAssignmentCounts.ContainsKey(post))
        {
            MiningPostBuildingsAdded = true;
        }

        SyncMiningPostBuildingsAddedState();
    }

    internal bool ShouldRebalanceMiningPostAssignments(MiningPost? currentPost)
    {
        if (!MiningPostBuildingsAdded || currentPost is null || !ShouldBalanceMiningPost(currentPost))
        {
            return false;
        }

        var leastCount = GetLeastAssignmentCount(_miningPostAssignmentCounts, ShouldBalanceMiningPost);
        return leastCount.HasValue && GetMiningPostAssignmentCount(currentPost) > leastCount.Value + 1;
    }

    internal bool ShouldRebalanceFighterStationAssignments(StationBuilding? currentStation)
    {
        if (!BarracksBuildingsAdded || currentStation is null)
        {
            return false;
        }

        return ShouldRebalanceFighterAssignments(currentStation);
    }

    private bool ShouldRebalanceFighterAssignments(StationBuilding currentStation)
    {
        if (!ShouldBalanceStation(currentStation))
        {
            return false;
        }

        var currentCount = GetStationAssignmentCount(currentStation);
        if (currentCount <= 0)
        {
            return false;
        }

        if (EnumerateFighterStations().Any(station =>
                ShouldBalanceStation(station) &&
                station.FighterAssignmentPriority > currentStation.FighterAssignmentPriority &&
                station.HasAssignmentSlot()))
        {
            return true;
        }

        var leastCount = GetLeastAssignmentCount(
            _fighterStationAssignmentCounts,
            station => ShouldBalanceStation(station) &&
                       station.FighterAssignmentPriority == currentStation.FighterAssignmentPriority);
        return leastCount.HasValue && currentCount > leastCount.Value + 1;
    }

    // This cache is only true while at least one active mining post still has
    // mineable work and room to store the haul.
    internal bool SyncMiningPostAssignmentAvailability()
    {
        HasAvailableMiningPostAssignments = _miningPosts.Any(post =>
            post.Location is not null &&
            post.TileArray.Count > 0 &&
            post.AssignmentsAvailable &&
            post.GetInventorySpace() > 0);
        return HasAvailableMiningPostAssignments;
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
                if (tile is null ||
                    tile.Built is not null ||
                    HasBlockingSurfaceFeature(tile) ||
                    tile.Base != "empty" ||
                    !tile.CreatureFits() ||
                    tile.EnemyOccupant is not null)
                {
                    return false;
                }

                if (requireReachableTiles && !IsTileReachable(tile))
                {
                    return false;
                }
            }
        }

        if (!CanPlaceRanchBuilding(building, location))
        {
            return false;
        }

        if (preserveReachability && requireReachableTiles && !SimulatedBuildPreservesReachability(building, location))
        {
            return false;
        }

        if (preserveReachability &&
            requireReachableTiles &&
            !ShouldSkipSimulatedBuildingAccessCheck(building) &&
            !SimulatedBuildPreservesBuildingAccess(building, location))
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

        var simulatedOpenMap = GetReachabilitySimulationOpenMap(building);
        for (var x = 0; x < building.Size.X; x++)
        {
            for (var y = 0; y < building.Size.Y; y++)
            {
                if (simulatedOpenMap[y][x] < 1)
                {
                    reachableKeys.Remove(new GridPoint(location.Value.X + x, location.Value.Y + y).ToString());
                }
            }
        }

        return reachableKeys;
    }

    // Simulations use the finished building footprint so walkable scaffolds do not hide future blockers.
    private static int[][] GetReachabilitySimulationOpenMap(Building building)
    {
        return building is Scaffolding scaffolding
            ? scaffolding.TargetBuilding.OpenMap
            : building.OpenMap;
    }

    private static bool ShouldSkipSimulatedBuildingAccessCheck(Building building)
    {
        return building is Wall ||
               building is Scaffolding { TargetBuilding: Wall };
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

        PlaceBuildingUnchecked(building, location);
        OnRanchBuildingBuilt(building);
        FinalizeBuiltBuildings([building]);
        return true;
    }

    private void PlaceBuildingUnchecked(Building building, GridPoint location)
    {
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
    }

    private void FinalizeBuiltBuildings(IReadOnlyList<Building> builtBuildings)
    {
        AdvanceTopologyVersion();

        var dirtyKeys = builtBuildings
            .SelectMany(building => building.TileArray)
            .Select(tile => tile.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var reachability = RefreshReachableTiles();
        var ownershipDirtyKeys = dirtyKeys.Concat(reachability.ChangedKeys).Distinct(StringComparer.Ordinal).ToArray();
        MarkAllBuildingFieldsDirty(ownershipDirtyKeys, builtBuildings, []);
        MarkAllBuildingOwnershipFieldsDirty(ownershipDirtyKeys, builtBuildings);
        RebalanceAllBfsFields(dirtyKeys, builtBuildings, []);
        RebalanceAllBuildingOwnershipFields(dirtyKeys, builtBuildings);
        for (var index = 0; index < builtBuildings.Count; index++)
        {
            var buildingField = GetBuildingBfsFieldObject(builtBuildings[index]);
            buildingField.Rebuild();
            buildingField.MarkDirty(ownershipDirtyKeys, builtBuildings, []);
        }

        if (builtBuildings.Any(static building => building is Wall))
        {
            RebuildWallBfsField();
        }
    }

    public bool RemoveBuilding(Building building, object? source = null)
    {
        if (!Buildings.Remove(building))
        {
            return false;
        }

        var affectedCreatures = new List<Creature>();
        foreach (var creature in GetCreatures().ToArray())
        {
            var creatureWasAffected = false;
            if (building is StationBuilding stationBuilding &&
                (stationBuilding.IsCreatureStationed(creature) || creature.IsHostedOnBuilding(stationBuilding)))
            {
                if (creature is Trilobite stationedTrilobite)
                {
                    RestoreStationedTrilobiteToLastTrackedTile(stationBuilding, stationedTrilobite);
                    stationedTrilobite.ReleaseAssignedBuilding();
                    creatureWasAffected = true;
                }
                else
                {
                    creatureWasAffected = stationBuilding.TryRestoreCreatureToTileSystem(creature);
                    stationBuilding.RemoveAssignment(creature);
                }
            }

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
                case Ranch ranch:
                    ranch.RemoveAssignment(creature);
                    break;
                case StationBuilding station:
                    station.RemoveAssignment(creature);
                    break;
                case Scaffolding scaffold:
                    scaffold.RemoveAssignment(creature);
                    scaffold.ReleaseMaterialReservation(creature);
                    break;
            }

            if (creature is Trilobite assignedTrilobite && ReferenceEquals(assignedTrilobite.GetAssignedBuilding(), building))
            {
                assignedTrilobite.ReleaseAssignedBuilding();
                creatureWasAffected = true;
            }

            if (!creatureWasAffected)
            {
                continue;
            }

            creature.ClearActionQueue();
            affectedCreatures.Add(creature);
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

        OnRanchBuildingRemoved(building);
        building.CleanupBeforeRemoval(source);
        AdvanceTopologyVersion();
        var reachability = RefreshReachableTiles();
        var ownershipDirtyKeys = dirtyKeys.Concat(reachability.ChangedKeys).Distinct(StringComparer.Ordinal).ToArray();
        MarkAllBuildingFieldsDirty(ownershipDirtyKeys, [building], []);
        MarkAllBuildingOwnershipFieldsDirty(ownershipDirtyKeys, [building]);
        building.TileArray = [];
        building.Location = null;
        building.Cave = null;
        building.BfsField.SetCave(null);
        RebalanceAllBfsFields(dirtyKeys, [building], []);
        RebalanceAllBuildingOwnershipFields(dirtyKeys, [building]);
        if (building is Wall)
        {
            RebuildWallBfsField();
        }

        foreach (var creature in affectedCreatures)
        {
            creature.RestartBehavior(false);
        }

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

    internal bool TryAddReachableTile(Tile tile, ISet<string>? changedKeys = null)
    {
        if (!tile.CreatureFits() || !ReachableTiles.Add(tile))
        {
            return false;
        }

        changedKeys?.Add(tile.Key);
        return true;
    }

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
            var changedKeys = GetReachabilityChangedKeys(previousReachableTiles, nextReachableTiles);
            if (changedKeys.Count > 0)
            {
                AdvanceReachabilityVersion();
            }

            return new ReachabilityRefreshResult(0, changedKeys);
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
        var finalChangedKeys = GetReachabilityChangedKeys(previousReachableTiles, nextReachableTiles);
        if (finalChangedKeys.Count > 0)
        {
            AdvanceReachabilityVersion();
        }

        return new ReachabilityRefreshResult(ReachableTiles.Count, finalChangedKeys);
    }
}

public readonly record struct ReachabilityRefreshResult(int Count, IReadOnlyList<string> ChangedKeys);

public sealed partial class Cave
{
    private static bool HasActiveOwnershipBuildings<TBuilding>(IReadOnlyList<TBuilding> buildings)
        where TBuilding : Building
    {
        return buildings.Any(building => building.Location is not null && building.TileArray.Count > 0);
    }

    private TField GetBuildingOwnershipFieldObject<TField, TBuilding>(TField field, IReadOnlyList<TBuilding> buildings)
        where TField : BuildingOwnershipField<TBuilding>
        where TBuilding : Building
    {
        if (!HasActiveOwnershipBuildings(buildings))
        {
            field.Deactivate();
            return field;
        }

        field.SetCave(this);
        return field;
    }

    public MiningPostOwnershipField GetMiningPostOwnershipFieldObject()
    {
        return GetBuildingOwnershipFieldObject(_miningPostOwnershipField, _miningPosts);
    }

    public AlgaeFarmOwnershipField GetAlgaeFarmOwnershipFieldObject()
    {
        return GetBuildingOwnershipFieldObject(_algaeFarmOwnershipField, _algaeFarms);
    }

    public BarracksOwnershipField GetBarracksOwnershipFieldObject()
    {
        return GetBuildingOwnershipFieldObject(_barracksOwnershipField, _barracks);
    }

    public TurretOwnershipField GetTurretOwnershipFieldObject()
    {
        return GetBuildingOwnershipFieldObject(_turretOwnershipField, _turrets);
    }

    public MiningPostOwnershipField MarkMiningPostOwnershipFieldDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetMiningPostOwnershipFieldObject();
        field.MarkDirty(tileKeys, dirtyBuildings);
        return field;
    }

    public AlgaeFarmOwnershipField MarkAlgaeFarmOwnershipFieldDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetAlgaeFarmOwnershipFieldObject();
        field.MarkDirty(tileKeys, dirtyBuildings);
        return field;
    }

    public BarracksOwnershipField MarkBarracksOwnershipFieldDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetBarracksOwnershipFieldObject();
        field.MarkDirty(tileKeys, dirtyBuildings);
        return field;
    }

    public TurretOwnershipField MarkTurretOwnershipFieldDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetTurretOwnershipFieldObject();
        field.MarkDirty(tileKeys, dirtyBuildings);
        return field;
    }

    public bool MarkAllBuildingOwnershipFieldsDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        MarkMiningPostOwnershipFieldDirty(tileKeys, dirtyBuildings);
        MarkAlgaeFarmOwnershipFieldDirty(tileKeys, dirtyBuildings);
        MarkBarracksOwnershipFieldDirty(tileKeys, dirtyBuildings);
        MarkTurretOwnershipFieldDirty(tileKeys, dirtyBuildings);
        return true;
    }

    public MiningPostOwnershipField RefreshMiningPostOwnershipField()
    {
        var field = GetMiningPostOwnershipFieldObject();
        field.Refresh();
        return field;
    }

    public AlgaeFarmOwnershipField RefreshAlgaeFarmOwnershipField()
    {
        var field = GetAlgaeFarmOwnershipFieldObject();
        field.Refresh();
        return field;
    }

    public BarracksOwnershipField RefreshBarracksOwnershipField()
    {
        var field = GetBarracksOwnershipFieldObject();
        field.Refresh();
        return field;
    }

    public TurretOwnershipField RefreshTurretOwnershipField()
    {
        var field = GetTurretOwnershipFieldObject();
        field.Refresh();
        return field;
    }

    public MiningPostOwnershipField RebuildMiningPostOwnershipField()
    {
        var field = GetMiningPostOwnershipFieldObject();
        field.Rebuild();
        return field;
    }

    public AlgaeFarmOwnershipField RebuildAlgaeFarmOwnershipField()
    {
        var field = GetAlgaeFarmOwnershipFieldObject();
        field.Rebuild();
        return field;
    }

    public BarracksOwnershipField RebuildBarracksOwnershipField()
    {
        var field = GetBarracksOwnershipFieldObject();
        field.Rebuild();
        return field;
    }

    public TurretOwnershipField RebuildTurretOwnershipField()
    {
        var field = GetTurretOwnershipFieldObject();
        field.Rebuild();
        return field;
    }

    public bool RebuildAllBuildingOwnershipFields()
    {
        RebuildMiningPostOwnershipField();
        RebuildAlgaeFarmOwnershipField();
        RebuildBarracksOwnershipField();
        RebuildTurretOwnershipField();
        return true;
    }

    public MiningPostOwnershipField RebalanceMiningPostOwnershipField(IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetMiningPostOwnershipFieldObject();
        field.MarkDirty(dirtyKeys, dirtyBuildings);
        field.Refresh();
        return field;
    }

    public AlgaeFarmOwnershipField RebalanceAlgaeFarmOwnershipField(IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetAlgaeFarmOwnershipFieldObject();
        field.MarkDirty(dirtyKeys, dirtyBuildings);
        field.Refresh();
        return field;
    }

    public BarracksOwnershipField RebalanceBarracksOwnershipField(IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetBarracksOwnershipFieldObject();
        field.MarkDirty(dirtyKeys, dirtyBuildings);
        field.Refresh();
        return field;
    }

    public TurretOwnershipField RebalanceTurretOwnershipField(IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        var field = GetTurretOwnershipFieldObject();
        field.MarkDirty(dirtyKeys, dirtyBuildings);
        field.Refresh();
        return field;
    }

    public bool RebalanceAllBuildingOwnershipFields(IEnumerable<string>? dirtyKeys = null, IEnumerable<Building>? dirtyBuildings = null)
    {
        RebalanceMiningPostOwnershipField(dirtyKeys, dirtyBuildings);
        RebalanceAlgaeFarmOwnershipField(dirtyKeys, dirtyBuildings);
        RebalanceBarracksOwnershipField(dirtyKeys, dirtyBuildings);
        RebalanceTurretOwnershipField(dirtyKeys, dirtyBuildings);
        return true;
    }

    public bool ApplyMinedTileUpdateToAllBuildingOwnershipFields(IEnumerable<string>? tileKeys)
    {
        GetMiningPostOwnershipFieldObject().ApplyMinedTileUpdates(tileKeys);
        GetAlgaeFarmOwnershipFieldObject().ApplyMinedTileUpdates(tileKeys);
        GetBarracksOwnershipFieldObject().ApplyMinedTileUpdates(tileKeys);
        GetTurretOwnershipFieldObject().ApplyMinedTileUpdates(tileKeys);
        return true;
    }

    public MiningPostOwnership GetMiningPostOwnership(GridPoint location)
    {
        return MiningPostOwnership.From(GetMiningPostOwnershipFieldObject().GetOwnership(location));
    }

    public MiningPost? GetNearestMiningPost(GridPoint location)
    {
        return GetMiningPostOwnershipFieldObject().GetOwner(location);
    }

    public int GetNearestMiningPostDistance(GridPoint location)
    {
        return GetMiningPostOwnershipFieldObject().GetDistance(location);
    }

    public IReadOnlyCollection<MiningPost> GetAdjacentMiningPosts(MiningPost post)
    {
        return GetMiningPostOwnershipFieldObject().GetAdjacentBuildings(post);
    }

    public IReadOnlyDictionary<MiningPost, IReadOnlyCollection<MiningPost>> GetMiningPostAdjacencyGraph()
    {
        return GetMiningPostOwnershipFieldObject().GetAdjacencyGraph();
    }

    public BuildingOwnership<AlgaeFarm> GetAlgaeFarmOwnership(GridPoint location)
    {
        return GetAlgaeFarmOwnershipFieldObject().GetOwnership(location);
    }

    public AlgaeFarm? GetNearestAlgaeFarm(GridPoint location)
    {
        return GetAlgaeFarmOwnershipFieldObject().GetOwner(location);
    }

    public int GetNearestAlgaeFarmDistance(GridPoint location)
    {
        return GetAlgaeFarmOwnershipFieldObject().GetDistance(location);
    }

    public IReadOnlyCollection<AlgaeFarm> GetAdjacentAlgaeFarms(AlgaeFarm farm)
    {
        return GetAlgaeFarmOwnershipFieldObject().GetAdjacentBuildings(farm);
    }

    public IReadOnlyDictionary<AlgaeFarm, IReadOnlyCollection<AlgaeFarm>> GetAlgaeFarmAdjacencyGraph()
    {
        return GetAlgaeFarmOwnershipFieldObject().GetAdjacencyGraph();
    }

    public BuildingOwnership<Barracks> GetBarracksOwnership(GridPoint location)
    {
        return GetBarracksOwnershipFieldObject().GetOwnership(location);
    }

    public Barracks? GetNearestBarracks(GridPoint location)
    {
        return GetBarracksOwnershipFieldObject().GetOwner(location);
    }

    public int GetNearestBarracksDistance(GridPoint location)
    {
        return GetBarracksOwnershipFieldObject().GetDistance(location);
    }

    public IReadOnlyCollection<Barracks> GetAdjacentBarracks(Barracks barracks)
    {
        return GetBarracksOwnershipFieldObject().GetAdjacentBuildings(barracks);
    }

    public IReadOnlyDictionary<Barracks, IReadOnlyCollection<Barracks>> GetBarracksAdjacencyGraph()
    {
        return GetBarracksOwnershipFieldObject().GetAdjacencyGraph();
    }

    public BuildingOwnership<Turret> GetTurretOwnership(GridPoint location)
    {
        return GetTurretOwnershipFieldObject().GetOwnership(location);
    }

    public Turret? GetNearestTurret(GridPoint location)
    {
        return GetTurretOwnershipFieldObject().GetOwner(location);
    }

    public int GetNearestTurretDistance(GridPoint location)
    {
        return GetTurretOwnershipFieldObject().GetDistance(location);
    }

    public IReadOnlyCollection<Turret> GetAdjacentTurrets(Turret turret)
    {
        return GetTurretOwnershipFieldObject().GetAdjacentBuildings(turret);
    }

    public IReadOnlyDictionary<Turret, IReadOnlyCollection<Turret>> GetTurretAdjacencyGraph()
    {
        return GetTurretOwnershipFieldObject().GetAdjacencyGraph();
    }

    public IReadOnlyDictionary<string, Building> GetNearestBuildings(GridPoint location)
    {
        var nearestBuildings = new Dictionary<string, Building>(StringComparer.Ordinal);
        var buildingOwnerships = GetNearestBuildingOwnerships(location);
        foreach (var pair in buildingOwnerships)
        {
            if (pair.Value.Building is not null)
            {
                nearestBuildings[pair.Key] = pair.Value.Building;
            }
        }

        return nearestBuildings;
    }

    public IReadOnlyDictionary<string, int> GetNearestBuildingDistances(GridPoint location)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in GetNearestBuildingOwnerships(location))
        {
            if (pair.Value.IsOwned)
            {
                distances[pair.Key] = pair.Value.Distance;
            }
        }

        return distances;
    }

    public IReadOnlyDictionary<string, BuildingOwnershipSnapshot> GetNearestBuildingOwnerships(GridPoint location)
    {
        var ownerships = new Dictionary<string, BuildingOwnershipSnapshot>(StringComparer.Ordinal);
        AddBuildingOwnershipSnapshot(ownerships, GetMiningPostOwnershipFieldObject().BuildingName, GetMiningPostOwnershipFieldObject().GetOwnership(location));
        AddBuildingOwnershipSnapshot(ownerships, GetAlgaeFarmOwnershipFieldObject().BuildingName, GetAlgaeFarmOwnership(location));
        AddBuildingOwnershipSnapshot(ownerships, GetBarracksOwnershipFieldObject().BuildingName, GetBarracksOwnership(location));
        AddBuildingOwnershipSnapshot(ownerships, GetTurretOwnershipFieldObject().BuildingName, GetTurretOwnership(location));
        return ownerships;
    }

    private static void AddBuildingOwnershipSnapshot<TBuilding>(
        IDictionary<string, BuildingOwnershipSnapshot> ownerships,
        string buildingName,
        BuildingOwnership<TBuilding> ownership)
        where TBuilding : Building
    {
        if (!ownership.IsOwned || ownership.Building is null)
        {
            return;
        }

        ownerships[buildingName] = new BuildingOwnershipSnapshot(ownership.Building, ownership.Distance);
    }

    internal MiningPostMovementCacheEntry GetMiningPostMovementCacheEntry(MiningPost post)
    {
        if (!_miningPostMovementCache.TryGetValue(post, out var cacheEntry))
        {
            cacheEntry = new MiningPostMovementCacheEntry(post, this);
            _miningPostMovementCache[post] = cacheEntry;
        }

        return cacheEntry;
    }

    public bool InvalidateMiningPostMovementCache(MiningPost post, bool staleFailure = false)
    {
        if (!_miningPostMovementCache.TryGetValue(post, out var cacheEntry))
        {
            return false;
        }

        cacheEntry.ForceRebuild = true;
        if (staleFailure)
        {
            Session.MiningPostMovementTelemetry.RecordStalePathInvalidation();
        }

        return true;
    }

    public BfsField GetMiningPostMovementFieldObject(MiningPost post)
    {
        var cacheEntry = GetMiningPostMovementCacheEntry(post);
        var telemetry = Session.MiningPostMovementTelemetry;
        var forceRebuild = cacheEntry.ForceRebuild;
        var versionDirty = cacheEntry.TopologyVersion != TopologyVersion ||
                           cacheEntry.ReachabilityVersion != ReachabilityVersion;
        var hasCachedCoverage = cacheEntry.Field.HasCoverage();

        cacheEntry.Field.SetOwnerBuilding(post);
        cacheEntry.Field.SetCave(post.Cave ?? this);

        if (forceRebuild || !hasCachedCoverage)
        {
            telemetry.RecordCacheMiss();
            cacheEntry.Field.Rebuild();
            cacheEntry.TopologyVersion = TopologyVersion;
            cacheEntry.ReachabilityVersion = ReachabilityVersion;
            cacheEntry.ForceRebuild = false;
            telemetry.RecordCacheRebuild();
        }
        else
        {
            telemetry.RecordCacheHit();
            if (!versionDirty)
            {
                cacheEntry.TopologyVersion = TopologyVersion;
                cacheEntry.ReachabilityVersion = ReachabilityVersion;
            }
        }

        return cacheEntry.Field;
    }

    public List<GridPoint>? BuildPathToMiningPost(MiningPost post, GridPoint startLocation)
    {
        if (post.Location is null || post.TileArray.Count == 0)
        {
            return null;
        }

        return GetMiningPostMovementFieldObject(post).BuildPathFrom(startLocation, refresh: false);
    }

    public bool ShouldInvalidateMiningPostMovementCacheOnFailure(MiningPost post, GridPoint currentLocation, GridPoint attemptedLocation)
    {
        if (GridPoint.ManhattanDistance(currentLocation, attemptedLocation) != 1)
        {
            return true;
        }

        var tile = GetTile(attemptedLocation.ToString());
        if (tile is null || !tile.CreatureFits() || !IsTileReachable(tile))
        {
            return true;
        }

        if (_miningPostMovementCache.TryGetValue(post, out var cacheEntry))
        {
            return cacheEntry.TopologyVersion != TopologyVersion ||
                   cacheEntry.ReachabilityVersion != ReachabilityVersion ||
                   cacheEntry.ForceRebuild;
        }

        return false;
    }

    public BfsField GetBuildingBfsFieldObject(Building building)
    {
        building.BfsField ??= new BfsField(building.Name, "building", this, building);
        building.BfsField.SetOwnerBuilding(building);
        building.BfsField.SetCave(building.Cave ?? this);
        return building.BfsField;
    }

    private BfsField GetAccessibleBuildingBfsFieldObject(Building building, bool rebuildIfEmpty, GridPoint? accessLocation = null)
    {
        var field = GetBuildingBfsFieldObject(building);
        if (rebuildIfEmpty &&
            field.HasActiveBuildingTarget() &&
            !field.HasCoverage())
        {
            field.Rebuild();
        }

        if (accessLocation.HasValue &&
            field.HasActiveBuildingTarget() &&
            !field.IsUpdated())
        {
            var accessTile = GetTile(accessLocation.Value.ToString());
            if (accessTile is not null &&
                IsTileReachable(accessTile) &&
                field.GetFieldValue(accessLocation.Value, refresh: false) == int.MaxValue)
            {
                field.Rebuild();
            }
        }

        return field;
    }

    internal BfsField GetAccessibleBuildingBfsFieldObject(Building building, GridPoint accessLocation, bool rebuildIfEmpty = true)
    {
        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty, accessLocation);
    }

    public Dictionary<string, int> EnsureBuildingBfsField(Building building)
    {
        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true).GetField(false);
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
        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var timerStart = Stopwatch.GetTimestamp();
        try
        {
            if (field is null)
            {
                return null;
            }

            var tempField = new BfsField(cave: this);
            tempField.SetField(field);
            return tempField.BuildPathFrom(startLocation, false);
        }
        finally
        {
            NavigationInstrumentation.RecordBuildPathFromField(
                Stopwatch.GetElapsedTime(timerStart).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        }
    }

    public List<GridPoint>? BuildDirectPathToPoint(GridPoint startLocation, GridPoint destination)
    {
        var startTile = GetTile(startLocation.ToString());
        var destinationTile = GetTile(destination.ToString());
        if (startTile is null || destinationTile is null ||
            !startTile.CreatureFits() || !destinationTile.CreatureFits() ||
            !IsTileReachable(startTile) || !IsTileReachable(destinationTile))
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
            foreach (var neighbor in current.Neighbors.OrderBy(neighbor => neighbor.Key, StringComparer.Ordinal))
            {
                if (!neighbor.CreatureFits() || !IsTileReachable(neighbor) || !visited.Add(neighbor.Key))
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

    public MineablePathResult? BuildPathToNearestMineableType(
        GridPoint startLocation,
        MiningPost post,
        string mineableType,
        ISet<string>? reservedTileKeys = null)
    {
        var startTile = GetTile(startLocation.ToString());
        if (startTile is null || !startTile.CreatureFits() || !IsTileReachable(startTile))
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

            foreach (var neighbor in current.Neighbors.OrderBy(neighbor => neighbor.Key, StringComparer.Ordinal))
            {
                if (!neighbor.CreatureFits() || !IsTileReachable(neighbor) || !visited.Add(neighbor.Key))
                {
                    continue;
                }

                cameFrom[neighbor.Key] = current.Key;
                queue.Enqueue(neighbor);
            }
        }

        return null;
    }

    private MineablePathResult? TryCreateMineablePathResult(
        Tile current,
        MiningPost post,
        string mineableType,
        ISet<string> reservedTileKeys,
        IReadOnlyDictionary<string, string> cameFrom,
        string startKey)
    {
        if (string.Equals(mineableType, "wall", StringComparison.Ordinal))
        {
            foreach (var neighbor in current.Neighbors.OrderBy(neighbor => neighbor.Key, StringComparer.Ordinal))
            {
                if (!string.Equals(neighbor.Base, "wall", StringComparison.Ordinal) ||
                    reservedTileKeys.Contains(neighbor.Key) ||
                    !post.IsLocationInArea(neighbor.Coordinates))
                {
                    continue;
                }

                return new MineablePathResult(
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

        return new MineablePathResult(
            current.Key,
            current.Coordinates,
            ReconstructDirectPath(cameFrom, startKey, current.Key));
    }

    public Dictionary<string, int>? BuildPointBfsField(GridPoint destination)
    {
        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var timerStart = Stopwatch.GetTimestamp();
        try
        {
            var destinationTile = GetTile(destination);
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
        finally
        {
            NavigationInstrumentation.RecordBuildPointBfsField(
                Stopwatch.GetElapsedTime(timerStart).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        }
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

    public int GetBuildingBfsFieldValue(Building building, GridPoint location)
    {
        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true, accessLocation: location).GetFieldValue(location, refresh: false);
    }

    public GridPoint? GetBuildingBfsFieldNextStep(Building building, GridPoint location)
    {
        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true, accessLocation: location).GetNextStep(location, refresh: false);
    }

    public void ApplyMinedTileUpdateToAllBfsFields(string tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey))
        {
            return;
        }

        foreach (var field in Session.BfsFields.Values)
        {
            if (string.Equals(field.Type, "wall", StringComparison.Ordinal))
            {
                continue;
            }

            field.ApplyMinedTileUpdate(tileKey);
        }

        foreach (var building in _buildingList)
        {
            building.BfsField.SetOwnerBuilding(building);
            building.BfsField.SetCave(building.Cave ?? this);
            building.BfsField.ApplyMinedTileUpdate(tileKey);
        }

        foreach (var cacheEntry in _miningPostMovementCache.Values)
        {
            cacheEntry.Field.ApplyMinedTileUpdate(tileKey);
        }
    }

    public int RevealTile(Tile tile, ISet<string>? newlyRevealedKeys = null)
    {
        if (!RevealedTiles.Add(tile))
        {
            return 0;
        }

        newlyRevealedKeys?.Add(tile.Key);
        if (tile.EnemyOccupant is not null)
        {
            RefreshDangerState();
        }
        return 1;
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

    public int RevealCave(
        ISet<string>? newlyRevealedKeys = null,
        bool rebalanceFields = true,
        ISet<string>? newlyReachableKeys = null)
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
            revealedCount += RevealTile(currentTile, newlyRevealedKeys);
            TryAddReachableTile(currentTile, newlyReachableKeys);
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

                revealedCount += RevealTile(neighbor, newlyRevealedKeys);
                TryAddReachableTile(neighbor, newlyReachableKeys);
                revealedKeys.Add(neighbor.Key);
                if (neighbor.Base != "wall")
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (rebalanceFields && revealedKeys.Count > 0)
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

    private BfsField? RebuildWallBfsField()
    {
        if (_walls.Count == 0)
        {
            if (Session.BfsFields.TryGetValue("wall", out var existingField))
            {
                existingField.SetCave(this);
                existingField.ClearField();
                return existingField;
            }

            return null;
        }

        var wallField = GetBfsFieldObject("wall");
        wallField?.Rebuild();
        return wallField;
    }

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
        if (creature is Trilobite && !Session.Danger)
        {
            return true;
        }

        foreach (var fieldName in GetCreatureBfsFieldNames(creature))
        {
            GetBfsFieldObject(fieldName)?.MarkDirty(tileKeys, [], [creature]);
        }

        return true;
    }

    private bool MarkCreatureBfsFieldsDirty(Creature creature, string? firstTileKey, string? secondTileKey = null)
    {
        if (creature is Trilobite && !Session.Danger)
        {
            return true;
        }

        foreach (var fieldName in GetCreatureBfsFieldNames(creature))
        {
            var field = GetBfsFieldObject(fieldName);
            if (field is null)
            {
                continue;
            }

            field.MarkTileDirty(firstTileKey);
            field.MarkTileDirty(secondTileKey);
            field.MarkCreatureDirty(creature);
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

    private static bool ContainsProjectedBuilding(IReadOnlyList<Building>? projections, Building building)
    {
        if (projections is null)
        {
            return false;
        }

        for (var index = 0; index < projections.Count; index++)
        {
            if (ReferenceEquals(projections[index], building))
            {
                return true;
            }
        }

        return false;
    }

    private static void NotifyProjectedRadiusExit(Creature creature, Tile? fromTile, Tile? toTile)
    {
        if (fromTile is null || fromTile.Projections.Count == 0)
        {
            return;
        }

        var toProjections = toTile?.Projections;
        for (var index = 0; index < fromTile.Projections.Count; index++)
        {
            var building = fromTile.Projections[index];
            if (!ContainsProjectedBuilding(toProjections, building))
            {
                building.TargetNoLongerInRadius(creature);
            }
        }
    }

    private static void NotifyProjectedRadiusEntry(Creature creature, Tile? fromTile, Tile? toTile)
    {
        if (toTile is null || toTile.Projections.Count == 0)
        {
            return;
        }

        var fromProjections = fromTile?.Projections;
        for (var index = 0; index < toTile.Projections.Count; index++)
        {
            var building = toTile.Projections[index];
            if (!ContainsProjectedBuilding(fromProjections, building))
            {
                building.TargetInRadius(creature);
            }
        }
    }

    public bool SyncTrilobiteTileOccupancy(Creature creature, Tile? fromTile = null, Tile? toTile = null)
    {
        if (creature is Trilobite trilobite)
        {
            fromTile?.RemoveTrilobite(trilobite);
            NotifyProjectedRadiusExit(creature, fromTile, toTile);
            toTile?.AddTrilobite(trilobite);
            NotifyProjectedRadiusEntry(creature, fromTile, toTile);
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

        NotifyProjectedRadiusExit(creature, fromTile, toTile);

        if (toTile is not null)
        {
            toTile.SetEnemyOccupant(enemy);
            _enemyOccupancy[toTile.Key] = enemy;
        }

        NotifyProjectedRadiusEntry(creature, fromTile, toTile);

        return true;
    }

    private void RestoreStationedTrilobiteToLastTrackedTile(StationBuilding stationBuilding, Trilobite trilobite)
    {
        if (!ReferenceEquals(trilobite.HostedBuilding, stationBuilding))
        {
            return;
        }

        if (PlaceCreatureOnTile(trilobite, trilobite.Location, randomizeMovementOffset: false))
        {
            return;
        }

        if (stationBuilding.TryRestoreCreatureToTileSystem(trilobite))
        {
            return;
        }

        var fallbackTile = GetTile(trilobite.Location);
        trilobite.ReturnToTileSystem();
        trilobite.Cave = this;
        SyncTrilobiteTileOccupancy(trilobite, null, fallbackTile);
        trilobite.UpdateMovementOffset(false);
        MarkCreatureBfsFieldsDirty(trilobite, fallbackTile?.Key);
    }

    public bool RemoveCreatureFromTileSystem(Creature creature)
    {
        if (!creature.IsTrackedInTileSystem)
        {
            return true;
        }

        var currentTile = GetTile(creature.Location);
        SyncTrilobiteTileOccupancy(creature, currentTile, null);
        creature.LeaveTileSystem();
        MarkCreatureBfsFieldsDirty(creature, currentTile?.Key);
        return true;
    }

    public bool CanCreatureTraverseTile(Creature creature, Tile? tile)
    {
        return tile is not null && tile.CreatureFits(creature);
    }

    // Resource-complete scaffolds are temporary no-entry tiles for normal trilobite movement.
    public bool IsResourceCompleteScaffoldingTile(Tile? tile)
    {
        return tile?.Built is Scaffolding { ResourceComplete: true };
    }

    public bool IsResourceCompleteScaffoldingLocation(GridPoint location)
    {
        return IsResourceCompleteScaffoldingTile(GetTile(location));
    }

    public bool PlaceCreatureOnTile(Creature creature, GridPoint location, bool randomizeMovementOffset = false)
    {
        var tile = GetTile(location);
        if (tile is null || !CanCreatureTraverseTile(creature, tile))
        {
            return false;
        }

        if (creature is not Enemy && !IsTileReachable(tile))
        {
            return false;
        }

        var currentTile = creature.IsTrackedInTileSystem
            ? GetTile(creature.Location)
            : null;
        creature.ReturnToTileSystem();
        creature.Location = location;
        SyncTrilobiteTileOccupancy(creature, currentTile, tile);
        creature.UpdateMovementOffset(randomizeMovementOffset);
        MarkCreatureBfsFieldsDirty(creature, currentTile?.Key, tile.Key);
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

        var currentTile = creature.IsTrackedInTileSystem
            ? GetTile(creature.Location)
            : null;
        creature.ClearActionQueue();
        creature.NotifyTrackedByCreatureDied();
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
                case Ranch ranch:
                    ranch.RemoveAssignment(creature);
                    break;
                case StationBuilding station:
                    station.RemoveAssignment(creature);
                    break;
                case Scaffolding scaffold:
                    scaffold.RemoveAssignment(creature);
                    scaffold.ReleaseMaterialReservation(creature);
                    break;
            }
        }

        SyncTrilobiteTileOccupancy(creature, currentTile, null);

        if (removedEnemy)
        {
            HandleRemovedEnemySurfaceFeature((Enemy)creature);
            RefreshDangerState();
            if (!Session.Danger)
            {
                RestoreAllCreatureHealth();
            }
        }

        if (!(removedEnemy && !Session.Danger))
        {
            MarkCreatureBfsFieldsDirty(creature, currentTile?.Key);
        }

        creature.ReturnToTileSystem();
        creature.Location = GridPoint.Zero;
        creature.Cave = null;
        creature.UpdateMovementOffset(false);
        return true;
    }

    public bool Spawn(Creature creature, Tile tile)
    {
        if (tile.Base == "wall" || !CanCreatureTraverseTile(creature, tile))
        {
            return false;
        }

        if (creature is not Enemy && !IsTileReachable(tile))
        {
            return false;
        }

        creature.ReturnToTileSystem();
        creature.Location = GridPoint.Parse(tile.Key);
        creature.Cave = this;
        SyncTrilobiteTileOccupancy(creature, null, tile);
        creature.UpdateMovementOffset(false);

        if (creature is Enemy enemy)
        {
            Enemies.Add(enemy);
            _enemyList.Add(enemy);
        }
        else
        {
            var trilobite = (Trilobite)creature;
            Trilobites.Add(trilobite);
            _trilobiteList.Add(trilobite);
        }

        RefreshDangerState();
        MarkCreatureBfsFieldsDirty(creature, tile.Key);
        return true;
    }

    public bool MoveCreature(Creature creature, GridPoint nextLocation, bool allowResourceCompleteScaffolding = false)
    {
        if (!creature.IsTrackedInTileSystem)
        {
            return false;
        }

        var current = creature.Location;
        var nextTile = GetTile(nextLocation);
        if (nextTile is null || !CanCreatureTraverseTile(creature, nextTile))
        {
            return false;
        }

        if (!allowResourceCompleteScaffolding &&
            creature is Trilobite &&
            IsResourceCompleteScaffoldingTile(nextTile))
        {
            return false;
        }

        if (GridPoint.ManhattanDistance(current, nextLocation) != 1)
        {
            return false;
        }

        var currentTile = GetTile(current);
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
        MarkCreatureBfsFieldsDirty(creature, currentTile?.Key, nextTile.Key);
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
