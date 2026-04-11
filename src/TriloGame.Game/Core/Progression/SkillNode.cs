using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Research;

namespace TriloGame.Game.Core.Progression;

// SkillNode is a tree node that represents one unlockable upgrade.
// Its parent acts as the prerequisite, and its effect can only be applied once.
public sealed class SkillNode
{
    private readonly List<SkillNode> _children = [];

    public SkillNode(string name, string description, IEnumerable<ResearchEffectDescriptor>? effectDescriptors = null)
    {
        Name = RequireText(name, nameof(name));
        Description = RequireText(description, nameof(description));
        EffectDescriptors = (effectDescriptors ?? []).ToArray();
    }

    public string Name { get; }

    public string Description { get; }

    public SkillNode? Parent { get; private set; }

    public SkillNode? Prerequisite => Parent;

    public IReadOnlyList<SkillNode> Children => _children;

    public IReadOnlyList<ResearchEffectDescriptor> EffectDescriptors { get; }

    public bool IsAcquired { get; private set; }

    public bool IsRoot => Parent is null;

    public bool IsLeaf => _children.Count == 0;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    public void AddChild(SkillNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("A skill node cannot be its own child.");
        }

        if (child.IsAncestorOf(this))
        {
            throw new InvalidOperationException("Adding this child would create a cycle in the feature tree.");
        }

        if (ReferenceEquals(child.Parent, this))
        {
            return;
        }

        child.Parent?.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
    }

    public bool RemoveChild(SkillNode child)
    {
        if (child is null)
        {
            return false;
        }

        var removed = _children.Remove(child);
        if (removed && ReferenceEquals(child.Parent, this))
        {
            child.Parent = null;
        }

        return removed;
    }

    public bool CanAcquire()
    {
        return !IsAcquired && (Prerequisite is null || Prerequisite.IsAcquired);
    }

    public bool TryAcquire(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!CanAcquire())
        {
            return false;
        }

        session.GlobalResearch.Intake(this);
        IsAcquired = true;
        return true;
    }

    public SkillNode CreateDetachedCopy()
    {
        return new SkillNode(Name, Description, EffectDescriptors);
    }

    public IEnumerable<SkillNode> TraverseDepthFirst()
    {
        yield return this;

        foreach (var child in _children)
        {
            foreach (var descendant in child.TraverseDepthFirst())
            {
                yield return descendant;
            }
        }
    }

    private bool IsAncestorOf(SkillNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
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
