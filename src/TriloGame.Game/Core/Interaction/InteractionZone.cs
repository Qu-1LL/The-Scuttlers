using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Interaction;

public enum InteractionZonePurpose
{
    Feeding,
    Brooding,
    ResourceTransfer,
    Work,
    Construction,
    Station,
    Approach
}

public readonly record struct WorldRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public int Area => checked(Width * Height);

    public bool Contains(WorldPoint point)
    {
        return point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
    }
}

public sealed record InteractionZoneDefinition(
    string Name,
    InteractionZonePurpose Purpose,
    GridPoint Origin,
    GridPoint Size,
    IReadOnlyList<GridPoint> Slots,
    bool IsNavigationTarget = true);

public readonly record struct ZoneReservation(
    int CreatureId,
    int SlotIndex,
    int ExpiresAtTick);

public sealed class InteractionZone
{
    public const int ReservationLeaseTicks = 30;
    private readonly WorldPoint[] _slotPositions;
    private readonly ZoneReservation?[] _reservations;

    public InteractionZone(
        int id,
        Building owner,
        string name,
        InteractionZonePurpose purpose,
        WorldRectangle worldBounds,
        IReadOnlyList<WorldPoint> slotPositions,
        bool isNavigationTarget)
    {
        Id = id;
        Owner = owner;
        Name = name;
        Purpose = purpose;
        WorldBounds = worldBounds;
        IsNavigationTarget = isNavigationTarget;
        _slotPositions = slotPositions.ToArray();
        _reservations = new ZoneReservation?[_slotPositions.Length];
    }

    public int Id { get; }

    public Building Owner { get; }

    public string Name { get; }

    public InteractionZonePurpose Purpose { get; }

    public WorldRectangle WorldBounds { get; }

    // Navigation fields seed walkable player-interaction slots, not spawn or hosted-only slots.
    public bool IsNavigationTarget { get; }

    public IReadOnlyList<WorldPoint> SlotPositions => _slotPositions;

    public int Capacity => _slotPositions.Length;

    public bool IsShared => Purpose == InteractionZonePurpose.ResourceTransfer;

    public int OccupiedCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < _reservations.Length; index++)
            {
                if (_reservations[index].HasValue)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool TryReserve(Creature creature, int tick, out int slotIndex)
    {
        if (IsShared)
        {
            slotIndex = GetNearestSlotIndex(creature.Position);
            return slotIndex >= 0;
        }

        ExpireReservations(tick);
        for (var index = 0; index < _reservations.Length; index++)
        {
            if (_reservations[index] is { } existing && existing.CreatureId == creature.Id)
            {
                _reservations[index] = existing with { ExpiresAtTick = tick + ReservationLeaseTicks };
                slotIndex = index;
                return true;
            }
        }

        for (var index = 0; index < _reservations.Length; index++)
        {
            if (_reservations[index].HasValue)
            {
                continue;
            }

            _reservations[index] = new ZoneReservation(creature.Id, index, tick + ReservationLeaseTicks);
            slotIndex = index;
            return true;
        }

        slotIndex = -1;
        return false;
    }

    public bool TryRenew(Creature creature, int tick, int slotIndex)
    {
        if (IsShared)
        {
            return slotIndex >= 0 && slotIndex < _slotPositions.Length;
        }

        if (slotIndex < 0 || slotIndex >= _reservations.Length ||
            _reservations[slotIndex] is not { } reservation ||
            reservation.CreatureId != creature.Id)
        {
            return false;
        }

        _reservations[slotIndex] = reservation with { ExpiresAtTick = tick + ReservationLeaseTicks };
        return true;
    }

    public bool TryMoveReservation(Creature creature, int tick, int targetSlotIndex)
    {
        if (IsShared)
        {
            return targetSlotIndex >= 0 && targetSlotIndex < _slotPositions.Length;
        }

        if (targetSlotIndex < 0 || targetSlotIndex >= _reservations.Length)
        {
            return false;
        }

        var currentSlotIndex = -1;
        for (var index = 0; index < _reservations.Length; index++)
        {
            if (_reservations[index] is { } reservation && reservation.CreatureId == creature.Id)
            {
                currentSlotIndex = index;
                break;
            }
        }

        if (currentSlotIndex < 0 ||
            (targetSlotIndex != currentSlotIndex && _reservations[targetSlotIndex].HasValue))
        {
            return false;
        }

        _reservations[currentSlotIndex] = null;
        _reservations[targetSlotIndex] = new ZoneReservation(
            creature.Id,
            targetSlotIndex,
            tick + ReservationLeaseTicks);
        return true;
    }

    public bool IsReservedBy(int slotIndex, int creatureId)
    {
        if (IsShared)
        {
            return false;
        }

        return slotIndex >= 0 &&
               slotIndex < _reservations.Length &&
               _reservations[slotIndex] is { } reservation &&
               reservation.CreatureId == creatureId;
    }

    public bool Release(Creature creature)
    {
        if (IsShared)
        {
            return true;
        }

        for (var index = 0; index < _reservations.Length; index++)
        {
            if (_reservations[index] is not { } reservation || reservation.CreatureId != creature.Id)
            {
                continue;
            }

            _reservations[index] = null;
            return true;
        }

        return false;
    }

    public bool IsReserved(int slotIndex)
    {
        if (IsShared)
        {
            return false;
        }

        return slotIndex >= 0 && slotIndex < _reservations.Length && _reservations[slotIndex].HasValue;
    }

    public void ExpireReservations(int tick)
    {
        for (var index = 0; index < _reservations.Length; index++)
        {
            if (_reservations[index] is { } reservation && reservation.ExpiresAtTick < tick)
            {
                _reservations[index] = null;
            }
        }
    }

    public void ClearReservations()
    {
        Array.Clear(_reservations);
    }

    private int GetNearestSlotIndex(WorldPoint position)
    {
        var bestIndex = -1;
        var bestDistance = long.MaxValue;
        for (var index = 0; index < _slotPositions.Length; index++)
        {
            var delta = _slotPositions[index] - position;
            var distance = delta.LengthSquared;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = index;
        }

        return bestIndex;
    }
}
