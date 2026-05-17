using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Progression;

// ResearchBranch is a preview bundle of binary skill nodes before they are placed
// into the player's persistent skill tree.
// The branch uses an invisible origin at 0,0, while visible nodes occupy
// positive-grid deltas relative to that origin.
public sealed class ResearchBranch
{
    private static readonly GridPoint EntryLeftDelta = new(1, 0);
    private static readonly GridPoint EntryRightDelta = new(0, 1);

    private readonly List<ResearchBranchNode> _nodes = [];
    private readonly Dictionary<GridPoint, ResearchBranchNode> _nodesByDelta = new();

    public GridPoint OriginDelta => GridPoint.Zero;

    public IReadOnlyList<ResearchBranchNode> Nodes => _nodes;

    public IReadOnlyDictionary<GridPoint, ResearchBranchNode> NodesByDelta => _nodesByDelta;

    public ResearchBranchNode? Root { get; private set; }

    public int Count => _nodes.Count;

    // Install the visible branch root in one of the two entry slots beside the hidden origin.
    public ResearchBranchNode SetRoot(BinarySkillNode node, GridPoint delta)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (Root is not null)
        {
            throw new InvalidOperationException("The research branch already has a visible root.");
        }

        if (delta != EntryLeftDelta && delta != EntryRightDelta)
        {
            throw new InvalidOperationException("The research branch root must be placed at 1,0 or 0,1.");
        }

        var root = AddNode(node, delta, parent: null, isLeftChild: false);
        Root = root;
        return root;
    }

    // Add a left child by advancing one column from the parent branch delta.
    public ResearchBranchNode AddLeftChild(ResearchBranchNode parent, BinarySkillNode node)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(node);

        var delta = new GridPoint(parent.Delta.X + 1, parent.Delta.Y);
        return AddNode(node, delta, parent, isLeftChild: true);
    }

    // Add a right child by advancing one row from the parent branch delta.
    public ResearchBranchNode AddRightChild(ResearchBranchNode parent, BinarySkillNode node)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(node);

        var delta = new GridPoint(parent.Delta.X, parent.Delta.Y + 1);
        return AddNode(node, delta, parent, isLeftChild: false);
    }

    // Report every currently open placement slot in the preview branch grid.
    public IReadOnlyList<ResearchBranchSlot> GetAvailableSlots()
    {
        if (Root is null)
        {
            return
            [
                new ResearchBranchSlot(null, EntryLeftDelta, true),
                new ResearchBranchSlot(null, EntryRightDelta, false)
            ];
        }

        var slotsByDelta = new Dictionary<GridPoint, ResearchBranchSlot>();
        // Collect each open child delta once so branch previews never overlap on the grid.
        foreach (var node in Root.TraverseDepthFirst())
        {
            if (node.Left is null)
            {
                var leftDelta = new GridPoint(node.Delta.X + 1, node.Delta.Y);
                AddAvailableSlot(slotsByDelta, node, leftDelta, isLeftChild: true);
            }

            if (node.Right is null)
            {
                var rightDelta = new GridPoint(node.Delta.X, node.Delta.Y + 1);
                AddAvailableSlot(slotsByDelta, node, rightDelta, isLeftChild: false);
            }
        }

        return slotsByDelta.Values.ToArray();
    }

    // Resolve a preview node by its branch-relative grid delta.
    public ResearchBranchNode? FindByDelta(GridPoint delta)
    {
        return _nodesByDelta.GetValueOrDefault(delta);
    }

    // Check whether the preview already occupies the requested branch delta.
    public bool ContainsDelta(GridPoint delta)
    {
        return _nodesByDelta.ContainsKey(delta);
    }

    // Prevent duplicate template skills from appearing twice in the same branch preview.
    public bool ContainsSourceSkill(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        foreach (var node in _nodes)
        {
            if (string.Equals(node.Node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) &&
                string.Equals(node.Node.Name, skillName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Create and register a preview node after validating its grid slot and parent link.
    private ResearchBranchNode AddNode(
        BinarySkillNode node,
        GridPoint delta,
        ResearchBranchNode? parent,
        bool isLeftChild)
    {
        if (node.NodeDelta != delta)
        {
            throw new InvalidOperationException("Research-branch nodes must be created with the same delta they occupy.");
        }

        if (_nodesByDelta.ContainsKey(delta))
        {
            throw new InvalidOperationException("The selected research-branch grid delta is already occupied.");
        }

        var branchNode = new ResearchBranchNode(node, delta);
        if (parent is not null)
        {
            if (isLeftChild)
            {
                parent.AttachLeft(branchNode);
            }
            else
            {
                parent.AttachRight(branchNode);
            }
        }

        _nodes.Add(branchNode);
        _nodesByDelta.Add(delta, branchNode);
        return branchNode;
    }

    // Register an open preview slot only once for a given branch delta.
    private void AddAvailableSlot(
        Dictionary<GridPoint, ResearchBranchSlot> slotsByDelta,
        ResearchBranchNode parent,
        GridPoint delta,
        bool isLeftChild)
    {
        if (_nodesByDelta.ContainsKey(delta) || slotsByDelta.ContainsKey(delta))
        {
            return;
        }

        slotsByDelta.Add(delta, new ResearchBranchSlot(parent, delta, isLeftChild));
    }
}

public readonly record struct ResearchBranchSlot(
    ResearchBranchNode? Parent,
    GridPoint Delta,
    bool IsLeftChild);

// ResearchBranchNode wraps a binary skill node with a relative offset inside a
// generated research branch preview.
public sealed class ResearchBranchNode
{
    // Pair a binary skill node with its preview-grid location inside a draft branch.
    public ResearchBranchNode(BinarySkillNode node, GridPoint delta)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Delta = delta;
    }

    public BinarySkillNode Node { get; }

    public GridPoint Delta { get; }

    public ResearchBranchNode? Parent { get; private set; }

    public ResearchBranchNode? Left { get; private set; }

    public ResearchBranchNode? Right { get; private set; }

    // Traverse the preview branch in parent-before-children order.
    public IEnumerable<ResearchBranchNode> TraverseDepthFirst()
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

    // Attach a preview child into the left branch slot.
    internal void AttachLeft(ResearchBranchNode child)
    {
        AttachChild(child, isLeftChild: true);
    }

    // Attach a preview child into the right branch slot.
    internal void AttachRight(ResearchBranchNode child)
    {
        AttachChild(child, isLeftChild: false);
    }

    // Enforce detachment and slot-occupancy rules before wiring a preview child in.
    private void AttachChild(ResearchBranchNode child, bool isLeftChild)
    {
        if (child.Parent is not null)
        {
            throw new InvalidOperationException("A research-branch node must be detached before it can be attached.");
        }

        var currentChild = isLeftChild ? Left : Right;
        if (currentChild is not null)
        {
            throw new InvalidOperationException("The selected research-branch child slot is already occupied.");
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
}
