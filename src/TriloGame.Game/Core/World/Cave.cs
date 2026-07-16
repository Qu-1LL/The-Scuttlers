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
    private readonly Dictionary<MiningPost, int> _miningPostAssignmentCounts = [];
    private readonly Dictionary<StationBuilding, int> _fighterStationAssignmentCounts = [];
    private Queen? _queenBuilding;
    private int _nextBuildingRuntimeId = 1;

    public Cave(GameSession session)
        : this(session, WorldGenerationMethod.Version0)
    {
    }

    public Cave(GameSession session, WorldGenerationMethod worldGenerationMethod)
        : this(session, generationMethod: worldGenerationMethod)
    {
    }

    internal Cave(GameSession session, bool generateDefaultMap)
        : this(session, generateDefaultMap ? WorldGenerationMethod.Version0 : null)
    {
    }

    private Cave(GameSession session, WorldGenerationMethod? generationMethod)
    {
        Session = session;
        if (generationMethod.HasValue)
        {
            new MapGenerator().Generate(this, generationMethod.Value);
        }
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

    public bool HasOpenAlgaeFarms { get; private set; }

    public bool HasAvailableMiningPostAssignments { get; private set; }

    public bool MiningPostBuildingsAdded { get; private set; }

    public bool BarracksBuildingsAdded { get; private set; }

    public long TopologyVersion { get; private set; }

    public long ReachabilityVersion { get; private set; }

    public event Action<BuildingNavigationTopologyChange>? NavigationTopologyChanged;

    public IReadOnlyList<Trilobite> GetTrilobiteList() => _trilobiteList;

    public IReadOnlyList<Enemy> GetEnemyList() => _enemyList;

    public IReadOnlyList<Vehicle> GetVehicles() => _vehicles;

    public IReadOnlyList<Building> GetBuildingList() => _buildingList;

    // Build an immutable value graph on the main thread before handing navigation work to Runtime.
    public BuildingNavigationTopologySnapshot CreateBuildingNavigationTopologySnapshot()
    {
        var tiles = new List<BuildingNavigationTileSnapshot>(GetTiles().Count);
        foreach (var tile in GetTiles())
        {
            tiles.Add(CreateBuildingNavigationTileSnapshot(tile));
        }

        return new BuildingNavigationTopologySnapshot(
            TopologyVersion,
            ReachabilityVersion,
            TileCapacity,
            tiles,
            CreateBuildingNavigationBuildingSnapshots());
    }

    // Capture only changed tiles plus their neighbors; the worker retains all other topology.
    public BuildingNavigationTopologyDelta CreateBuildingNavigationTopologyDelta(
        IEnumerable<string>? dirtyTileKeys,
        bool structuralChange = false)
    {
        var requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in dirtyTileKeys ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                requestedKeys.Add(key);
            }
        }

        var updateIds = new HashSet<int>();
        var updateKeys = new HashSet<string>(requestedKeys, StringComparer.Ordinal);
        var removedKeys = new List<string>();
        var dirtyIds = new List<int>();
        foreach (var key in requestedKeys)
        {
            var tile = GetTile(key);
            if (tile is null)
            {
                removedKeys.Add(key);
                continue;
            }

            dirtyIds.Add(tile.Id);
            updateIds.Add(tile.Id);
            foreach (var neighbor in tile.Neighbors)
            {
                updateKeys.Add(neighbor.Key);
            }
        }

        foreach (var key in updateKeys)
        {
            var tile = GetTile(key);
            if (tile is not null)
            {
                updateIds.Add(tile.Id);
            }
        }

        var sortedUpdateIds = updateIds.ToArray();
        Array.Sort(sortedUpdateIds);
        var tileUpdates = new List<BuildingNavigationTileSnapshot>(sortedUpdateIds.Length);
        for (var index = 0; index < sortedUpdateIds.Length; index++)
        {
            var tile = GetTileById(sortedUpdateIds[index]);
            if (tile is not null)
            {
                tileUpdates.Add(CreateBuildingNavigationTileSnapshot(tile));
            }
        }

        var sortedDirtyIds = dirtyIds.Distinct().ToArray();
        Array.Sort(sortedDirtyIds);
        removedKeys.Sort(StringComparer.Ordinal);
        return new BuildingNavigationTopologyDelta(
            TopologyVersion,
            ReachabilityVersion,
            TileCapacity,
            tileUpdates,
            removedKeys,
            sortedDirtyIds,
            structuralChange ? CreateBuildingNavigationBuildingSnapshots() : null);
    }

    private BuildingNavigationTileSnapshot CreateBuildingNavigationTileSnapshot(Tile tile)
    {
        var neighbors = tile.Neighbors
            .OrderBy(neighbor => neighbor.Key, StringComparer.Ordinal)
            .Select(neighbor => neighbor.Id)
            .ToArray();
        return new BuildingNavigationTileSnapshot(
            tile.Id,
            tile.Key,
            tile.CreatureFits(),
            IsTileReachable(tile),
            neighbors);
    }

    private IReadOnlyList<BuildingNavigationBuildingSnapshot> CreateBuildingNavigationBuildingSnapshots()
    {
        var buildings = new List<BuildingNavigationBuildingSnapshot>();
        for (var index = 0; index < _buildingList.Count; index++)
        {
            var building = _buildingList[index];
            if (!building.MaintainsNavigationField ||
                building.NavigationFieldMaintenanceMode != BuildingNavigationMaintenanceMode.Asynchronous ||
                building.RuntimeId <= 0 ||
                building.Location is null ||
                building.TileArray.Count == 0)
            {
                continue;
            }

            var footprint = new List<int>();
            for (var y = 0; y < building.OpenMap.Length; y++)
            {
                for (var x = 0; x < building.OpenMap[y].Length; x++)
                {
                    if (building.OpenMap[y][x] > 1)
                    {
                        continue;
                    }

                    var tile = GetTile(new GridPoint(building.Location.Value.X + x, building.Location.Value.Y + y));
                    if (tile is not null)
                    {
                        footprint.Add(tile.Id);
                    }
                }
            }

            var seeds = building.GetNavigationSeedTiles(this).Select(tile => tile.Id).ToArray();
            buildings.Add(new BuildingNavigationBuildingSnapshot(
                building.RuntimeId,
                index,
                building.Name,
                building.NavigationSeedMode,
                building.NavigationFieldMaintenanceMode,
                footprint,
                seeds,
                building.PendingNavigationFieldInheritance));
        }

        return buildings;
    }

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

    internal void NotifyBuildingNavigationTopologyChanged(IEnumerable<string>? dirtyTileKeys, bool structuralChange = false)
    {
        var dirtyKeys = (dirtyTileKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        NavigationTopologyChanged?.Invoke(new BuildingNavigationTopologyChange(dirtyKeys, structuralChange));
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
        return EvaluateBuildPlacement(building, location, preserveReachability).CanBuild &&
               CanPlaceRanchBuilding(building, location);
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

    public bool Build(Building building, GridPoint location, bool preserveReachability = false)
    {
        if (!CanBuild(building, location, preserveReachability))
        {
            return false;
        }

        PlaceBuildingUnchecked(building, location);
        OnRanchBuildingBuilt(building);
        FinalizeBuiltBuildings([building]);
        return true;
    }

    public bool ReplaceBuilding(Building existingBuilding, Building replacementBuilding, GridPoint location, object? source = null)
    {
        if (!EvaluateBuildReplacement(existingBuilding, replacementBuilding, location).CanBuild)
        {
            return false;
        }

        var inheritedNavigationField = existingBuilding.PublishedNavigationField;

        if (!RemoveBuilding(existingBuilding, source))
        {
            return false;
        }

        replacementBuilding.SetPendingNavigationFieldInheritance(inheritedNavigationField);
        PlaceBuildingUnchecked(replacementBuilding, location);
        OnRanchBuildingBuilt(replacementBuilding);
        FinalizeBuiltBuildings([replacementBuilding]);
        return true;
    }

    private void PlaceBuildingUnchecked(Building building, GridPoint location)
    {
        if (building.RuntimeId == 0)
        {
            building.AssignRuntimeId(_nextBuildingRuntimeId++);
        }

        Buildings.Add(building);
        _buildingList.Add(building);
        building.Cave = this;
        building.TileArray = [];
        building.Location = location;

        for (var x = 0; x < building.Size.X; x++)
        {
            for (var y = 0; y < building.Size.Y; y++)
            {
                var tile = GetTile(new GridPoint(location.X + x, location.Y + y).ToString());
                if (tile is null)
                {
                    continue;
                }

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
        var reachability = RefreshReachableTiles(refreshQueenNavigationField: false);
        var navigationDirtyKeys = dirtyKeys.Concat(reachability.ChangedKeys).Distinct(StringComparer.Ordinal).ToArray();
        MarkAllBuildingFieldsDirty(navigationDirtyKeys, builtBuildings, []);
        for (var index = 0; index < builtBuildings.Count; index++)
        {
            var building = builtBuildings[index];
            if (!building.MaintainsNavigationField)
            {
                continue;
            }

            if (building.NavigationFieldMaintenanceMode == BuildingNavigationMaintenanceMode.Synchronous ||
                Session.BuildingNavigationFieldService is null)
            {
                var buildingField = GetBuildingBfsFieldObject(building);
                buildingField.Rebuild();
                buildingField.MarkDirty(navigationDirtyKeys, builtBuildings, []);
            }
        }

        if (builtBuildings.Any(static building => building is Wall))
        {
            RebuildWallBfsField();
        }

        MarkQueenNavigationFieldDirty(navigationDirtyKeys);
        NotifyBuildingNavigationTopologyChanged(navigationDirtyKeys, structuralChange: true);
        for (var index = 0; index < builtBuildings.Count; index++)
        {
            builtBuildings[index].ClearPendingNavigationFieldInheritance();
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

            if (creature is Trilobite trilobite && ReferenceEquals(trilobite.BuilderSourceBuilding, building))
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
        building.ClearPublishedNavigationField();

        var dirtyKeys = new List<string>();
        foreach (var tile in building.TileArray)
        {
            dirtyKeys.Add(tile.Key);
            if (ReferenceEquals(tile.Built, building))
            {
                tile.SetBuilt(null);
                tile.CreatureCanFit = true;
            }
        }

        OnRanchBuildingRemoved(building);
        building.CleanupBeforeRemoval(source);
        AdvanceTopologyVersion();
        var reachability = RefreshReachableTiles(refreshQueenNavigationField: false);
        var navigationDirtyKeys = dirtyKeys.Concat(reachability.ChangedKeys).Distinct(StringComparer.Ordinal).ToArray();
        MarkAllBuildingFieldsDirty(navigationDirtyKeys, [building], []);
        building.TileArray = [];
        building.Location = null;
        building.Cave = null;
        if (building is Wall)
        {
            RebuildWallBfsField();
        }

        foreach (var creature in affectedCreatures)
        {
            creature.RestartBehavior(false);
        }

        MarkQueenNavigationFieldDirty(navigationDirtyKeys);
        NotifyBuildingNavigationTopologyChanged(navigationDirtyKeys, structuralChange: true);

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

    // Mark the queen's synchronous field after topology changes; normal repair is local and lazy.
    private void RefreshQueenNavigationField(IEnumerable<string>? dirtyKeys)
    {
        MarkQueenNavigationFieldDirty(dirtyKeys);
        var queen = GetQueenBuilding();
        if (queen is null || queen.Cave is null || queen.TileArray.Count == 0)
        {
            return;
        }

        var field = GetBuildingBfsFieldObject(queen);
        field.RefreshBuildingIncrementally(field.UpdatedTiles);
    }

    // Queue only the changed region; the synchronous queen field is refreshed lazily on demand.
    private void MarkQueenNavigationFieldDirty(IEnumerable<string>? dirtyKeys)
    {
        var queen = GetQueenBuilding();
        if (queen is null || queen.Cave is null || queen.TileArray.Count == 0)
        {
            return;
        }

        var field = GetBuildingBfsFieldObject(queen);
        field.MarkDirty(dirtyKeys, [], []);
    }

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

    public ReachabilityRefreshResult RefreshReachableTiles(bool refreshQueenNavigationField = true)
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
                if (refreshQueenNavigationField)
                {
                    RefreshQueenNavigationField(changedKeys);
                }
                NotifyBuildingNavigationTopologyChanged(changedKeys);
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
            if (refreshQueenNavigationField)
            {
                RefreshQueenNavigationField(finalChangedKeys);
            }
            NotifyBuildingNavigationTopologyChanged(finalChangedKeys);
        }

        return new ReachabilityRefreshResult(ReachableTiles.Count, finalChangedKeys);
    }
}

public readonly record struct ReachabilityRefreshResult(int Count, IReadOnlyList<string> ChangedKeys);

public sealed partial class Cave
{
    // Query ownership by comparing the already-maintained per-building navigation fields.
    private BuildingOwnership<TBuilding> FindNearestBuilding<TBuilding>(
        IReadOnlyList<TBuilding> buildings,
        GridPoint location)
        where TBuilding : Building
    {
        TBuilding? nearest = null;
        var nearestDistance = int.MaxValue;
        var tile = GetTile(location.ToString());
        if (tile is null || !tile.CreatureFits() || !IsTileReachable(tile))
        {
            return new BuildingOwnership<TBuilding>(null, int.MaxValue);
        }

        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (building.Location is null || building.TileArray.Count == 0)
            {
                continue;
            }

            var distance = GetBuildingNavigationDistance(building, location);
            if (distance == int.MaxValue || distance > nearestDistance)
            {
                continue;
            }

            var order = CompareBuildingOwnershipOrder(building, nearest);
            if (distance == nearestDistance && order >= 0)
            {
                continue;
            }

            nearest = building;
            nearestDistance = distance;
        }

        return new BuildingOwnership<TBuilding>(nearest, nearestDistance);
    }

    private int GetBuildingPlacementOrder(Building building)
    {
        for (var index = 0; index < _buildingList.Count; index++)
        {
            if (ReferenceEquals(_buildingList[index], building))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    // Derive adjacency lazily from neighboring tiles' nearest per-building fields.
    private IReadOnlyCollection<TBuilding> FindAdjacentBuildings<TBuilding>(
        TBuilding building,
        IReadOnlyList<TBuilding> buildings)
        where TBuilding : Building
    {
        var ownersByTileId = BuildNearestOwnerMap(buildings);
        var adjacent = new List<TBuilding>();
        foreach (var tile in ReachableTiles)
        {
            if (!ownersByTileId.TryGetValue(tile.Id, out var owner) || !ReferenceEquals(owner, building))
            {
                continue;
            }

            foreach (var neighbor in tile.Neighbors)
            {
                if (!ownersByTileId.TryGetValue(neighbor.Id, out var neighborOwner) ||
                    neighborOwner is null ||
                    ReferenceEquals(neighborOwner, building) ||
                    adjacent.Contains(neighborOwner))
                {
                    continue;
                }

                adjacent.Add(neighborOwner);
            }
        }

        adjacent.Sort(CompareBuildingPlacementOrder);
        return adjacent;
    }

    private Dictionary<int, TBuilding?> BuildNearestOwnerMap<TBuilding>(IReadOnlyList<TBuilding> buildings)
        where TBuilding : Building
    {
        var ownersByTileId = new Dictionary<int, TBuilding?>();
        foreach (var tile in ReachableTiles)
        {
            ownersByTileId[tile.Id] = FindNearestBuilding(buildings, tile.Coordinates).Building;
        }

        return ownersByTileId;
    }

    private int CompareBuildingPlacementOrder<TBuilding>(TBuilding left, TBuilding right)
        where TBuilding : Building
    {
        var order = GetBuildingPlacementOrder(left).CompareTo(GetBuildingPlacementOrder(right));
        if (order != 0)
        {
            return order;
        }

        var locationOrder = string.CompareOrdinal(left.Location?.ToString(), right.Location?.ToString());
        return locationOrder != 0 ? locationOrder : string.CompareOrdinal(left.Name, right.Name);
    }

    private static int CompareBuildingOwnershipOrder(Building left, Building? right)
    {
        if (right is null)
        {
            return -1;
        }

        var locationOrder = string.CompareOrdinal(left.Location?.ToString(), right.Location?.ToString());
        return locationOrder != 0 ? locationOrder : string.CompareOrdinal(left.Name, right.Name);
    }

    public MiningPostOwnership GetMiningPostOwnership(GridPoint location)
    {
        return MiningPostOwnership.From(FindNearestBuilding(_miningPosts, location));
    }

    public MiningPost? GetNearestMiningPost(GridPoint location) => GetMiningPostOwnership(location).Post;

    public int GetNearestMiningPostDistance(GridPoint location) => GetMiningPostOwnership(location).Distance;

    public IReadOnlyCollection<MiningPost> GetAdjacentMiningPosts(MiningPost post) =>
        FindAdjacentBuildings(post, _miningPosts);

    public IReadOnlyDictionary<MiningPost, IReadOnlyCollection<MiningPost>> GetMiningPostAdjacencyGraph() =>
        BuildAdjacencyGraph(_miningPosts);

    public BuildingOwnership<AlgaeFarm> GetAlgaeFarmOwnership(GridPoint location) =>
        FindNearestBuilding(_algaeFarms, location);

    public AlgaeFarm? GetNearestAlgaeFarm(GridPoint location) => GetAlgaeFarmOwnership(location).Building;

    public int GetNearestAlgaeFarmDistance(GridPoint location) => GetAlgaeFarmOwnership(location).Distance;

    public IReadOnlyCollection<AlgaeFarm> GetAdjacentAlgaeFarms(AlgaeFarm farm) =>
        FindAdjacentBuildings(farm, _algaeFarms);

    public IReadOnlyDictionary<AlgaeFarm, IReadOnlyCollection<AlgaeFarm>> GetAlgaeFarmAdjacencyGraph() =>
        BuildAdjacencyGraph(_algaeFarms);

    public BuildingOwnership<Barracks> GetBarracksOwnership(GridPoint location) =>
        FindNearestBuilding(_barracks, location);

    public Barracks? GetNearestBarracks(GridPoint location) => GetBarracksOwnership(location).Building;

    public int GetNearestBarracksDistance(GridPoint location) => GetBarracksOwnership(location).Distance;

    public IReadOnlyCollection<Barracks> GetAdjacentBarracks(Barracks barracks) =>
        FindAdjacentBuildings(barracks, _barracks);

    public IReadOnlyDictionary<Barracks, IReadOnlyCollection<Barracks>> GetBarracksAdjacencyGraph() =>
        BuildAdjacencyGraph(_barracks);

    public BuildingOwnership<Turret> GetTurretOwnership(GridPoint location) =>
        FindNearestBuilding(_turrets, location);

    public Turret? GetNearestTurret(GridPoint location) => GetTurretOwnership(location).Building;

    public int GetNearestTurretDistance(GridPoint location) => GetTurretOwnership(location).Distance;

    public IReadOnlyCollection<Turret> GetAdjacentTurrets(Turret turret) =>
        FindAdjacentBuildings(turret, _turrets);

    public IReadOnlyDictionary<Turret, IReadOnlyCollection<Turret>> GetTurretAdjacencyGraph() =>
        BuildAdjacencyGraph(_turrets);

    private IReadOnlyDictionary<TBuilding, IReadOnlyCollection<TBuilding>> BuildAdjacencyGraph<TBuilding>(
        IReadOnlyList<TBuilding> buildings)
        where TBuilding : Building
    {
        var graph = new Dictionary<TBuilding, IReadOnlyCollection<TBuilding>>();
        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            graph[building] = FindAdjacentBuildings(building, buildings);
        }

        return graph;
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
        AddBuildingOwnershipSnapshot(ownerships, "Mining Post", MiningPostOwnershipToBuildingOwnership(GetMiningPostOwnership(location)));
        AddBuildingOwnershipSnapshot(ownerships, "Algae Farm", GetAlgaeFarmOwnership(location));
        AddBuildingOwnershipSnapshot(ownerships, "Barracks", GetBarracksOwnership(location));
        AddBuildingOwnershipSnapshot(ownerships, "Turret", GetTurretOwnership(location));
        return ownerships;
    }

    private static BuildingOwnership<MiningPost> MiningPostOwnershipToBuildingOwnership(MiningPostOwnership ownership)
    {
        return new BuildingOwnership<MiningPost>(ownership.Post, ownership.Distance);
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

    public bool InvalidateMiningPostMovementCache(MiningPost post, bool staleFailure = false)
    {
        if (staleFailure)
        {
            Session.MiningPostMovementTelemetry.RecordStalePathInvalidation();
        }

        // The general field is topology-driven. A movement failure only clears the legacy
        // compatibility field; async snapshots are replaced by topology commands.
        if (post.PublishedNavigationField is not null)
        {
            return true;
        }

        post.BfsField.ClearField();
        return true;
    }

    public BfsField GetMiningPostMovementFieldObject(MiningPost post)
    {
        return GetBuildingBfsFieldObject(post);
    }

    public List<GridPoint>? BuildPathToMiningPost(MiningPost post, GridPoint startLocation)
    {
        if (post.Location is null || post.TileArray.Count == 0)
        {
            return null;
        }

        if (post.PublishedNavigationField is null && !UsesAsyncBuildingNavigationFields)
        {
            return GetMiningPostMovementFieldObject(post).BuildPathFrom(startLocation, refresh: false);
        }

        return BuildPathToBuilding(post, startLocation);
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

        return post.PublishedNavigationField is null && post.BfsField.IsUpdated();
    }

    public BfsField GetBuildingBfsFieldObject(Building building)
    {
        building.BfsField.SetOwnerBuilding(building);
        building.BfsField.SetCave(building.Cave ?? this);
        return building.BfsField;
    }

    public bool UsesAsyncBuildingNavigationFields => Session.BuildingNavigationFieldService?.IsAttached == true;

    public bool UsesAsyncBuildingNavigationField(Building building)
    {
        return UsesAsyncBuildingNavigationFields &&
               building.NavigationFieldMaintenanceMode == BuildingNavigationMaintenanceMode.Asynchronous;
    }

    public int GetBuildingNavigationDistance(Building building, GridPoint location)
    {
        if (!building.MaintainsNavigationField)
        {
            return int.MaxValue;
        }

        if (building.PublishedNavigationField is { } snapshot &&
            GetTile(location) is { } tile)
        {
            return snapshot.GetDistance(tile.Id);
        }

        if (UsesAsyncBuildingNavigationField(building))
        {
            return int.MaxValue;
        }

        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true, accessLocation: location)
            .GetFieldValue(location, refresh: false);
    }

    public GridPoint? GetBuildingNavigationNextStep(Building building, GridPoint location)
    {
        if (!building.MaintainsNavigationField)
        {
            return null;
        }

        if (building.PublishedNavigationField is { } snapshot &&
            GetTile(location) is { } tile)
        {
            var nextId = snapshot.GetNextStepTileId(tile.Id);
            return nextId < 0 ? null : GetTileById(nextId)?.Coordinates;
        }

        if (UsesAsyncBuildingNavigationField(building))
        {
            return null;
        }

        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true, accessLocation: location)
            .GetNextStep(location, refresh: false);
    }

    // Follow only the last stable published snapshot; this method never waits for Runtime.
    public bool TryMoveAlongBuildingNavigationField(Creature creature, Building building)
    {
        var distance = GetBuildingNavigationDistance(building, creature.Location);
        if (distance == 0)
        {
            return true;
        }

        var next = GetBuildingNavigationNextStep(building, creature.Location);
        return next.HasValue && MoveCreature(creature, next.Value);
    }

    public List<GridPoint>? BuildPathToBuilding(Building building, GridPoint startLocation)
    {
        if (!building.MaintainsNavigationField)
        {
            return null;
        }

        if (UsesAsyncBuildingNavigationField(building) &&
            building.PublishedNavigationField is null)
        {
            return null;
        }

        if (building.PublishedNavigationField is not { } snapshot ||
            GetTile(startLocation) is not { } startTile)
        {
            return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true, accessLocation: startLocation)
                .BuildPathFrom(startLocation, refresh: false);
        }

        if (snapshot.GetDistance(startTile.Id) == int.MaxValue)
        {
            return null;
        }

        var path = new List<GridPoint> { startLocation };
        var current = startTile;
        var guard = 0;
        while (snapshot.GetDistance(current.Id) > 0 && guard++ < TileCapacity)
        {
            var nextId = snapshot.GetNextStepTileId(current.Id);
            var next = nextId < 0 ? null : GetTileById(nextId);
            if (next is null)
            {
                return null;
            }

            path.Add(next.Coordinates);
            current = next;
        }

        return snapshot.GetDistance(current.Id) == 0 ? path : null;
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

        if (building is Queen && field.HasActiveBuildingTarget() && !field.IsUpdated())
        {
            field.RefreshBuildingIncrementally(field.UpdatedTiles);
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
        if (!building.MaintainsNavigationField)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        if (building.PublishedNavigationField is { } snapshot)
        {
            var published = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var tile in GetTiles())
            {
                var distance = snapshot.GetDistance(tile.Id);
                if (distance != int.MaxValue)
                {
                    published[tile.Key] = distance;
                }
            }

            return published;
        }

        if (UsesAsyncBuildingNavigationField(building))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return GetAccessibleBuildingBfsFieldObject(building, rebuildIfEmpty: true).GetField(false);
    }

    public bool MarkAllBuildingFieldsDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? dirtyBuildings = null, IEnumerable<Creature>? dirtyCreatures = null)
    {
        foreach (var building in _buildingList)
        {
            if (!building.MaintainsNavigationField || UsesAsyncBuildingNavigationField(building))
            {
                continue;
            }

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
        return CavePathfinder.BuildDirectPathToPoint(this, startLocation, destination);
    }

    public List<GridPoint>? BuildPathToNearestEmptyTile(GridPoint startLocation)
    {
        return CavePathfinder.BuildPathToNearestEmptyTile(this, startLocation);
    }

    public MineablePathResult? BuildPathToNearestMineableType(
        GridPoint startLocation,
        MiningPost post,
        string mineableType,
        ISet<string>? reservedTileKeys = null)
    {
        return CavePathfinder.BuildPathToNearestMineableType(this, startLocation, post, mineableType, reservedTileKeys);
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

    public int GetBuildingBfsFieldValue(Building building, GridPoint location)
    {
        return GetBuildingNavigationDistance(building, location);
    }

    public GridPoint? GetBuildingBfsFieldNextStep(Building building, GridPoint location)
    {
        return GetBuildingNavigationNextStep(building, location);
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
            if (!building.MaintainsNavigationField || UsesAsyncBuildingNavigationField(building))
            {
                continue;
            }

            building.BfsField.SetOwnerBuilding(building);
            building.BfsField.SetCave(building.Cave ?? this);
            building.BfsField.ApplyMinedTileUpdate(tileKey);
        }

        NotifyBuildingNavigationTopologyChanged([tileKey]);
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

}
