using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Research;

namespace TriloGame.Game.Core.Progression;

// SkillNode is a tree node that represents one unlockable upgrade.
// Its parent acts as the prerequisite, and its effect can only be applied once.
public sealed class SkillNode
{
    private readonly List<SkillNode> _children = [];

    // Capture the authored display text and research effects for a template skill node.
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

    // Attach a child while preserving the single-parent tree invariant.
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

    // Detach a child from this node if it is currently attached here.
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

    // Allow acquisition only when this node is still locked and its prerequisite is met.
    public bool CanAcquire()
    {
        return !IsAcquired && (Prerequisite is null || Prerequisite.IsAcquired);
    }

    // Apply this node's authored effects exactly once when it becomes acquired.
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

    // Clone the authored node data without carrying over tree links or acquisition state.
    public SkillNode CreateDetachedCopy()
    {
        return new SkillNode(Name, Description, EffectDescriptors);
    }

    // Traverse this authored feature tree in parent-before-children order.
    public IEnumerable<SkillNode> TraverseDepthFirst()
    {
        yield return this;

        // Recurse through the authored child list in insertion order for stable traversal.
        foreach (var child in _children)
        {
            foreach (var descendant in child.TraverseDepthFirst())
            {
                yield return descendant;
            }
        }
    }

    // Detect ancestor relationships so child attachment cannot create cycles.
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

    // Reject blank authored text fields before they enter feature-tree content.
    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value;
    }
}
