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
    private static readonly IReadOnlyDictionary<string, int> EmptyInventory = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<Soil> _soilTiles = [];
    private readonly HashSet<Creature> _assignments = [];
    private Trilobite? _waitingFarmer;
    private GridPoint? _waitingFarmerRestoreLocation;
    private int _garageWaitTicksRemaining;
    private Plow? _plow;

    public Ranch(GameSession session)
        : base("Ranch", new GridPoint(1, 1), [[1]], session, false)
    {
        TextureKey = "Garage";
        Description = "A ranch groups one garage with its connected soil tiles.";
    }

    public Garage? Garage { get; private set; }

    public IReadOnlyCollection<Soil> SoilTiles => _soilTiles;

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
        return Math.Max(0, MaxTrilobites - _assignments.Count);
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
        farmer.HostOnBuilding(Garage, GetGarageWorldCenter(Garage));
        farmer.IsVisible = false;
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
        var soilSnapshot = _soilTiles.ToArray();
        if (Garage?.Cave == cave)
        {
            removed |= Garage.RemoveFromGame(source ?? "ranchRemove");
        }

        for (var index = 0; index < soilSnapshot.Length; index++)
        {
            if (soilSnapshot[index].Cave == cave)
            {
                removed |= soilSnapshot[index].RemoveFromGame(source ?? "ranchRemove");
            }
        }

        return removed;
    }

    internal bool Contains(Building building)
    {
        return building switch
        {
            Garage garage => ReferenceEquals(Garage, garage),
            Soil soil => _soilTiles.Contains(soil),
            _ => false
        };
    }

    internal void SetGarage(Garage garage)
    {
        Garage = garage;
        garage.Ranch = this;
        Cave = garage.Cave;
        RefreshSelectionFootprint();
    }

    internal void ClearGarage(Garage garage)
    {
        if (ReferenceEquals(Garage, garage))
        {
            garage.Ranch = null;
            Garage = null;
            RefreshSelectionFootprint();
        }
    }

    internal bool AddSoil(Soil soil)
    {
        if (!_soilTiles.Add(soil))
        {
            return false;
        }

        soil.Ranch = this;
        soil.TileAddedToRanch();
        Cave = soil.Cave ?? Cave;
        RefreshSelectionFootprint();
        return true;
    }

    internal bool RemoveSoil(Soil soil)
    {
        if (!_soilTiles.Remove(soil))
        {
            return false;
        }

        if (ReferenceEquals(soil.Ranch, this))
        {
            soil.Ranch = null;
            soil.TileRemovedFromRanch();
        }

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

        foreach (var soil in _soilTiles)
        {
            if (ReferenceEquals(soil.Ranch, this))
            {
                soil.Ranch = null;
                soil.TileRemovedFromRanch();
            }
        }

        _soilTiles.Clear();
        TileArray = [];
        Location = null;
        Size = new GridPoint(1, 1);
        DisplayBaseSize = Size;
        OpenMap = [[1]];
        Description = "A ranch groups one garage with its connected soil tiles.";
        Cave = null;
    }

    // Keep the aggregate ranch footprint aligned to its garage plus every member soil tile.
    internal void RefreshSelectionFootprint()
    {
        var tiles = new List<Tile>();
        if (Garage is not null)
        {
            tiles.AddRange(Garage.TileArray);
        }

        foreach (var soil in _soilTiles)
        {
            tiles.AddRange(soil.TileArray);
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

        _plow ??= new Plow(Session);
        if (_plow.Cave is null)
        {
            if (!TryFindPlowSpawnLocation(cave, _plow, out var spawnLocation) ||
                !cave.SpawnVehicle(_plow, spawnLocation))
            {
                return false;
            }
        }

        var farmer = _waitingFarmer;
        farmer.Location = _waitingFarmerRestoreLocation ?? farmer.Location;
        farmer.IsVisible = true;
        if (!_plow.StationCreature(farmer))
        {
            return false;
        }

        _waitingFarmer = null;
        _waitingFarmerRestoreLocation = null;
        _garageWaitTicksRemaining = 0;
        return true;
    }

    private bool TryFindPlowSpawnLocation(Cave cave, Plow plow, out GridPoint location)
    {
        location = default;
        if (Garage?.Location is not { } garageLocation)
        {
            return false;
        }

        var candidates = new[]
        {
            new GridPoint(garageLocation.X + Garage.Size.X, garageLocation.Y),
            new GridPoint(garageLocation.X - plow.Size.X, garageLocation.Y),
            new GridPoint(garageLocation.X, garageLocation.Y + Garage.Size.Y),
            new GridPoint(garageLocation.X, garageLocation.Y - plow.Size.Y)
        };

        for (var index = 0; index < candidates.Length; index++)
        {
            if (cave.CanPlaceVehicle(plow, candidates[index]))
            {
                location = candidates[index];
                return true;
            }
        }

        return false;
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
