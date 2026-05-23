using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Ranch : Building, IStorage
{
    private sealed class PlowPathNode
    {
        public PlowPathNode(GridPoint location, int rotationTurns)
        {
            Location = location;
            RotationTurns = rotationTurns;
        }

        public GridPoint Location { get; }

        public int RotationTurns { get; }

        public PlowPathNode? Next { get; set; }
    }

    private readonly record struct PlowPose(GridPoint Location, int RotationTurns);
    private readonly record struct CoverageSearchState(int LocationIndex, int RotationTurns, ulong CoveredMask);
    private readonly record struct PoseSearchState(GridPoint Location, int RotationTurns);

    private static readonly GridPoint[] PlowMoveDirections =
    [
        new GridPoint(1, 0),
        new GridPoint(0, 1),
        new GridPoint(-1, 0),
        new GridPoint(0, -1)
    ];

    private static readonly IReadOnlyDictionary<string, int> EmptyInventory = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<SoilArea> _soilAreas = [];
    private readonly HashSet<SoilTile> _soilTiles = [];
    private readonly HashSet<Creature> _assignments = [];
    private Trilobite? _waitingFarmer;
    private GridPoint? _waitingFarmerRestoreLocation;
    private int _garageWaitTicksRemaining;
    private Plow? _plow;
    private PlowPathNode? _plowPathHead;
    private bool _plowPathDirty = true;

    public Ranch(GameSession session)
        : base("Ranch", new GridPoint(1, 1), [[1]], session, false)
    {
        TextureKey = "Garage";
        Description = "A ranch groups one garage with its connected soil tiles.";
    }

    public Garage? Garage { get; private set; }

    public IReadOnlyCollection<SoilTile> SoilTiles => _soilTiles;

    public IReadOnlyCollection<SoilArea> SoilAreas => _soilAreas;

    public IReadOnlyCollection<Creature> Assignments => _assignments;

    public int FarmerAssignmentPriority => 10;

    public int MaxTrilobites => 1;

    public int AssignmentCapacity => MaxTrilobites;

    public Plow? Plow => _plow;

    public int Capacity => Garage?.Capacity ?? 0;

    public IReadOnlyDictionary<string, int> GetInventory() => Garage?.GetInventory() ?? EmptyInventory;

    public int GetInventoryTotal() => Garage?.GetInventoryTotal() ?? 0;

    public int GetInventorySpace() => Garage?.GetInventorySpace() ?? 0;

    public int Deposit(string resourceType, int amount) => Garage?.Deposit(resourceType, amount) ?? 0;

    public int Withdraw(string resourceType, int amount) => Garage?.Withdraw(resourceType, amount) ?? 0;

    public override int Tick(Cave cave)
    {
        if (_plow?.Cave == cave)
        {
            return _plow.PathPreview.Count == 0 && TryCompleteActivePlowCycle() ? 1 : 0;
        }

        if (_waitingFarmer is null)
        {
            return 0;
        }

        if (!_assignments.Contains(_waitingFarmer) || Garage is null)
        {
            ClearWaitingFarmerState(restoreToTileSystem: true);
            return 0;
        }

        if (_garageWaitTicksRemaining > 0)
        {
            _garageWaitTicksRemaining--;
        }

        if (_garageWaitTicksRemaining > 0)
        {
            return 0;
        }

        if (Session.Danger)
        {
            _garageWaitTicksRemaining = 20;
            return 0;
        }

        return TrySpawnPlowForWaitingFarmer(cave) ? 1 : 0;
    }

    public bool HasAssignmentSlot(Creature? creature = null)
    {
        return (creature is not null && _assignments.Contains(creature)) || _assignments.Count < MaxTrilobites;
    }

    public bool CanAssign(Creature creature)
    {
        return creature is Trilobite trilobite &&
               trilobite.IsFarmer() &&
               HasAssignmentSlot(creature);
    }

    public bool Assign(Creature creature)
    {
        if (!CanAssign(creature))
        {
            return false;
        }

        var added = _assignments.Add(creature);
        if (added)
        {
            TrackCreature(creature);
        }

        return added || _assignments.Contains(creature);
    }

    public bool RemoveAssignment(Creature creature)
    {
        var removed = _assignments.Remove(creature);
        if (!removed)
        {
            return false;
        }

        UntrackCreature(creature);
        if (ReferenceEquals(_waitingFarmer, creature))
        {
            ClearWaitingFarmerState(restoreToTileSystem: true);
        }

        if (_plow?.IsCreatureStationed(creature) == true)
        {
            _plow.DestationCreature(creature);
            if (_plow.Cave is not null)
            {
                _plow.RemoveFromGame("ranchAssignmentRemoved");
            }
        }

        return true;
    }

    public override void TrackedCreatureDied(Creature creature)
    {
        RemoveAssignment(creature);
    }

    public int GetVolume() => _assignments.Count;

    public int GetAvailableAssignmentSlots()
    {
        return System.Math.Max(0, MaxTrilobites - _assignments.Count);
    }

    public bool IsAssigned(Creature creature) => _assignments.Contains(creature);

    public bool IsHandlingFarmer(Trilobite farmer)
    {
        return ReferenceEquals(_waitingFarmer, farmer) ||
               _plow?.IsCreatureStationed(farmer) == true;
    }

    public bool TryBeginGarageWait(Trilobite farmer)
    {
        if (!IsAssigned(farmer) || Garage is null || Cave is null)
        {
            return false;
        }

        if (IsHandlingFarmer(farmer))
        {
            return true;
        }

        _waitingFarmer = farmer;
        _waitingFarmerRestoreLocation = farmer.Location;
        _garageWaitTicksRemaining = 20;
        Cave.RemoveCreatureFromTileSystem(farmer);
        farmer.Location = Garage.GetCenter();
        farmer.HostOnBuilding(Garage, GetGarageWorldCenter(Garage), drawBelowBuildings: true);
        farmer.IsVisible = true;
        farmer.ClearActionQueue();
        return true;
    }

    public override bool RemoveFromGame(object? source = null)
    {
        var cave = Cave;
        if (cave is null)
        {
            return false;
        }

        var removed = false;
        var soilPatchSnapshot = new HashSet<SoilPatch>();
        foreach (var soilTile in _soilTiles)
        {
            soilPatchSnapshot.Add(soilTile.ParentPatch);
        }

        if (Garage?.Cave == cave)
        {
            removed |= Garage.RemoveFromGame(source ?? "ranchRemove");
        }

        foreach (var soilPatch in soilPatchSnapshot)
        {
            if (soilPatch.Cave == cave)
            {
                removed |= soilPatch.RemoveFromGame(source ?? "ranchRemove");
            }
        }

        return removed;
    }

    internal bool Contains(Building building)
    {
        if (building is Garage garage)
        {
            return ReferenceEquals(Garage, garage);
        }

        if (building is SoilArea soilArea)
        {
            return _soilAreas.Contains(soilArea);
        }

        if (building is not SoilPatch soilPatch)
        {
            return false;
        }

        foreach (var soilTile in _soilTiles)
        {
            if (ReferenceEquals(soilTile.ParentPatch, soilPatch))
            {
                return true;
            }
        }

        return false;
    }

    internal void SetGarage(Garage garage)
    {
        Garage = garage;
        garage.Ranch = this;
        Cave = garage.Cave;
        MarkPlowPathDirty();
        RefreshSelectionFootprint();
    }

    internal void ClearGarage(Garage garage)
    {
        if (ReferenceEquals(Garage, garage))
        {
            garage.Ranch = null;
            Garage = null;
            MarkPlowPathDirty();
            RefreshSelectionFootprint();
        }
    }

    internal bool AddSoil(SoilTile soilTile)
    {
        if (!_soilTiles.Add(soilTile))
        {
            return false;
        }

        soilTile.Ranch = this;
        soilTile.TileAddedToRanch();
        if (soilTile.ParentPatch.SoilArea is { } soilArea)
        {
            _soilAreas.Add(soilArea);
            soilArea.Ranch = this;
            soilArea.RefreshSelectionFootprint(this);
        }

        Cave = soilTile.ParentPatch.Cave ?? Cave;
        MarkPlowPathDirty();
        RefreshSelectionFootprint();
        return true;
    }

    internal bool RemoveSoil(SoilTile soilTile)
    {
        if (!_soilTiles.Remove(soilTile))
        {
            return false;
        }

        if (ReferenceEquals(soilTile.Ranch, this))
        {
            soilTile.Ranch = null;
            soilTile.TileRemovedFromRanch();
        }

        if (soilTile.ParentPatch.SoilArea is { } soilArea && !ContainsSoilFromArea(soilArea))
        {
            _soilAreas.Remove(soilArea);
            if (ReferenceEquals(soilArea.Ranch, this))
            {
                soilArea.Ranch = null;
            }
        }

        soilTile.ParentPatch.SoilArea?.RefreshSelectionFootprint(this);

        MarkPlowPathDirty();
        RefreshSelectionFootprint();
        return true;
    }

    internal void Dissolve()
    {
        foreach (var assignedCreature in _assignments.ToArray())
        {
            RemoveAssignment(assignedCreature);
            if (assignedCreature is Trilobite trilobite && ReferenceEquals(trilobite.GetAssignedRanch(), this))
            {
                trilobite.ReleaseAssignedBuilding();
            }
        }

        if (_plow is not null)
        {
            _plow.RemoveFromGame("ranchDissolve");
            _plow = null;
        }

        if (Garage is not null)
        {
            Garage.Ranch = null;
            Garage = null;
        }

        foreach (var soilTile in _soilTiles)
        {
            if (ReferenceEquals(soilTile.Ranch, this))
            {
                soilTile.Ranch = null;
                soilTile.TileRemovedFromRanch();
            }
        }

        _soilTiles.Clear();
        foreach (var soilArea in _soilAreas)
        {
            if (ReferenceEquals(soilArea.Ranch, this))
            {
                soilArea.Ranch = null;
                soilArea.RefreshSelectionFootprint(this);
            }
        }

        _soilAreas.Clear();
        _plowPathHead = null;
        _plowPathDirty = true;
        TileArray = [];
        Location = null;
        Size = new GridPoint(1, 1);
        DisplayBaseSize = Size;
        OpenMap = [[1]];
        Description = "A ranch groups one garage with its connected soil tiles.";
        Cave = null;
    }

    internal bool RebuildPlowPath()
    {
        if (_plow?.Cave is not null)
        {
            _plow.ClearMoveQueue();
        }

        _plowPathHead = null;
        _plowPathDirty = false;

        if (Garage is null || (Garage.Cave ?? Cave) is not { } cave || _soilTiles.Count == 0)
        {
            return false;
        }

        if (!TryResolvePlowStartLocation(cave, out var startLocation, out var startRotationTurns) ||
            !TryBuildPlowPoseSequence(cave, startLocation, startRotationTurns, out var poses) ||
            poses.Count == 0)
        {
            return false;
        }

        PlowPathNode? previous = null;
        for (var index = 0; index < poses.Count; index++)
        {
            var pose = poses[index];
            var node = new PlowPathNode(pose.Location, pose.RotationTurns);
            if (previous is null)
            {
                _plowPathHead = node;
            }
            else
            {
                previous.Next = node;
            }

            previous = node;
        }

        return _plowPathHead is not null;
    }

    // Keep the aggregate ranch footprint aligned to its garage plus every member soil tile.
    internal void RefreshSelectionFootprint()
    {
        var tiles = new List<Tile>();
        var seen = new HashSet<Tile>();
        if (Garage is not null)
        {
            for (var index = 0; index < Garage.TileArray.Count; index++)
            {
                var tile = Garage.TileArray[index];
                if (seen.Add(tile))
                {
                    tiles.Add(tile);
                }
            }
        }

        if (Cave is not null)
        {
            foreach (var soilTile in _soilTiles)
            {
                var worldLocation = soilTile.WorldLocation;
                if (worldLocation is null)
                {
                    continue;
                }

                var tile = Cave.GetTile(worldLocation.Value);
                if (tile is not null && seen.Add(tile))
                {
                    tiles.Add(tile);
                }
            }
        }

        TileArray = tiles;
        if (tiles.Count == 0)
        {
            Location = null;
            Size = new GridPoint(1, 1);
            DisplayBaseSize = Size;
            OpenMap = [[1]];
            Description = "A ranch groups one garage with its connected soil tiles.";
            return;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        for (var index = 0; index < tiles.Count; index++)
        {
            var point = tiles[index].Coordinates;
            minX = System.Math.Min(minX, point.X);
            minY = System.Math.Min(minY, point.Y);
            maxX = System.Math.Max(maxX, point.X);
            maxY = System.Math.Max(maxY, point.Y);
        }

        Location = new GridPoint(minX, minY);
        Size = new GridPoint((maxX - minX) + 1, (maxY - minY) + 1);
        DisplayBaseSize = Size;
        OpenMap = BuildOpenMap(tiles, Location.Value, Size);
        TextureKey = Garage?.TextureKey ?? "SoilTile_1";
        Description = $"A ranch anchored by one garage with {_soilTiles.Count} connected soil tile{(_soilTiles.Count == 1 ? string.Empty : "s")}.";
    }

    private bool ContainsSoilFromArea(SoilArea soilArea)
    {
        foreach (var soilTile in _soilTiles)
        {
            if (ReferenceEquals(soilTile.ParentPatch.SoilArea, soilArea))
            {
                return true;
            }
        }

        return false;
    }

    private static int[][] BuildOpenMap(IReadOnlyList<Tile> tiles, GridPoint location, GridPoint size)
    {
        var map = new int[size.Y][];
        for (var row = 0; row < size.Y; row++)
        {
            map[row] = new int[size.X];
            Array.Fill(map[row], 2);
        }

        for (var index = 0; index < tiles.Count; index++)
        {
            var point = tiles[index].Coordinates;
            map[point.Y - location.Y][point.X - location.X] = 1;
        }

        return map;
    }

    private bool TrySpawnPlowForWaitingFarmer(Cave cave)
    {
        if (_waitingFarmer is null || Garage is null)
        {
            return false;
        }

        if (_plowPathDirty)
        {
            RebuildPlowPath();
        }

        if (_plowPathHead is null)
        {
            return false;
        }

        _plow ??= new Plow(Session);
        if (_plow.Cave is not null)
        {
            return false;
        }

        _plow.ClearMoveQueue();
        _plow.SetDisplayRotationTurns(_plowPathHead.RotationTurns);
        if (!cave.SpawnVehicle(_plow, _plowPathHead.Location))
        {
            return false;
        }

        for (var node = _plowPathHead.Next; node is not null; node = node.Next)
        {
            _plow.EnqueueMove(node.Location, node.RotationTurns);
        }

        var farmer = _waitingFarmer;
        farmer.Location = _waitingFarmerRestoreLocation ?? farmer.Location;
        farmer.IsVisible = true;
        if (!_plow.StationCreature(farmer))
        {
            _plow.ClearMoveQueue();
            _plow.RemoveFromGame("ranchStationFailed");
            return false;
        }

        _waitingFarmer = null;
        _waitingFarmerRestoreLocation = null;
        _garageWaitTicksRemaining = 0;
        return true;
    }

    // Solve plow coverage over legal 2x2 poses instead of traversing the full pose graph and backtracking it.
    private bool TryBuildPlowPoseSequence(Cave cave, GridPoint startLocation, int startRotationTurns, out List<PlowPose> poses)
    {
        poses = [];
        var legalLocations = BuildLegalPlowLocations(cave);
        if (!legalLocations.Contains(startLocation))
        {
            return false;
        }

        var requiredLocations = BuildRequiredCoverageLocations(legalLocations, startLocation);
        if (requiredLocations.Count == 0)
        {
            return false;
        }

        var garageAdjacentEndLocations = BuildGarageAdjacentLegalLocations(legalLocations);
        if (garageAdjacentEndLocations.Count == 0)
        {
            garageAdjacentEndLocations.Add(startLocation);
        }

        if (requiredLocations.Count <= 20 &&
            TryBuildExactCoveragePoseSequence(
                legalLocations,
                startLocation,
                startRotationTurns,
                requiredLocations,
                garageAdjacentEndLocations,
                out poses))
        {
            return true;
        }

        return TryBuildGreedyCoveragePoseSequence(
            legalLocations,
            startLocation,
            startRotationTurns,
            requiredLocations,
            garageAdjacentEndLocations,
            out poses);
    }

    private HashSet<GridPoint> BuildLegalPlowLocations(Cave cave)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var soilTile in _soilTiles)
        {
            var worldLocation = soilTile.WorldLocation;
            if (worldLocation is null)
            {
                continue;
            }

            minX = System.Math.Min(minX, worldLocation.Value.X);
            minY = System.Math.Min(minY, worldLocation.Value.Y);
            maxX = System.Math.Max(maxX, worldLocation.Value.X);
            maxY = System.Math.Max(maxY, worldLocation.Value.Y);
        }

        var locations = new HashSet<GridPoint>();
        if (minX == int.MaxValue)
        {
            return locations;
        }

        var maxRootX = maxX - Plow.DefaultSize.X + 1;
        var maxRootY = maxY - Plow.DefaultSize.Y + 1;
        for (var y = minY; y <= maxRootY; y++)
        {
            for (var x = minX; x <= maxRootX; x++)
            {
                var location = new GridPoint(x, y);
                if (CanOccupyPlowFootprint(cave, location))
                {
                    locations.Add(location);
                }
            }
        }

        return locations;
    }

    private bool CanOccupyPlowFootprint(Cave cave, GridPoint location)
    {
        for (var x = 0; x < Plow.DefaultSize.X; x++)
        {
            for (var y = 0; y < Plow.DefaultSize.Y; y++)
            {
                var point = new GridPoint(location.X + x, location.Y + y);
                var tile = cave.GetTile(point);
                var soilTile = cave.GetSoilTile(point);
                if (tile is null ||
                    string.Equals(tile.Base, "wall", StringComparison.Ordinal) ||
                    soilTile is null ||
                    !ReferenceEquals(soilTile.Ranch, this))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private List<GridPoint> BuildRequiredCoverageLocations(ISet<GridPoint> legalLocations, GridPoint startLocation)
    {
        var requiredLocations = new List<GridPoint> { startLocation };
        var uncoveredSoilLocations = BuildUncoveredSoilLocationSet();
        RemoveCoveredSoilLocations(uncoveredSoilLocations, startLocation);

        var orderedLegalLocations = BuildSortedLegalLocations(legalLocations);
        while (uncoveredSoilLocations.Count > 0)
        {
            var bestNewCoverage = 0;
            var foundCandidate = false;
            var bestCandidate = GridPoint.Zero;

            for (var index = 0; index < orderedLegalLocations.Count; index++)
            {
                var candidate = orderedLegalLocations[index];
                if (requiredLocations.Contains(candidate))
                {
                    continue;
                }

                var newCoverage = CountCoveredSoilLocations(uncoveredSoilLocations, candidate);
                if (newCoverage <= bestNewCoverage)
                {
                    continue;
                }

                bestNewCoverage = newCoverage;
                bestCandidate = candidate;
                foundCandidate = true;
            }

            if (!foundCandidate)
            {
                break;
            }

            requiredLocations.Add(bestCandidate);
            RemoveCoveredSoilLocations(uncoveredSoilLocations, bestCandidate);
        }

        PruneRedundantRequiredLocations(requiredLocations, startLocation);
        return requiredLocations;
    }

    private HashSet<GridPoint> BuildGarageAdjacentLegalLocations(ISet<GridPoint> legalLocations)
    {
        var adjacentLocations = new HashSet<GridPoint>();
        if (Garage?.Location is not { } garageLocation)
        {
            return adjacentLocations;
        }

        for (var sideIndex = 0; sideIndex < 4; sideIndex++)
        {
            var candidate = GetGarageAdjacentPlowRoot(Garage, garageLocation, sideIndex);
            if (legalLocations.Contains(candidate))
            {
                adjacentLocations.Add(candidate);
            }
        }

        return adjacentLocations;
    }

    private bool TryBuildExactCoveragePoseSequence(
        ISet<GridPoint> legalLocations,
        GridPoint startLocation,
        int startRotationTurns,
        IReadOnlyList<GridPoint> requiredLocations,
        ISet<GridPoint> garageAdjacentEndLocations,
        out List<PlowPose> poses)
    {
        poses = [];
        var orderedLegalLocations = BuildSortedLegalLocations(legalLocations);
        var legalLocationIndexes = new Dictionary<GridPoint, int>(orderedLegalLocations.Count);
        for (var index = 0; index < orderedLegalLocations.Count; index++)
        {
            legalLocationIndexes[orderedLegalLocations[index]] = index;
        }

        if (!legalLocationIndexes.TryGetValue(startLocation, out var startLocationIndex))
        {
            return false;
        }

        var requiredBitIndexes = new Dictionary<GridPoint, int>(requiredLocations.Count);
        ulong requiredMask = 0UL;
        for (var index = 0; index < requiredLocations.Count; index++)
        {
            requiredBitIndexes[requiredLocations[index]] = index;
            requiredMask |= 1UL << index;
        }

        var endLocationIndexes = new HashSet<int>();
        foreach (var endLocation in garageAdjacentEndLocations)
        {
            if (legalLocationIndexes.TryGetValue(endLocation, out var endLocationIndex))
            {
                endLocationIndexes.Add(endLocationIndex);
            }
        }

        var startState = new CoverageSearchState(
            startLocationIndex,
            NormalizeRotationTurns(startRotationTurns),
            GetRequiredCoverageMask(startLocation, requiredBitIndexes));
        var queue = new Queue<CoverageSearchState>();
        var seen = new HashSet<CoverageSearchState> { startState };
        var previousStates = new Dictionary<CoverageSearchState, CoverageSearchState>();
        queue.Enqueue(startState);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.CoveredMask == requiredMask && endLocationIndexes.Contains(current.LocationIndex))
            {
                poses = ReconstructCoveragePosePath(current, startState, previousStates, orderedLegalLocations);
                return AreAllSoilTilesCovered(poses);
            }

            foreach (var nextState in EnumerateCoverageTransitions(current, orderedLegalLocations, legalLocationIndexes, requiredBitIndexes))
            {
                if (!seen.Add(nextState))
                {
                    continue;
                }

                previousStates[nextState] = current;
                queue.Enqueue(nextState);
            }
        }

        return false;
    }

    private IEnumerable<CoverageSearchState> EnumerateCoverageTransitions(
        CoverageSearchState state,
        IReadOnlyList<GridPoint> orderedLegalLocations,
        IReadOnlyDictionary<GridPoint, int> legalLocationIndexes,
        IReadOnlyDictionary<GridPoint, int> requiredBitIndexes)
    {
        var location = orderedLegalLocations[state.LocationIndex];
        var forwardDirection = PlowMoveDirections[state.RotationTurns];
        var forwardLocation = new GridPoint(location.X + forwardDirection.X, location.Y + forwardDirection.Y);
        if (legalLocationIndexes.TryGetValue(forwardLocation, out var forwardLocationIndex))
        {
            yield return new CoverageSearchState(
                forwardLocationIndex,
                state.RotationTurns,
                state.CoveredMask | GetRequiredCoverageMask(forwardLocation, requiredBitIndexes));
        }

        yield return new CoverageSearchState(state.LocationIndex, NormalizeRotationTurns(state.RotationTurns + 1), state.CoveredMask);
        yield return new CoverageSearchState(state.LocationIndex, NormalizeRotationTurns(state.RotationTurns - 1), state.CoveredMask);
    }

    private static ulong GetRequiredCoverageMask(GridPoint location, IReadOnlyDictionary<GridPoint, int> requiredBitIndexes)
    {
        return requiredBitIndexes.TryGetValue(location, out var bitIndex)
            ? 1UL << bitIndex
            : 0UL;
    }

    private static List<PlowPose> ReconstructCoveragePosePath(
        CoverageSearchState endState,
        CoverageSearchState startState,
        IReadOnlyDictionary<CoverageSearchState, CoverageSearchState> previousStates,
        IReadOnlyList<GridPoint> orderedLegalLocations)
    {
        var reversed = new List<PlowPose>();
        var current = endState;
        while (true)
        {
            reversed.Add(new PlowPose(orderedLegalLocations[current.LocationIndex], current.RotationTurns));
            if (current.Equals(startState))
            {
                break;
            }

            current = previousStates[current];
        }

        reversed.Reverse();
        return reversed;
    }

    private bool TryBuildGreedyCoveragePoseSequence(
        ISet<GridPoint> legalLocations,
        GridPoint startLocation,
        int startRotationTurns,
        IReadOnlyList<GridPoint> requiredLocations,
        ISet<GridPoint> garageAdjacentEndLocations,
        out List<PlowPose> poses)
    {
        poses = [new PlowPose(startLocation, NormalizeRotationTurns(startRotationTurns))];
        var remainingRequiredLocations = new HashSet<GridPoint>(requiredLocations);
        remainingRequiredLocations.Remove(startLocation);

        while (remainingRequiredLocations.Count > 0)
        {
            if (!TryBuildShortestPosePathToAnyTarget(poses[^1], legalLocations, remainingRequiredLocations, out var nextSegment))
            {
                return false;
            }

            AppendPoseSegment(poses, nextSegment);
            for (var index = 0; index < nextSegment.Count; index++)
            {
                remainingRequiredLocations.Remove(nextSegment[index].Location);
            }
        }

        if (!garageAdjacentEndLocations.Contains(poses[^1].Location))
        {
            if (!TryBuildShortestPosePathToAnyTarget(poses[^1], legalLocations, garageAdjacentEndLocations, out var endSegment))
            {
                return false;
            }

            AppendPoseSegment(poses, endSegment);
        }

        return AreAllSoilTilesCovered(poses);
    }

    private bool TryBuildShortestPosePathToAnyTarget(
        PlowPose startPose,
        ISet<GridPoint> legalLocations,
        ISet<GridPoint> targetLocations,
        out List<PlowPose> poses)
    {
        poses = [];
        if (targetLocations.Contains(startPose.Location))
        {
            poses.Add(startPose);
            return true;
        }

        var queue = new Queue<PoseSearchState>();
        var startState = new PoseSearchState(startPose.Location, NormalizeRotationTurns(startPose.RotationTurns));
        var seen = new HashSet<PoseSearchState> { startState };
        var previousStates = new Dictionary<PoseSearchState, PoseSearchState>();
        queue.Enqueue(startState);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var nextState in EnumeratePoseTransitions(current, legalLocations))
            {
                if (!seen.Add(nextState))
                {
                    continue;
                }

                previousStates[nextState] = current;
                if (targetLocations.Contains(nextState.Location))
                {
                    poses = ReconstructPosePath(nextState, startState, previousStates);
                    return true;
                }

                queue.Enqueue(nextState);
            }
        }

        return false;
    }

    private IEnumerable<PoseSearchState> EnumeratePoseTransitions(PoseSearchState state, ISet<GridPoint> legalLocations)
    {
        var forwardDirection = PlowMoveDirections[state.RotationTurns];
        var forwardLocation = new GridPoint(state.Location.X + forwardDirection.X, state.Location.Y + forwardDirection.Y);
        if (legalLocations.Contains(forwardLocation))
        {
            yield return new PoseSearchState(forwardLocation, state.RotationTurns);
        }

        yield return new PoseSearchState(state.Location, NormalizeRotationTurns(state.RotationTurns + 1));
        yield return new PoseSearchState(state.Location, NormalizeRotationTurns(state.RotationTurns - 1));
    }

    private static List<PlowPose> ReconstructPosePath(
        PoseSearchState endState,
        PoseSearchState startState,
        IReadOnlyDictionary<PoseSearchState, PoseSearchState> previousStates)
    {
        var reversed = new List<PlowPose>();
        var current = endState;
        while (true)
        {
            reversed.Add(new PlowPose(current.Location, current.RotationTurns));
            if (current.Equals(startState))
            {
                break;
            }

            current = previousStates[current];
        }

        reversed.Reverse();
        return reversed;
    }

    private static void AppendPoseSegment(List<PlowPose> poses, IReadOnlyList<PlowPose> segment)
    {
        for (var index = 1; index < segment.Count; index++)
        {
            poses.Add(segment[index]);
        }
    }

    private static List<GridPoint> BuildSortedLegalLocations(ISet<GridPoint> legalLocations)
    {
        var orderedLegalLocations = new List<GridPoint>(legalLocations.Count);
        foreach (var location in legalLocations)
        {
            orderedLegalLocations.Add(location);
        }

        orderedLegalLocations.Sort(static (left, right) =>
        {
            var yComparison = left.Y.CompareTo(right.Y);
            return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
        });
        return orderedLegalLocations;
    }

    private HashSet<GridPoint> BuildUncoveredSoilLocationSet()
    {
        var uncoveredSoilLocations = new HashSet<GridPoint>();
        foreach (var soilTile in _soilTiles)
        {
            var worldLocation = soilTile.WorldLocation;
            if (worldLocation is not null)
            {
                uncoveredSoilLocations.Add(worldLocation.Value);
            }
        }

        return uncoveredSoilLocations;
    }

    private static int CountCoveredSoilLocations(ISet<GridPoint> uncoveredSoilLocations, GridPoint poseLocation)
    {
        var coveredCount = 0;
        for (var x = 0; x < Plow.DefaultSize.X; x++)
        {
            for (var y = 0; y < Plow.DefaultSize.Y; y++)
            {
                if (uncoveredSoilLocations.Contains(new GridPoint(poseLocation.X + x, poseLocation.Y + y)))
                {
                    coveredCount++;
                }
            }
        }

        return coveredCount;
    }

    private static void RemoveCoveredSoilLocations(ISet<GridPoint> uncoveredSoilLocations, GridPoint poseLocation)
    {
        for (var x = 0; x < Plow.DefaultSize.X; x++)
        {
            for (var y = 0; y < Plow.DefaultSize.Y; y++)
            {
                uncoveredSoilLocations.Remove(new GridPoint(poseLocation.X + x, poseLocation.Y + y));
            }
        }
    }

    private void PruneRedundantRequiredLocations(List<GridPoint> requiredLocations, GridPoint startLocation)
    {
        for (var index = requiredLocations.Count - 1; index >= 0; index--)
        {
            if (requiredLocations[index] == startLocation)
            {
                continue;
            }

            var removed = requiredLocations[index];
            requiredLocations.RemoveAt(index);
            if (!AreAllSoilTilesCovered(requiredLocations))
            {
                requiredLocations.Insert(index, removed);
            }
        }
    }

    private bool AreAllSoilTilesCovered(IReadOnlyList<PlowPose> posePath)
    {
        foreach (var soilTile in _soilTiles)
        {
            var worldLocation = soilTile.WorldLocation;
            if (worldLocation is null || !IsSoilTileCovered(posePath, worldLocation.Value))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreAllSoilTilesCovered(IReadOnlyList<GridPoint> poseLocations)
    {
        foreach (var soilTile in _soilTiles)
        {
            var worldLocation = soilTile.WorldLocation;
            if (worldLocation is null || !IsSoilTileCovered(poseLocations, worldLocation.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSoilTileCovered(IReadOnlyList<PlowPose> posePath, GridPoint soilLocation)
    {
        for (var index = 0; index < posePath.Count; index++)
        {
            var location = posePath[index].Location;
            if (soilLocation.X >= location.X &&
                soilLocation.X < location.X + Plow.DefaultSize.X &&
                soilLocation.Y >= location.Y &&
                soilLocation.Y < location.Y + Plow.DefaultSize.Y)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSoilTileCovered(IReadOnlyList<GridPoint> locationPath, GridPoint soilLocation)
    {
        for (var index = 0; index < locationPath.Count; index++)
        {
            var pose = locationPath[index];
            if (soilLocation.X >= pose.X &&
                soilLocation.X < pose.X + Plow.DefaultSize.X &&
                soilLocation.Y >= pose.Y &&
                soilLocation.Y < pose.Y + Plow.DefaultSize.Y)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryCompleteActivePlowCycle()
    {
        if (_plow is null || _plow.Cave is null)
        {
            return false;
        }

        var farmer = GetStationedFarmer(_plow);
        if (farmer is null || Garage is null)
        {
            _plow.RemoveFromGame("ranchCycleComplete");
            return true;
        }

        var restoreLocation = _plow.Location ?? farmer.Location;
        if (!_plow.DestationCreatureWithoutRestore(farmer))
        {
            _plow.RemoveFromGame("ranchCycleComplete");
            return true;
        }

        _waitingFarmer = farmer;
        _waitingFarmerRestoreLocation = restoreLocation;
        _garageWaitTicksRemaining = 20;
        farmer.Location = restoreLocation;
        farmer.HostOnBuilding(Garage, GetGarageWorldCenter(Garage), drawBelowBuildings: true);
        farmer.IsVisible = true;
        farmer.ClearActionQueue();
        _plow.RemoveFromGame("ranchCycleComplete");
        return true;
    }

    private static Trilobite? GetStationedFarmer(Plow plow)
    {
        foreach (var creature in plow.StationedCreatures)
        {
            if (creature is Trilobite trilobite)
            {
                return trilobite;
            }
        }

        return null;
    }

    private bool TryResolvePlowStartLocation(Cave cave, out GridPoint startLocation, out int rotationTurns)
    {
        startLocation = GridPoint.Zero;
        rotationTurns = 0;
        if (Garage?.Location is not { } garageLocation)
        {
            return false;
        }

        var frontSideIndex = Garage.GetDisplayRotationTurns();
        for (var offset = 0; offset < 4; offset++)
        {
            var sideIndex = (frontSideIndex + offset) % 4;
            var direction = GetSideDirection(sideIndex);
            var candidate = GetGarageAdjacentPlowRoot(Garage, garageLocation, sideIndex);
            if (!CanOccupyPlowFootprint(cave, candidate))
            {
                continue;
            }

            startLocation = candidate;
            rotationTurns = TryGetRotationTurns(direction, out var turns) ? turns : 0;
            return true;
        }

        return false;
    }

    private static GridPoint GetGarageAdjacentPlowRoot(Garage garage, GridPoint garageLocation, int sideIndex)
    {
        return NormalizeRotationTurns(sideIndex) switch
        {
            0 => new GridPoint(garageLocation.X + garage.Size.X, garageLocation.Y),
            1 => new GridPoint(garageLocation.X, garageLocation.Y + garage.Size.Y),
            2 => new GridPoint(garageLocation.X - Plow.DefaultSize.X, garageLocation.Y),
            _ => new GridPoint(garageLocation.X, garageLocation.Y - Plow.DefaultSize.Y)
        };
    }

    private static GridPoint GetSideDirection(int sideIndex)
    {
        return NormalizeRotationTurns(sideIndex) switch
        {
            0 => new GridPoint(1, 0),
            1 => new GridPoint(0, 1),
            2 => new GridPoint(-1, 0),
            _ => new GridPoint(0, -1)
        };
    }

    private static bool TryGetRotationTurns(GridPoint direction, out int rotationTurns)
    {
        rotationTurns = 0;
        if (direction == new GridPoint(1, 0))
        {
            return true;
        }

        if (direction == new GridPoint(0, 1))
        {
            rotationTurns = 1;
            return true;
        }

        if (direction == new GridPoint(-1, 0))
        {
            rotationTurns = 2;
            return true;
        }

        if (direction == new GridPoint(0, -1))
        {
            rotationTurns = 3;
            return true;
        }

        return false;
    }

    private static int NormalizeRotationTurns(int turns)
    {
        return ((turns % 4) + 4) % 4;
    }

    private void MarkPlowPathDirty()
    {
        _plowPathHead = null;
        _plowPathDirty = true;
    }

    private void ClearWaitingFarmerState(bool restoreToTileSystem)
    {
        if (_waitingFarmer is not { } farmer)
        {
            return;
        }

        farmer.IsVisible = true;
        if (_waitingFarmerRestoreLocation is { } restoreLocation)
        {
            farmer.Location = restoreLocation;
        }

        if (restoreToTileSystem)
        {
            if (Cave?.PlaceCreatureOnTile(farmer, farmer.Location, randomizeMovementOffset: false) != true)
            {
                farmer.LeaveTileSystem();
            }
        }

        _waitingFarmer = null;
        _waitingFarmerRestoreLocation = null;
        _garageWaitTicksRemaining = 0;
    }

    private static Vector2 GetGarageWorldCenter(Garage garage)
    {
        var location = garage.Location ?? GridPoint.Zero;
        return new Vector2(
            (location.X * TileConstants.TileSize) + ((garage.Size.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((garage.Size.Y - 1) * TileConstants.TileHalfSize));
    }
}
