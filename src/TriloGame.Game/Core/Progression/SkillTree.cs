namespace TriloGame.Game.Core.Progression;

// SkillTree is the per-game progression state assembled from local
// TreeInstanceNode copies of authored feature-tree SkillNodes.
public sealed class SkillTree
{
    public SkillTree(TriloDex? sourceDex = null)
    {
        SourceDex = sourceDex ?? TriloDex.Global;
    }

    public TriloDex SourceDex { get; }

    public TreeInstanceNode? Root { get; private set; }

    public bool HasRoot => Root is not null;

    public int UnlockedCount
    {
        get
        {
            var count = 0;
            foreach (var _ in TraverseUnlocked())
            {
                count++;
            }

            return count;
        }
    }

    public int Count
    {
        get
        {
            var count = 0;
            foreach (var _ in TraverseDepthFirst())
            {
                count++;
            }

            return count;
        }
    }

    public bool IsEmpty => Root is null;

    public TreeInstanceNode IntakeSkillNode(SkillNode node, string? sourceFeatureTreeName = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new TreeInstanceNode(node, sourceFeatureTreeName);
    }

    public TreeInstanceNode SetRoot(TreeInstanceNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
        {
            throw new InvalidOperationException("A root skill node cannot already have a prerequisite.");
        }

        if (Root is not null && !ReferenceEquals(Root, root))
        {
            throw new InvalidOperationException("The skill tree already has a root.");
        }

        EnsureNodeDoesNotAlreadyBelongToTree(root);
        Root = root;
        return root;
    }

    public TreeInstanceNode SetRoot(SkillNode node, string? sourceFeatureTreeName = null)
    {
        return SetRoot(IntakeSkillNode(node, sourceFeatureTreeName));
    }

    public TreeInstanceNode AddChild(TreeInstanceNode parent, TreeInstanceNode child, int? childIndex = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent skill node must belong to this skill tree.");
        }

        EnsureNodeDoesNotAlreadyBelongToTree(child);
        parent.AddChild(child, childIndex);
        return child;
    }

    public TreeInstanceNode? ImportRoot(string featureTreeName, string skillName)
    {
        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : SetRoot(importedNode);
    }

    public TreeInstanceNode? ImportChild(TreeInstanceNode parent, string featureTreeName, string skillName, int? childIndex = null)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : AddChild(parent, importedNode, childIndex);
    }

    public bool RemoveSubtree(TreeInstanceNode node)
    {
        if (node is null)
        {
            return false;
        }

        if (ReferenceEquals(Root, node))
        {
            Root = null;
            return true;
        }

        if (!Contains(node))
        {
            return false;
        }

        return node.Parent?.RemoveChild(node) ?? false;
    }

    public bool Contains(TreeInstanceNode node)
    {
        if (node is null)
        {
            return false;
        }

        foreach (var current in TraverseDepthFirst())
        {
            if (ReferenceEquals(current, node))
            {
                return true;
            }
        }

        return false;
    }

    public TreeInstanceNode? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var node in TraverseDepthFirst())
        {
            if (string.Equals(node.Name, name, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public TreeInstanceNode? FindBySourceSkill(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return null;
        }

        foreach (var node in TraverseDepthFirst())
        {
            if (string.Equals(node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) &&
                string.Equals(node.Name, skillName, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public bool ContainsSourceSkill(string featureTreeName, string skillName)
    {
        return FindBySourceSkill(featureTreeName, skillName) is not null;
    }

    public string? GetSourceFeatureTreeName(TreeInstanceNode node)
    {
        return node?.SourceFeatureTreeName;
    }

    public IEnumerable<TreeInstanceNode> GetNodesFromFeatureTree(string featureTreeName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName))
        {
            yield break;
        }

        foreach (var node in TraverseDepthFirst())
        {
            if (string.Equals(node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal))
            {
                yield return node;
            }
        }
    }

    public IEnumerable<TreeInstanceNode> TraverseDepthFirst()
    {
        if (Root is null)
        {
            yield break;
        }

        foreach (var node in Root.TraverseDepthFirst())
        {
            yield return node;
        }
    }

    public IEnumerable<TreeInstanceNode> TraverseUnlocked()
    {
        foreach (var node in TraverseDepthFirst())
        {
            if (node.IsUnlocked)
            {
                yield return node;
            }
        }
    }

    public ResearchBranchGenerationResult GenerateResearchBranches(
        Random? random = null,
        int branchCount = 3,
        int nodesPerBranch = 4)
    {
        return new ResearchBranchGenerator(random).Generate(this, branchCount, nodesPerBranch);
    }

    public bool CanPlaceResearchBranch(ResearchBranch branch, TreeInstanceNode anchorNode, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(anchorNode);

        if (branch.Root is null)
        {
            failureReason = "The selected research branch has no visible root.";
            return false;
        }

        if (!Contains(anchorNode))
        {
            failureReason = "Drop the branch on the root anchor or an existing skill node.";
            return false;
        }

        if (IsCoreRoot(anchorNode) && anchorNode.ChildCount >= 1)
        {
            failureReason = "The core node can only support one direct research branch.";
            return false;
        }

        foreach (var branchNode in branch.Root.TraverseDepthFirst())
        {
            if (Contains(branchNode))
            {
                failureReason = "The selected research branch has already been placed.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(branchNode.SourceFeatureTreeName) &&
                ContainsSourceSkill(branchNode.SourceFeatureTreeName, branchNode.Name))
            {
                failureReason = "That research branch overlaps skills already in the colony tree.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    public bool TryPlaceResearchBranch(ResearchBranch branch, TreeInstanceNode anchorNode, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(anchorNode);

        if (!CanPlaceResearchBranch(branch, anchorNode, out failureReason))
        {
            return false;
        }

        AddChild(anchorNode, branch.Root!);
        failureReason = null;
        return true;
    }

    private TreeInstanceNode? CreateImportedNode(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return null;
        }

        var featureTree = SourceDex.FindFeatureTree(featureTreeName);
        if (featureTree is null)
        {
            return null;
        }

        var templateNode = featureTree.FindByName(skillName);
        return templateNode is null ? null : IntakeSkillNode(templateNode, featureTree.Name);
    }

    private void EnsureNodeDoesNotAlreadyBelongToTree(TreeInstanceNode node)
    {
        if (Contains(node))
        {
            throw new InvalidOperationException("The skill node already belongs to this skill tree.");
        }
    }

    private static bool IsCoreRoot(TreeInstanceNode node)
    {
        return node.IsRoot && string.IsNullOrWhiteSpace(node.SourceFeatureTreeName);
    }
}
