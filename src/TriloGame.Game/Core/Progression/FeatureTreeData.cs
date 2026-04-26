namespace TriloGame.Game.Core.Progression;

// FeatureTreeData is the authored source of truth for the curated hard-coded feature trees.
internal static class FeatureTreeData
{
    public static IReadOnlyList<FeatureTree> BuildGlobalFeatureTrees()
    {
        return
        [
            CreateSpineFanTree("B1", "Building curated tier 1 feature tree.", ["building"], 1),
            CreateDualPathTree("B2", "Building curated tier 1 feature tree.", ["building"], 1),
            CreateSplitLadderTree("B3", "Building curated tier 1 feature tree.", ["building"], 1),

            CreateSpineFanTree("C1", "Combat curated tier 1 feature tree.", ["combat"], 1),
            CreateDualPathTree("C2", "Combat curated tier 1 feature tree.", ["combat"], 1),
            CreateSplitLadderTree("C3", "Combat curated tier 1 feature tree.", ["combat"], 1),

            CreateSpineFanTree("F1", "Farming curated tier 1 feature tree.", ["farming"], 1),
            CreateDualPathTree("F2", "Farming curated tier 1 feature tree.", ["farming"], 1),
            CreateSplitLadderTree("F3", "Farming curated tier 1 feature tree.", ["farming"], 1),

            CreateSpineFanTree("M1", "Mining curated tier 1 feature tree.", ["mining"], 1),
            CreateDualPathTree("M2", "Mining curated tier 1 feature tree.", ["mining"], 1),
            CreateSplitLadderTree("M3", "Mining curated tier 1 feature tree.", ["mining"], 1),

            CreateSpineFanTree("BC1", "Building and combat curated tier 2 feature tree.", ["building", "combat"], 2, ["B1", "C1"]),
            CreateDualPathTree("BF1", "Building and farming curated tier 2 feature tree.", ["building", "farming"], 2, ["B1", "F1"]),
            CreateSplitLadderTree("BM1", "Building and mining curated tier 2 feature tree.", ["building", "mining"], 2, ["B1", "M1"]),
            CreateSpineFanTree("CF1", "Combat and farming curated tier 2 feature tree.", ["combat", "farming"], 2, ["C1", "F1"]),
            CreateDualPathTree("CM1", "Combat and mining curated tier 2 feature tree.", ["combat", "mining"], 2, ["C1", "M1"]),

            CreateTierThreeCrownTree("BCF1", "Building, combat, and farming curated tier 3 feature tree.", ["building", "combat", "farming"], 3, ["BC1", "BF1", "CF1"])
        ];
    }

    private static FeatureTree CreateSpineFanTree(
        string name,
        string description,
        IReadOnlyList<string> featuresAffected,
        int tier,
        IReadOnlyList<string>? prerequisiteTrees = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees);
        var nodes = CreateNodes(name, ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l"]);

        tree.SetRoot(nodes["a"]);
        AddEdges(
            tree,
            nodes,
            ("a", "b"),
            ("b", "c"),
            ("c", "d"),
            ("d", "e"),
            ("d", "f"),
            ("d", "g"),
            ("d", "h"),
            ("e", "i"),
            ("f", "j"),
            ("g", "k"),
            ("h", "l"));

        return tree;
    }

    private static FeatureTree CreateDualPathTree(
        string name,
        string description,
        IReadOnlyList<string> featuresAffected,
        int tier,
        IReadOnlyList<string>? prerequisiteTrees = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees);
        var nodes = CreateNodes(name, ["a", "b", "c", "d", "e", "f", "v", "w", "x", "y", "z", "i", "j", "k", "l"]);

        tree.SetRoot(nodes["a"]);
        AddEdges(
            tree,
            nodes,
            ("a", "b"),
            ("a", "v"),
            ("b", "c"),
            ("v", "w"),
            ("c", "d"),
            ("w", "x"),
            ("d", "e"),
            ("d", "f"),
            ("x", "y"),
            ("x", "z"),
            ("e", "i"),
            ("f", "j"),
            ("y", "k"),
            ("z", "l"));

        return tree;
    }

    private static FeatureTree CreateSplitLadderTree(
        string name,
        string description,
        IReadOnlyList<string> featuresAffected,
        int tier,
        IReadOnlyList<string>? prerequisiteTrees = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees);
        var nodes = CreateNodes(name, ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l"]);

        tree.SetRoot(nodes["a"]);
        AddEdges(
            tree,
            nodes,
            ("a", "b"),
            ("b", "c"),
            ("b", "f"),
            ("c", "d"),
            ("f", "g"),
            ("d", "e"),
            ("g", "h"),
            ("e", "i"),
            ("i", "j"),
            ("h", "k"),
            ("k", "l"));

        return tree;
    }

    private static FeatureTree CreateTierThreeCrownTree(
        string name,
        string description,
        IReadOnlyList<string> featuresAffected,
        int tier,
        IReadOnlyList<string>? prerequisiteTrees = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees);
        var nodes = CreateNodes(name, ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n"]);

        tree.SetRoot(nodes["a"]);
        AddEdges(
            tree,
            nodes,
            ("a", "b"),
            ("b", "c"),
            ("b", "g"),
            ("c", "d"),
            ("d", "e"),
            ("d", "f"),
            ("g", "h"),
            ("h", "i"),
            ("e", "j"),
            ("f", "k"),
            ("i", "l"),
            ("i", "m"),
            ("m", "n"));

        return tree;
    }

    private static FeatureTree CreateTree(
        string name,
        string description,
        IReadOnlyList<string> featuresAffected,
        int tier,
        IReadOnlyList<string>? prerequisiteTrees)
    {
        return new FeatureTree(name, description, featuresAffected, tier, prerequisiteTrees ?? []);
    }

    private static Dictionary<string, SkillNode> CreateNodes(string treeName, IEnumerable<string> suffixes)
    {
        var nodes = new Dictionary<string, SkillNode>(StringComparer.Ordinal);
        foreach (var suffix in suffixes)
        {
            nodes[suffix] = new SkillNode($"{treeName}-{suffix}", $"{treeName} node {suffix}.");
        }

        return nodes;
    }

    private static void AddEdges(
        FeatureTree tree,
        IReadOnlyDictionary<string, SkillNode> nodes,
        params (string ParentSuffix, string ChildSuffix)[] edges)
    {
        foreach (var (parentSuffix, childSuffix) in edges)
        {
            tree.AddChild(nodes[parentSuffix], nodes[childSuffix]);
        }
    }
}
