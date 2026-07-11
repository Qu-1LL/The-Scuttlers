using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

internal static class SkillTreeUnlockSystem
{
    public static ResourceCategory UnlockResourceCategory => ResourceCategory.Rock;

    public static string UnlockResourceType => UnlockResourceCategory.ToString();

    public static int CalculateUnlockCost(TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return 20 + (20 * Math.Max(0, node.Depth));
    }

    public static SkillTreeUnlockQuote GetUnlockQuote(GameSession session, TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        var cost = CalculateUnlockCost(node);
        var available = ResourceStockpileSystem.GetStoredAmount(session, UnlockResourceCategory);
        var reason = GetBlockReason(session, node, available, cost);
        return new SkillTreeUnlockQuote(
            UnlockResourceType,
            available,
            cost,
            reason == SkillTreeUnlockBlockReason.None,
            reason);
    }

    public static bool TryUnlock(GameSession session, TreeInstanceNode node, out SkillTreeUnlockResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        var quote = GetUnlockQuote(session, node);
        if (!quote.CanUnlock)
        {
            result = new SkillTreeUnlockResult(false, quote.BlockReason, quote.ResourceType, quote.Available, quote.Cost);
            return false;
        }

        if (!ResourceStockpileSystem.TryWithdrawStoredResource(session, UnlockResourceCategory, quote.Cost))
        {
            var available = ResourceStockpileSystem.GetStoredAmount(session, UnlockResourceCategory);
            result = new SkillTreeUnlockResult(
                false,
                SkillTreeUnlockBlockReason.NotEnoughResources,
                quote.ResourceType,
                available,
                quote.Cost);
            return false;
        }

        if (!node.TryUnlock(session))
        {
            result = new SkillTreeUnlockResult(
                false,
                node.IsUnlocked ? SkillTreeUnlockBlockReason.AlreadyUnlocked : SkillTreeUnlockBlockReason.NoPathToNode,
                quote.ResourceType,
                quote.Available,
                quote.Cost);
            return false;
        }

        result = new SkillTreeUnlockResult(true, SkillTreeUnlockBlockReason.None, quote.ResourceType, quote.Available, quote.Cost);
        return true;
    }

    private static SkillTreeUnlockBlockReason GetBlockReason(
        GameSession session,
        TreeInstanceNode node,
        int available,
        int cost)
    {
        if (!session.SkillTree.Contains(node))
        {
            return SkillTreeUnlockBlockReason.NotInTree;
        }

        if (node.IsUnlocked)
        {
            return SkillTreeUnlockBlockReason.AlreadyUnlocked;
        }

        if (!node.CanUnlock())
        {
            return SkillTreeUnlockBlockReason.NoPathToNode;
        }

        return available >= cost
            ? SkillTreeUnlockBlockReason.None
            : SkillTreeUnlockBlockReason.NotEnoughResources;
    }
}

internal enum SkillTreeUnlockBlockReason
{
    None,
    AlreadyUnlocked,
    NoPathToNode,
    NotEnoughResources,
    NotInTree
}

internal readonly record struct SkillTreeUnlockQuote(
    string ResourceType,
    int Available,
    int Cost,
    bool CanUnlock,
    SkillTreeUnlockBlockReason BlockReason);

internal readonly record struct SkillTreeUnlockResult(
    bool Unlocked,
    SkillTreeUnlockBlockReason BlockReason,
    string ResourceType,
    int Available,
    int Cost);
