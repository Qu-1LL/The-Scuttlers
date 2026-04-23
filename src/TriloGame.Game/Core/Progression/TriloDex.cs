namespace TriloGame.Game.Core.Progression;

// TriloDex is the global catalog wrapper for hard-coded feature trees.
// The authored tree data lives in FeatureTreeData so lookup stays separate from content.
public sealed class TriloDex
{
    private static readonly Lazy<TriloDex> GlobalInstance = new(CreateGlobal);
    private readonly Dictionary<string, FeatureTree> _featureTreesByName;

    public TriloDex(IEnumerable<FeatureTree> featureTrees)
    {
        var orderedTrees = (featureTrees ?? throw new ArgumentNullException(nameof(featureTrees))).ToArray();
        FeatureTrees = orderedTrees;
        _featureTreesByName = BuildFeatureTreeLookup(orderedTrees);
    }

    public static TriloDex Global => GlobalInstance.Value;

    public static IReadOnlyList<FeatureTree> GlobalFeatureTrees => Global.FeatureTrees;

    public IReadOnlyList<FeatureTree> FeatureTrees { get; }

    public int Count => FeatureTrees.Count;

    public bool IsEmpty => Count == 0;

    public bool ContainsFeatureTree(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && _featureTreesByName.ContainsKey(name);
    }

    public FeatureTree? FindFeatureTree(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _featureTreesByName.GetValueOrDefault(name);
    }

    private static TriloDex CreateGlobal()
    {
        return new TriloDex(FeatureTreeData.BuildGlobalFeatureTrees());
    }

    private static Dictionary<string, FeatureTree> BuildFeatureTreeLookup(IEnumerable<FeatureTree> featureTrees)
    {
        var lookup = new Dictionary<string, FeatureTree>(StringComparer.Ordinal);
        foreach (var featureTree in featureTrees)
        {
            if (!lookup.TryAdd(featureTree.Name, featureTree))
            {
                throw new InvalidOperationException($"Duplicate feature tree name '{featureTree.Name}' in TriloDex.");
            }
        }

        return lookup;
    }
}
