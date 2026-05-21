using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Vehicles;

public abstract class Vehicle : IVehicle
{
    private readonly Queue<GridPoint> _moveQueue = [];
    private readonly HashSet<Creature> _stationedCreatures = [];
    private readonly Dictionary<Creature, int> _stationSlotIndexes = [];
    private readonly List<VehicleStationSlot> _stationSlots;

    protected Vehicle(
        string name,
        string textureKey,
        string assignmentClassification,
        GridPoint size,
        int health,
        int maxStationedCreatures,
        IEnumerable<VehicleStationSlot> stationSlots,
        GameSession session)
    {
        Name = name;
        TextureKey = textureKey;
        AssignmentClassification = assignmentClassification;
        Size = size;
        Health = health;
        MaxHealth = health;
        MaxStationedCreatures = Math.Max(0, maxStationedCreatures);
        _stationSlots = new List<VehicleStationSlot>(stationSlots);
        Session = session;
        TileArray = [];
        PathPreview = [];
    }

    public string Name { get; }

    public string TextureKey { get; }

    public string AssignmentClassification { get; }

    public GameSession Session { get; }

    public Cave? Cave { get; private set; }

    public GridPoint Size { get; }

    public GridPoint? Location { get; private set; }

    public int Health { get; private set; }

    public int MaxHealth { get; }

    public int MaxStationedCreatures { get; }

    public IReadOnlyCollection<Creature> StationedCreatures => _stationedCreatures;

    public IReadOnlyList<VehicleStationSlot> StationSlots => _stationSlots;

    public List<Tile> TileArray { get; private set; }

    IReadOnlyList<Tile> IVehicle.TileArray => TileArray;

    public List<GridPoint> PathPreview { get; }

    IReadOnlyList<GridPoint> IVehicle.PathPreview => PathPreview;

    public int DisplayRotationTurns { get; private set; }

    public int GetDisplayRotationTurns() => ((DisplayRotationTurns % 4) + 4) % 4;

    public void SetDisplayRotationTurns(int turns)
    {
        DisplayRotationTurns = ((turns % 4) + 4) % 4;
        SyncStationedCreatureTransforms();
    }

    public GridPoint GetRotatedSize()
    {
        return GetDisplayRotationTurns() % 2 == 0
            ? Size
            : new GridPoint(Size.Y, Size.X);
    }

