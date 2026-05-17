using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Progression;

// BinarySkillNode is the local per-run copy of a feature-tree skill node.
// Unlike SkillNode, it only supports a left child and a right child.
// Each node also carries a relative grid delta plus an optional placed grid location.
public sealed class BinarySkillNode
{
    // Create a branch-ready copy that starts at the invisible origin delta.
    public BinarySkillNode(SkillNode sourceSkillNode, string? sourceFeatureTreeName = null)
        : this(sourceSkillNode, GridPoint.Zero, sourceFeatureTreeName)
    {
    }

    // Copy authored skill data into a per-run binary node with an assigned branch delta.
    public BinarySkillNode(SkillNode sourceSkillNode, GridPoint nodeDelta, string? sourceFeatureTreeName = null)
    {
        SourceSkillNode = sourceSkillNode ?? throw new ArgumentNullException(nameof(sourceSkillNode));
        SourceFeatureTreeName = string.IsNullOrWhiteSpace(sourceFeatureTreeName)
            ? null
            : sourceFeatureTreeName.Trim();
        NodeDelta = RequireNonNegativeGridPoint(nodeDelta, nameof(nodeDelta));
        Name = sourceSkillNode.Name;
        Description = sourceSkillNode.Description;
        EffectDescriptors = sourceSkillNode.EffectDescriptors.ToArray();
    }

    public SkillNode SourceSkillNode { get; }

    public string? SourceFeatureTreeName { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<ResearchEffectDescriptor> EffectDescriptors { get; }

    public GridPoint NodeDelta { get; }

    public GridPoint? NodeLocation { get; private set; }

    public bool HasNodeLocation => NodeLocation is not null;

    public BinarySkillNode? Parent { get; private set; }

    public BinarySkillNode? Prerequisite => Parent;

    public BinarySkillNode? Left { get; private set; }

    public BinarySkillNode? Right { get; private set; }

    public bool IsUnlocked { get; private set; }

    public bool IsLocked => !IsUnlocked;

    public bool IsAcquired => IsUnlocked;

    public bool IsRoot => Parent is null;

    public bool IsLeaf => Left is null && Right is null;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    // Record the placed grid location once this node is inserted into the live tree.
    internal void SetNodeLocation(GridPoint nodeLocation)
    {
        NodeLocation = RequireNonNegativeGridPoint(nodeLocation, nameof(nodeLocation));
    }

    // Clear any placed grid location when the node leaves the live tree.
    internal void ClearNodeLocation()
    {
        NodeLocation = null;
    }

    // Preserve the skill-node acquisition terminology while reusing unlock rules.
    public bool CanAcquire()
    {
        return CanUnlock();
    }

    // Preserve the skill-node acquisition terminology while reusing unlock behavior.
    public bool TryAcquire(GameSession session)
    {
        return TryUnlock(session);
    }

    // Allow unlocks only when this node is still locked and its prerequisite is satisfied.
    public bool CanUnlock()
    {
        return !IsUnlocked && (Prerequisite is null || Prerequisite.IsUnlocked);
    }

    // Apply this node's research effects exactly once when it becomes unlocked.
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

    // Attach a child into the left binary branch slot.
    public void SetLeft(BinarySkillNode child)
    {
        AttachChild(child, isLeftChild: true);
    }

    // Attach a child into the right binary branch slot.
    public void SetRight(BinarySkillNode child)
    {
        AttachChild(child, isLeftChild: false);
    }

    // Detach and return the current left child if one is present.
    public BinarySkillNode? RemoveLeft()
    {
        var removed = Left;
        if (removed is not null)
        {
            Left = null;
            removed.Parent = null;
        }

        return removed;
    }

    // Detach and return the current right child if one is present.
    public BinarySkillNode? RemoveRight()
    {
        var removed = Right;
        if (removed is not null)
        {
            Right = null;
            removed.Parent = null;
        }

        return removed;
    }

    // Traverse this branch in parent-before-children order.
    public IEnumerable<BinarySkillNode> TraverseDepthFirst()
    {
        yield return this;

        if (Left is not null)
        {
            foreach (var node in Left.TraverseDepthFirst())
            {
                yield return node;
            }
        }

        if (Right is not null)
        {
            foreach (var node in Right.TraverseDepthFirst())
            {
                yield return node;
            }
        }
    }

    // Enforce binary-slot, detachment, and cycle rules before wiring a child node in.
    private void AttachChild(BinarySkillNode child, bool isLeftChild)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("A binary skill node cannot be its own child.");
        }

        if (child.IsAncestorOf(this))
        {
            throw new InvalidOperationException("Adding this child would create a cycle in the skill tree.");
        }

        if (child.Parent is not null)
        {
            throw new InvalidOperationException("A binary skill node must be detached before it can be attached.");
        }

        var currentChild = isLeftChild ? Left : Right;
        if (currentChild is not null && !ReferenceEquals(currentChild, child))
        {
            throw new InvalidOperationException("The selected child slot is already occupied.");
        }

        if (ReferenceEquals(currentChild, child))
        {
            return;
        }

        child.Parent = this;
        if (isLeftChild)
        {
            Left = child;
        }
        else
        {
            Right = child;
        }
    }

    // Detect ancestor relationships so attach operations cannot introduce cycles.
    private bool IsAncestorOf(BinarySkillNode node)
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

    // Keep authored branch deltas and placed locations in the non-negative grid space.
    private static GridPoint RequireNonNegativeGridPoint(GridPoint point, string parameterName)
    {
        if (point.X < 0 || point.Y < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Grid coordinates must be zero or positive.");
        }

        return point;
    }
}
