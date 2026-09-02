using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Barracks : StationBuilding
{
    public const int DefaultFighterAssignmentPriority = 0;

    private static readonly int[][] DefaultOpenMap =
    [
        [1, 1, 1],
        [1, 0, 1],
        [1, 1, 1]
    ];
    private static readonly int[] StationRingOrder = [0, 1, 2, 4, 7, 6, 5, 3];

    public Barracks(GameSession session)
        : base(
            "Barracks",
            new GridPoint(3, 3),
            DefaultOpenMap,
            session,
            "Barracks",
            "Fighters will wait here until danger arises.",
            DefaultFighterAssignmentPriority,
            CreateTileStations(DefaultOpenMap))
    {
    }

    protected override bool TracksAssignments => true;

    protected override bool AllowsSharedStationSlots => true;

    // Barracks are an unlimited home assignment; any walkable barracks tile is a valid wait location.
    public override bool IsCreatureAtNavigationTarget(Creature creature)
    {
        return creature.IsLocomotionEnabled &&
               Cave?.GetTile(creature.Location) is { } tile &&
               ReferenceEquals(tile.Built, this) &&
               tile.CreatureFits(creature);
    }

    public override bool IsCreatureStationed(Creature creature)
    {
        return creature.IsLocomotionEnabled &&
               TryGetAssignedStationTile(creature, out var assignedTile) &&
               creature.Location == assignedTile;
    }

    public override bool TryStationCreature(Creature creature)
    {
        if (!IsCreatureAtNavigationTarget(creature))
        {
            return false;
        }

        if (!TryGetAssignedStationTile(creature, out var assignedTile))
        {
            return false;
        }

        if (creature.Location != assignedTile)
        {
            if (TryGetNextStationRingTile(creature.Location, assignedTile, out var nextTile))
            {
                creature.BeginMovement(nextTile);
                return true;
            }

            return false;
        }

        creature.SetActivity(CreatureActivity.Stationed);
        return true;
    }

    // Walk the passable perimeter rather than cutting through the blocked center tile.
    private bool TryGetNextStationRingTile(GridPoint currentTile, GridPoint assignedTile, out GridPoint nextTile)
    {
        var currentIndex = GetStationRingIndex(currentTile);
        var assignedIndex = GetStationRingIndex(assignedTile);
        if (currentIndex < 0 || assignedIndex < 0)
        {
            nextTile = default;
            return false;
        }

        var clockwiseDistance = (assignedIndex - currentIndex + StationRingOrder.Length) % StationRingOrder.Length;
        var counterClockwiseDistance = (currentIndex - assignedIndex + StationRingOrder.Length) % StationRingOrder.Length;
        var nextRingIndex = clockwiseDistance <= counterClockwiseDistance
            ? (currentIndex + 1) % StationRingOrder.Length
            : (currentIndex + StationRingOrder.Length - 1) % StationRingOrder.Length;
        return TryGetStationTile(StationRingOrder[nextRingIndex], out nextTile);
    }

    private int GetStationRingIndex(GridPoint tileLocation)
    {
        for (var ringIndex = 0; ringIndex < StationRingOrder.Length; ringIndex++)
        {
            if (TryGetStationTile(StationRingOrder[ringIndex], out var stationTile) && stationTile == tileLocation)
            {
                return ringIndex;
            }
        }

        return -1;
    }

}
