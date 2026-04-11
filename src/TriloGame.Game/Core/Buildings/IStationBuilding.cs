using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Buildings;

public interface IStationBuilding
{
    IReadOnlyList<StationSlot> Stations { get; }

    int Capacity { get; }

    int FighterAssignmentPriority { get; }

    IReadOnlyCollection<Creature> Assignments { get; }

    bool HasAssignmentSlot(Creature? creature = null);

    bool CanAssign(Creature creature);

    bool IsAssigned(Creature creature);

    int? GetAssignedStationIndex(Creature creature);

    int GetVolume();

    bool Assign(Creature creature);

    bool RemoveAssignment(Creature creature);
}
