using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

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

        var placed = system.TryPlaceBranch(session, 0, GridPoint.Zero, out var failureReason);

        Assert.True(placed);
        Assert.Null(failureReason);
        Assert.False(system.HasPendingDraft);
        Assert.Equal(1 + draft.Branches[0].Count, session.SkillTree.Count);
    }
}
