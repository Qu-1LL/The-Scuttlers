using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Progression;

public sealed class ResearchBranchGenerator
{
    private readonly Random _random;

    // Use the supplied RNG when tests need determinism, otherwise fall back to the shared helper.
    public ResearchBranchGenerator(Random? random = null)
    {
        _random = random ?? RandomUtil.Shared;
    }

    // Build a set of draft branches from the currently available progression candidates.
    public ResearchBranchGenerationResult Generate(
        SkillTree skillTree,
        int branchCount = 3,
        int nodesPerBranch = 4)
    {
        ArgumentNullException.ThrowIfNull(skillTree);

        if (branchCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(branchCount), "Branch count must be at least 1.");
        }

        if (nodesPerBranch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodesPerBranch), "Nodes per branch must be at least 1.");
        }

        var registry = BuildRegistry(skillTree.SourceDex);
        var allFeatureNames = CollectAllFeatureNames(skillTree.SourceDex);
        var existingKeys = CollectExistingKeys(skillTree);
        var unlockedKeys = CollectUnlockedKeys(skillTree);
        var availableCandidates = CollectAvailableCandidates(skillTree, registry, existingKeys, unlockedKeys);
        var availableNodeCount = availableCandidates.Count;
        var unlockedFeatureCounts = CountUnlockedFeatures(skillTree.SourceDex, unlockedKeys);
        var candidateScores = ScoreCandidates(availableCandidates, availableNodeCount, allFeatureNames, unlockedFeatureCounts, unlockedKeys);
        var weightedPool = BuildWeightedPool(candidateScores);

        var branches = new List<ResearchBranch>(branchCount);
        for (var branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            branches.Add(BuildBranch(nodesPerBranch, weightedPool, registry, existingKeys));
        }

        return new ResearchBranchGenerationResult(branches, candidateScores, availableNodeCount);
    }

    // Score each available template node so higher-value options appear more often in the pool.
    private IReadOnlyList<ResearchBranchCandidateScore> ScoreCandidates(
        IReadOnlyList<TemplateNodeEntry> availableCandidates,
        int availableNodeCount,
        IReadOnlyList<string> allFeatureNames,
        IReadOnlyDictionary<string, int> unlockedFeatureCounts,
        IReadOnlySet<TemplateNodeKey> unlockedKeys)
    {
        var scores = new List<ResearchBranchCandidateScore>(availableCandidates.Count);
        // Start from tier weight, then bias toward underrepresented features and unlocked chains.
        foreach (var candidate in availableCandidates)
        {
            var score = candidate.FeatureTree.Tier;
            score += CalculateFeatureBalanceAdjustment(candidate, allFeatureNames, unlockedFeatureCounts);
            if (score < 0)
            {
                score = 0;
            }

            if (candidate.ParentKey is TemplateNodeKey parentKey && unlockedKeys.Contains(parentKey))
            {
                score += availableNodeCount;
            }

            scores.Add(new ResearchBranchCandidateScore(
                candidate.FeatureTree.Name,
                candidate.TemplateNode.Name,
                candidate.FeatureTree.Tier,
                score));
        }

        return scores;
    }

    // Favor feature trees that currently lag behind the rest of the unlocked portfolio.
    private int CalculateFeatureBalanceAdjustment(
        TemplateNodeEntry candidate,
        IReadOnlyList<string> allFeatureNames,
        IReadOnlyDictionary<string, int> unlockedFeatureCounts)
    {
        var affectedTotal = 0;
        var unaffectedTotal = 0;
        var affectedCount = 0;
        var unaffectedCount = 0;

        // Compare the candidate's affected feature families against everything it does not touch.
        foreach (var featureName in allFeatureNames)
        {
            var count = unlockedFeatureCounts.GetValueOrDefault(featureName);
            if (candidate.FeatureTree.FeaturesAffected.Contains(featureName, StringComparer.Ordinal))
            {
                affectedTotal += count;
                affectedCount++;
            }
            else
            {
                unaffectedTotal += count;
                unaffectedCount++;
            }
        }

        var affectedAverage = affectedCount == 0 ? 0d : (double)affectedTotal / affectedCount;
        var unaffectedAverage = unaffectedCount == 0 ? 0d : (double)unaffectedTotal / unaffectedCount;
        return (int)Math.Floor(unaffectedAverage - affectedAverage);
    }

    // Assemble one research draft by repeatedly pulling legal nodes from the weighted pool.
    private ResearchBranch BuildBranch(
        int nodesPerBranch,
        IReadOnlyList<TemplateNodeKey> weightedPool,
        IReadOnlyDictionary<TemplateNodeKey, TemplateNodeEntry> registry,
        IReadOnlySet<TemplateNodeKey> existingKeys)
    {
        var branch = new ResearchBranch();
        var pool = weightedPool.ToList();

        // Keep drafting until the branch is full or no legal slot/candidate pair remains.
        while (branch.Count < nodesPerBranch)
        {
            var availableSlots = branch.GetAvailableSlots();
            if (pool.Count == 0 || availableSlots.Count == 0)
            {
                break;
            }

            Shuffle(pool);
            var selected = SelectNextNode(pool, branch, registry, existingKeys);
            if (selected is null)
            {
                break;
            }

            availableSlots = branch.GetAvailableSlots();
            if (availableSlots.Count == 0)
            {
                break;
            }

            var slot = availableSlots[_random.Next(availableSlots.Count)];
            var binaryNode = new BinarySkillNode(selected.TemplateNode, slot.Delta, selected.FeatureTree.Name);
            if (slot.Parent is null)
            {
                branch.SetRoot(binaryNode, slot.Delta);
            }
            else if (slot.IsLeftChild)
            {
                branch.AddLeftChild(slot.Parent, binaryNode);
            }
            else
            {
                branch.AddRightChild(slot.Parent, binaryNode);
            }

            EnqueueChildren(pool, selected, branch, existingKeys, registry);
        }

        return branch;
    }

    // Pull the next registry entry that is not already present in the live tree or this draft.
    private TemplateNodeEntry? SelectNextNode(
        List<TemplateNodeKey> pool,
        ResearchBranch branch,
        IReadOnlyDictionary<TemplateNodeKey, TemplateNodeEntry> registry,
        IReadOnlySet<TemplateNodeKey> existingKeys)
    {
        while (pool.Count > 0)
        {
            var key = pool[0];
            pool.RemoveAt(0);

            if (existingKeys.Contains(key) ||
                branch.ContainsSourceSkill(key.FeatureTreeName, key.SkillName) ||
                !registry.TryGetValue(key, out var entry))
            {
                continue;
            }

            return entry;
        }

        return null;
    }

    // Seed the pool with children of the selected node so the draft can continue along that tree.
    private void EnqueueChildren(
        List<TemplateNodeKey> pool,
        TemplateNodeEntry selected,
        ResearchBranch branch,
        IReadOnlySet<TemplateNodeKey> existingKeys,
        IReadOnlyDictionary<TemplateNodeKey, TemplateNodeEntry> registry)
    {
        // Weight children by tree tier so later-tier follow-ups remain visible in the pool.
        foreach (var child in selected.TemplateNode.Children)
        {
            var childKey = new TemplateNodeKey(selected.FeatureTree.Name, child.Name);
            if (existingKeys.Contains(childKey) ||
                branch.ContainsSourceSkill(childKey.FeatureTreeName, childKey.SkillName) ||
                !registry.TryGetValue(childKey, out var childEntry))
            {
                continue;
            }

            for (var copyIndex = 0; copyIndex < childEntry.FeatureTree.Tier; copyIndex++)
            {
                pool.Add(childKey);
            }
        }
    }

    // Expand scored candidates into a simple weighted draw pool.
    private IReadOnlyList<TemplateNodeKey> BuildWeightedPool(IReadOnlyList<ResearchBranchCandidateScore> candidateScores)
    {
        var pool = new List<TemplateNodeKey>();
        foreach (var candidate in candidateScores)
        {
            for (var copyIndex = 0; copyIndex < candidate.Points; copyIndex++)
            {
                pool.Add(new TemplateNodeKey(candidate.FeatureTreeName, candidate.SkillName));
            }
        }

        return pool;
    }

    // Collect every template node that is not already present and satisfies unlock gating.
    private IReadOnlyList<TemplateNodeEntry> CollectAvailableCandidates(
        SkillTree skillTree,
        IReadOnlyDictionary<TemplateNodeKey, TemplateNodeEntry> registry,
        IReadOnlySet<TemplateNodeKey> existingKeys,
        IReadOnlySet<TemplateNodeKey> unlockedKeys)
    {
        var hasCompletedTierOneTree = HasCompletedTree(skillTree.SourceDex, unlockedKeys, tier: 1);
        var hasCompletedTierTwoTree = HasCompletedTree(skillTree.SourceDex, unlockedKeys, tier: 2);
        var unlockedCount = unlockedKeys.Count;
        var available = new List<TemplateNodeEntry>();

        // Filter the registry down to nodes the current run is actually allowed to draft next.
        foreach (var entry in registry.Values)
        {
            if (existingKeys.Contains(entry.Key))
            {
                continue;
            }

            if (!IsAvailable(entry, hasCompletedTierOneTree, hasCompletedTierTwoTree, unlockedCount, unlockedKeys))
            {
                continue;
            }

            available.Add(entry);
        }

        return available;
    }

    // Gate root availability by tier progression, while child nodes follow unlocked parents.
    private bool IsAvailable(
        TemplateNodeEntry entry,
        bool hasCompletedTierOneTree,
        bool hasCompletedTierTwoTree,
        int unlockedCount,
        IReadOnlySet<TemplateNodeKey> unlockedKeys)
    {
        if (entry.ParentKey is not TemplateNodeKey parentKey)
        {
            return entry.FeatureTree.Tier switch
            {
                <= 1 => true,
                2 => hasCompletedTierOneTree || unlockedCount >= 20,
                3 => hasCompletedTierTwoTree || unlockedCount >= 40,
                _ => false
            };
        }

        return unlockedKeys.Contains(parentKey);
    }

    // Check whether an entire authored tree at the requested tier has been unlocked this run.
    private bool HasCompletedTree(TriloDex dex, IReadOnlySet<TemplateNodeKey> unlockedKeys, int tier)
    {
        foreach (var featureTree in dex.FeatureTrees)
        {
            if (featureTree.Tier != tier || featureTree.Root is null)
            {
                continue;
            }

            var isComplete = true;
            foreach (var node in featureTree.TraverseDepthFirst())
            {
                if (!unlockedKeys.Contains(new TemplateNodeKey(featureTree.Name, node.Name)))
                {
                    isComplete = false;
                    break;
                }
            }

            if (isComplete)
            {
                return true;
            }
        }

        return false;
    }

    // Count how many unlocked trees currently contribute to each feature family.
    private IReadOnlyDictionary<string, int> CountUnlockedFeatures(
        TriloDex dex,
        IReadOnlySet<TemplateNodeKey> unlockedKeys)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var featureName in CollectAllFeatureNames(dex))
        {
            counts[featureName] = 0;
        }

        // Count a feature family once per tree when that tree has at least one unlocked node.
        foreach (var featureTree in dex.FeatureTrees)
        {
            var treeHasUnlockedNode = false;
            foreach (var node in featureTree.TraverseDepthFirst())
            {
                if (unlockedKeys.Contains(new TemplateNodeKey(featureTree.Name, node.Name)))
                {
                    treeHasUnlockedNode = true;
                    break;
                }
            }

            if (!treeHasUnlockedNode)
            {
                continue;
            }

            foreach (var featureName in featureTree.FeaturesAffected)
            {
                counts[featureName] = counts.GetValueOrDefault(featureName) + 1;
            }
        }

        return counts;
    }

    // Snapshot every template skill already imported into the live skill tree.
    private IReadOnlySet<TemplateNodeKey> CollectExistingKeys(SkillTree skillTree)
    {
        var keys = new HashSet<TemplateNodeKey>();
        foreach (var node in skillTree.TraverseDepthFirst())
        {
            if (string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
            {
                continue;
            }

            keys.Add(new TemplateNodeKey(node.SourceFeatureTreeName, node.Name));
        }

        return keys;
    }

    // Snapshot only the imported template skills that have actually been unlocked so far.
    private IReadOnlySet<TemplateNodeKey> CollectUnlockedKeys(SkillTree skillTree)
    {
        var keys = new HashSet<TemplateNodeKey>();
        foreach (var node in skillTree.TraverseUnlocked())
        {
            if (string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
            {
                continue;
            }

            keys.Add(new TemplateNodeKey(node.SourceFeatureTreeName, node.Name));
        }

        return keys;
    }

    // Flatten the authored progression catalog into a lookup keyed by tree name and skill name.
    private IReadOnlyDictionary<TemplateNodeKey, TemplateNodeEntry> BuildRegistry(TriloDex dex)
    {
        var registry = new Dictionary<TemplateNodeKey, TemplateNodeEntry>();
        // Preserve parent links so availability checks can follow authored prerequisites.
        foreach (var featureTree in dex.FeatureTrees)
        {
            foreach (var node in featureTree.TraverseDepthFirst())
            {
                var key = new TemplateNodeKey(featureTree.Name, node.Name);
                TemplateNodeKey? parentKey = node.Parent is null
                    ? null
                    : new TemplateNodeKey(featureTree.Name, node.Parent.Name);
                registry.Add(key, new TemplateNodeEntry(key, featureTree, node, parentKey));
            }
        }

        return registry;
    }

    // Gather every distinct feature-family tag referenced by the authored progression catalog.
    private IReadOnlyList<string> CollectAllFeatureNames(TriloDex dex)
    {
        var featureNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var featureTree in dex.FeatureTrees)
        {
            foreach (var featureName in featureTree.FeaturesAffected)
            {
                featureNames.Add(featureName);
            }
        }

        return featureNames.ToArray();
    }

    // Shuffle the candidate pool in place before each branch-selection attempt.
    private void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private readonly record struct TemplateNodeKey(string FeatureTreeName, string SkillName);

    private sealed class TemplateNodeEntry
    {
        public TemplateNodeEntry(
            TemplateNodeKey key,
            FeatureTree featureTree,
            SkillNode templateNode,
            TemplateNodeKey? parentKey)
        {
            Key = key;
            FeatureTree = featureTree;
            TemplateNode = templateNode;
            ParentKey = parentKey;
        }

        public TemplateNodeKey Key { get; }

        public FeatureTree FeatureTree { get; }

        public SkillNode TemplateNode { get; }

        public TemplateNodeKey? ParentKey { get; }
    }
}

