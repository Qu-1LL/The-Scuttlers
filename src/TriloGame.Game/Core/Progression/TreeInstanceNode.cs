using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Core.Progression;

// TreeInstanceNode is the local per-run instance of an authored SkillNode.
// It preserves prerequisite topology through Parent/Children while allowing
// the run-specific tree to be assembled independently from the authored source.
public sealed class TreeInstanceNode
{
    private readonly List<TreeInstanceNode> _children = [];

    public TreeInstanceNode(SkillNode sourceSkillNode, string? sourceFeatureTreeName = null)
    {
        SourceSkillNode = sourceSkillNode ?? throw new ArgumentNullException(nameof(sourceSkillNode));
        SourceFeatureTreeName = string.IsNullOrWhiteSpace(sourceFeatureTreeName)
            ? null
            : sourceFeatureTreeName.Trim();
        Name = sourceSkillNode.Name;
        Description = sourceSkillNode.Description;
        EffectDescriptors = sourceSkillNode.EffectDescriptors.ToArray();
    }

    public SkillNode SourceSkillNode { get; }

    public string? SourceFeatureTreeName { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<ResearchEffectDescriptor> EffectDescriptors { get; }

    public TreeInstanceNode? Parent { get; private set; }

    public TreeInstanceNode? Prerequisite => Parent;

    public IReadOnlyList<TreeInstanceNode> Children => _children;

    public int ChildCount => _children.Count;

    public bool IsUnlocked { get; private set; }

    public bool IsLocked => !IsUnlocked;

    public bool IsAcquired => IsUnlocked;

    public bool IsRoot => Parent is null;

    public bool IsLeaf => _children.Count == 0;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    public void AddChild(TreeInstanceNode child, int? childIndex = null)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("A tree instance node cannot be its own child.");
        }

        if (child.IsAncestorOf(this))
        {
            throw new InvalidOperationException("Adding this child would create a cycle in the instance tree.");
        }

        if (ReferenceEquals(child.Parent, this))
        {
            if (childIndex is int existingIndex)
            {
                MoveExistingChild(child, existingIndex);
            }

            return;
        }

        child.Parent?.RemoveChild(child);
        child.Parent = this;
        InsertChild(child, childIndex);
    }

    public bool RemoveChild(TreeInstanceNode child)
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
        return CanUnlock();
    }

    public bool TryAcquire(GameSession session)
    {
        return TryUnlock(session);
    }

    public bool CanUnlock()
    {
        return !IsUnlocked && (Prerequisite is null || Prerequisite.IsUnlocked);
    }

    public bool TryUnlock(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!CanUnlock())
        {
            return false;
        }

        session.GlobalResearch.Intake(this);
        IsUnlocked = true;
        return true;
    }

    public IEnumerable<TreeInstanceNode> TraverseDepthFirst()
    {
        yield return this;

        foreach (var child in _children)
        {
            foreach (var node in child.TraverseDepthFirst())
            {
                yield return node;
            }
        }
    }

    private void InsertChild(TreeInstanceNode child, int? childIndex)
    {
        if (childIndex is not int index)
        {
            _children.Add(child);
            return;
        }

        if (index < 0 || index > _children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(childIndex), "Child index must be within the current child-slot range.");
        }

        _children.Insert(index, child);
    }

    private void MoveExistingChild(TreeInstanceNode child, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(targetIndex), "Child index must be within the current child-slot range.");
        }

        var currentIndex = _children.IndexOf(child);
        if (currentIndex < 0 || currentIndex == targetIndex)
        {
            return;
        }

        _children.RemoveAt(currentIndex);
        _children.Insert(targetIndex, child);
    }

    private bool IsAncestorOf(TreeInstanceNode node)
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
}
