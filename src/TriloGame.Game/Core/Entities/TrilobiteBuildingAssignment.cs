using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.Core.Entities;

internal sealed class TrilobiteBuildingAssignment
{
    public Building? Building { get; private set; }

    public bool IsAssignedTo(Building? building)
    {
        return ReferenceEquals(Building, building);
    }

    public void Set(Building? building)
    {
        Building = building;
    }

    public void Release(Trilobite trilobite, bool restoreHostedCreatureLocomotion, bool invalidateMineableQueue)
    {
        switch (Building)
        {
            case MiningPost post:
                post.RemoveAssignment(trilobite);
                break;
            case AlgaeFarm farm:
                farm.RemoveAssignment(trilobite);
                break;
            case Ranch ranch:
                ranch.RemoveAssignment(trilobite);
                break;
            case StationBuilding station:
                if (ReferenceEquals(trilobite.HostedBuilding, station))
                {
                    if (restoreHostedCreatureLocomotion)
                    {
                        station.TryRestoreCreatureLocomotion(trilobite);
                    }
                    else
                    {
                        trilobite.DisableLocomotion();
                    }
                }

                station.RemoveAssignment(trilobite);
                break;
            case Scaffolding scaffolding:
                scaffolding.RemoveAssignment(trilobite);
                scaffolding.ReleaseMaterialReservation(trilobite);
                break;
        }

        Building = null;
    }
}
