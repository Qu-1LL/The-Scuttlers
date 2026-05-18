using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Systems;

public sealed class ResearchDraftSystem
{
    public ResearchDraftOffer? PendingDraft { get; private set; }

    public bool HasPendingDraft => PendingDraft is not null;

    // Clear any unplaced research offer between runs or resets.
    public void Reset()
    {
        PendingDraft = null;
    }

    // Generate one round-scoped research offer and keep it stable until placement resolves it.
    public ResearchDraftOffer? CreateDraft(GameSession session, RoundInfo round)
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

        PendingDraft = new ResearchDraftOffer(round.RoundNumber, seed, generation);
        return PendingDraft;
    }

    // Place one branch from the pending offer and clear the offer on success.
    public bool TryPlaceBranch(GameSession session, int branchIndex, GridPoint anchorLocation, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(session);

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
        if (!session.SkillTree.TryPlaceResearchBranch(branch, anchorLocation, out failureReason))
        {
            return false;
        }

        PendingDraft = null;
        return true;
    }
}

public sealed class ResearchDraftOffer
{
    // Capture the generated research options and the seed that produced them.
    public ResearchDraftOffer(
        int sourceRoundNumber,
        int seed,
        ResearchBranchGenerationResult generation)
    {
        SourceRoundNumber = sourceRoundNumber >= 0
            ? sourceRoundNumber
            : throw new ArgumentOutOfRangeException(nameof(sourceRoundNumber), "Round number cannot be negative.");
        Seed = seed;
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    public int SourceRoundNumber { get; }

    public int Seed { get; }

    public ResearchBranchGenerationResult Generation { get; }

    public IReadOnlyList<ResearchBranch> Branches => Generation.Branches;
}
