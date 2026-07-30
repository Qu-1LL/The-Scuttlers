using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed partial class Trilobite
{
    private readonly CombatAgentController _combatAgentController = new();
    private bool _fighterPreferAssignedStation = true;

    private bool AdvanceFighterRole()
    {
        return _combatAgentController.Advance(this);
    }

    internal bool TryStationAtFighterStation(StationBuilding station)
    {
        return station.TryStationCreature(this);
    }

    public void ClearFighterTarget()
    {
        if (FighterTarget is null)
        {
            return;
        }

        FighterTarget = null;
    }

    internal void SetFighterTarget(Enemy? target)
    {
        if (ReferenceEquals(FighterTarget, target))
        {
            return;
        }

        FighterTarget = target;
    }

    internal bool HasValidFighterTarget()
    {
        return FighterTarget is { Health: > 0 } target && ReferenceEquals(target.Cave, Cave);
    }

    internal bool CanReachFighterTarget()
    {
        return HasValidFighterTarget() &&
               CombatWorld.CanMeleeReach(this, Combat.CombatTargetRef.For(FighterTarget!));
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

    public Enemy? GetReachableEnemy()
    {
        return Session.Combat.FindDirectedEnemy(this) ?? Session.Combat.FindReachableEnemy(this);
    }

    public bool QueueFighterPath(IReadOnlyList<GridPoint> path, string? mode = null, bool clearExisting = true)
    {
        if (path.Count < 2)
        {
            FighterPathMode = null;
            return path.Count > 0;
        }

        FighterPathMode = mode;
        return clearExisting ? QueueMovePath(path) : AppendMovePath(path);
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

        var purpose = station is Turret
            ? InteractionZonePurpose.Approach
            : InteractionZonePurpose.Station;
        FighterPathMode = "station";
        if (NavigateToInteractionZone(station, purpose))
        {
            return true;
        }

        ReleaseAssignedBuilding();
        excludedStations.Add(station);
        return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
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

    internal bool AdvanceFighterReturnToStation(bool preferAssignedStation = true)
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
        return AdvanceFighterReturnToStation(preferAssignedBarracks);
    }
}