public sealed class ResearchBranchGenerationResult
{
    // Capture the generated branches and the scoring data that produced them.
    public ResearchBranchGenerationResult(
        IEnumerable<ResearchBranch> branches,
        IEnumerable<ResearchBranchCandidateScore> candidateScores,
        int availableNodeCount)
    {
        Branches = (branches ?? throw new ArgumentNullException(nameof(branches))).ToArray();
        CandidateScores = (candidateScores ?? throw new ArgumentNullException(nameof(candidateScores))).ToArray();
        AvailableNodeCount = availableNodeCount >= 0
            ? availableNodeCount
            : throw new ArgumentOutOfRangeException(nameof(availableNodeCount), "Available node count cannot be negative.");
    }

    public IReadOnlyList<ResearchBranch> Branches { get; }

    public IReadOnlyList<ResearchBranchCandidateScore> CandidateScores { get; }

    public int AvailableNodeCount { get; }
}

public sealed class ResearchBranchCandidateScore
{
    // Store the final draft weight for one candidate template node.
    public ResearchBranchCandidateScore(
        string featureTreeName,
        string skillName,
        int tier,
        int points)
    {
        FeatureTreeName = RequireText(featureTreeName, nameof(featureTreeName));
        SkillName = RequireText(skillName, nameof(skillName));
        Tier = tier >= 1
            ? tier
            : throw new ArgumentOutOfRangeException(nameof(tier), "Tier must be at least 1.");
        Points = points >= 0
            ? points
            : throw new ArgumentOutOfRangeException(nameof(points), "Points cannot be negative.");
    }

    public string FeatureTreeName { get; }

    public string SkillName { get; }

    public int Tier { get; }

    public int Points { get; }

    // Reject blank authored lookup keys before scoring data is stored.
    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value;
    }
}
