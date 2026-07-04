namespace TriloGame.Game.Core.Progression;

// FeatureTreeData is the authored source of truth for the curated hard-coded feature trees.
internal static class FeatureTreeData
{
    private static readonly FeatureTreeColor[] GlobalTreeColors =
    [
        new(0x26, 0x46, 0x53),
        new(0x2a, 0x9d, 0x8f),
        new(0x9a, 0x03, 0x1e),
        new(0x5f, 0x0f, 0x40),
        new(0x81, 0xb2, 0x9a),
        new(0x94, 0xd2, 0xbd),
        new(0x23, 0x19, 0x42),
        new(0xfc, 0xa3, 0x11),
        new(0x6d, 0x59, 0x7a),
        new(0x89, 0x00, 0xf2),
        new(0x4e, 0xcd, 0xc4),
        new(0x8e, 0xa6, 0x04),
        new(0xf1, 0x15, 0x15),
        new(0xb2, 0x1e, 0x4b),
        new(0x7e, 0x76, 0x6d),
        new(0x94, 0x56, 0x00),
        new(0xaf, 0xfc, 0x41),
        new(0xb2, 0xff, 0x9e)
    ];

    public static IReadOnlyList<FeatureTree> BuildGlobalFeatureTrees()
    {
        var treeIndex = 0;
        FeatureTree[] trees =
        [
            CreateSpineFanTree("B1", "Building curated tier 1 feature tree.", ["building"], 1, displayColor: NextColor(ref treeIndex)),
            CreateDualPathTree("B2", "Building curated tier 1 feature tree.", ["building"], 1, displayColor: NextColor(ref treeIndex)),
            CreateSplitLadderTree("B3", "Building curated tier 1 feature tree.", ["building"], 1, displayColor: NextColor(ref treeIndex)),

            CreateSpineFanTree("C1", "Combat curated tier 1 feature tree.", ["combat"], 1, displayColor: NextColor(ref treeIndex)),
            CreateDualPathTree("C2", "Combat curated tier 1 feature tree.", ["combat"], 1, displayColor: NextColor(ref treeIndex)),
            CreateSplitLadderTree("C3", "Combat curated tier 1 feature tree.", ["combat"], 1, displayColor: NextColor(ref treeIndex)),

            CreateSpineFanTree("F1", "Farming curated tier 1 feature tree.", ["farming"], 1, displayColor: NextColor(ref treeIndex)),
            CreateDualPathTree("F2", "Farming curated tier 1 feature tree.", ["farming"], 1, displayColor: NextColor(ref treeIndex)),
            CreateSplitLadderTree("F3", "Farming curated tier 1 feature tree.", ["farming"], 1, displayColor: NextColor(ref treeIndex)),

            CreateSpineFanTree("M1", "Mining curated tier 1 feature tree.", ["mining"], 1, displayColor: NextColor(ref treeIndex)),
            CreateDualPathTree("M2", "Mining curated tier 1 feature tree.", ["mining"], 1, displayColor: NextColor(ref treeIndex)),
            CreateSplitLadderTree("M3", "Mining curated tier 1 feature tree.", ["mining"], 1, displayColor: NextColor(ref treeIndex)),

            CreateSpineFanTree("BC1", "Building and combat curated tier 2 feature tree.", ["building", "combat"], 2, ["B1", "C1"], NextColor(ref treeIndex)),
            CreateDualPathTree("BF1", "Building and farming curated tier 2 feature tree.", ["building", "farming"], 2, ["B1", "F1"], NextColor(ref treeIndex)),
            CreateSplitLadderTree("BM1", "Building and mining curated tier 2 feature tree.", ["building", "mining"], 2, ["B1", "M1"], NextColor(ref treeIndex)),
            CreateSpineFanTree("CF1", "Combat and farming curated tier 2 feature tree.", ["combat", "farming"], 2, ["C1", "F1"], NextColor(ref treeIndex)),
            CreateDualPathTree("CM1", "Combat and mining curated tier 2 feature tree.", ["combat", "mining"], 2, ["C1", "M1"], NextColor(ref treeIndex)),

            CreateTierThreeCrownTree("BCF1", "Building, combat, and farming curated tier 3 feature tree.", ["building", "combat", "farming"], 3, ["BC1", "BF1", "CF1"], NextColor(ref treeIndex))
        ];

        if (treeIndex != GlobalTreeColors.Length)
        {
            throw new InvalidOperationException("Every global feature tree must consume exactly one display color.");
        }

        return trees;
    }

    private static FeatureTree CreateSpineFanTree(
        string name,
        string description,
        IReadOnlyList<string> featuresAffected,
        int tier,
        IReadOnlyList<string>? prerequisiteTrees = null,
        FeatureTreeColor? displayColor = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees, displayColor);
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
        IReadOnlyList<string>? prerequisiteTrees = null,
        FeatureTreeColor? displayColor = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees, displayColor);
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
        IReadOnlyList<string>? prerequisiteTrees = null,
        FeatureTreeColor? displayColor = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees, displayColor);
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
        IReadOnlyList<string>? prerequisiteTrees = null,
        FeatureTreeColor? displayColor = null)
    {
        var tree = CreateTree(name, description, featuresAffected, tier, prerequisiteTrees, displayColor);
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
        IReadOnlyList<string>? prerequisiteTrees,
        FeatureTreeColor? displayColor)
    {
        return new FeatureTree(
            name,
            description,
            featuresAffected,
            tier,
            prerequisiteTrees ?? [],
            displayColor: displayColor,
            displayName: GetDisplayName(name),
            branchName: GetBranchName(name));
    }

    private static string GetDisplayName(string name)
    {
        return name switch
        {
            "B1" => "Shellwright Basics",
            "B2" => "Load-Bearing Arches",
            "B3" => "Colony Foundations",
            "C1" => "Guard Instincts",
            "C2" => "Mandible Drill",
            "C3" => "Carapace Tactics",
            "F1" => "Algae Blooming",
            "F2" => "Spore Gardens",
            "F3" => "Queen's Pantry",
            "M1" => "Stone Sense",
            "M2" => "Deep Veins",
            "M3" => "Tunnel Discipline",
            "BC1" => "Fortified Works",
            "BF1" => "Living Architecture",
            "BM1" => "Excavation Frames",
            "CF1" => "Battle Harvest",
            "CM1" => "Ore Guard Patrols",
            "BCF1" => "Citadel Ecology",
            _ => name
        };
    }

    private static string GetBranchName(string name)
    {
        return name switch
        {
            "B1" => "Founder's Shell",
            "B2" => "Archmaker's Path",
            "B3" => "Hearthstone Line",
            "C1" => "Sentinel Spur",
            "C2" => "Red Mandible",
            "C3" => "Carapace Guard",
            "F1" => "Greenwake Sprout",
            "F2" => "Sporekeeper's Trail",
            "F3" => "Pantry Bloom",
            "M1" => "Stonewhisper Run",
            "M2" => "Deepvein Fork",
            "M3" => "Tunnelborn Ladder",
            "BC1" => "Bulwark Crown",
            "BF1" => "Living Rampart",
            "BM1" => "Quarryframe Spur",
            "CF1" => "Harvest Blade",
            "CM1" => "Orewatch Talon",
            "BCF1" => "Citadel Bloom",
            _ => name
        };
    }

    private static FeatureTreeColor NextColor(ref int treeIndex)
    {
        if ((uint)treeIndex >= (uint)GlobalTreeColors.Length)
        {
            throw new InvalidOperationException("Not enough authored display colors for global feature trees.");
        }

        return GlobalTreeColors[treeIndex++];
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
