namespace TriloGame.Game.Core.Progression;

// SkillTree is the per-game progression state.
// It is a binary tree built from local BinarySkillNode copies of feature-tree SkillNodes.
public sealed class SkillTree
{
    public SkillTree(TriloDex? sourceDex = null)
    {
        SourceDex = sourceDex ?? TriloDex.Global;
    }

    public TriloDex SourceDex { get; }

    public BinarySkillNode? Root { get; private set; }

    public bool HasRoot => Root is not null;

    public int Count => TraverseDepthFirst().Count();

    public bool IsEmpty => Root is null;

    public BinarySkillNode IntakeSkillNode(SkillNode node, string? sourceFeatureTreeName = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new BinarySkillNode(node, sourceFeatureTreeName);
    }

    public BinarySkillNode SetRoot(BinarySkillNode root)
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

    public BinarySkillNode SetRoot(SkillNode node, string? sourceFeatureTreeName = null)
    {
        return SetRoot(IntakeSkillNode(node, sourceFeatureTreeName));
    }

    public BinarySkillNode AddLeftChild(BinarySkillNode parent, BinarySkillNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent skill node must belong to this skill tree.");
        }

        EnsureNodeDoesNotAlreadyBelongToTree(child);
        parent.SetLeft(child);
        return child;
    }

    public BinarySkillNode AddRightChild(BinarySkillNode parent, BinarySkillNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent skill node must belong to this skill tree.");
        }

        EnsureNodeDoesNotAlreadyBelongToTree(child);
        parent.SetRight(child);
        return child;
    }

    public BinarySkillNode? ImportRoot(string featureTreeName, string skillName)
    {
        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : SetRoot(importedNode);
    }

    public BinarySkillNode? ImportLeftChild(BinarySkillNode parent, string featureTreeName, string skillName)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : AddLeftChild(parent, importedNode);
    }

    public BinarySkillNode? ImportRightChild(BinarySkillNode parent, string featureTreeName, string skillName)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : AddRightChild(parent, importedNode);
    }

    public bool RemoveSubtree(BinarySkillNode node)
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

        var parent = node.Parent;
        if (parent is null)
        {
            return false;
        }

        if (ReferenceEquals(parent.Left, node))
        {
            return parent.RemoveLeft() is not null;
        }

        if (ReferenceEquals(parent.Right, node))
        {
            return parent.RemoveRight() is not null;
        }

        return false;
    }

    public bool Contains(BinarySkillNode node)
    {
        return node is not null && TraverseDepthFirst().Any(current => ReferenceEquals(current, node));
    }

    public BinarySkillNode? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return TraverseDepthFirst().FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }

    public string? GetSourceFeatureTreeName(BinarySkillNode node)
    {
        return node?.SourceFeatureTreeName;
    }

    public IEnumerable<BinarySkillNode> GetNodesFromFeatureTree(string featureTreeName)
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

    public IEnumerable<BinarySkillNode> TraverseDepthFirst()
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

    private BinarySkillNode? CreateImportedNode(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return null;
        }

        var featureTree = SourceDex.FindFeatureTree(featureTreeName);
        var templateNode = featureTree?.FindByName(skillName);
        return templateNode is null ? null : IntakeSkillNode(templateNode, featureTree.Name);
    }

    private void EnsureNodeDoesNotAlreadyBelongToTree(BinarySkillNode node)
    {
        if (Contains(node))
        {
            throw new InvalidOperationException("The skill node already belongs to this skill tree.");
        }
    }
}