    public Vector2 GetWorldCenter()
    {
        var location = Location ?? GridPoint.Zero;
        var rotatedSize = GetRotatedSize();
        return new Vector2(
            (location.X * TileConstants.TileSize) + ((rotatedSize.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((rotatedSize.Y - 1) * TileConstants.TileHalfSize));
    }

    public bool CanStation(Creature creature)
    {
        return creature.Assignment == AssignmentClassification &&
               (_stationedCreatures.Contains(creature) || _stationedCreatures.Count < MaxStationedCreatures) &&
               (_stationedCreatures.Contains(creature) || _stationedCreatures.Count < _stationSlots.Count);
    }

    public bool StationCreature(Creature creature)
    {
        if (_stationedCreatures.Contains(creature))
        {
            SyncStationedCreatureTransform(creature);
            return true;
        }

        if (!CanStation(creature))
        {
            return false;
        }

        var slotIndex = GetFirstOpenStationSlotIndex();
        if (slotIndex < 0)
        {
            return false;
        }

        if (Cave is not null && ReferenceEquals(creature.Cave, Cave))
        {
            Cave.RemoveCreatureFromTileSystem(creature);
        }
        else
        {
            creature.LeaveTileSystem();
        }

        _stationedCreatures.Add(creature);
        _stationSlotIndexes[creature] = slotIndex;
        SyncStationedCreatureTransform(creature);
        OnStationCreature(creature);
        return true;
    }

    public bool DestationCreature(Creature creature)
    {
        if (!_stationedCreatures.Remove(creature))
        {
            return false;
        }

        _stationSlotIndexes.Remove(creature);
        OnDestationCreature(creature);
        RestoreDestationedCreature(creature);
        return true;
    }

    public bool IsCreatureStationed(Creature creature) => _stationedCreatures.Contains(creature);

    public void EnqueueMove(GridPoint destination)
    {
        _moveQueue.Enqueue(destination);
        PathPreview.Add(destination);
    }

    public void ClearMoveQueue()
    {
        _moveQueue.Clear();
        PathPreview.Clear();
    }

    public object? Move()
    {
        if (_moveQueue.Count == 0)
        {
            return null;
        }

        var destination = _moveQueue.Dequeue();
        var moved = Cave?.MoveVehicle(this, destination) ?? false;
        if (moved && PathPreview.Count > 0)
        {
            PathPreview.RemoveAt(0);
        }

        return moved;
    }

    public int TakeDamage(int amount, object? source = null)
    {
        if (amount <= 0 || Health <= 0)
        {
            return 0;
        }

        var applied = Math.Min(Health, amount);
        Health -= applied;
        if (Health <= 0)
        {
            Health = 0;
            RemoveFromGame(source);
        }

        return applied;
    }

    public bool RemoveFromGame(object? source = null)
    {
        return Cave?.RemoveVehicle(this, source) ?? true;
    }

    internal void AttachToCave(Cave cave, GridPoint location, List<Tile> tiles)
    {
        Cave = cave;
        Location = location;
        TileArray = tiles;
        SyncStationedCreatureTransforms();
    }

    internal void MoveWithinCave(GridPoint location, List<Tile> tiles)
    {
        Location = location;
        TileArray = tiles;
        SyncStationedCreatureTransforms();
    }

    internal void DetachFromCave()
    {
        Cave = null;
        Location = null;
        TileArray = [];
        ClearMoveQueue();
    }

    internal void CleanupBeforeRemoval(object? source = null)
    {
        var stationedSnapshot = _stationedCreatures.ToArray();
        for (var index = 0; index < stationedSnapshot.Length; index++)
        {
            DestationCreature(stationedSnapshot[index]);
        }

        OnVehicleDestroyed(source);
    }

    internal IEnumerable<GridPoint> EnumerateOccupiedPoints(GridPoint location)
    {
        var rotatedSize = GetRotatedSize();
        for (var x = 0; x < rotatedSize.X; x++)
        {
            for (var y = 0; y < rotatedSize.Y; y++)
            {
                yield return new GridPoint(location.X + x, location.Y + y);
            }
        }
    }

    private int GetFirstOpenStationSlotIndex()
    {
        for (var index = 0; index < _stationSlots.Count; index++)
        {
            if (!_stationSlotIndexes.ContainsValue(index))
            {
                return index;
            }
        }

        return -1;
    }

    private void SyncStationedCreatureTransforms()
    {
        foreach (var creature in _stationedCreatures)
        {
            SyncStationedCreatureTransform(creature);
        }
    }

    private void SyncStationedCreatureTransform(Creature creature)
    {
        if (!_stationSlotIndexes.TryGetValue(creature, out var slotIndex) ||
            slotIndex < 0 ||
            slotIndex >= _stationSlots.Count)
        {
            return;
        }

        var vehicleRotation = GetDisplayRotationTurns() * MathF.PI / 2f;
        var slot = _stationSlots[slotIndex];
        var localOffset = Rotate(slot.LocalPixelOffset, vehicleRotation);
        creature.HostOnVehicle(this, GetWorldCenter() + localOffset);
        creature.RotationRadians = NormalizeRadians(vehicleRotation + slot.CreatureRotationRadians);
        creature.IsVisible = true;
    }

    private void RestoreDestationedCreature(Creature creature)
    {
        creature.IsVisible = true;
        if (Cave is not null && ReferenceEquals(creature.Cave, Cave))
        {
            foreach (var location in EnumerateRestorationLocations(creature))
            {
                if (Cave.PlaceCreatureOnTile(creature, location, randomizeMovementOffset: false))
                {
                    return;
                }
            }
        }

        creature.LeaveTileSystem();
    }

    // Vehicles restore passengers back into the cave using their own footprint first.
    protected virtual IEnumerable<GridPoint> EnumerateRestorationLocations(Creature creature)
    {
        if (Location is { } location)
        {
            yield return location;
        }

        for (var index = 0; index < TileArray.Count; index++)
        {
            var candidate = TileArray[index].Coordinates;
            if (Location is { } root && candidate == root)
            {
                continue;
            }

            yield return candidate;
        }

        yield return creature.Location;
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(
            (value.X * cos) - (value.Y * sin),
            (value.X * sin) + (value.Y * cos));
    }

    private static float NormalizeRadians(float radians)
    {
        var twoPi = MathF.PI * 2f;
        var normalized = radians % twoPi;
        return normalized < 0f ? normalized + twoPi : normalized;
    }

    protected abstract void OnStationCreature(Creature creature);

    protected abstract void OnDestationCreature(Creature creature);

    protected abstract void OnVehicleDestroyed(object? source);
}
