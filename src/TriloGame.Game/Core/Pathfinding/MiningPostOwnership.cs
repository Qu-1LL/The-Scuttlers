using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.Core.Pathfinding;

public readonly record struct MiningPostOwnership(MiningPost? Post, int Distance)
{
    public bool IsOwned => Post is not null && Distance != int.MaxValue;

    internal static MiningPostOwnership From(BuildingOwnership<MiningPost> ownership)
    {
        return new MiningPostOwnership(ownership.Building, ownership.Distance);
    }
}
