using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Core.Pathfinding;

public sealed class TurretOwnershipField : BuildingOwnershipField<Turret>
{
    public TurretOwnershipField(Cave? cave = null)
        : base("Turret", static world => world.GetTurretList(), cave)
    {
    }
}
