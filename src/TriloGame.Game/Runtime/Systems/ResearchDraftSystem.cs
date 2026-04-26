using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
namespace TriloGame.Game.Runtime.Systems;

public enum ResearchDraftSource
{
    RoundReward,
    InfiniteDraft
}

public sealed class ResearchDraftSystem
{
    public ResearchDraftOffer? PendingDraft { get; private set; }

    public bool HasPendingDraft => PendingDraft is not null;

    public void Reset()
    {
        PendingDraft = null;
    }

    public ResearchDraftOffer? CreateDraft(
        GameSession session,
        RoundInfo round,
        ResearchDraftSource source = ResearchDraftSource.RoundReward)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (PendingDraft is not null)
        {
            return PendingDraft;
        }

        var seed = HashCode.Combine(
            round.RoundNumber,
            session.TickCount,
            session.SkillTree.Count,
            session.SkillTree.UnlockedCount);
        var generation = session.SkillTree.GenerateResearchBranches(new Random(seed));
        if (generation.AvailableNodeCount <= 0 || generation.Branches.All(branch => branch.Count == 0))
        {
            return null;
        }

        PendingDraft = new ResearchDraftOffer(round.RoundNumber, source, seed, generation);
        return PendingDraft;
    }

    public bool TryPlaceBranch(GameSession session, int branchIndex, TreeInstanceNode anchorNode, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(anchorNode);

        if (PendingDraft is null)
        {
            failureReason = "No research branches are waiting to be placed.";
            return false;
        }

        if (branchIndex < 0 || branchIndex >= PendingDraft.Branches.Count)
        {
            failureReason = "The selected research branch is no longer available.";
            return false;
        }

        var branch = PendingDraft.Branches[branchIndex];
        if (!session.SkillTree.TryPlaceResearchBranch(branch, anchorNode, out failureReason))
        {
            return false;
        }

        PendingDraft = null;
        return true;
    }
}

public sealed class ResearchDraftOffer
{
    public ResearchDraftOffer(
        int sourceRoundNumber,
        ResearchDraftSource source,
        int seed,
        ResearchBranchGenerationResult generation)
    {
        SourceRoundNumber = sourceRoundNumber >= 0
            ? sourceRoundNumber
            : throw new ArgumentOutOfRangeException(nameof(sourceRoundNumber), "Round number cannot be negative.");
        Source = source;
        Seed = seed;
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    public int SourceRoundNumber { get; }

    public ResearchDraftSource Source { get; }

    public int Seed { get; }

    public ResearchBranchGenerationResult Generation { get; }

    public IReadOnlyList<ResearchBranch> Branches => Generation.Branches;
}
