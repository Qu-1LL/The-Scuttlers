using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Core.Pathfinding;

public sealed class AlgaeFarmOwnershipField : BuildingOwnershipField<AlgaeFarm>
{
    public AlgaeFarmOwnershipField(Cave? cave = null)
        : base("Algae Farm", static world => world.GetAlgaeFarms(), cave)
    {
    }
}
