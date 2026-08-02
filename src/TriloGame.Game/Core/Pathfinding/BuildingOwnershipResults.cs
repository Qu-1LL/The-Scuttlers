using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.Core.Pathfinding;

// A point-in-time result from comparing the individual building navigation fields.
public readonly record struct BuildingOwnership<TBuilding>(TBuilding? Building, int Distance)
    where TBuilding : Building
{
    public bool IsOwned => Building is not null && Distance != int.MaxValue;
}

public readonly record struct BuildingOwnershipSnapshot(Building? Building, int Distance)
{
    public bool IsOwned => Building is not null && Distance != int.MaxValue;
}
