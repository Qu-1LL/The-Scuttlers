using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;

namespace TriloGame.Game.UI.Research;

internal sealed class ResearchTreeViewNode
{
    private readonly List<ResearchTreeViewNode> _children = [];

    private ResearchTreeViewNode(
        string name,
        string description,
        string? sourceFeatureTreeName,
        bool isUnlocked,
        IEnumerable<ResearchEffectDescriptor>? effectDescriptors)
    {
        Name = name;
        Description = description;
        SourceFeatureTreeName = sourceFeatureTreeName;
        IsUnlocked = isUnlocked;
        EffectDescriptors = (effectDescriptors ?? []).ToArray();
    }

    public string Name { get; }

    public string Description { get; }

    public string? SourceFeatureTreeName { get; }

    public bool IsUnlocked { get; }

    public IReadOnlyList<ResearchEffectDescriptor> EffectDescriptors { get; }

    public IReadOnlyList<ResearchTreeViewNode> Children => _children;

    public static ResearchTreeViewNode FromFeatureTree(FeatureTree featureTree)
    {
        ArgumentNullException.ThrowIfNull(featureTree);
        if (featureTree.Root is null)
        {
            throw new ArgumentException("Feature tree preview requires a root node.", nameof(featureTree));
        }

        return FromSkillNode(featureTree.Root, featureTree.Name);
    }

    public static ResearchTreeViewNode FromResearchBranch(ResearchBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (branch.Root is null)
        {
            throw new ArgumentException("Research branch preview requires a root node.", nameof(branch));
        }

        return FromTreeInstanceNode(branch.Root);
    }

    public static ResearchTreeViewNode FromSkillTree(SkillTree skillTree)
    {
        ArgumentNullException.ThrowIfNull(skillTree);
        if (skillTree.Root is null)
        {
            throw new ArgumentException("Skill tree preview requires a root node.", nameof(skillTree));
        }

        return FromTreeInstanceNode(skillTree.Root);
    }

    private static ResearchTreeViewNode FromSkillNode(SkillNode source, string featureTreeName)
    {
        var node = new ResearchTreeViewNode(
            source.Name,
            source.Description,
            featureTreeName,
            source.IsAcquired,
            source.EffectDescriptors);
        foreach (var child in source.Children)
        {
            node._children.Add(FromSkillNode(child, featureTreeName));
        }

        return node;
    }

    private static ResearchTreeViewNode FromTreeInstanceNode(TreeInstanceNode source)
    {
        var node = new ResearchTreeViewNode(
            source.Name,
            source.Description,
            source.SourceFeatureTreeName,
            source.IsUnlocked,
            source.EffectDescriptors);
        foreach (var child in source.Children)
        {
            node._children.Add(FromTreeInstanceNode(child));
        }

        return node;
    }
}
