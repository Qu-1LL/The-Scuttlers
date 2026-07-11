using TriloGame.Game.Core.Progression;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
namespace TriloGame.Tests.Runtime;

public sealed class ResearchDraftSystemTests
{
    [Fact]
    public void CreateDraft_GeneratesThreeBranchesForANewRun()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var system = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);

        var draft = system.CreateDraft(session, round);

        Assert.NotNull(draft);
        Assert.True(system.HasPendingDraft);
        Assert.Equal(3, draft!.Branches.Count);
        Assert.All(draft.Branches, branch => Assert.True(branch.Count > 0));
    }

    [Fact]
    public void TryPlaceBranch_MergesTheSelectedBranchAndClearsThePendingDraft()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var system = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        var draft = Assert.IsType<ResearchDraftOffer>(system.CreateDraft(session, round));

        var placed = system.TryPlaceBranch(session, 0, session.SkillTree.Root!, out var failureReason);

        Assert.True(placed);
        Assert.Null(failureReason);
        Assert.False(system.HasPendingDraft);
        Assert.Equal(1 + draft.Branches[0].Count, session.SkillTree.Count);
    }

    [Fact]
    public void TryPlaceBranch_RejectsBoundaryCollisionsAndPreservesPendingDraft()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var system = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        var draft = Assert.IsType<ResearchDraftOffer>(system.CreateDraft(session, round));
        var branch = draft.Branches[0];
        var branchRoot = branch.Root ?? throw new InvalidOperationException("Generated branch must have a root.");
        TreeInstanceNode? rightmostChild = null;
        for (var index = 0; index < 32; index++)
        {
            rightmostChild = branch.AddChild(
                branchRoot,
                new TreeInstanceNode(new SkillNode($"Boundary Child {index}", "Boundary child."), "Boundary"));
        }

        for (var index = 0; index < 32; index++)
        {
            branch.AddChild(
                rightmostChild!,
                new TreeInstanceNode(new SkillNode($"Boundary Grandchild {index}", "Boundary grandchild."), "Boundary"));
        }

        var placed = system.TryPlaceBranch(session, 0, session.SkillTree.Root!, out var failureReason);

        Assert.False(placed);
        Assert.Equal(ResearchDraftPlacementValidator.BoundaryCollisionFailureReason, failureReason);
        Assert.True(system.HasPendingDraft);
        Assert.Same(draft, system.PendingDraft);
        Assert.Equal(1, session.SkillTree.Count);
    }

    [Fact]
    public void CreateDraft_CanGenerateAnotherOfferImmediatelyForInfiniteDraftMode()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var system = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);
        var firstDraft = Assert.IsType<ResearchDraftOffer>(system.CreateDraft(session, round, ResearchDraftSource.InfiniteDraft));

        Assert.True(system.TryPlaceBranch(session, 0, session.SkillTree.Root!, out var failureReason));
        Assert.Null(failureReason);

        var followUpDraft = system.CreateDraft(session, round, ResearchDraftSource.InfiniteDraft);

        Assert.NotNull(followUpDraft);
        Assert.Equal(ResearchDraftSource.InfiniteDraft, followUpDraft!.Source);
        Assert.NotEqual(firstDraft.Seed, followUpDraft.Seed);
        Assert.True(system.HasPendingDraft);
    }

    [Fact]
    public void CreateDraft_InfiniteDraftContinuesPastTheInitialRootPool()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var system = new ResearchDraftSystem();
        var round = new RoundInfo(0, 180000d, 180000d, 120000d, 30000d, 4, false);

        for (var draftIndex = 0; draftIndex < 8; draftIndex++)
        {
            var draft = system.CreateDraft(session, round, ResearchDraftSource.InfiniteDraft);

            Assert.NotNull(draft);
            Assert.Contains(draft!.Branches, branch => branch.Count > 0);
            var (branchIndex, anchor) = GetValidDraftPlacement(session.SkillTree, draft);
            Assert.True(system.TryPlaceBranch(session, branchIndex, anchor, out var failureReason));
            Assert.Null(failureReason);
        }
    }

    private static (int BranchIndex, TreeInstanceNode Anchor) GetValidDraftPlacement(
        SkillTree skillTree,
        ResearchDraftOffer draft)
    {
        var root = skillTree.Root ?? throw new InvalidOperationException("Test skill tree must have a root.");
        for (var branchIndex = 0; branchIndex < draft.Branches.Count; branchIndex++)
        {
            var branch = draft.Branches[branchIndex];
            if (branch.Count <= 0)
            {
                continue;
            }

            foreach (var anchor in skillTree.TraverseDepthFirst())
            {
                var validation = ResearchDraftPlacementValidator.Validate(skillTree, branch, anchor);
                if (validation.CanPlace)
                {
                    return (branchIndex, anchor);
                }
            }
        }

        throw new InvalidOperationException("Expected at least one generated branch to have a boundary-valid placement.");
    }
}
