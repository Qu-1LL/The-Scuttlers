using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Core.Pathfinding;

public sealed class BarracksOwnershipField : BuildingOwnershipField<Barracks>
{
    public BarracksOwnershipField(Cave? cave = null)
        : base("Barracks", static world => world.GetBarracksList(), cave)
    {
    }
}
