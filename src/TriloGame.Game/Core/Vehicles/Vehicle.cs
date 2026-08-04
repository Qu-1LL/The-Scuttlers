using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Vehicles;

public abstract class Vehicle : IVehicle
{
    private const int DefaultMovementSpeed = WorldUnits.UnitsPerTile / 2;
    private readonly Queue<MoveStep> _moveQueue = [];
    private readonly HashSet<Creature> _stationedCreatures = [];
    private readonly Dictionary<Creature, int> _stationSlotIndexes = [];
    private readonly List<VehicleStationSlot> _stationSlots;
    private Creature? _driver;
    private bool _hasActiveMove;
    private GridPoint _activeMoveDestination;
    private bool _hasActiveRotation;
    private int _activeRotationTargetTurns;
    private int _activeRotationTicksElapsed;
    private int _activeRotationTicksTotal;
    private float _activeRotationStartRadians;
    private float _activeRotationDeltaRadians;
    private float _rotationRadians;
    private float _previousRotationRadians;

    private readonly record struct MoveStep(GridPoint Destination, int RotationTurns);

    protected Vehicle(
        string name,
        string description,
        string textureKey,
        string assignmentClassification,
        GridPoint size,
        int health,
        int maxStationedCreatures,
        IEnumerable<VehicleStationSlot> stationSlots,
        GameSession session)
    {
        Id = session.AllocateWorldObjectId();
        Name = name;
        Description = description;
        TextureKey = textureKey;
        AssignmentClassification = assignmentClassification;
        AssignmentRole = CreatureRoleNames.Parse(assignmentClassification);
        Size = size;
        Health = health;
        MaxHealth = health;
        MaxStationedCreatures = Math.Max(0, maxStationedCreatures);
        _stationSlots = new List<VehicleStationSlot>(stationSlots);
        Session = session;
        TileArray = [];
        RouteCells = [];
    }

    public string Name { get; }

    public int Id { get; }

    public string Description { get; }

    public string TextureKey { get; }

    public string AssignmentClassification { get; }

    public CreatureRole AssignmentRole { get; }

    public GameSession Session { get; }

    public Cave? Cave { get; private set; }

    public GridPoint Size { get; }

    public GridPoint? Location { get; private set; }

    // Continuous vehicle center used by the renderer and stationed passenger transforms.
    public WorldPoint Position { get; private set; }

    public WorldPoint PreviousPosition { get; private set; }

    public WorldPoint? MovementTarget { get; private set; }

    public bool HasActiveMovement => _hasActiveMove;

    public bool HasActiveRotation => _hasActiveRotation;

    public virtual int MovementSpeed => DefaultMovementSpeed;

    public virtual int RotationTicksPerQuarterTurn => 0;

    public int Health { get; private set; }

    public int MaxHealth { get; }

    public int MaxStationedCreatures { get; }

    public IReadOnlyCollection<Creature> StationedCreatures => _stationedCreatures;

    public IReadOnlyList<VehicleStationSlot> StationSlots => _stationSlots;

    public Creature? Driver => _driver;

    public List<Tile> TileArray { get; private set; }

    IReadOnlyList<Tile> IVehicle.TileArray => TileArray;

    public List<GridPoint> RouteCells { get; }

    IReadOnlyList<GridPoint> IVehicle.RouteCells => RouteCells;

    public int DisplayRotationTurns { get; private set; }

    public int GetDisplayRotationTurns() => ((DisplayRotationTurns % 4) + 4) % 4;

    // Most vehicles yield to creature bodies; dedicated work vehicles may opt into their fixed route.
    public virtual bool CanTraverseCreatureCells => false;

    public virtual int MaximumStraightTileStepDistance => 1;

    public void SetDisplayRotationTurns(int turns)
    {
        DisplayRotationTurns = ((turns % 4) + 4) % 4;
        _hasActiveRotation = false;
        _rotationRadians = DisplayRotationTurns * MathF.PI / 2f;
        _previousRotationRadians = _rotationRadians;
        SyncStationedCreatureTransforms();
    }

    public GridPoint GetRotatedSize()
    {
        return GetDisplayRotationTurns() % 2 == 0
            ? Size
            : new GridPoint(Size.Y, Size.X);
    }

    public Vector2 GetWorldCenter() => Position.ToWorldPixels();

    public Vector2 GetInterpolatedWorldCenter(float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        return Vector2.Lerp(PreviousPosition.ToWorldPixels(), Position.ToWorldPixels(), alpha);
    }

    public float GetInterpolatedRotationRadians(float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        return _previousRotationRadians + ((_rotationRadians - _previousRotationRadians) * alpha);
    }

