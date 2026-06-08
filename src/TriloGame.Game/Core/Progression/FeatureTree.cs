namespace TriloGame.Game.Core.Progression;

// FeatureTree is the tree-level container for a single upgrade line.
// It owns feature metadata plus tree traversal and lookup helpers.
public sealed class FeatureTree
{
    // Capture the authored tree metadata and optionally seed the root node.
    public FeatureTree(
        string name,
        string description,
        IEnumerable<string>? featuresAffected,
        int tier,
        IEnumerable<string>? prerequisiteTrees = null,
        SkillNode? root = null)
    {
        Name = RequireText(name, nameof(name));
        Description = RequireText(description, nameof(description));
        FeaturesAffected = NormalizeFeatures(featuresAffected);
        Tier = tier >= 1
            ? tier
            : throw new ArgumentOutOfRangeException(nameof(tier), "Tier must be at least 1.");
        PrerequisiteTrees = NormalizeTreeNames(prerequisiteTrees);

        if (root is not null)
        {
            SetRoot(root);
        }
    }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<string> FeaturesAffected { get; }

    public int Tier { get; }

    public IReadOnlyList<string> PrerequisiteTrees { get; }

    public bool HasPrerequisites => PrerequisiteTrees.Count > 0;

    public SkillNode? Root { get; private set; }

    public bool HasRoot => Root is not null;

    public int Count => TraverseDepthFirst().Count();

    // Install the tree root after verifying it is still detached from other trees.
    public void SetRoot(SkillNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
        {
            throw new InvalidOperationException("The root skill node cannot already have a prerequisite.");
        }

        Root = root;
    }

    // Attach a child node only when the proposed parent already belongs to this tree.
    public void AddChild(SkillNode parent, SkillNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent skill node must belong to this feature tree.");
        }

        parent.AddChild(child);
    }

    // Remove either the entire root or a descendant subtree from its current parent.
    public bool RemoveSubtree(SkillNode node)
    {
        if (node is null || Root is null)
        {
            return false;
        }

        if (ReferenceEquals(node, Root))
        {
            Root = null;
            return true;
        }

        return node.Parent?.RemoveChild(node) ?? false;
    }

    // Check membership by walking the currently connected node graph.
    public bool Contains(SkillNode node)
    {
        return node is not null && TraverseDepthFirst().Any(current => ReferenceEquals(current, node));
    }

    // Resolve an authored skill node name using ordinal matching.
    public SkillNode? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return TraverseDepthFirst().FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }

    // Yield nodes in parent-before-children order for deterministic scans.
    public IEnumerable<SkillNode> TraverseDepthFirst()
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

    // Yield nodes level by level for preview and lookup flows that care about breadth.
    public IEnumerable<SkillNode> TraverseBreadthFirst()
    {
        if (Root is null)
        {
            yield break;
        }

        var queue = new Queue<SkillNode>();
        queue.Enqueue(Root);

        // Walk outward from the root one breadth layer at a time.
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    // Normalize authored feature tags into a distinct ordinal list.
    private static IReadOnlyList<string> NormalizeFeatures(IEnumerable<string>? featuresAffected)
    {
        return featuresAffected?
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Select(feature => feature.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    // Normalize prerequisite tree names so authored lookups stay stable.
    private static IReadOnlyList<string> NormalizeTreeNames(IEnumerable<string>? treeNames)
    {
        return treeNames?
            .Where(treeName => !string.IsNullOrWhiteSpace(treeName))
            .Select(treeName => treeName.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    // Reject blank authored text fields before they enter progression data.
    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value;
    }
}
