namespace TriloGame.Game.Core.Progression;

// ResearchBranch is a detached preview tree built from local TreeInstanceNode
// copies before it is grafted into the run's persistent skill tree.
public sealed class ResearchBranch
{
    public ResearchBranch(string? name = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Branch" : name.Trim();
    }

    public string Name { get; private set; }

    public TreeInstanceNode? Root { get; private set; }

    public IReadOnlyList<TreeInstanceNode> Nodes => Root is null ? [] : Root.TraverseDepthFirst().ToArray();

    public int Count => Root is null ? 0 : Root.TraverseDepthFirst().Count();

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        }

        Name = name.Trim();
    }

    public TreeInstanceNode SetRoot(TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (Root is not null)
        {
            throw new InvalidOperationException("The research branch already has a visible root.");
        }

        if (node.Parent is not null)
        {
            throw new InvalidOperationException("The research-branch root must be detached before assignment.");
        }

        Root = node;
        return node;
    }

    public TreeInstanceNode AddChild(TreeInstanceNode parent, TreeInstanceNode node, int? childIndex = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(node);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent tree instance node must belong to this research branch.");
        }

        if (Contains(node))
        {
            throw new InvalidOperationException("The selected tree instance node already belongs to this research branch.");
        }

        parent.AddChild(node, childIndex);
        return node;
    }

    public TreeInstanceNode? FindBySourceSkill(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return null;
        }

        foreach (var node in Nodes)
        {
            if (string.Equals(node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) &&
                string.Equals(node.Name, skillName, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public bool Contains(TreeInstanceNode node)
    {
        if (node is null || Root is null)
        {
            return false;
        }

        foreach (var current in Root.TraverseDepthFirst())
        {
            if (ReferenceEquals(current, node))
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsSourceSkill(string featureTreeName, string skillName)
    {
        return FindBySourceSkill(featureTreeName, skillName) is not null;
    }
}
