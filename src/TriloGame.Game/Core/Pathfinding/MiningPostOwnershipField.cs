using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Core.Pathfinding;

public readonly record struct MiningPostOwnership(MiningPost? Post, int Distance)
{
    public bool IsOwned => Post is not null && Distance != int.MaxValue;

    internal static MiningPostOwnership From(BuildingOwnership<MiningPost> ownership)
    {
        return new MiningPostOwnership(ownership.Building, ownership.Distance);
    }
}

public sealed class MiningPostOwnershipField : BuildingOwnershipField<MiningPost>
{
    public MiningPostOwnershipField(Cave? cave = null)
        : base("Mining Post", static world => world.GetMiningPosts(), cave)
    {
    }
}
