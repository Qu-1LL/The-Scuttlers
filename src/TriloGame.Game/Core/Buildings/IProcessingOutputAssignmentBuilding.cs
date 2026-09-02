using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Buildings;

// Lets processors reserve complete output loads for collectors while they travel to the building.
public interface IProcessingOutputAssignmentBuilding
{
    int GetOutputCollectorCount(ResourceName resourceType);

    int GetAssignedOutputCarryingCapacity(ResourceName resourceType);

    bool CanAssignOutputCollector(Trilobite collector, ResourceName resourceType);

    bool TryAssignOutputCollector(Trilobite collector, ResourceName resourceType);

    bool ReleaseOutputCollector(Trilobite collector);
}
