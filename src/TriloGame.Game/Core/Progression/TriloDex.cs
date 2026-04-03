namespace TriloGame.Game.Core.Progression;

// TriloDex is the global catalog for every hard-coded feature tree in the game.
// The catalog is intentionally empty for now and will be filled as feature trees are authored.
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
        return new TriloDex(BuildGlobalFeatureTrees());
    }

    private static IEnumerable<FeatureTree> BuildGlobalFeatureTrees()
    {
        var myFeatureTrees = [];


        return myFeatureTrees;
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
