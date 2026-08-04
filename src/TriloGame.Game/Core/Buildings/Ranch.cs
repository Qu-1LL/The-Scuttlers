using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Pathfinding;
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
    private static readonly GridPoint[] PlowMoveDirections =
    [
        new GridPoint(1, 0),
        new GridPoint(0, 1),
        new GridPoint(-1, 0),
        new GridPoint(0, -1)
    ];

    private static readonly IReadOnlyDictionary<ResourceName, int> EmptyInventory = new Dictionary<ResourceName, int>();
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

    public override bool MaintainsNavigationField => false;

    public override BuildingNavigationSeedMode NavigationSeedMode => BuildingNavigationSeedMode.None;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.None;

    public Garage? Garage { get; private set; }

    public IReadOnlyCollection<SoilTile> SoilTiles => _soilTiles;

    public IReadOnlyCollection<SoilArea> SoilAreas => _soilAreas;

    public IReadOnlyCollection<Creature> Assignments => _assignments;

    public int FarmerAssignmentPriority => 10;

    public int MaxTrilobites => 1;

    public int AssignmentCapacity => MaxTrilobites;

    public Plow? Plow => _plow;

    public int Capacity => Garage?.Capacity ?? 0;

    public IReadOnlyDictionary<ResourceName, int> GetInventory() => Garage?.GetInventory() ?? EmptyInventory;

    public int GetInventoryTotal() => Garage?.GetInventoryTotal() ?? 0;

    public int GetInventorySpace() => Garage?.GetInventorySpace() ?? 0;

    public int Deposit(ResourceName resourceType, int amount) => Garage?.Deposit(resourceType, amount) ?? 0;

    public int Withdraw(ResourceName resourceType, int amount) => Garage?.Withdraw(resourceType, amount) ?? 0;

    public override int Tick(Cave cave)
    {
        if (_plow?.Cave == cave)
        {
            return _plow.RouteCells.Count == 0 && TryCompleteActivePlowCycle() ? 1 : 0;
        }

        if (_waitingFarmer is null)
        {
            return 0;
        }

        if (!_assignments.Contains(_waitingFarmer) || Garage is null)
        {
            ClearWaitingFarmerState(restoreLocomotion: true);
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
            ClearWaitingFarmerState(restoreLocomotion: true);
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
        Cave.DisableCreatureLocomotion(farmer);
        farmer.HostOnBuilding(Garage, GetGarageWorldCenter(Garage), drawBelowBuildings: true);
        farmer.IsVisible = true;
        farmer.ClearTaskQueue();
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

    // Rebuild the simple, deterministic coverage route whenever ranch soil changes.
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
            !TryBuildPlowPoseSequence(cave, startLocation, startRotationTurns, out var poses))
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
        TextureKey = Garage?.TextureKey ?? "SoilTile_0";
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

    // Spawn the plow from the garage and station its assigned farmer before it begins the route.
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
        farmer.IsVisible = true;
        if (!_plow.StationCreature(farmer))
        {
            _plow.ClearMoveQueue();
            _plow.RemoveFromGame("ranchStationFailed");
            return false;
        }

        _plow.ProcessCurrentFootprint();
        _waitingFarmer = null;
        _waitingFarmerRestoreLocation = null;
        _garageWaitTicksRemaining = 0;
        return true;
    }
    // Sweep legal soil footprints in rows; row changes advance by the plow's full two-tile height.
    private bool TryBuildPlowPoseSequence(Cave cave, GridPoint startLocation, int startRotationTurns, out List<PlowPose> poses)
    {
        poses = [];
        var legalLocations = BuildLegalPlowLocations(cave);
        if (!legalLocations.Contains(startLocation))
        {
            return false;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var location in legalLocations)
        {
            minX = System.Math.Min(minX, location.X);
            minY = System.Math.Min(minY, location.Y);
            maxX = System.Math.Max(maxX, location.X);
            maxY = System.Math.Max(maxY, location.Y);
        }

        var firstRowStart = new GridPoint(minX, minY);
        if (!legalLocations.Contains(firstRowStart))
        {
            return false;
        }

        poses.Add(new PlowPose(startLocation, NormalizeRotationTurns(startRotationTurns)));
        if (!TryAppendShortestPlowRoute(poses, firstRowStart, legalLocations))
        {
            return false;
        }

        var rowY = minY;
        var movesRight = true;
        while (true)
        {
            var rowStart = new GridPoint(movesRight ? minX : maxX, rowY);
            var rowEndX = movesRight ? maxX : minX;
            if (!legalLocations.Contains(rowStart) || !TryAppendShortestPlowRoute(poses, rowStart, legalLocations))
            {
                return false;
            }

            while (poses[^1].Location.X != rowEndX)
            {
                var stepX = movesRight ? 1 : -1;
                AppendPlowMove(poses, new GridPoint(poses[^1].Location.X + stepX, rowY));
            }

            var nextRowY = rowY + Plow.DefaultSize.Y;
            if (nextRowY > maxY)
            {
                AppendPlowTurn(poses, movesRight ? 2 : 0);
                break;
            }

            // One turn at the row end works the outer edge before the two-tile row change.
            AppendPlowTurn(poses, 1);
            var nextRowStart = new GridPoint(poses[^1].Location.X, nextRowY);
            if (!legalLocations.Contains(nextRowStart))
            {
                return false;
            }

            AppendPlowMove(poses, nextRowStart);
            movesRight = !movesRight;
            AppendPlowTurn(poses, movesRight ? 0 : 2);
            rowY = nextRowY;
        }

        if (!TryAppendShortestPlowRoute(poses, startLocation, legalLocations))
        {
            return false;
        }

        return AreAllSoilTilesCovered(poses);
    }

    // Connect the garage-side spawn and return endpoint to the standard sweep without pose searching.
    private static bool TryAppendShortestPlowRoute(
        List<PlowPose> poses,
        GridPoint destination,
        ISet<GridPoint> legalLocations)
    {
        var start = poses[^1].Location;
        if (start == destination)
        {
            return true;
        }

        var queue = new Queue<GridPoint>();
        var visited = new HashSet<GridPoint> { start };
        var previousLocations = new Dictionary<GridPoint, GridPoint>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            for (var index = 0; index < PlowMoveDirections.Length; index++)
            {
                var direction = PlowMoveDirections[index];
                var next = new GridPoint(current.X + direction.X, current.Y + direction.Y);
                if (!legalLocations.Contains(next) || !visited.Add(next))
                {
                    continue;
                }

                previousLocations[next] = current;
                if (next == destination)
                {
                    var reversedPath = new List<GridPoint>();
                    for (var pathLocation = destination; pathLocation != start; pathLocation = previousLocations[pathLocation])
                    {
                        reversedPath.Add(pathLocation);
                    }

                    for (var pathIndex = reversedPath.Count - 1; pathIndex >= 0; pathIndex--)
                    {
                        AppendPlowMove(poses, reversedPath[pathIndex]);
                    }

                    return true;
                }

                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static void AppendPlowTurn(List<PlowPose> poses, int rotationTurns)
    {
        var currentPose = poses[^1];
        var normalizedRotationTurns = NormalizeRotationTurns(rotationTurns);
        if (currentPose.RotationTurns != normalizedRotationTurns)
        {
            poses.Add(new PlowPose(currentPose.Location, normalizedRotationTurns));
        }
    }

    private static void AppendPlowMove(List<PlowPose> poses, GridPoint destination)
    {
        var currentLocation = poses[^1].Location;
        var deltaX = destination.X - currentLocation.X;
        var deltaY = destination.Y - currentLocation.Y;
        if ((deltaX != 0 && deltaY != 0) || (deltaX == 0 && deltaY == 0))
        {
            throw new InvalidOperationException("Plow coverage routes may only move in one cardinal direction.");
        }

        var direction = new GridPoint(System.Math.Sign(deltaX), System.Math.Sign(deltaY));
        if (!TryGetRotationTurns(direction, out var rotationTurns))
        {
            throw new InvalidOperationException("Plow coverage routes require a cardinal direction.");
        }

        poses.Add(new PlowPose(destination, rotationTurns));
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
    private bool TryCompleteActivePlowCycle()
    {
        if (_plow is null || _plow.Cave is null)
        {
            return false;
        }

        var garage = Garage;
        if (garage is not null)
        {
            _plow.TransferInventoryTo(garage);
        }

        var farmer = GetStationedFarmer(_plow);
        if (farmer is null || garage is null)
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
        farmer.HostOnBuilding(garage, GetGarageWorldCenter(garage), drawBelowBuildings: true);
        farmer.IsVisible = true;
        farmer.ClearTaskQueue();
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

    private void ClearWaitingFarmerState(bool restoreLocomotion)
    {
        if (_waitingFarmer is not { } farmer)
        {
            return;
        }

        farmer.IsVisible = true;
        if (restoreLocomotion)
        {
            var restoreLocation = _waitingFarmerRestoreLocation ?? farmer.Location;
            if (Cave?.PlaceCreatureOnTile(farmer, restoreLocation) != true)
            {
                farmer.DisableLocomotion();
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
