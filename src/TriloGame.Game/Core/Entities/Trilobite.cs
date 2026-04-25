using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed partial class Trilobite : Creature
{
    private readonly List<string> _manualMineTileKeys = [];
    private bool _fleeingToQueen;

    public Trilobite(string name, GridPoint location, GameSession session)
        : base(name, location, session)
    {
        Inventory = new Core.Economy.Inventory();
        InventoryCapacity = GameConstants.TrilobiteCarryCapacity;
        BuilderWorkRate = 5;
        TraitState = new TrilobiteTraitState(TrilobiteTraits.CreateRandomStarterTraits(GameConstants.TrilobiteStarterTraitCount));
    }

    public Core.Economy.Inventory Inventory { get; }

    public int InventoryCapacity { get; }

    public TrilobiteTraitState TraitState { get; }

    public Building? AssignedBuilding { get; private set; }

    public string? PendingMineType { get; private set; }

    public string? PendingMineTileKey { get; private set; }
    public string? PendingManualMineSelectionKey { get; private set; }

    private List<GridPoint>? PendingMinePath { get; set; }

    public string? FighterTargetTileKey { get; private set; }

    public string? FighterPathMode { get; private set; }

    public MiningPost? BuilderSourcePost { get; private set; }

    public int BuilderWorkRate { get; }

    internal MiningPostSelectionMetrics? LastMiningPostSelectionMetrics { get; private set; }

    public bool HasInventory() => Inventory.HasItems;

    public int GetInventorySpace() => System.Math.Max(0, InventoryCapacity - Inventory.Amount);

    public int AddToInventory(string resourceType, int amount) => Inventory.Add(resourceType, amount, InventoryCapacity);

    public int RemoveFromInventory(int amount) => Inventory.Remove(amount);

    public void ClearInventory() => Inventory.Clear();

    public bool ChangeAssignment(string assignment)
    {
        var normalizedAssignment = assignment.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAssignment) ||
            string.Equals(Assignment, normalizedAssignment, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(normalizedAssignment, "miner", StringComparison.Ordinal))
        {
            ClearManualMineOrders();
        }

        if (GetAssignedBuilding() is not null)
        {
            ReleaseAssignedBuilding();
        }

        Assignment = normalizedAssignment;
        RestartBehavior();
        return true;
    }

    public void SetTraits(IEnumerable<TrilobiteTrait> traits)
    {
        TraitState.SetTraits(traits);
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        if (Health <= 0 && TraitState.HasTrait(TrilobiteTrait.Explosive))
        {
            Cave?.TriggerDeathExplosion(this, source);
        }

        ClearActionQueue();
        ClearManualMineOrders();
        ClearFighterTarget();
        FighterPathMode = null;
        _fleeingToQueen = false;
        ReleaseAssignedBuilding(restoreHostedCreatureToTileSystem: false);
        PendingMineType = null;
        PendingMineTileKey = null;
        PendingMinePath = null;
        ClearInventory();
    }

    public override Action? GetBehavior()
    {
        return Assignment switch
        {
            "miner" => MinerBehavior,
            "farmer" => FarmerBehavior,
            "builder" => BuilderBehavior,
            "fighter" => FighterBehavior,
            _ => UnassignedBehavior
        };
    }

    private void UnassignedBehavior()
    {
        ClearFighterTarget();
        FighterPathMode = null;
        ReleaseAssignedBuilding();
    }

    private void MinerBehavior() => EnqueueAction(() => { MinerStep1(); });

    private void FarmerBehavior() => EnqueueAction(() => { FarmerStep1(); });

    private void BuilderBehavior() => EnqueueAction(() => { BuilderStep1(); });

    private void FighterBehavior() => EnqueueAction(() => { FighterStep1(); });

    public bool IsMiner() => Assignment == "miner";

    public bool IsFarmer() => Assignment == "farmer";

    public bool IsBuilder() => Assignment == "builder";

    public bool IsFighter() => Assignment == "fighter";

    protected override bool TryInterruptQueuedAction()
    {
        if (!ShouldFleeFromNearbyEnemy())
        {
            if (_fleeingToQueen)
            {
                _fleeingToQueen = false;
                ClearActionQueue();
            }

            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            _fleeingToQueen = false;
            return false;
        }

        if (!_fleeingToQueen)
        {
            _fleeingToQueen = true;
            ReleaseAssignedBuilding();
            ClearActionQueue();
            if (queen.CanBeFedAt(Location) || IsOnPassableBuildingTile(queen))
            {
                return true;
            }

            NavigateToBuilding(queen, null, true);
            return false;
        }

        if (queen.CanBeFedAt(Location) || IsOnPassableBuildingTile(queen))
        {
            ClearActionQueue();
            return true;
        }

        if (QueuedActionCount == 0)
        {
            ClearActionQueue();
            NavigateToBuilding(queen, null, true);
        }

        return false;
    }

    private bool ShouldFleeFromNearbyEnemy()
    {
        if (Cave is null ||
            !Session.Danger ||
            (!IsMiner() && !IsFarmer() && !IsBuilder()))
        {
            return false;
        }

        var enemyDistance = Cave.GetBfsFieldValue("enemy", Location);
        return enemyDistance < GameConstants.WorkerEnemyFleeRadius;
    }

    public bool EnsureMinerState()
    {
        ClearFighterTarget();
        FighterPathMode = null;

        if (IsMiner())
        {
            if (GetAssignedBuilding() is not null && GetAssignedMiningPost() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        if (GetAssignedMiningPost() is not null)
        {
            ReleaseAssignedBuilding();
        }

        var fallback = GetBehavior();
        if (fallback is not null && !ReferenceEquals(fallback, (Action)MinerBehavior))
        {
            fallback();
        }

        return false;
    }

    public bool EnsureFarmerState()
    {
        ClearFighterTarget();
        FighterPathMode = null;

        if (IsFarmer())
        {
            if (GetAssignedBuilding() is not null && GetAssignedAlgaeFarm() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        if (GetAssignedAlgaeFarm() is not null)
        {
            ReleaseAssignedBuilding();
        }

        var fallback = GetBehavior();
        if (fallback is not null && !ReferenceEquals(fallback, (Action)FarmerBehavior))
        {
            fallback();
        }

        return false;
    }

    public bool EnsureBuilderState()
    {
        ClearFighterTarget();
        FighterPathMode = null;

        if (IsBuilder())
        {
            if (GetAssignedBuilding() is not null && GetAssignedScaffolding() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        if (GetAssignedScaffolding() is not null)
        {
            ReleaseAssignedBuilding();
        }
        else
        {
            ClearBuilderSourcePost();
        }

        var fallback = GetBehavior();
        if (fallback is not null && !ReferenceEquals(fallback, (Action)BuilderBehavior))
        {
            fallback();
        }

        return false;
    }

    public bool EnsureFighterState()
    {
        if (IsFighter())
        {
            if (GetAssignedBuilding() is not null && GetAssignedFighterStation() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        ClearFighterTarget();
        FighterPathMode = null;
        if (GetAssignedFighterStation() is not null)
        {
            ReleaseAssignedBuilding();
        }

        var fallback = GetBehavior();
        if (fallback is not null && !ReferenceEquals(fallback, (Action)FighterBehavior))
        {
            fallback();
        }

        return false;
    }

    public IReadOnlyList<AlgaeFarm> GetAlgaeFarms()
    {
        return Cave?.GetAlgaeFarms() ?? [];
    }

    public Queen? GetQueen()
    {
        return Cave?.GetQueenBuilding();
    }

    public GridPoint? GetClosestPassableBuildingTile(Building building, GridPoint? startLocation = null)
    {
        var origin = startLocation ?? Location;
        GridPoint? bestTile = null;
        var bestDistance = int.MaxValue;

        foreach (var tile in building.TileArray)
        {
            if (!tile.CreatureFits())
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(origin, tile.Coordinates);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile.Coordinates;
            }
        }

        return bestTile;
    }

    public bool IsOnPassableBuildingTile(Building building, GridPoint? location = null)
    {
        var target = location ?? Location;
        foreach (var tile in building.TileArray)
        {
            if (tile.CreatureFits() && tile.Coordinates == target)
            {
                return true;
            }
        }

        return false;
    }

    public int FeedQueenAlgae(Queen queen)
    {
        if (!HasInventory() || Inventory.Type != "Algae")
        {
            return 0;
        }

        var result = queen.FeedAlgae(Inventory.Amount, this, Cave);
        if (result.Accepted <= 0)
        {
            return 0;
        }

        RemoveFromInventory(result.Accepted);
        return result.Accepted;
    }

    public Building? GetAssignedBuilding() => AssignedBuilding;

    public AlgaeFarm? GetAssignedAlgaeFarm() => AssignedBuilding as AlgaeFarm;

    public MiningPost? GetAssignedMiningPost() => AssignedBuilding as MiningPost;

    public StationBuilding? GetAssignedFighterStation() => AssignedBuilding as StationBuilding;

    public Barracks? GetAssignedBarracks() => GetAssignedFighterStation() as Barracks;

    public Scaffolding? GetAssignedScaffolding() => AssignedBuilding as Scaffolding;

    public void SetAssignedBuilding(Building? building)
    {
        if (!ReferenceEquals(AssignedBuilding, building))
        {
            ReleaseAssignedBuilding();
            AssignedBuilding = building;
        }
    }

    public void ReleaseAssignedBuilding(bool restoreHostedCreatureToTileSystem = true)
    {
        ClearBuilderSourcePost();
        if (AssignedBuilding is null)
        {
            PendingMineTileKey = null;
            PendingManualMineSelectionKey = null;
            return;
        }

        switch (AssignedBuilding)
        {
            case MiningPost post:
                if (PendingMineTileKey is not null)
                {
                    post.InvalidateMineableQueues();
                }
                post.RemoveAssignment(this);
                break;
            case AlgaeFarm farm:
                farm.RemoveAssignment(this);
                break;
            case StationBuilding station:
                if (ReferenceEquals(HostedBuilding, station))
                {
                    if (restoreHostedCreatureToTileSystem)
                    {
                        station.TryRestoreCreatureToTileSystem(this);
                    }
                    else
                    {
                        LeaveTileSystem();
                    }
                }

                station.RemoveAssignment(this);
                break;
            case Scaffolding scaffolding:
                scaffolding.RemoveAssignment(this);
                scaffolding.ReleaseMaterialReservation(this);
                break;
        }

        PendingMineTileKey = null;
        PendingManualMineSelectionKey = null;
        AssignedBuilding = null;
    }

    protected override bool EnsureReadyForTileNavigation()
    {
        return IsTrackedInTileSystem ||
               (HostedBuilding as StationBuilding)?.TryRestoreCreatureToTileSystem(this) == true;
    }

    private bool TryStationAtFighterStation(StationBuilding station)
    {
        return station.TryStationCreature(this);
    }

    public void ClearBuilderSourcePost(bool releaseReservation = true)
    {
        if (releaseReservation && BuilderSourcePost is not null)
        {
            BuilderSourcePost.ReleaseMaterialReservation(this);
        }

        BuilderSourcePost = null;
    }

    public void ClearFighterTarget()
    {
        FighterTargetTileKey = null;
    }

    public IReadOnlyList<Barracks> GetBarracksBuildings()
    {
        return Cave?.GetBarracksList() ?? [];
    }

    public IReadOnlyList<Turret> GetTurretBuildings()
    {
        return Cave?.GetTurretList() ?? [];
    }

    public IReadOnlyList<StationBuilding> GetFighterStationBuildings()
    {
        return Cave?.GetFighterStations() ?? [];
    }

    public StationBuilding? GetFighterStationAtLocation(GridPoint? location = null)
    {
        if (location is null && HostedBuilding is StationBuilding hostedStation)
        {
            return hostedStation;
        }

        var checkLocation = location ?? Location;
        return GetFighterStationBuildings()
            .Where(station => IsAtFighterStationNavigationTarget(station, checkLocation))
            .OrderByDescending(station => station.FighterAssignmentPriority)
            .ThenBy(GetOwnedBuildingSelectionKey, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public Barracks? GetBarracksAtLocation(GridPoint? location = null)
    {
        return GetFighterStationAtLocation(location) as Barracks;
    }

    private static string GetOwnedBuildingSelectionKey(Building? building)
    {
        return building?.Location?.ToString() ?? building?.Name ?? string.Empty;
    }

    private bool IsAtFighterStationNavigationTarget(StationBuilding station, GridPoint location)
    {
        if (Cave is null)
        {
            return false;
        }

        return station switch
        {
            Turret turret => Cave.GetTile(location.ToString()) is { } tile &&
                             tile.Neighbors.Any(neighbor => ReferenceEquals(neighbor.Built, turret)),
            _ => IsOnPassableBuildingTile(station, location)
        };
    }

    private bool IsSelectableStation(StationBuilding? station, ISet<StationBuilding>? excludedStations = null)
    {
        return station is not null &&
               station.Location is not null &&
               station.TileArray.Count > 0 &&
               station.CanAssign(this) &&
               excludedStations?.Contains(station) != true;
    }

    private bool IsStationedAtFighterStation(StationBuilding station)
    {
        return station.IsCreatureStationed(this);
    }

    private bool CanReachFighterStation(StationBuilding station)
    {
        return Cave is not null &&
               (IsStationedAtFighterStation(station) ||
                station.IsCreatureAtNavigationTarget(this) ||
                station switch
                {
                    Turret turret => ReferenceEquals(Cave.GetNearestTurret(Location), turret),
                    Barracks barracks => ReferenceEquals(Cave.GetNearestBarracks(Location), barracks),
                    _ => false
                } ||
                Cave.GetBuildingBfsFieldValue(station, Location) != int.MaxValue);
    }

    private bool ShouldBalanceFighterStationAssignments(StationBuilding? preferredStation)
    {
        return preferredStation is null || (Cave?.ShouldRebalanceFighterStationAssignments(preferredStation) ?? false);
    }

    private IEnumerable<TStation> EnumerateStationTypeCandidates<TStation>(
        int priority,
        TStation? nearestStation,
        Func<TStation, IReadOnlyCollection<TStation>> getAdjacentStations,
        IEnumerable<TStation> allStations,
        ISet<StationBuilding> excludedStations,
        ISet<StationBuilding> visited)
        where TStation : StationBuilding
    {
        var queue = new Queue<TStation>();
        if (IsSelectableStation(nearestStation, excludedStations) &&
            nearestStation!.FighterAssignmentPriority == priority &&
            visited.Add(nearestStation))
        {
            queue.Enqueue(nearestStation);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var neighbor in getAdjacentStations(current))
            {
                if (IsSelectableStation(neighbor, excludedStations) &&
                    neighbor.FighterAssignmentPriority == priority &&
                    visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (var station in allStations
                     .Where(station => IsSelectableStation(station, excludedStations) &&
                                       station.FighterAssignmentPriority == priority)
                     .OrderBy(GetOwnedBuildingSelectionKey, StringComparer.Ordinal))
        {
            if (visited.Add(station))
            {
                yield return station;
            }
        }
    }

    private IEnumerable<StationBuilding> EnumerateFighterStationCandidates(int priority, StationBuilding? preferredStation = null, ISet<StationBuilding>? excludedStations = null)
    {
        if (Cave is null)
        {
            yield break;
        }

        excludedStations ??= new HashSet<StationBuilding>();
        var visited = new HashSet<StationBuilding>();

        if (IsSelectableStation(preferredStation, excludedStations) &&
            preferredStation!.FighterAssignmentPriority == priority &&
            visited.Add(preferredStation))
        {
            yield return preferredStation;
        }

        foreach (var turret in EnumerateStationTypeCandidates(
                     priority,
                     Cave.GetNearestTurret(Location),
                     Cave.GetAdjacentTurrets,
                     GetTurretBuildings(),
                     excludedStations,
                     visited))
        {
            yield return turret;
        }

        foreach (var barracks in EnumerateStationTypeCandidates(
                     priority,
                     Cave.GetNearestBarracks(Location),
                     Cave.GetAdjacentBarracks,
                     GetBarracksBuildings(),
                     excludedStations,
                     visited))
        {
            yield return barracks;
        }
    }

    public List<StationBuilding> GetFighterStationPriorityList()
    {
        var prioritizedStations = new List<StationBuilding>();
        var visited = new HashSet<StationBuilding>();
        foreach (var priority in GetFighterStationBuildings()
                     .Select(station => station.FighterAssignmentPriority)
                     .Distinct()
                     .OrderByDescending(priority => priority))
        {
            foreach (var station in EnumerateFighterStationCandidates(priority, GetAssignedFighterStation(), visited))
            {
                if (!CanReachFighterStation(station) || !visited.Add(station))
                {
                    continue;
                }

                prioritizedStations.Add(station);
            }
        }

        return prioritizedStations;
    }

    internal StationBuilding? SelectFighterStation(StationBuilding? preferredStation = null, ISet<StationBuilding>? excludedStations = null)
    {
        excludedStations ??= new HashSet<StationBuilding>();
        foreach (var priority in GetFighterStationBuildings()
                     .Where(station => IsSelectableStation(station, excludedStations))
                     .Select(station => station.FighterAssignmentPriority)
                     .Distinct()
                     .OrderByDescending(priority => priority))
        {
            var shouldBalanceAssignments = ShouldBalanceFighterStationAssignments(
                preferredStation is not null && preferredStation.FighterAssignmentPriority == priority
                    ? preferredStation
                    : null);
            StationBuilding? bestStation = null;
            var bestCount = int.MaxValue;

            foreach (var station in EnumerateFighterStationCandidates(
                         priority,
                         shouldBalanceAssignments ? null : preferredStation,
                         excludedStations))
            {
                if (!CanReachFighterStation(station))
                {
                    continue;
                }

                if (!shouldBalanceAssignments)
                {
                    return station;
                }

                var assignmentCount = Cave?.GetStationAssignmentCount(station) ?? int.MaxValue;
                if (bestStation is null || assignmentCount < bestCount)
                {
                    bestStation = station;
                    bestCount = assignmentCount;
                }
            }

            if (bestStation is not null)
            {
                return bestStation;
            }
        }

        return null;
    }

    public List<Barracks> GetBarracksPriorityList()
    {
        return GetFighterStationPriorityList()
            .OfType<Barracks>()
            .ToList();
    }

    internal Barracks? SelectBarracks(Barracks? preferredBarracks = null, ISet<Barracks>? excludedBarracks = null)
    {
        HashSet<StationBuilding>? excludedStations = null;
        if (excludedBarracks is not null)
        {
            excludedStations = [];
            foreach (var barracks in excludedBarracks)
            {
                excludedStations.Add(barracks);
            }
        }

        return SelectFighterStation(preferredBarracks, excludedStations) as Barracks;
    }

    internal Turret? SelectTurret(Turret? preferredTurret = null, ISet<Turret>? excludedTurrets = null)
    {
        HashSet<StationBuilding>? excludedStations = null;
        if (excludedTurrets is not null)
        {
            excludedStations = [];
            foreach (var turret in excludedTurrets)
            {
                excludedStations.Add(turret);
            }
        }

        return SelectFighterStation(preferredTurret, excludedStations) as Turret;
    }

    public IReadOnlyList<Enemy> GetEnemyCreatures()
    {
        return Cave?.GetEnemyList() ?? [];
    }

    public Enemy? GetEnemyAtTileKey(string? tileKey)
    {
        return Cave?.GetEnemyAtTileKey(tileKey);
    }

    public bool IsAdjacentToTileKey(string tileKey, GridPoint? location = null)
    {
        return GridPoint.ManhattanDistance(location ?? Location, GridPoint.Parse(tileKey)) == 1;
    }

    public string? GetAdjacentEnemyTileKey(GridPoint? location = null)
    {
        var currentTile = Cave?.GetTile((location ?? Location).ToString());
        if (currentTile is null)
        {
            return null;
        }

        return currentTile.Neighbors
            .Select(neighbor => neighbor.EnemyOccupant is not null ? neighbor.Key : null)
            .FirstOrDefault(key => key is not null);
    }

    public bool QueueFighterPath(IReadOnlyList<GridPoint> path, string? mode = null, bool clearExisting = true)
    {
        if (clearExisting)
        {
            ClearActionQueue();
        }

        if (path.Count < 2)
        {
            FighterPathMode = null;
            return path.Count > 0;
        }

        FighterPathMode = mode;
        foreach (var step in path.Skip(1))
        {
            PathPreview.Add(step);
            EnqueueAction(() => { FighterStepMove(step); });
        }

        return true;
    }

    public bool TryNavigateToFighterStation(ISet<StationBuilding>? excludedStations = null, bool preferAssignedStation = true)
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        excludedStations ??= new HashSet<StationBuilding>();
        var preferredStation = preferAssignedStation ? GetAssignedFighterStation() : null;
        var station = SelectFighterStation(preferredStation, excludedStations);
        if (station is null)
        {
            return false;
        }

        if (!station.CanAssign(this))
        {
            excludedStations.Add(station);
            return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
        }

        SetAssignedBuilding(station);
        if (!station.Assign(this))
        {
            ReleaseAssignedBuilding();
            excludedStations.Add(station);
            return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
        }

        if (TryStationAtFighterStation(station))
        {
            return false;
        }

        var navigationTile = station.GetAssignedNavigationTile(this, Location);
        if (!navigationTile.HasValue)
        {
            ReleaseAssignedBuilding();
            excludedStations.Add(station);
            return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
        }

        var path = BuildNavigationPathToPoint(navigationTile.Value);
        if (path is null)
        {
            ReleaseAssignedBuilding();
            excludedStations.Add(station);
            return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
        }

        return QueueFighterPath(path, "station");
    }

    public bool TryNavigateBarracks(ISet<Barracks>? excludedBarracks = null, bool preferAssignedBarracks = true)
    {
        HashSet<StationBuilding>? excludedStations = null;
        if (excludedBarracks is not null)
        {
            excludedStations = [];
            foreach (var barracks in excludedBarracks)
            {
                excludedStations.Add(barracks);
            }
        }

        return TryNavigateToFighterStation(excludedStations, preferAssignedBarracks);
    }

    public bool FighterReturnToStation(bool preferAssignedStation = true)
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        var assignedStation = GetAssignedFighterStation();
        var shouldRebalanceAssignedStation = ShouldBalanceFighterStationAssignments(assignedStation);
        if (preferAssignedStation && assignedStation is not null)
        {
            var retainedAssignedStation = assignedStation.Assign(this);
            if (retainedAssignedStation && !shouldRebalanceAssignedStation && TryStationAtFighterStation(assignedStation))
            {
                return false;
            }
        }

        if (preferAssignedStation)
        {
            var currentStation = GetFighterStationAtLocation();
            if (currentStation is not null && currentStation.CanAssign(this))
            {
                SetAssignedBuilding(currentStation);
                currentStation.Assign(this);
                if (!ShouldBalanceFighterStationAssignments(currentStation) && TryStationAtFighterStation(currentStation))
                {
                    return false;
                }
            }
        }

        if (SelectFighterStation(preferAssignedStation ? assignedStation : null) is null)
        {
            if (!preferAssignedStation)
            {
                ReleaseAssignedBuilding();
            }

            return false;
        }

        return TryNavigateToFighterStation(preferAssignedStation: preferAssignedStation);
    }

    public bool FighterReturnToBarracks(bool preferAssignedBarracks = true)
    {
        return FighterReturnToStation(preferAssignedBarracks);
    }

    public bool FighterStep1()
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        FighterPathMode = null;

        if (ShouldHoldTurretPosition())
        {
            ClearFighterTarget();
            return false;
        }

        if (!Session.Danger)
        {
            ClearFighterTarget();
            return FighterReturnToStation(true);
        }

        if (!EnsureReadyForTileNavigation())
        {
            return false;
        }

        if (FighterTargetTileKey is not null && IsAdjacentToTileKey(FighterTargetTileKey))
        {
            return FighterStep2();
        }

        var adjacentEnemyTileKey = GetAdjacentEnemyTileKey();
        if (adjacentEnemyTileKey is not null)
        {
            FighterTargetTileKey = adjacentEnemyTileKey;
            return FighterStep2();
        }

        return FighterStep3();
    }

    public bool FighterStep2()
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        if (!Session.Danger)
        {
            ClearFighterTarget();
            return FighterReturnToStation(true);
        }

        if (!EnsureReadyForTileNavigation())
        {
            return false;
        }

        if (FighterTargetTileKey is null)
        {
            return FighterStep3();
        }

        var enemy = GetEnemyAtTileKey(FighterTargetTileKey);
        if (enemy is null)
        {
            ClearFighterTarget();
            return FighterStep3();
        }

        if (!IsAdjacentToTileKey(FighterTargetTileKey))
        {
            return FighterStep3();
        }

        var dealt = DealDamage(enemy);
        if (GetEnemyAtTileKey(FighterTargetTileKey) is null)
        {
            ClearFighterTarget();
        }

        return dealt > 0;
    }

    public bool FighterStep3()
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        if (!Session.Danger)
        {
            ClearFighterTarget();
            return FighterReturnToStation(true);
        }

        if (!EnsureReadyForTileNavigation())
        {
            return false;
        }

        if (FighterTargetTileKey is not null && GetEnemyAtTileKey(FighterTargetTileKey) is null)
        {
            ClearFighterTarget();
        }

        var cave = Cave;
        var field = cave?.GetBfsFieldObject("enemy");
        if (field is null || cave is null)
        {
            ClearFighterTarget();
            return FighterReturnToStation(false);
        }

        ClearActionQueue();
        var resolvedField = field;
        var resolvedNext = field.GetNextStep(Location, refresh: false);
        if (resolvedNext is null || (cave.GetTile(resolvedNext.Value.ToString()) is { } attemptedTile && !cave.CanCreatureTraverseTile(this, attemptedTile)))
        {
            var refreshedField = cave.GetBfsFieldObject("enemy");
            refreshedField?.Rebuild();
            if (refreshedField is null)
            {
                ClearFighterTarget();
                return FighterReturnToStation(false);
            }

            resolvedField = refreshedField;
            resolvedNext = refreshedField.GetNextStep(Location, refresh: false);
            if (resolvedField.GetFieldValue(Location, refresh: false) == 0)
            {
                ClearActionQueue();
                return false;
            }
        }

        if (resolvedNext is null)
        {
            ClearFighterTarget();
            return FighterReturnToStation(false);
        }

        ArmBfsTraversal(resolvedField, sharedFieldName: "enemy");
        PathPreview.Add(resolvedNext.Value);
        return FighterStepMove(resolvedNext.Value);
    }

    public bool FighterStepMove(GridPoint nextLocation)
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        if (!Session.Danger)
        {
            if (FighterPathMode != "station")
            {
                ClearActionQueue();
                return FighterStep1();
            }

            var assignedStation = GetAssignedFighterStation();
            if (assignedStation is not null && TryStationAtFighterStation(assignedStation))
            {
                FighterPathMode = null;
                ClearActionQueue();
                return false;
            }
        }
        else if (FighterPathMode == "station")
        {
            FighterPathMode = null;
            ClearActionQueue();
            return FighterStep1();
        }

        if (FighterPathMode != "station")
        {
            if (FighterTargetTileKey is not null && GetEnemyAtTileKey(FighterTargetTileKey) is null)
            {
                ClearFighterTarget();
                ClearActionQueue();
                return FighterStep3();
            }

            var adjacentEnemyTileKey = GetAdjacentEnemyTileKey();
            if (adjacentEnemyTileKey is not null)
            {
                FighterTargetTileKey = adjacentEnemyTileKey;
                ClearActionQueue();
                return FighterStep2();
            }
        }

        var wasStationMove = FighterPathMode == "station";
        ClearBfsTraversal();
        var moved = Cave?.MoveCreature(this, nextLocation) ?? false;
        if (!moved)
        {
            if (wasStationMove)
            {
                FighterPathMode = null;
            }

            ClearActionQueue();
            return wasStationMove ? FighterReturnToStation(true) : FighterStep3();
        }

        if (PathPreview.Count > 0)
        {
            PathPreview.RemoveAt(0);
        }

        if (wasStationMove)
        {
            var assignedStation = GetAssignedFighterStation();
            if (assignedStation is not null && TryStationAtFighterStation(assignedStation))
            {
                FighterPathMode = null;
                ClearActionQueue();
                return false;
            }

            return true;
        }

        if (FighterTargetTileKey is not null && IsAdjacentToTileKey(FighterTargetTileKey))
        {
            ClearActionQueue();
            return FighterStep2();
        }

        var nextAdjacentEnemyTileKey = GetAdjacentEnemyTileKey();
        if (nextAdjacentEnemyTileKey is not null)
        {
            FighterTargetTileKey = nextAdjacentEnemyTileKey;
            ClearActionQueue();
            return FighterStep2();
        }

        return true;
    }

    private bool ShouldHoldTurretPosition()
    {
        return GetAssignedFighterStation() is Turret turret &&
               IsHostedOnBuilding(turret) &&
               turret.IsCreatureStationed(this);
    }

    public List<AlgaeFarm> GetAlgaeFarmPriorityList()
    {
        return EnumerateAlgaeFarmCandidates(GetAssignedAlgaeFarm())
            .Where(CanReachAlgaeFarm)
            .ToList();
    }

    private bool CanSearchForAlgaeFarm(AlgaeFarm? preferredFarm = null)
    {
        return Cave is not null &&
               ((preferredFarm is not null && preferredFarm.HasAssignmentSlot(this)) || Cave.HasOpenAlgaeFarms);
    }

    private bool IsSelectableAlgaeFarm(AlgaeFarm? farm, ISet<AlgaeFarm>? excludedFarms = null)
    {
        return farm is not null &&
               farm.Location is not null &&
               farm.TileArray.Count > 0 &&
               farm.HasAssignmentSlot(this) &&
               excludedFarms?.Contains(farm) != true;
    }

    private bool CanReachAlgaeFarm(AlgaeFarm farm)
    {
        return Cave is not null &&
               (farm.IsLocationOnFarm(Location) ||
                ReferenceEquals(Cave.GetNearestAlgaeFarm(Location), farm) ||
                Cave.GetBuildingBfsFieldValue(farm, Location) != int.MaxValue);
    }

    private IEnumerable<AlgaeFarm> EnumerateAlgaeFarmCandidates(AlgaeFarm? preferredFarm = null, ISet<AlgaeFarm>? excludedFarms = null)
    {
        if (Cave is null)
        {
            yield break;
        }

        excludedFarms ??= new HashSet<AlgaeFarm>();
        var visited = new HashSet<AlgaeFarm>();

        if (IsSelectableAlgaeFarm(preferredFarm, excludedFarms) && visited.Add(preferredFarm!))
        {
            yield return preferredFarm!;
        }

        if (!CanSearchForAlgaeFarm(preferredFarm))
        {
            yield break;
        }

        var nearestFarm = Cave.GetNearestAlgaeFarm(Location);
        var queue = new Queue<AlgaeFarm>();
        if (IsSelectableAlgaeFarm(nearestFarm, excludedFarms) && visited.Add(nearestFarm!))
        {
            queue.Enqueue(nearestFarm!);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var neighbor in Cave.GetAdjacentAlgaeFarms(current))
            {
                if (IsSelectableAlgaeFarm(neighbor, excludedFarms) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (visited.Count > 0)
        {
            yield break;
        }

        foreach (var farm in GetAlgaeFarms()
                     .Where(farm => IsSelectableAlgaeFarm(farm, excludedFarms))
                     .OrderBy(farm => GetOwnedBuildingSelectionKey(farm), StringComparer.Ordinal))
        {
            if (visited.Add(farm))
            {
                yield return farm;
            }
        }
    }

    internal AlgaeFarm? SelectAlgaeFarm(AlgaeFarm? preferredFarm = null, ISet<AlgaeFarm>? excludedFarms = null)
    {
        foreach (var farm in EnumerateAlgaeFarmCandidates(preferredFarm, excludedFarms))
        {
            if (CanReachAlgaeFarm(farm))
            {
                return farm;
            }
        }

        return null;
    }

    public bool TryNavigateAlgaeFarms(ISet<AlgaeFarm>? excludedFarms = null)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        excludedFarms ??= new HashSet<AlgaeFarm>();
        var farm = SelectAlgaeFarm(GetAssignedAlgaeFarm(), excludedFarms);
        if (farm is null)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        SetAssignedBuilding(farm);
        if (!farm.Assign(this))
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            return TryNavigateAlgaeFarms(excludedFarms);
        }

        if (farm.IsLocationOnFarm(Location))
        {
            return FarmerStep2();
        }

        var navFallback = new Action(() =>
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            TryNavigateAlgaeFarms(excludedFarms);
        });

        if (!NavigateToBuilding(farm, navFallback))
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            return TryNavigateAlgaeFarms(excludedFarms);
        }

        EnqueueAction(() => { FarmerStep2(); });
        return true;
    }

    public bool FarmerStep1()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (HasInventory())
        {
            if (Inventory.Type == "Algae")
            {
                return FarmerStep4();
            }

            ClearInventory();
        }

        if (SelectAlgaeFarm(GetAssignedAlgaeFarm()) is null)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        return TryNavigateAlgaeFarms();
    }

    public bool FarmerStep2()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var farm = GetAssignedAlgaeFarm();
        if (farm is null)
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        if (!farm.IsLocationOnFarm(Location))
        {
            var navFallback = new Action(() =>
            {
                ReleaseAssignedBuilding();
                FarmerStep1();
            });

            if (!NavigateToBuilding(farm, navFallback))
            {
                return false;
            }

            EnqueueAction(() => { FarmerStep2(); });
            return true;
        }

        var nextLocation = farm.GetNextTraversalLocation(Location);
        if (nextLocation is null)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        EnqueueAction(() => { FarmerStep3(nextLocation.Value); });
        return true;
    }

    public bool FarmerStep3(GridPoint nextLocation)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var farm = GetAssignedAlgaeFarm();
        if (farm is null)
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        ClearBfsTraversal();
        var moved = Cave?.MoveCreature(this, nextLocation) ?? false;
        if (!moved)
        {
            ClearActionQueue();
            EnqueueAction(() => { FarmerStep2(); });
            return false;
        }

        if (!farm.TryHarvest(this))
        {
            EnqueueAction(() => { FarmerStep2(); });
            return true;
        }

        ClearActionQueue();
        return FarmerStep4();
    }

    public bool FarmerStep4()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (!HasInventory() || Inventory.Type != "Algae")
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        if (queen.CanBeFedAt(Location))
        {
            return FarmerStep5();
        }

        var field = GetBuildingNavigationField(queen);
        if (field is null)
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        ClearActionQueue();
        var resolvedField = field;
        var resolvedNext = field.GetNextStep(Location, refresh: false);
        if (resolvedNext is null || (Cave is not null && Cave.GetTile(resolvedNext.Value.ToString()) is { } attemptedTile && !Cave.CanCreatureTraverseTile(this, attemptedTile)))
        {
            var refreshedField = GetBuildingNavigationField(queen);
            refreshedField?.Rebuild();
            if (refreshedField is null)
            {
                EnqueueAction(() => { FarmerStep1(); });
                return false;
            }

            resolvedField = refreshedField;
            resolvedNext = refreshedField.GetNextStep(Location, refresh: false);
            if (resolvedField.GetFieldValue(Location, refresh: false) == 0)
            {
                ClearActionQueue();
                return FarmerStep5();
            }
        }

        if (resolvedNext is null)
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        ArmBfsTraversal(resolvedField, building: queen);
        PathPreview.Add(resolvedNext.Value);
        return FarmerStepMoveToQueen(resolvedNext.Value);
    }

    public bool FarmerStepMoveToQueen(GridPoint nextLocation)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var moved = PerformMove(nextLocation);
        if (!moved)
        {
            return FarmerStep4();
        }

        if (PathPreview.Count > 0)
        {
            PathPreview.RemoveAt(0);
        }

        return true;
    }

    public bool FarmerStep5()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            EnqueueAction(() => { FarmerStep1(); });
            return false;
        }

        if (!queen.CanBeFedAt(Location))
        {
            return FarmerStep4();
        }

        var fed = FeedQueenAlgae(queen);
        if (fed <= 0)
        {
            EnqueueAction(() => { FarmerStep4(); });
            return false;
        }

        return FarmerStep1();
    }

    public IReadOnlyList<MiningPost> GetMiningPosts()
    {
        return Cave?.GetMiningPosts() ?? [];
    }

    public IReadOnlyList<string> GetManualMineOrders() => _manualMineTileKeys;

    public void SetManualMineOrders(IEnumerable<string> tileKeys)
    {
        _manualMineTileKeys.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tileKey in tileKeys)
        {
            if (!string.IsNullOrWhiteSpace(tileKey) && seen.Add(tileKey))
            {
                _manualMineTileKeys.Add(tileKey);
            }
        }

        if (_manualMineTileKeys.Count == 0)
        {
            return;
        }

        ResetPendingMineTarget();
        RestartBehavior();
    }

    public void ClearManualMineOrders(bool restartBehavior = false)
    {
        _manualMineTileKeys.Clear();
        if (restartBehavior)
        {
            RestartBehavior();
        }
    }

    public void ResetPendingMineTarget(bool requeue = false)
    {
        var miningPost = GetAssignedMiningPost();
        if (requeue && miningPost is not null && PendingMineTileKey is not null)
        {
            miningPost.InvalidateMineableQueues();
        }

        miningPost?.Assign(this, null);
        PendingMineType = null;
        PendingMineTileKey = null;
        PendingMinePath = null;
        PendingManualMineSelectionKey = null;
    }

    private bool RetargetStalePendingMineTarget(MiningPost miningPost, string tileKey)
    {
        miningPost.InvalidateMineableQueuesForKeys([tileKey]);
        miningPost.Assign(this, null);
        PendingMineTileKey = null;
        PendingMinePath = null;
        var wasManualTarget = HasPendingManualMineSelection();
        PendingManualMineSelectionKey = null;
        if (wasManualTarget)
        {
            PruneResolvedManualMineOrders();
        }
        return string.IsNullOrWhiteSpace(PendingMineType) ? MinerStep3() : MinerStep4();
    }

    private static string? GetMiningPostSelectionKey(MiningPost? post)
    {
        return post?.Location?.ToString() ?? post?.Name;
    }

    private bool IsSelectableMiningPost(MiningPost? post, ISet<MiningPost>? excludedPosts = null)
    {
        return post is not null &&
               post.Location is not null &&
               post.TileArray.Count > 0 &&
               excludedPosts?.Contains(post) != true;
    }

    private bool CanSearchForMiningPost(MiningPost? preferredPost = null)
    {
        return Cave is not null &&
               ((preferredPost is not null && preferredPost.AssignmentsAvailable) || Cave.HasAvailableMiningPostAssignments);
    }

    private bool WaitForMiningAssignmentAvailability(MiningPost? currentPost = null)
    {
        if (Cave is null ||
            Cave.HasAvailableMiningPostAssignments ||
            currentPost?.AssignmentsAvailable == true)
        {
            return false;
        }

        EnqueueAction(() => { MinerStep1(); });
        return true;
    }

    private bool ShouldBalanceMiningPostAssignments(MiningPost? preferredPost)
    {
        return preferredPost is null || (Cave?.ShouldRebalanceMiningPostAssignments(preferredPost) ?? false);
    }

    private bool CanReachMiningPostArea(MiningPost post)
    {
        return Cave is not null &&
               (post.IsLocationInArea(Location) || Cave.GetBuildingBfsFieldValue(post, Location) != int.MaxValue);
    }

    private bool CanReachMiningPostInventory(MiningPost post)
    {
        return Cave is not null &&
               (post.IsLocationOnPost(Location) || Cave.GetBuildingBfsFieldValue(post, Location) != int.MaxValue);
    }

    private IEnumerable<MiningPost> EnumerateMiningPostCandidates(string purpose, MiningPost? preferredPost = null, ISet<MiningPost>? excludedPosts = null)
    {
        var metrics = new MiningPostSelectionMetrics
        {
            Purpose = purpose,
            PreferredPostKey = GetMiningPostSelectionKey(preferredPost)
        };
        LastMiningPostSelectionMetrics = metrics;

        if (Cave is null)
        {
            yield break;
        }

        excludedPosts ??= new HashSet<MiningPost>();
        var visited = new HashSet<MiningPost>();

        if (IsSelectableMiningPost(preferredPost, excludedPosts) && visited.Add(preferredPost!))
        {
            metrics.ReusedPreferredPost = true;
            metrics.CandidateCount++;
            yield return preferredPost!;
        }

        if (purpose.StartsWith("miner", StringComparison.Ordinal) && !CanSearchForMiningPost(preferredPost))
        {
            yield break;
        }

        var nearestOwner = Cave.GetNearestMiningPost(Location);
        metrics.NearestOwnerPostKey = GetMiningPostSelectionKey(nearestOwner);

        var queue = new Queue<MiningPost>();
        if (IsSelectableMiningPost(nearestOwner, excludedPosts) && visited.Add(nearestOwner!))
        {
            queue.Enqueue(nearestOwner!);
        }

        if (queue.Count > 0)
        {
            Session.MiningPostMovementTelemetry.RecordSelectionGraphBfs();
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            metrics.CandidateCount++;
            if (!ReferenceEquals(current, preferredPost) && !ReferenceEquals(current, nearestOwner))
            {
                metrics.UsedAdjacencyFallback = true;
            }

            yield return current;

            foreach (var neighbor in Cave.GetAdjacentMiningPosts(current))
            {
                if (IsSelectableMiningPost(neighbor, excludedPosts) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (metrics.CandidateCount > 0)
        {
            yield break;
        }

        metrics.FullScanFallbackCount++;
        foreach (var post in GetMiningPosts()
                     .Where(post => IsSelectableMiningPost(post, excludedPosts))
                     .OrderBy(GetMiningPostSelectionKey, StringComparer.Ordinal))
        {
            if (!visited.Add(post))
            {
                continue;
            }

            metrics.CandidateCount++;
            yield return post;
        }
    }

    internal MiningPost? SelectMiningPostForMining(ISet<MiningPost>? excludedPosts = null)
    {
        if (Cave is null)
        {
            return null;
        }

        var preferredPost = GetAssignedMiningPost();
        var shouldBalanceAssignments = ShouldBalanceMiningPostAssignments(preferredPost);
        MiningPost? bestPost = null;
        var bestCount = int.MaxValue;

        foreach (var post in EnumerateMiningPostCandidates("miner", shouldBalanceAssignments ? null : preferredPost, excludedPosts))
        {
            if (!CanReachMiningPostArea(post) ||
                post.GetInventorySpace() <= 0 ||
                (!HasManualMineOrders() && !post.AssignmentsAvailable))
            {
                continue;
            }

            if (!shouldBalanceAssignments)
            {
                return post;
            }

            var assignmentCount = Cave.GetMiningPostAssignmentCount(post);
            if (bestPost is null || assignmentCount < bestCount)
            {
                bestPost = post;
                bestCount = assignmentCount;
            }
        }

        return bestPost;
    }

    internal MiningPost? SelectMiningPostForInventoryDeposit(ISet<MiningPost>? excludedPosts = null)
    {
        foreach (var post in EnumerateMiningPostCandidates("builder-deposit", null, excludedPosts))
        {
            if (!CanReachMiningPostInventory(post) || post.GetInventorySpace() <= 0)
            {
                continue;
            }

            return post;
        }

        return null;
    }

    public List<MiningPost> GetMiningPostPriorityList()
    {
        return EnumerateMiningPostCandidates("miner-priority", GetAssignedMiningPost())
            .Where(post => Cave is not null && CanReachMiningPostArea(post) && post.GetInventorySpace() > 0 && post.AssignmentsAvailable)
            .ToList();
    }

    public bool TryNavigateMiningPosts(ISet<MiningPost>? excludedPosts = null)
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var assignedPost = GetAssignedMiningPost();
        if (WaitForMiningAssignmentAvailability(assignedPost))
        {
            return false;
        }

        if (assignedPost is not null && !assignedPost.AssignmentsAvailable)
        {
            ReleaseAssignedBuilding();
        }

        if (GetAssignedMiningPost() is null && Cave is not null && !Cave.HasAvailableMiningPostAssignments)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        excludedPosts ??= new HashSet<MiningPost>();
        var post = SelectMiningPostForMining(excludedPosts);
        if (post is null)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        SetAssignedBuilding(post);
        post.Assign(this, null);

        if (post.IsLocationInArea(Location))
        {
            return MinerStep2();
        }

        var navFallback = new Action(() =>
        {
            ReleaseAssignedBuilding();
            excludedPosts.Add(post);
            TryNavigateMiningPosts(excludedPosts);
        });

        if (!NavigateToBuilding(post, navFallback))
        {
            ReleaseAssignedBuilding();
            excludedPosts.Add(post);
            return TryNavigateMiningPosts(excludedPosts);
        }

        EnqueueAction(() => { MinerStep2(); });
        return true;
    }

    public bool MinerStep1()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        return TryNavigateMiningPosts();
    }

    public bool MinerStep2()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null)
        {
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        if (HasInventory())
        {
            if (!miningPost.IsLocationOnPost(Location))
            {
                var navFallback = new Action(() =>
                {
                    ReleaseAssignedBuilding();
                    MinerStep1();
                });

                if (!NavigateToBuilding(miningPost, navFallback))
                {
                    return false;
                }

                EnqueueAction(() => { MinerStep2(); });
                return true;
            }

            var accepted = miningPost.Deposit(Inventory.Type!, Inventory.Amount);
            RemoveFromInventory(accepted);
            if (HasInventory())
            {
                EnqueueAction(() => { MinerStep1(); });
                return false;
            }
        }

        if (WaitForMiningAssignmentAvailability(miningPost))
        {
            return false;
        }

        if (!miningPost.AssignmentsAvailable)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        return MinerStep3();
    }

    public bool MinerStep3()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null)
        {
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        var manualTarget = GetNextManualMineOrder();
        if (manualTarget is not null)
        {
            if (miningPost.IsTileAssignedToOther(this, manualTarget.Value.TargetTile.Key))
            {
                RotateManualMineOrderToBack(manualTarget.Value.RequestedKey);
                EnqueueAction(() => { MinerStep1(); });
                return false;
            }

            PendingManualMineSelectionKey = manualTarget.Value.RequestedKey;
            PendingMineTileKey = manualTarget.Value.TargetTile.Key;
            miningPost.Assign(this, manualTarget.Value.TargetTile.Key);
            PendingMineType = null;
            PendingMinePath = null;
            return MinerStep5();
        }

        if (WaitForMiningAssignmentAvailability(miningPost))
        {
            return false;
        }

        if (!miningPost.AssignmentsAvailable)
        {
            ReleaseAssignedBuilding();
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        var targetType = miningPost.GrabMineableType(Cave!, this);
        if (targetType is null)
        {
            miningPost.Assign(this, null);
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        PendingMineType = targetType;
        PendingMineTileKey = null;
        PendingMinePath = null;
        PendingManualMineSelectionKey = null;
        return MinerStep4();
    }

    public bool MinerStep4()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null || PendingMineType is null)
        {
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        var reservedTiles = miningPost.GetAssignedTileKeys(this);
        var targetResult = Cave?.BuildPathToNearestMineableType(Location, miningPost, PendingMineType, reservedTiles);
        if (targetResult is null)
        {
            ResetPendingMineTarget(false);
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        PendingMineTileKey = targetResult.Value.TileKey;
        PendingMinePath = targetResult.Value.Path;
        miningPost.Assign(this, PendingMineTileKey);
        return MinerStep5();
    }

    public bool MinerStep5()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null || PendingMineTileKey is null)
        {
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        var targetTile = Cave?.GetTile(PendingMineTileKey);
        if (targetTile is null)
        {
            return RetargetStalePendingMineTarget(miningPost, PendingMineTileKey);
        }

        if (!Building.IsMineableType(targetTile.Base))
        {
            return RetargetStalePendingMineTarget(miningPost, PendingMineTileKey);
        }

        var navTarget = miningPost.GetNavigationTarget(Cave!, targetTile);
        if (navTarget is null)
        {
            var wasManualTarget = HasPendingManualMineSelection();
            ResetPendingMineTarget(!wasManualTarget);
            if (wasManualTarget)
            {
                PruneResolvedManualMineOrders();
            }
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        if (miningPost.GetAssignment(this) != PendingMineTileKey)
        {
            ResetPendingMineTarget(true);
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        var path = PendingMinePath;
        PendingMinePath = null;
        if (path is null || path.Count == 0 || path[0] != Location || path[^1] != navTarget.Value)
        {
            path = Cave?.BuildDirectPathToPoint(Location, navTarget.Value);
            if (path is null)
            {
                ResetPendingMineTarget(true);
                EnqueueAction(() => { MinerStep1(); });
                return false;
            }
        }

        var navFallback = new Action(() =>
        {
            var pendingMineTileKey = PendingMineTileKey;
            var wasManualTarget = HasPendingManualMineSelection();
            ResetPendingMineTarget(!wasManualTarget);
            if (wasManualTarget)
            {
                PruneResolvedManualMineOrders();
            }
            MinerStep1();
        });

        if (path.Count < 2)
        {
            return MinerStep6();
        }

        if (!EnqueueResolvedPath(path, navFallback, clearExisting: true))
        {
            return false;
        }

        EnqueueAction(() => { MinerStep6(); });
        return true;
    }

    public MineTileResult MineTile(string tileKey)
    {
        var tile = Cave?.GetTile(tileKey);
        if (tile is null)
        {
            return MineTileResult.NotApplied;
        }

        if (!Building.IsMineableType(tile.Base))
        {
            return MineTileResult.NotApplied;
        }

        var tileCoords = GridPoint.Parse(tileKey);
        if (tile.Base == "wall")
        {
            if (GridPoint.ManhattanDistance(Location, tileCoords) != 1 ||
                GetInventorySpace() < GameConstants.WallDropAmount)
            {
                return MineTileResult.NotApplied;
            }
        }
        else if (Location != tileCoords || GetInventorySpace() < 1)
        {
            return MineTileResult.NotApplied;
        }

        var result = Session.MineTile(Cave!, tileKey, source: "creature");
        if (!result.HitApplied)
        {
            return result;
        }

        if (result.ResourceAmount > 0 && AddToInventory(result.ResourceType!, result.ResourceAmount) != result.ResourceAmount)
        {
            RemoveFromInventory(result.ResourceAmount);
            return MineTileResult.NotApplied;
        }

        return result;
    }

    public bool MinerStep6()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null || PendingMineTileKey is null)
        {
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        var targetTile = Cave?.GetTile(PendingMineTileKey);
        if (targetTile is null || !Building.IsMineableType(targetTile.Base))
        {
            return RetargetStalePendingMineTarget(miningPost, PendingMineTileKey);
        }

        var wasManualTarget = HasPendingManualMineSelection();
        var result = MineTile(PendingMineTileKey);
        if (!result.HitApplied)
        {
            ResetPendingMineTarget(!wasManualTarget);
            if (wasManualTarget)
            {
                PruneResolvedManualMineOrders();
            }
            EnqueueAction(() => { MinerStep1(); });
            return false;
        }

        if (result.TileDepleted)
        {
            var shouldReturnToPostForRevealedSelection = wasManualTarget && ShouldReturnToPostAfterManualReveal();
            ResetPendingMineTarget(false);
            PruneResolvedManualMineOrders();

            if (shouldReturnToPostForRevealedSelection)
            {
                return MinerStep2();
            }

            return HasInventory() ? MinerStep2() : MinerStep1();
        }

        if (GetInventorySpace() <= 0)
        {
            ResetPendingMineTarget(!wasManualTarget);
            return MinerStep2();
        }

        EnqueueAction(() => { MinerStep6(); });
        return true;
    }

    private bool HasManualMineOrders()
    {
        return _manualMineTileKeys.Count > 0;
    }

    private (string RequestedKey, World.Tile TargetTile)? GetNextManualMineOrder()
    {
        if (Cave is null)
        {
            return null;
        }

        for (var index = 0; index < _manualMineTileKeys.Count; index++)
        {
            var tileKey = _manualMineTileKeys[index];
            var tile = Cave.GetTile(tileKey);
            if (tile is null)
            {
                _manualMineTileKeys.RemoveAt(index);
                index--;
                continue;
            }

            if (Cave.IsTileRevealed(tile) && !Building.IsMineableType(tile.Base))
            {
                _manualMineTileKeys.RemoveAt(index);
                index--;
                continue;
            }

            var resolvedTile = MineOrderPlanner.ResolveTarget(Cave, tile);
            if (resolvedTile is not null)
            {
                return (tileKey, resolvedTile);
            }
        }

        return null;
    }

    private bool HasPendingManualMineSelection()
    {
        return !string.IsNullOrWhiteSpace(PendingManualMineSelectionKey);
    }

    private void PruneResolvedManualMineOrders()
    {
        if (Cave is null)
        {
            return;
        }

        for (var index = _manualMineTileKeys.Count - 1; index >= 0; index--)
        {
            var tile = Cave.GetTile(_manualMineTileKeys[index]);
            if (tile is null ||
                (Cave.IsTileRevealed(tile) && !Building.IsMineableType(tile.Base)))
            {
                _manualMineTileKeys.RemoveAt(index);
            }
        }
    }

    private void RotateManualMineOrderToBack(string tileKey)
    {
        var index = _manualMineTileKeys.FindIndex(key => string.Equals(key, tileKey, StringComparison.Ordinal));
        if (index < 0 || _manualMineTileKeys.Count <= 1)
        {
            return;
        }

        var current = _manualMineTileKeys[index];
        _manualMineTileKeys.RemoveAt(index);
        _manualMineTileKeys.Add(current);
    }

    private bool ShouldReturnToPostAfterManualReveal()
    {
        if (Cave is null || string.IsNullOrWhiteSpace(PendingManualMineSelectionKey))
        {
            return false;
        }

        var tile = Cave.GetTile(PendingManualMineSelectionKey);
        return tile is not null &&
               Cave.IsTileRevealed(tile) &&
               !string.Equals(tile.Base, "wall", StringComparison.Ordinal);
    }

    public IReadOnlyList<Scaffolding> GetScaffoldingBuildings()
    {
        if (Cave is null)
        {
            return [];
        }

        return Cave.GetScaffoldingList().Where(scaffold => scaffold.IsInProgress()).ToList();
    }

    private (MiningPost Post, string ResourceType, int Amount)? GetBuilderSupplyOptionFromCandidates(
        Scaffolding scaffold,
        IReadOnlyList<string> neededResources,
        IEnumerable<MiningPost> candidatePosts,
        bool orderedCandidates = false)
    {
        if (orderedCandidates)
        {
            LastMiningPostSelectionMetrics = new MiningPostSelectionMetrics
            {
                Purpose = "builder-supply-ordered"
            };
        }

        foreach (var post in candidatePosts)
        {
            if (orderedCandidates && LastMiningPostSelectionMetrics is not null)
            {
                LastMiningPostSelectionMetrics.CandidateCount++;
            }

            if (!CanReachMiningPostInventory(post))
            {
                continue;
            }

            foreach (var resourceType in neededResources)
            {
                var missingAmount = scaffold.GetUnreservedRemainingRequirement(resourceType, this);
                var availableAmount = post.GetAvailableInventory(resourceType, this);
                var reserveAmount = System.Math.Min(InventoryCapacity, System.Math.Min(missingAmount, availableAmount));
                if (reserveAmount > 0)
                {
                    return (post, resourceType, reserveAmount);
                }
            }
        }

        return null;
    }

    public (MiningPost Post, string ResourceType, int Amount)? GetBuilderSupplyOptionForScaffold(Scaffolding scaffold, IReadOnlyList<MiningPost>? orderedPosts = null)
    {
        var neededResources = scaffold.GetNeededResourceTypes(true, this);
        if (neededResources.Count == 0)
        {
            return null;
        }

        if (orderedPosts is not null)
        {
            return GetBuilderSupplyOptionFromCandidates(scaffold, neededResources, orderedPosts, orderedCandidates: true);
        }

        return GetBuilderSupplyOptionFromCandidates(scaffold, neededResources, EnumerateMiningPostCandidates("builder-supply"));
    }

    public bool CanActOnScaffold(Scaffolding scaffold)
    {
        if (!scaffold.IsInProgress())
        {
            return false;
        }

        if (!CanReachScaffolding(scaffold))
        {
            return false;
        }

        if (HasInventory())
        {
            return scaffold.NeedsResource(Inventory.Type!);
        }

        if (scaffold.GetMaterialReservation(this) is not null || BuilderSourcePost?.GetMaterialReservation(this) is not null)
        {
            return true;
        }

        if (!scaffold.IsRecipeComplete())
        {
            return GetBuilderSupplyOptionForScaffold(scaffold) is not null;
        }

        if (scaffold.NeedsConstructionWork())
        {
            return true;
        }

        return scaffold.IsConstructionComplete();
    }

    public List<Scaffolding> GetScaffoldingPriorityList(bool actionableOnly = false, IEnumerable<Scaffolding>? excludeScaffolds = null)
    {
        var excluded = excludeScaffolds?.ToHashSet() ?? [];
        return GetScaffoldingBuildings()
            .Where(scaffold =>
                !excluded.Contains(scaffold) &&
                (Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue) != int.MaxValue &&
                (!actionableOnly || CanActOnScaffold(scaffold)))
            .OrderBy(scaffold => scaffold.GetVolume())
            .ThenBy(scaffold => Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue)
            .ThenBy(scaffold => scaffold.Location is null ? int.MaxValue : GridPoint.SquaredDistance(Location, scaffold.Location.Value))
            .ToList();
    }

    private Scaffolding? GetBestScaffolding(bool actionableOnly = false, ISet<Scaffolding>? excludedScaffolds = null)
    {
        Scaffolding? bestScaffold = null;
        var bestVolume = int.MaxValue;
        var bestBfs = int.MaxValue;
        var bestDistance = int.MaxValue;
        string? bestKey = null;

        foreach (var scaffold in GetScaffoldingBuildings())
        {
            if (excludedScaffolds?.Contains(scaffold) == true)
            {
                continue;
            }

            var bfsValue = Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue;
            if (bfsValue == int.MaxValue || (actionableOnly && !CanActOnScaffold(scaffold)))
            {
                continue;
            }

            var volume = scaffold.GetVolume();
            var distance = scaffold.Location is null ? int.MaxValue : GridPoint.SquaredDistance(Location, scaffold.Location.Value);
            var tieKey = scaffold.Location?.ToString() ?? scaffold.Name;
            if (bestScaffold is null ||
                volume < bestVolume ||
                (volume == bestVolume && bfsValue < bestBfs) ||
                (volume == bestVolume && bfsValue == bestBfs && distance < bestDistance) ||
                (volume == bestVolume && bfsValue == bestBfs && distance == bestDistance && string.CompareOrdinal(tieKey, bestKey) < 0))
            {
                bestScaffold = scaffold;
                bestVolume = volume;
                bestBfs = bfsValue;
                bestDistance = distance;
                bestKey = tieKey;
            }
        }

        return bestScaffold;
    }

    public List<MiningPost> GetBuilderMiningPostPriorityList()
    {
        return EnumerateMiningPostCandidates("builder-priority")
            .Where(CanReachMiningPostInventory)
            .ToList();
    }

    public bool IsInBuildingWorkRange(Building building, GridPoint? location = null)
    {
        return (Cave?.GetBuildingBfsFieldValue(building, location ?? Location) ?? int.MaxValue) == 0;
    }

    private bool CanReachScaffolding(Scaffolding scaffold)
    {
        return (Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue) != int.MaxValue;
    }

    public Scaffolding? EnsureBuilderAssignment(bool actionableOnly = false, IEnumerable<Scaffolding>? excludeScaffolds = null)
    {
        var excluded = excludeScaffolds?.ToHashSet() ?? [];
        var assignedScaffold = GetAssignedScaffolding();
        if (assignedScaffold is not null &&
            assignedScaffold.IsInProgress() &&
            CanReachScaffolding(assignedScaffold) &&
            !excluded.Contains(assignedScaffold) &&
            (!actionableOnly || CanActOnScaffold(assignedScaffold)))
        {
            assignedScaffold.Assign(this);
            return assignedScaffold;
        }

        if (assignedScaffold is not null)
        {
            ReleaseAssignedBuilding();
        }

        var scaffold = GetBestScaffolding(actionableOnly, excluded);
        if (scaffold is null)
        {
            ReleaseAssignedBuilding();
            return null;
        }

        SetAssignedBuilding(scaffold);
        scaffold.Assign(this);
        return scaffold;
    }

    public bool BuilderDepositInventoryToNearestMiningPost()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        if (!HasInventory())
        {
            return BuilderStep1();
        }

        var post = SelectMiningPostForInventoryDeposit();
        if (post is null)
        {
            return false;
        }

        if (!post.IsLocationOnPost(Location))
        {
            var navFallback = new Action(() => { BuilderDepositInventoryToNearestMiningPost(); });
            if (!NavigateToBuilding(post, navFallback))
            {
                return false;
            }

            EnqueueAction(() => { BuilderDepositInventoryToNearestMiningPost(); });
            return true;
        }

        var accepted = post.Deposit(Inventory.Type!, Inventory.Amount);
        RemoveFromInventory(accepted);
        return HasInventory() ? BuilderDepositInventoryToNearestMiningPost() : BuilderStep1();
    }

    public bool BuilderStep1()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = EnsureBuilderAssignment(true);
        if (scaffold is null)
        {
            return HasInventory() ? BuilderDepositInventoryToNearestMiningPost() : false;
        }

        var scaffoldReservation = scaffold.GetMaterialReservation(this);
        var postReservation = BuilderSourcePost?.GetMaterialReservation(this);

        if (HasInventory())
        {
            if (scaffold.NeedsResource(Inventory.Type!))
            {
                return BuilderStep4();
            }

            scaffold.ReleaseMaterialReservation(this);
            return BuilderDepositInventoryToNearestMiningPost();
        }

        if (scaffoldReservation is not null && BuilderSourcePost is not null && postReservation is not null)
        {
            return BuilderStep3();
        }

        if (scaffoldReservation is not null && BuilderSourcePost is null)
        {
            scaffold.ReleaseMaterialReservation(this);
        }
        else if (scaffoldReservation is null && BuilderSourcePost is not null)
        {
            ClearBuilderSourcePost();
        }

        if (scaffold.NeedsAnyResource(true, this) && BuilderStep2())
        {
            return true;
        }

        if (scaffold.IsRecipeComplete() && scaffold.NeedsConstructionWork())
        {
            return BuilderStep5();
        }

        if (scaffold.IsRecipeComplete() && scaffold.IsConstructionComplete() && scaffold.TryCompleteConstruction(this))
        {
            return true;
        }

        ReleaseAssignedBuilding();
        return false;
    }

    public bool BuilderStep2()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null)
        {
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        var supplyOption = GetBuilderSupplyOptionForScaffold(scaffold);
        if (supplyOption is null)
        {
            return false;
        }

        var scaffoldReserved = scaffold.ReserveMaterial(this, supplyOption.Value.ResourceType, supplyOption.Value.Amount);
        if (scaffoldReserved <= 0)
        {
            return false;
        }

        var postReserved = supplyOption.Value.Post.ReserveMaterial(this, supplyOption.Value.ResourceType, scaffoldReserved);
        if (postReserved != scaffoldReserved)
        {
            scaffold.ReleaseMaterialReservation(this);
            supplyOption.Value.Post.ReleaseMaterialReservation(this);
            return false;
        }

        BuilderSourcePost = supplyOption.Value.Post;

        if (supplyOption.Value.Post.IsLocationOnPost(Location))
        {
            return BuilderStep3();
        }

        var navFallback = new Action(() =>
        {
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            BuilderStep1();
        });

        if (!NavigateToBuilding(supplyOption.Value.Post, navFallback))
        {
            return false;
        }

        EnqueueAction(() => { BuilderStep3(); });
        return true;
    }

    public bool BuilderStep3()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        var post = BuilderSourcePost;
        var scaffoldReservation = scaffold?.GetMaterialReservation(this);
        var postReservation = post?.GetMaterialReservation(this);

        if (scaffold is null || post is null || scaffoldReservation is null || postReservation is null || scaffoldReservation.ResourceType != postReservation.ResourceType)
        {
            scaffold?.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        if (HasInventory())
        {
            return BuilderStep4();
        }

        if (!post.IsLocationOnPost(Location))
        {
            var navFallback = new Action(() =>
            {
                scaffold.ReleaseMaterialReservation(this);
                ClearBuilderSourcePost();
                BuilderStep1();
            });

            if (!NavigateToBuilding(post, navFallback))
            {
                return false;
            }

            EnqueueAction(() => { BuilderStep3(); });
            return true;
        }

        var withdrawn = post.WithdrawReservedMaterial(this, System.Math.Min(GetInventorySpace(), scaffoldReservation.Amount));
        if (withdrawn is null || withdrawn.Amount <= 0)
        {
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        if (AddToInventory(withdrawn.ResourceType, withdrawn.Amount) != withdrawn.Amount)
        {
            post.Deposit(withdrawn.ResourceType, withdrawn.Amount);
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        BuilderSourcePost = null;
        return BuilderStep4();
    }

    public bool BuilderStep4()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (!HasInventory())
        {
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        if (scaffold is null || !scaffold.IsInProgress())
        {
            return BuilderDepositInventoryToNearestMiningPost();
        }

        if (!scaffold.NeedsResource(Inventory.Type!))
        {
            scaffold.ReleaseMaterialReservation(this);
            return BuilderDepositInventoryToNearestMiningPost();
        }

        if (!IsInBuildingWorkRange(scaffold))
        {
            var navFallback = new Action(() =>
            {
                scaffold.ReleaseMaterialReservation(this);
                BuilderDepositInventoryToNearestMiningPost();
            });

            if (!NavigateToBuilding(scaffold, navFallback))
            {
                return false;
            }

            EnqueueAction(() => { BuilderStep4(); });
            return true;
        }

        var accepted = scaffold.Deposit(Inventory.Type!, Inventory.Amount, this);
        RemoveFromInventory(accepted);
        return HasInventory() ? BuilderDepositInventoryToNearestMiningPost() : BuilderStep1();
    }

    public bool BuilderStep5()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null || !scaffold.IsInProgress())
        {
            ReleaseAssignedBuilding();
            return false;
        }

        if (HasInventory())
        {
            return BuilderStep4();
        }

        if (!scaffold.IsRecipeComplete())
        {
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        if (!scaffold.NeedsConstructionWork())
        {
            return BuilderStep1();
        }

        if (!IsInBuildingWorkRange(scaffold))
        {
            var navFallback = new Action(() => { BuilderStep1(); });
            if (!NavigateToBuilding(scaffold, navFallback))
            {
                return false;
            }

            EnqueueAction(() => { BuilderStep5(); });
            return true;
        }

        var worked = scaffold.ApplyConstructionWork(BuilderWorkRate, this);
        if (worked <= 0)
        {
            EnqueueAction(() => { BuilderStep1(); });
            return false;
        }

        return true;
    }
}
