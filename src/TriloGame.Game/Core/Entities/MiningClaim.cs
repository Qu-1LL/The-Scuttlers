using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public readonly record struct MiningClaim(
    int MiningPostId,
    string TileKey,
    WorldPoint ApproachPoint,
    int ClaimedTick,
    IReadOnlyList<GridPoint>? Route = null);

public enum MiningClaimFailureReason
{
    None,
    NoCompatibleResource,
    NoReachableApproach,
    PostFull,
    StaleQueue,
    NoCave
}

public readonly record struct MiningClaimResult(MiningClaim? Claim, MiningClaimFailureReason FailureReason)
{
    public bool Claimed => Claim.HasValue;

    public static MiningClaimResult Success(MiningClaim claim) => new(claim, MiningClaimFailureReason.None);

    public static MiningClaimResult Failed(MiningClaimFailureReason reason) => new(null, reason);
}

public static class MiningClaimAllocator
{
    public static MiningClaim? TryClaimNext(Trilobite miner, MiningPost post, ResourceName? carriedResource = null)
    {
        return TryClaimNextDetailed(miner, post, carriedResource).Claim;
    }

    public static MiningClaimResult TryClaimNextDetailed(Trilobite miner, MiningPost post, ResourceName? carriedResource = null)
    {
        if (miner.Cave is not { } cave)
        {
            return MiningClaimResult.Failed(MiningClaimFailureReason.NoCave);
        }

        if (post.GetInventorySpace() <= 0)
        {
            return MiningClaimResult.Failed(MiningClaimFailureReason.PostFull);
        }

        if (!post.HasClaimableMineableFor(cave, miner, carriedResource))
        {
            return MiningClaimResult.Failed(ResolveNoClaimReason(cave, post, miner, carriedResource));
        }

        var routeResult = cave.BuildPathToNearestTrackedMineableApproach(miner, post, carriedResource);
        if (!routeResult.HasValue)
        {
            return MiningClaimResult.Failed(MiningClaimFailureReason.NoReachableApproach);
        }

        var tile = cave.GetTile(routeResult.Value.TileKey);
        if (tile is null)
        {
            return MiningClaimResult.Failed(MiningClaimFailureReason.StaleQueue);
        }

        var claim = TryBuildClaim(miner, post, tile, routeResult.Value.Path);
        if (!claim.HasValue)
        {
            return MiningClaimResult.Failed(MiningClaimFailureReason.NoReachableApproach);
        }

        post.Assign(miner, tile.Key);
        return MiningClaimResult.Success(claim.Value);
    }

    public static MiningClaim? TryClaim(Trilobite miner, MiningPost post, Tile tile)
    {
        if (miner.Cave is null)
        {
            return null;
        }

        var claim = TryBuildClaim(miner, post, tile);
        if (!claim.HasValue)
        {
            return null;
        }

        post.Assign(miner, tile.Key);
        return claim;
    }

    private static MiningClaim? TryBuildClaim(Trilobite miner, MiningPost post, Tile tile, IReadOnlyList<GridPoint>? resolvedPath = null)
    {
        var cave = miner.Cave!;
        var path = resolvedPath ?? cave.BuildPathToMineableApproach(miner, tile);
        if (path is null || path.Count == 0)
        {
            return null;
        }

        var approach = WorldPoint.FromGridPoint(path[^1]);
        return new MiningClaim(post.Id, tile.Key, approach, miner.Session.TickCount, path);
    }

    private static MiningClaimFailureReason ResolveNoClaimReason(
        Cave cave,
        MiningPost post,
        Creature creature,
        ResourceName? carriedResource)
    {
        if (carriedResource.HasValue && post.HasAnyClaimableMineable(cave))
        {
            return MiningClaimFailureReason.NoCompatibleResource;
        }

        return post.MineableQueuesDirty
            ? MiningClaimFailureReason.StaleQueue
            : MiningClaimFailureReason.NoReachableApproach;
    }
}
