using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Runtime;

public sealed class SkillTreeUnlockSystemTests
{
    [Fact]
    public void CalculateUnlockCost_UsesTwentyPlusTwentyPerDepth()
    {
        var skillTree = CreateTree(out _, out var child, out var grandchild);

        Assert.Equal(20, SkillTreeUnlockSystem.CalculateUnlockCost(skillTree.Root!));
        Assert.Equal(40, SkillTreeUnlockSystem.CalculateUnlockCost(child));
        Assert.Equal(60, SkillTreeUnlockSystem.CalculateUnlockCost(grandchild));
    }

    [Fact]
    public void GetUnlockQuote_BlocksNodeWhenParentIsLocked()
    {
        var session = new GameSession();
        CreateTree(session, out _, out _, out var grandchild);
        DepositChitinstone(session, 100);

        var quote = SkillTreeUnlockSystem.GetUnlockQuote(session, grandchild);

        Assert.False(quote.CanUnlock);
        Assert.Equal(SkillTreeUnlockBlockReason.NoPathToNode, quote.BlockReason);
        Assert.Equal(100, quote.Available);
        Assert.Equal(60, quote.Cost);
    }

    [Fact]
    public void GetUnlockQuote_BlocksNodeWhenChitinstoneIsInsufficient()
    {
        var session = new GameSession();
        CreateTree(session, out var root, out var child, out _);
        Assert.True(root.TryUnlock(session));
        DepositChitinstone(session, 39);

        var quote = SkillTreeUnlockSystem.GetUnlockQuote(session, child);

        Assert.False(quote.CanUnlock);
        Assert.Equal(SkillTreeUnlockBlockReason.NotEnoughResources, quote.BlockReason);
        Assert.Equal(39, quote.Available);
        Assert.Equal(40, quote.Cost);
    }

    [Fact]
    public void GetUnlockQuote_MatchesStockpileTotalAcrossMultipleStorageBuildings()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(14, 8);
        CreateTree(session, out var root, out var child, out _);
        Assert.True(root.TryUnlock(session));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 0));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(5, 0)));
        Assert.Equal(7, post.Deposit(ResourceName.Chitinstone, 7));
        Assert.Equal(3, storage.Deposit(ResourceName.Chitinstone, 3));
        var stockpile = new ResourceStockpileSystem().Refresh(session);

        var quote = SkillTreeUnlockSystem.GetUnlockQuote(session, child);

        Assert.Equal(10, stockpile.GetAmount(ResourceName.Chitinstone));
        Assert.Equal(stockpile.GetAmount(ResourceName.Chitinstone), quote.Available);
    }

    [Fact]
    public void TryUnlock_WithdrawsChitinstoneAndAppliesResearch()
    {
        var session = new GameSession();
        CreateTree(session, out var root, out var child, out _);
        Assert.True(root.TryUnlock(session));
        DepositChitinstone(session, 45);

        var unlocked = SkillTreeUnlockSystem.TryUnlock(session, child, out var result);

        Assert.True(unlocked);
        Assert.True(result.Unlocked);
        Assert.Equal(SkillTreeUnlockBlockReason.None, result.BlockReason);
        Assert.True(child.IsUnlocked);
        Assert.Equal(5, ResourceStockpileSystem.GetStoredAmount(session, ResourceName.Chitinstone));
        Assert.Single(session.GlobalResearch.Descriptors);
        Assert.Equal("Trilobite.MoveSpeed", session.GlobalResearch.Descriptors[0].StatKey);
    }

    [Fact]
    public void TryUnlock_DoesNotWithdrawWhenChitinstoneIsInsufficient()
    {
        var session = new GameSession();
        CreateTree(session, out var root, out var child, out _);
        Assert.True(root.TryUnlock(session));
        DepositChitinstone(session, 39);

        var unlocked = SkillTreeUnlockSystem.TryUnlock(session, child, out var result);

        Assert.False(unlocked);
        Assert.Equal(SkillTreeUnlockBlockReason.NotEnoughResources, result.BlockReason);
        Assert.False(child.IsUnlocked);
        Assert.Equal(39, ResourceStockpileSystem.GetStoredAmount(session, ResourceName.Chitinstone));
    }

    [Fact]
    public void UnlockingParentMakesEveryChildBranchAvailable()
    {
        var session = new GameSession();
        var skillTree = session.SkillTree;
        var root = skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Root", "Root.")));
        var first = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("First", "First.")));
        var firstChild = skillTree.AddChild(first, skillTree.IntakeSkillNode(new SkillNode("First Child", "First child.")));
        var branchChild = skillTree.AddChild(first, skillTree.IntakeSkillNode(new SkillNode("Branch Child", "Branch child.")));
        Assert.True(root.TryUnlock(session));
        DepositChitinstone(session, 200);

        Assert.True(SkillTreeUnlockSystem.TryUnlock(session, first, out _));

        Assert.Equal(SkillTreeUnlockBlockReason.None, SkillTreeUnlockSystem.GetUnlockQuote(session, firstChild).BlockReason);
        Assert.Equal(SkillTreeUnlockBlockReason.None, SkillTreeUnlockSystem.GetUnlockQuote(session, branchChild).BlockReason);
    }

    private static SkillTree CreateTree(out TreeInstanceNode root, out TreeInstanceNode child, out TreeInstanceNode grandchild)
    {
        var session = new GameSession();
        return CreateTree(session, out root, out child, out grandchild);
    }

    private static SkillTree CreateTree(
        GameSession session,
        out TreeInstanceNode root,
        out TreeInstanceNode child,
        out TreeInstanceNode grandchild)
    {
        var skillTree = session.SkillTree;
        root = skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Root", "Root.")));
        child = skillTree.AddChild(
            root,
            skillTree.IntakeSkillNode(
                new SkillNode(
                    "Child",
                    "Child.",
                    [new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddFlat, 1d)])));
        grandchild = skillTree.AddChild(child, skillTree.IntakeSkillNode(new SkillNode("Grandchild", "Grandchild.")));
        return skillTree;
    }

    private static void DepositChitinstone(GameSession session, int amount)
    {
        var cave = session.Cave ?? new TriloGame.Game.Core.World.Cave(session);
        TestWorldFactory.ResetToRectangularMap(cave, 8, 8);
        var post = new MiningPost(session);
        Assert.True(cave.Build(post, new GridPoint(0, 0)));
        Assert.Equal(amount, post.Deposit(ResourceName.Chitinstone, amount));
    }
}
