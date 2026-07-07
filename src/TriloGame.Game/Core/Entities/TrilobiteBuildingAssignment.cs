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

    public void Release(Trilobite trilobite, bool restoreHostedCreatureToTileSystem, bool invalidateMineableQueue)
    {
        switch (Building)
        {
            case MiningPost post:
                if (invalidateMineableQueue)
                {
                    post.InvalidateMineableQueues();
                }

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
                    if (restoreHostedCreatureToTileSystem)
                    {
                        station.TryRestoreCreatureToTileSystem(trilobite);
                    }
                    else
                    {
                        trilobite.LeaveTileSystem();
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
