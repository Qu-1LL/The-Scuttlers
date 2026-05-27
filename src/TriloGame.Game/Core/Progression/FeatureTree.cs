namespace TriloGame.Game.Core.Progression;

// FeatureTree is the tree-level container for a single upgrade line.
// It owns feature metadata plus tree traversal and lookup helpers.
public sealed class FeatureTree
{
    public FeatureTree(
        string name,
        string description,
        IEnumerable<string>? featuresAffected,
        int tier,
        IEnumerable<string>? prerequisiteTrees = null,
        SkillNode? root = null,
        FeatureTreeColor? displayColor = null)
    {
        Name = RequireText(name, nameof(name));
        Description = RequireText(description, nameof(description));
        FeaturesAffected = NormalizeFeatures(featuresAffected);
        Tier = tier >= 1
            ? tier
            : throw new ArgumentOutOfRangeException(nameof(tier), "Tier must be at least 1.");
        PrerequisiteTrees = NormalizeTreeNames(prerequisiteTrees);
        DisplayColor = displayColor;

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

    public FeatureTreeColor? DisplayColor { get; }

    public SkillNode? Root { get; private set; }

    public bool HasRoot => Root is not null;

    public int Count => TraverseDepthFirst().Count();

    public void SetRoot(SkillNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
        {
            throw new InvalidOperationException("The root skill node cannot already have a prerequisite.");
        }

        Root = root;
    }

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

    public bool Contains(SkillNode node)
    {
        return node is not null && TraverseDepthFirst().Any(current => ReferenceEquals(current, node));
    }

    public SkillNode? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return TraverseDepthFirst().FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }

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

    public IEnumerable<SkillNode> TraverseBreadthFirst()
    {
        if (Root is null)
        {
            yield break;
        }

        var queue = new Queue<SkillNode>();
        queue.Enqueue(Root);

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

    private static IReadOnlyList<string> NormalizeFeatures(IEnumerable<string>? featuresAffected)
    {
        return featuresAffected?
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Select(feature => feature.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private static IReadOnlyList<string> NormalizeTreeNames(IEnumerable<string>? treeNames)
    {
        return treeNames?
            .Where(treeName => !string.IsNullOrWhiteSpace(treeName))
            .Select(treeName => treeName.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value;
    }
}
