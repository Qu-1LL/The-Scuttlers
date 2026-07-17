using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Movement;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;
using System.Diagnostics;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave : Graph
{
    public const int MaximumPointRouteBuildsPerTick = 32;
    public readonly record struct MineablePathResult(string TileKey, GridPoint NavigationTarget, List<GridPoint> Path);
    private readonly List<Trilobite> _trilobiteList = [];
    private readonly List<Enemy> _enemyList = [];
    private readonly List<Vehicle> _vehicles = [];
    private readonly List<Building> _buildingList = [];
    private readonly List<MiningPost> _miningPosts = [];
    private readonly List<AlgaeFarm> _algaeFarms = [];
    private readonly List<Barracks> _barracks = [];
    private readonly List<Turret> _turrets = [];
    private readonly List<Radar> _radars = [];
    private readonly List<Wall> _walls = [];
    private readonly List<Scaffolding> _scaffolds = [];
    private readonly Dictionary<string, Vehicle> _vehicleOccupancy = new(StringComparer.Ordinal);
    private readonly Dictionary<MiningPost, MiningPostMovementCacheEntry> _miningPostMovementCache = [];
    private readonly Dictionary<MiningPost, int> _miningPostAssignmentCounts = [];
    private readonly Dictionary<StationBuilding, int> _fighterStationAssignmentCounts = [];
    private readonly CreatureMovementSystem _creatureMovementSystem;
    private readonly PointRouteFieldCache _pointRouteFieldCache;
    private int _pointRouteBudgetTick = -1;
    private int _pointRouteBuildCount;
    private readonly MiningPostOwnershipField _miningPostOwnershipField;
    private readonly AlgaeFarmOwnershipField _algaeFarmOwnershipField;
    private readonly BarracksOwnershipField _barracksOwnershipField;
    private readonly TurretOwnershipField _turretOwnershipField;
    private Queen? _queenBuilding;

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
        _creatureMovementSystem = new CreatureMovementSystem();
        _pointRouteFieldCache = new PointRouteFieldCache(this);
        if (generationMethod.HasValue)
        {
            new MapGenerator().Generate(this, generationMethod.Value);
        }
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

    public IReadOnlyList<Radar> GetRadars() => _radars;

    public IReadOnlyList<Wall> GetWalls() => _walls;

    public IReadOnlyList<Scaffolding> GetScaffoldingList() => _scaffolds;

    public void AdvanceCreatureMovement()
    {
        _creatureMovementSystem.Advance(this);
        Session.Combat.MarkTargetSpatialDirty();
    }

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
                !HasCreatureInCell(tile.Coordinates) &&
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
                !HasCreatureInCell(current.Coordinates) &&
                !IsEnemySpawnBlockedTile(current))
            {
                selectedTiles.Add(current);
            }

            foreach (var neighbor in RandomUtil.Shuffle(current.Neighbors))
            {
                if (!visited.Add(neighbor.Key) ||
                    IsTileRevealed(neighbor) ||
                    !neighbor.CreatureFits() ||
                    HasCreatureInCell(neighbor.Coordinates) ||
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
            case Radar radar:
                _radars.Add(radar);
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
            case Radar radar:
                _radars.Remove(radar);
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

        if (!RemoveBuilding(existingBuilding, source))
        {
            return false;
        }

        PlaceBuildingUnchecked(replacementBuilding, location);
        OnRanchBuildingBuilt(replacementBuilding);
        FinalizeBuiltBuildings([replacementBuilding]);
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
            if (ReferenceEquals(creature.ReservedZone?.Owner, building))
            {
                creature.ReleaseInteractionReservation();
                creatureWasAffected = true;
            }

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
                    creatureWasAffected = stationBuilding.TryRestoreCreatureLocomotion(creature);
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

            creature.ClearTaskQueue();
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
                tile.CreatureCanFit = true;
            }
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
        return CavePathfinder.BuildDirectPathToPoint(this, startLocation, destination);
    }

    public List<GridPoint>? BuildPointPath(GridPoint startLocation, GridPoint destination)
    {
        return BuildPointPath(startLocation, destination, out _);
    }

    internal List<GridPoint>? BuildPointPath(GridPoint startLocation, GridPoint destination, out bool deferred)
    {
        return _pointRouteFieldCache.TryBuildPath(startLocation, destination, out var path, out deferred)
            ? path
            : null;
    }

    internal List<GridPoint>? BuildPointPathChunk(
        GridPoint startLocation,
        GridPoint destination,
        int maximumSteps,
        out bool deferred,
        out bool reachedDestination)
    {
        return _pointRouteFieldCache.TryBuildPathChunk(
            startLocation,
            destination,
            maximumSteps,
            out var path,
            out deferred,
            out reachedDestination)
            ? path
            : null;
    }

    internal int PointRouteFieldCacheCount => _pointRouteFieldCache.Count;

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
        if (GetEnemyAtTileKey(tile.Key) is not null)
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
            revealedCount += RevealTile(tile, revealedKeys);
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
            NotifyMineableTilesChanged(revealedKeys);
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
                    revealedCount += RevealTile(tile, revealedKeys);
                    break;
                }
            }
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
            NotifyMineableTilesChanged(revealedKeys);
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
                    revealedCount += RevealTile(tile, revealedKeys);
                }
            }
        }

        if (revealedKeys.Count > 0)
        {
            RebalanceAllBfsFields(revealedKeys, [], []);
            NotifyMineableTilesChanged(revealedKeys);
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
            if (IsTileRevealed(currentTile))
            {
                revealedKeys.Add(currentTile.Key);
            }

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
                if (IsTileRevealed(neighbor))
                {
                    revealedKeys.Add(neighbor.Key);
                }
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

        if (newlyRevealedKeys is not null && newlyRevealedKeys.Count > 0)
        {
            NotifyMineableTilesChanged(newlyRevealedKeys);
        }
        else if (revealedKeys.Count > 0)
        {
            NotifyMineableTilesChanged(revealedKeys);
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

    internal bool TryConsumePointRouteBuildBudget()
    {
        if (_pointRouteBudgetTick != Session.TickCount)
        {
            _pointRouteBudgetTick = Session.TickCount;
            _pointRouteBuildCount = 0;
        }

        if (_pointRouteBuildCount >= MaximumPointRouteBuildsPerTick)
        {
            return false;
        }

        _pointRouteBuildCount++;
        return true;
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
        if (!Session.Danger)
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

    // Cell transitions update projected influence areas without making tiles own moving bodies.
    private static void NotifyCreatureCellTransition(Creature creature, Tile? fromTile, Tile? toTile)
    {
        NotifyProjectedRadiusExit(creature, fromTile, toTile);
        NotifyProjectedRadiusEntry(creature, fromTile, toTile);
    }

    private void RestoreStationedTrilobiteToLastTrackedTile(StationBuilding stationBuilding, Trilobite trilobite)
    {
        if (!ReferenceEquals(trilobite.HostedBuilding, stationBuilding))
        {
            return;
        }

        if (PlaceCreatureOnTile(trilobite, trilobite.LocomotionRestoreCell))
        {
            return;
        }

        if (stationBuilding.TryRestoreCreatureLocomotion(trilobite))
        {
            return;
        }

        trilobite.DisableLocomotion();
        trilobite.Cave = this;
    }

    public bool DisableCreatureLocomotion(Creature creature)
    {
        if (!creature.IsLocomotionEnabled)
        {
            return true;
        }

        var currentTile = GetTile(creature.Location);
        NotifyCreatureCellTransition(creature, currentTile, null);
        creature.DisableLocomotion();
        MarkCreatureBfsFieldsDirty(creature, currentTile?.Key);
        return true;
    }

    public bool CanCreatureTraverseTile(Creature creature, Tile? tile)
    {
        return tile is not null && tile.CreatureFits(creature);
    }

    // Validate a circular body against blocked and missing tile rectangles around its bounds.
    public bool CanCreatureOccupyWorldPosition(Creature creature, WorldPoint position)
    {
        var radius = creature.CollisionRadius;
        var minCell = new WorldPoint(position.X - radius, position.Y - radius).ToGridPoint();
        var maxCell = new WorldPoint(position.X + radius, position.Y + radius).ToGridPoint();
        var radiusSquared = (long)radius * radius;

        for (var y = minCell.Y; y <= maxCell.Y; y++)
        {
            for (var x = minCell.X; x <= maxCell.X; x++)
            {
                var tile = GetTile(new GridPoint(x, y));
                if (CanCreatureTraverseTile(creature, tile))
                {
                    continue;
                }

                var centerX = x * WorldUnits.UnitsPerTile;
                var centerY = y * WorldUnits.UnitsPerTile;
                var minX = centerX - WorldUnits.UnitsPerHalfTile;
                var maxX = centerX + WorldUnits.UnitsPerHalfTile;
                var minY = centerY - WorldUnits.UnitsPerHalfTile;
                var maxY = centerY + WorldUnits.UnitsPerHalfTile;
                var closestX = Math.Clamp(position.X, minX, maxX);
                var closestY = Math.Clamp(position.Y, minY, maxY);
                var dx = (long)position.X - closestX;
                var dy = (long)position.Y - closestY;
                if ((dx * dx) + (dy * dy) < radiusSquared)
                {
                    return false;
                }
            }
        }

        return !OverlapsVehicleBody(creature, position, radius);
    }

    private bool OverlapsVehicleBody(Creature creature, WorldPoint position, int radius)
    {
        var radiusSquared = (long)radius * radius;
        for (var index = 0; index < _vehicles.Count; index++)
        {
            var vehicle = _vehicles[index];
            if (vehicle.Health <= 0 || creature.IsHostedOnVehicle(vehicle))
            {
                continue;
            }

            var bounds = vehicle.GetWorldBounds();
            var closestX = Math.Clamp(position.X, bounds.X, bounds.Right);
            var closestY = Math.Clamp(position.Y, bounds.Y, bounds.Bottom);
            var dx = (long)position.X - closestX;
            var dy = (long)position.Y - closestY;
            if ((dx * dx) + (dy * dy) < radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasClearStaticSweep(Creature creature, WorldPoint start, WorldPoint end)
    {
        var displacement = end - start;
        var distance = displacement.Length;
        if (distance == 0)
        {
            return CanCreatureOccupyWorldPosition(creature, start);
        }

        var sampleSpacing = Math.Max(1, creature.CollisionRadius / 2);
        var sampleCount = Math.Max(1, (distance + sampleSpacing - 1) / sampleSpacing);
        for (var sample = 0; sample <= sampleCount; sample++)
        {
            var position = new WorldPoint(
                start.X + (int)(((long)displacement.X * sample) / sampleCount),
                start.Y + (int)(((long)displacement.Y * sample) / sampleCount));
            if (!CanCreatureOccupyWorldPosition(creature, position))
            {
                return false;
            }
        }

        return true;
    }

    internal void CommitCreaturePosition(Creature creature, WorldPoint position, WorldVector velocity)
    {
        var previousCell = creature.Location;
        var previousTile = creature.IsLocomotionEnabled ? GetTile(previousCell) : null;
        creature.CommitMovement(position, velocity);
        if (!creature.IsLocomotionEnabled || creature.Location == previousCell)
        {
            return;
        }

        var nextTile = GetTile(creature.Location);
        NotifyCreatureCellTransition(creature, previousTile, nextTile);
        MarkCreatureBfsFieldsDirty(creature, previousTile?.Key, nextTile?.Key);
    }

    internal void CorrectCreaturePosition(Creature creature, WorldPoint position)
    {
        var previousCell = creature.Location;
        var previousTile = creature.IsLocomotionEnabled ? GetTile(previousCell) : null;
        creature.SetWorldPosition(position, snapPrevious: false);
        if (!creature.IsLocomotionEnabled || creature.Location == previousCell)
        {
            return;
        }

        var nextTile = GetTile(creature.Location);
        NotifyCreatureCellTransition(creature, previousTile, nextTile);
        MarkCreatureBfsFieldsDirty(creature, previousTile?.Key, nextTile?.Key);
    }

    private bool TryFindCreaturePlacement(Creature creature, GridPoint requestedCell, out WorldPoint position)
    {
        var origin = WorldPoint.FromGridPoint(requestedCell);
        if (CanCreatureOccupyWorldPosition(creature, origin))
        {
            position = origin;
            return true;
        }

        var spacing = (creature.CollisionRadius + creature.SeparationPadding) * 2;
        for (var ring = 1; ring <= 12; ring++)
        {
            for (var y = -ring; y <= ring; y++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    if (Math.Abs(x) != ring && Math.Abs(y) != ring)
                    {
                        continue;
                    }

                    var candidate = origin + new WorldVector(x * spacing, y * spacing);
                    if (CanCreatureOccupyWorldPosition(creature, candidate))
                    {
                        position = candidate;
                        return true;
                    }
                }
            }
        }

        position = default;
        return false;
    }

    public bool IsResourceCompleteScaffoldingTile(Tile? tile)
    {
        return tile?.Built is Scaffolding { ResourceComplete: true };
    }

    public bool IsResourceCompleteScaffoldingLocation(GridPoint location)
    {
        return IsResourceCompleteScaffoldingTile(GetTile(location));
    }

    public bool PlaceCreatureOnTile(Creature creature, GridPoint location)
    {
        var tile = GetTile(location);
        if (tile is null || !CanCreatureTraverseTile(creature, tile) ||
            !TryFindCreaturePlacement(creature, location, out var worldPosition))
        {
            return false;
        }

        if (creature is not Enemy && !IsTileReachable(tile))
        {
            return false;
        }

        var currentTile = creature.IsLocomotionEnabled
            ? GetTile(creature.Location)
            : null;
        creature.EnableLocomotion();
        creature.SetWorldPosition(worldPosition, snapPrevious: true);
        var placedTile = GetTile(creature.Location);
        NotifyCreatureCellTransition(creature, currentTile, placedTile);
        MarkCreatureBfsFieldsDirty(creature, currentTile?.Key, placedTile?.Key);
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

        var currentTile = creature.IsLocomotionEnabled
            ? GetTile(creature.Location)
            : null;
        creature.ClearTaskQueue();
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

        NotifyCreatureCellTransition(creature, currentTile, null);

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

        creature.EnableLocomotion();
        creature.SetWorldPosition(WorldPoint.FromGridPoint(GridPoint.Zero), snapPrevious: true);
        creature.Cave = null;
        Session.Combat.MarkSpatialDirty();
        return true;
    }

    public bool Spawn(Creature creature, Tile tile)
    {
        if (tile.Base == "wall" || !CanCreatureTraverseTile(creature, tile) ||
            !TryFindCreaturePlacement(creature, tile.Coordinates, out var worldPosition))
        {
            return false;
        }

        if (creature is not Enemy && !IsTileReachable(tile))
        {
            return false;
        }

        return SpawnAtWorldPosition(creature, worldPosition);
    }

    public bool SpawnAtWorldPosition(Creature creature, WorldPoint worldPosition)
    {
        var spawnTile = GetTile(worldPosition.ToGridPoint());
        if (spawnTile is null ||
            !CanCreatureTraverseTile(creature, spawnTile) ||
            (creature is not Enemy && !IsTileReachable(spawnTile)) ||
            !CanCreatureOccupyWorldPosition(creature, worldPosition))
        {
            return false;
        }

        creature.EnableLocomotion();
        creature.SetWorldPosition(worldPosition, snapPrevious: true);
        creature.Cave = this;
        NotifyCreatureCellTransition(creature, null, spawnTile);

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
        MarkCreatureBfsFieldsDirty(creature, spawnTile?.Key);
        Session.Combat.MarkSpatialDirty();
        return true;
    }

    public bool RequestCreatureMove(Creature creature, GridPoint nextLocation, bool allowResourceCompleteScaffolding = false)
    {
        if (!creature.IsLocomotionEnabled || creature.HasActiveMovement)
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

        var moveX = current.X - nextLocation.X;
        var moveY = current.Y - nextLocation.Y;

        if (moveX == 0)
        {
            creature.RotationRadians = -moveY == 1 ? MathF.PI : 0f;
        }
        else
        {
            creature.RotationRadians = -moveX == 1 ? MathF.PI / 2f : MathF.PI * 1.5f;
        }

        creature.BeginMovement(nextLocation);
        return true;
    }

    public IReadOnlyList<string> GetCoords() => Tiles.Keys.ToArray();

    public Trilobite? GetTrilobiteAtTileKey(string? tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey))
        {
            return null;
        }

        if (!GridPoint.TryParse(tileKey, out var cell))
        {
            return null;
        }

        for (var index = 0; index < _trilobiteList.Count; index++)
        {
            var trilobite = _trilobiteList[index];
            if (trilobite.Cave == this && trilobite.CurrentCell == cell)
            {
                return trilobite;
            }
        }

        return null;
    }

    public Enemy? GetEnemyAtTileKey(string? tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey) || !GridPoint.TryParse(tileKey, out var cell))
        {
            return null;
        }

        for (var index = 0; index < _enemyList.Count; index++)
        {
            var enemy = _enemyList[index];
            if (enemy.Cave == this && enemy.CurrentCell == cell)
            {
                return enemy;
            }
        }

        return null;
    }

    public bool HasCreatureInCell(GridPoint cell, Creature? ignoredCreature = null)
    {
        for (var index = 0; index < _trilobiteList.Count; index++)
        {
            var creature = _trilobiteList[index];
            if (!ReferenceEquals(creature, ignoredCreature) && creature.Cave == this && creature.CurrentCell == cell)
            {
                return true;
            }
        }

        for (var index = 0; index < _enemyList.Count; index++)
        {
            var creature = _enemyList[index];
            if (!ReferenceEquals(creature, ignoredCreature) && creature.Cave == this && creature.CurrentCell == cell)
            {
                return true;
            }
        }

        return false;
    }

}