    private WorldPoint GetGridWorldCenter(GridPoint location)
    {
        var rotatedSize = GetRotatedSize();
        return WorldPoint.FromWorldPixels(new Vector2(
            (location.X * TileConstants.TileSize) + ((rotatedSize.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((rotatedSize.Y - 1) * TileConstants.TileHalfSize)));
    }

    public Interaction.WorldRectangle GetWorldBounds()
    {
        var rotatedSize = GetRotatedSize();
        var width = rotatedSize.X * WorldUnits.UnitsPerTile;
        var height = rotatedSize.Y * WorldUnits.UnitsPerTile;
        return new Interaction.WorldRectangle(
            Position.X - (width / 2),
            Position.Y - (height / 2),
            width,
            height);
    }

    public bool CanStation(Creature creature)
    {
        return creature.Role == AssignmentRole &&
               (_stationedCreatures.Contains(creature) || _stationedCreatures.Count < MaxStationedCreatures) &&
               (_stationedCreatures.Contains(creature) || _stationedCreatures.Count < _stationSlots.Count);
    }

    public bool StationCreature(Creature creature)
    {
        if (_stationedCreatures.Contains(creature))
        {
            UpdateDriver(creature);
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
            Cave.DisableCreatureLocomotion(creature);
        }
        else
        {
            creature.DisableLocomotion();
        }

        _stationedCreatures.Add(creature);
        _stationSlotIndexes[creature] = slotIndex;
        UpdateDriver(creature);
        SyncStationedCreatureTransform(creature);
        OnStationCreature(creature);
        return true;
    }

    public bool DestationCreature(Creature creature)
    {
        return DestationCreatureInternal(creature, restoreLocomotion: true);
    }

    internal bool DestationCreatureWithoutRestore(Creature creature)
    {
        return DestationCreatureInternal(creature, restoreLocomotion: false);
    }

    private bool DestationCreatureInternal(Creature creature, bool restoreLocomotion)
    {
        if (!_stationedCreatures.Remove(creature))
        {
            return false;
        }

        _stationSlotIndexes.Remove(creature);
        if (ReferenceEquals(_driver, creature))
        {
            _driver = null;
        }
        OnDestationCreature(creature);
        if (restoreLocomotion)
        {
            RestoreDestationedCreature(creature);
        }
        else
        {
            creature.DisableLocomotion();
        }

        return true;
    }

    public bool IsCreatureStationed(Creature creature) => _stationedCreatures.Contains(creature);

    public bool IsCreatureDriving(Creature creature) => ReferenceEquals(_driver, creature);

    public void EnqueueMove(GridPoint destination)
    {
        EnqueueMove(destination, GetDisplayRotationTurns());
    }

    public void EnqueueMove(GridPoint destination, int rotationTurns)
    {
        _moveQueue.Enqueue(new MoveStep(destination, ((rotationTurns % 4) + 4) % 4));
        RouteCells.Add(destination);
    }

    public void ClearMoveQueue()
    {
        _moveQueue.Clear();
        RouteCells.Clear();
        _hasActiveMove = false;
        _hasActiveRotation = false;
        MovementTarget = null;
    }

    public object? Move()
    {
        PreviousPosition = Position;
        _previousRotationRadians = _rotationRadians;
        if (_hasActiveRotation)
        {
            return AdvanceActiveRotation();
        }

        if (_hasActiveMove)
        {
            return AdvanceActiveMove();
        }

        if (_moveQueue.Count == 0 || Location is not { } location)
        {
            return null;
        }

        var moveStep = _moveQueue.Peek();
        if (moveStep.RotationTurns != GetDisplayRotationTurns())
        {
            if (RotationTicksPerQuarterTurn > 0)
            {
                BeginActiveRotation(moveStep.RotationTurns);
                return AdvanceActiveRotation();
            }

            SetDisplayRotationTurns(moveStep.RotationTurns);
            if (moveStep.Destination == location)
            {
                CompleteMoveStep(location, moveStep.Destination);
            }
            else
            {
                OnMoveSucceeded(location, location);
            }

            return true;
        }

        if (moveStep.Destination == location)
        {
            CompleteMoveStep(location, moveStep.Destination);
            return true;
        }

        _activeMoveDestination = moveStep.Destination;
        _hasActiveMove = true;
        MovementTarget = GetGridWorldCenter(moveStep.Destination);
        return AdvanceActiveMove();
    }

    // Animate a cardinal turn over a deterministic number of simulation ticks before moving again.
    private void BeginActiveRotation(int targetTurns)
    {
        var currentTurns = GetDisplayRotationTurns();
        var clockwiseDistance = ((targetTurns - currentTurns) + 4) % 4;
        var counterClockwiseDistance = ((currentTurns - targetTurns) + 4) % 4;
        var signedQuarterTurns = clockwiseDistance <= counterClockwiseDistance
            ? clockwiseDistance
            : -counterClockwiseDistance;

        _activeRotationTargetTurns = ((targetTurns % 4) + 4) % 4;
        _activeRotationTicksElapsed = 0;
        _activeRotationTicksTotal = System.Math.Abs(signedQuarterTurns) * RotationTicksPerQuarterTurn;
        _activeRotationStartRadians = _rotationRadians;
        _activeRotationDeltaRadians = signedQuarterTurns * MathF.PI / 2f;
        _hasActiveRotation = _activeRotationTicksTotal > 0;
    }

    private bool AdvanceActiveRotation()
    {
        if (!_hasActiveRotation || Location is not { } location || _moveQueue.Count == 0)
        {
            return false;
        }

        _activeRotationTicksElapsed++;
        _rotationRadians = _activeRotationStartRadians +
                           (_activeRotationDeltaRadians * _activeRotationTicksElapsed / _activeRotationTicksTotal);
        SyncStationedCreatureTransforms();
        if (_activeRotationTicksElapsed < _activeRotationTicksTotal)
        {
            return true;
        }

        _hasActiveRotation = false;
        DisplayRotationTurns = _activeRotationTargetTurns;
        var moveStep = _moveQueue.Peek();
        if (moveStep.Destination == location)
        {
            CompleteMoveStep(location, moveStep.Destination);
        }
        else
        {
            OnMoveSucceeded(location, location);
        }

        return true;
    }

    // Advance a straight route segment at continuous creature-equivalent speed.
    private bool AdvanceActiveMove()
    {
        if (!_hasActiveMove || MovementTarget is not { } target || Location is not { } previousLocation)
        {
            return false;
        }

        var delta = target - Position;
        var distance = delta.Length;
        if (distance > MovementSpeed)
        {
            Position += delta.WithMagnitude(MovementSpeed);
            SyncStationedCreatureTransforms();
            return true;
        }

        if (Cave?.MoveVehicle(this, _activeMoveDestination) != true)
        {
            return false;
        }

        Position = target;
        CompleteMoveStep(previousLocation, _activeMoveDestination);
        SyncStationedCreatureTransforms();
        return true;
    }

    private void CompleteMoveStep(GridPoint previousLocation, GridPoint currentLocation)
    {
        if (_moveQueue.Count > 0)
        {
            _moveQueue.Dequeue();
        }

        if (RouteCells.Count > 0)
        {
            RouteCells.RemoveAt(0);
        }

        _hasActiveMove = false;
        MovementTarget = null;
        OnMoveSucceeded(previousLocation, currentLocation);
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
        Position = GetGridWorldCenter(location);
        PreviousPosition = Position;
        MovementTarget = null;
        _hasActiveMove = false;
        _previousRotationRadians = _rotationRadians;
        SyncStationedCreatureTransforms();
    }

    internal void MoveWithinCave(GridPoint location, List<Tile> tiles)
    {
        Location = location;
        TileArray = tiles;
    }

    internal void DetachFromCave()
    {
        Cave = null;
        Location = null;
        TileArray = [];
        ClearMoveQueue();
        Position = WorldPoint.Zero;
        PreviousPosition = Position;
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

    private void UpdateDriver(Creature creature)
    {
        if (this is IDriveable && creature is Trilobite trilobite && (_driver is null || ReferenceEquals(_driver, trilobite)))
        {
            _driver = trilobite;
        }
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

        var slot = _stationSlots[slotIndex];
        var previousLocalOffset = Rotate(slot.LocalPixelOffset, _previousRotationRadians);
        var localOffset = Rotate(slot.LocalPixelOffset, _rotationRadians);
        var previousRotation = NormalizeRadians(_previousRotationRadians + slot.CreatureRotationRadians);
        var rotation = NormalizeRadians(_rotationRadians + slot.CreatureRotationRadians);
        if (!creature.IsHostedOnVehicle(this))
        {
            creature.HostOnVehicle(this, GetWorldCenter() + localOffset);
            creature.UpdateHostedVehiclePose(
                this,
                WorldPoint.FromWorldPixels(Position.ToWorldPixels() + localOffset),
                WorldPoint.FromWorldPixels(Position.ToWorldPixels() + localOffset),
                rotation,
                rotation);
            return;
        }

        creature.UpdateHostedVehiclePose(
            this,
            WorldPoint.FromWorldPixels(PreviousPosition.ToWorldPixels() + previousLocalOffset),
            WorldPoint.FromWorldPixels(Position.ToWorldPixels() + localOffset),
            previousRotation,
            rotation);
    }

    private void RestoreDestationedCreature(Creature creature)
    {
        creature.IsVisible = true;
        if (Cave is not null && ReferenceEquals(creature.Cave, Cave))
        {
            foreach (var location in EnumerateRestorationLocations(creature))
            {
                if (Cave.PlaceCreatureOnTile(creature, location))
                {
                    return;
                }
            }
        }

        creature.DisableLocomotion();
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

    protected virtual void OnMoveSucceeded(GridPoint previousLocation, GridPoint currentLocation)
    {
    }

    protected abstract void OnVehicleDestroyed(object? source);
}
