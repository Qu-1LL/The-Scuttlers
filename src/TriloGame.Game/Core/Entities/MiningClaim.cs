using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public readonly record struct MiningClaim(
    int MiningPostId,
    string TileKey,
    WorldPoint ApproachPoint,
    int ClaimedTick);

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

        var maximumAttempts = Math.Max(1, cave.GetTiles().Count);
        var sawCandidateWithoutApproach = false;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var tile = post.GrabMineableTile(cave, miner, carriedResource);
            if (tile is null)
            {
                return MiningClaimResult.Failed(
                    sawCandidateWithoutApproach
                        ? MiningClaimFailureReason.NoReachableApproach
                        : MiningClaimFailureReason.StaleQueue);
            }

            var claim = TryBuildClaim(miner, post, tile);
            if (claim.HasValue)
            {
                return MiningClaimResult.Success(claim.Value);
            }

            sawCandidateWithoutApproach = true;
            post.RemoveAssignment(miner);
        }

        return MiningClaimResult.Failed(MiningClaimFailureReason.NoReachableApproach);
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

    private static MiningClaim? TryBuildClaim(Trilobite miner, MiningPost post, Tile tile)
    {
        var cave = miner.Cave!;
        MiningClaim? bestClaim = null;
        var bestDistanceSquared = long.MaxValue;
        foreach (var neighbor in tile.Neighbors)
        {
            if (!cave.CanCreatureTraverseTile(miner, neighbor))
            {
                continue;
            }

            var approach = WorldPoint.FromGridPoint(neighbor.Coordinates);
            if (IsApproachAvailable(cave, miner, approach))
            {
                var distanceSquared = (approach - miner.Position).LengthSquared;
                if (bestClaim.HasValue &&
                    (distanceSquared > bestDistanceSquared ||
                     (distanceSquared == bestDistanceSquared &&
                      string.CompareOrdinal(neighbor.Key, bestClaim.Value.ApproachPoint.ToGridPoint().ToString()) >= 0)))
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestClaim = new MiningClaim(post.Id, tile.Key, approach, miner.Session.TickCount);
            }
        }

        return bestClaim;
    }

    private static bool IsApproachAvailable(Cave cave, Trilobite miner, WorldPoint approach)
    {
        return cave.CanCreatureOccupyWorldPosition(miner, approach);
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
