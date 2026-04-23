using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Progression;

// SkillTree is the per-game progression state.
// It is a binary tree built from local BinarySkillNode copies of feature-tree SkillNodes.
// It also tracks the occupied skill-grid locations used to place nodes in the tree.
public sealed class SkillTree
{
    public const int MaxLateralDifference = 8;

    private readonly Dictionary<GridPoint, BinarySkillNode> _nodesByLocation = new();

    public SkillTree(TriloDex? sourceDex = null)
    {
        SourceDex = sourceDex ?? TriloDex.Global;
    }

    public TriloDex SourceDex { get; }

    public BinarySkillNode? Root { get; private set; }

    public IReadOnlyDictionary<GridPoint, BinarySkillNode> NodesByLocation => _nodesByLocation;

    public bool HasRoot => Root is not null;

    public int UnlockedCount
    {
        get
        {
            var count = 0;
            foreach (var node in TraverseUnlocked())
            {
                count++;
            }

            return count;
        }
    }

    public int Count
    {
        get
        {
            var count = 0;
            foreach (var _ in TraverseDepthFirst())
            {
                count++;
            }

            return count;
        }
    }

    public bool IsEmpty => Root is null;

    public BinarySkillNode IntakeSkillNode(SkillNode node, string? sourceFeatureTreeName = null)
    {
        return IntakeSkillNode(node, GridPoint.Zero, sourceFeatureTreeName);
    }

    public BinarySkillNode IntakeSkillNode(SkillNode node, GridPoint nodeDelta, string? sourceFeatureTreeName = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new BinarySkillNode(node, nodeDelta, sourceFeatureTreeName);
    }

    public BinarySkillNode SetRoot(BinarySkillNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
        {
            throw new InvalidOperationException("A root skill node cannot already have a prerequisite.");
        }

        if (root.NodeDelta != GridPoint.Zero)
        {
            throw new InvalidOperationException("The root skill node must use the 0,0 grid delta.");
        }

        if (Root is not null && !ReferenceEquals(Root, root))
        {
            throw new InvalidOperationException("The skill tree already has a root.");
        }

        EnsureNodeDoesNotAlreadyBelongToTree(root);
        RegisterNodeLocation(root, GridPoint.Zero);
        Root = root;
        return root;
    }

    public BinarySkillNode SetRoot(SkillNode node, string? sourceFeatureTreeName = null)
    {
        return SetRoot(IntakeSkillNode(node, sourceFeatureTreeName));
    }

    public BinarySkillNode AddLeftChild(BinarySkillNode parent, BinarySkillNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent skill node must belong to this skill tree.");
        }

        EnsureNodeLocationCanBeRegistered(child);
        EnsureNodeDoesNotAlreadyBelongToTree(child);
        parent.SetLeft(child);
        RegisterExistingNodeLocation(child);
        return child;
    }

    public BinarySkillNode AddRightChild(BinarySkillNode parent, BinarySkillNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (!Contains(parent))
        {
            throw new InvalidOperationException("The parent skill node must belong to this skill tree.");
        }

        EnsureNodeLocationCanBeRegistered(child);
        EnsureNodeDoesNotAlreadyBelongToTree(child);
        parent.SetRight(child);
        RegisterExistingNodeLocation(child);
        return child;
    }

    public BinarySkillNode SetNodeLocation(BinarySkillNode node, GridPoint location)
    {
        ArgumentNullException.ThrowIfNull(node);
        EnsureNodeBelongsToTree(node);
        RegisterNodeLocation(node, location);
        return node;
    }

    public bool IsLocationOccupied(GridPoint location)
    {
        return IsValidGridLocation(location) && _nodesByLocation.ContainsKey(location);
    }

    public bool TryGetNodeAtLocation(GridPoint location, out BinarySkillNode? node)
    {
        return _nodesByLocation.TryGetValue(location, out node);
    }

    public BinarySkillNode? FindByLocation(GridPoint location)
    {
        return _nodesByLocation.GetValueOrDefault(location);
    }

    public GridPoint GetLeftChildLocation(BinarySkillNode parent)
    {
        var parentLocation = RequireNodeLocation(parent);
        return new GridPoint(parentLocation.X + 1, parentLocation.Y);
    }

    public GridPoint GetRightChildLocation(BinarySkillNode parent)
    {
        var parentLocation = RequireNodeLocation(parent);
        return new GridPoint(parentLocation.X, parentLocation.Y + 1);
    }

    public BinarySkillNode? ImportRoot(string featureTreeName, string skillName)
    {
        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : SetRoot(importedNode);
    }

    public BinarySkillNode? ImportLeftChild(BinarySkillNode parent, string featureTreeName, string skillName)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : AddLeftChild(parent, importedNode);
    }

    public BinarySkillNode? ImportRightChild(BinarySkillNode parent, string featureTreeName, string skillName)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var importedNode = CreateImportedNode(featureTreeName, skillName);
        return importedNode is null ? null : AddRightChild(parent, importedNode);
    }

    public bool RemoveSubtree(BinarySkillNode node)
    {
        if (node is null)
        {
            return false;
        }

        if (ReferenceEquals(Root, node))
        {
            ClearSubtreeLocations(node);
            Root = null;
            return true;
        }

        if (!Contains(node))
        {
            return false;
        }

        var parent = node.Parent;
        if (parent is null)
        {
            return false;
        }

        if (ReferenceEquals(parent.Left, node))
        {
            ClearSubtreeLocations(node);
            return parent.RemoveLeft() is not null;
        }

        if (ReferenceEquals(parent.Right, node))
        {
            ClearSubtreeLocations(node);
            return parent.RemoveRight() is not null;
        }

        return false;
    }

    public bool Contains(BinarySkillNode node)
    {
        if (node is null)
        {
            return false;
        }

        foreach (var current in TraverseDepthFirst())
        {
            if (ReferenceEquals(current, node))
            {
                return true;
            }
        }

        return false;
    }

    public BinarySkillNode? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var node in TraverseDepthFirst())
        {
            if (string.Equals(node.Name, name, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public BinarySkillNode? FindBySourceSkill(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return null;
        }

        foreach (var node in TraverseDepthFirst())
        {
            if (string.Equals(node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal) &&
                string.Equals(node.Name, skillName, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public bool ContainsSourceSkill(string featureTreeName, string skillName)
    {
        return FindBySourceSkill(featureTreeName, skillName) is not null;
    }

    public string? GetSourceFeatureTreeName(BinarySkillNode node)
    {
        return node?.SourceFeatureTreeName;
    }

    public IEnumerable<BinarySkillNode> GetNodesFromFeatureTree(string featureTreeName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName))
        {
            yield break;
        }

        foreach (var node in TraverseDepthFirst())
        {
            if (string.Equals(node.SourceFeatureTreeName, featureTreeName, StringComparison.Ordinal))
            {
                yield return node;
            }
        }
    }

    public IEnumerable<BinarySkillNode> TraverseDepthFirst()
    {
        if (Root is null)
        {
            yield break;
        }

        foreach (var node in Root.TraverseDepthFirst())
        {
            yield return node;
        }
    }

    public IEnumerable<BinarySkillNode> TraverseUnlocked()
    {
        foreach (var node in TraverseDepthFirst())
        {
            if (node.IsUnlocked)
            {
                yield return node;
            }
        }
    }

    public ResearchBranchGenerationResult GenerateResearchBranches(
        Random? random = null,
        int branchCount = 3,
        int nodesPerBranch = 4)
    {
        return new ResearchBranchGenerator(random).Generate(this, branchCount, nodesPerBranch);
    }

    public bool CanPlaceResearchBranch(ResearchBranch branch, GridPoint anchorLocation, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(branch);

        if (branch.Root is null)
        {
            failureReason = "The selected research branch has no visible root.";
            return false;
        }

        if (FindByLocation(anchorLocation) is not BinarySkillNode anchorNode)
        {
            failureReason = "Drop the branch on the root anchor or an existing skill node.";
            return false;
        }

        foreach (var branchNode in branch.Nodes)
        {
            if (branchNode.Node.Parent is not null || branchNode.Node.NodeLocation is not null)
            {
                failureReason = "The selected research branch has already been placed.";
                return false;
            }

            var location = GetBranchNodeLocation(anchorLocation, branchNode.Delta);
            if (!IsValidGridLocation(location))
            {
                failureReason = "That placement would move part of the branch outside the skill grid.";
                return false;
            }

            if (IsLocationOccupied(location))
            {
                failureReason = "That placement overlaps an existing skill node.";
                return false;
            }
        }

        if (branch.Root.Delta == new GridPoint(1, 0))
        {
            if (anchorNode.Left is not null)
            {
                failureReason = "That anchor already has a left branch.";
                return false;
            }
        }
        else if (branch.Root.Delta == new GridPoint(0, 1))
        {
            if (anchorNode.Right is not null)
            {
                failureReason = "That anchor already has a right branch.";
                return false;
            }
        }
        else
        {
            failureReason = "Research branches must enter through the left or right root slot.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public bool TryPlaceResearchBranch(ResearchBranch branch, GridPoint anchorLocation, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(branch);

        if (!CanPlaceResearchBranch(branch, anchorLocation, out failureReason))
        {
            return false;
        }

        var anchorNode = FindByLocation(anchorLocation)!;
        var rootBranchNode = branch.Root!;
        if (rootBranchNode.Delta == new GridPoint(1, 0))
        {
            AddLeftChild(anchorNode, rootBranchNode.Node);
        }
        else
        {
            AddRightChild(anchorNode, rootBranchNode.Node);
        }

        SetNodeLocation(rootBranchNode.Node, GetBranchNodeLocation(anchorLocation, rootBranchNode.Delta));
        PlaceResearchBranchChildren(rootBranchNode, rootBranchNode.Node, anchorLocation);
        failureReason = null;
        return true;
    }

    private BinarySkillNode? CreateImportedNode(string featureTreeName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(featureTreeName) || string.IsNullOrWhiteSpace(skillName))
        {
            return null;
        }

        var featureTree = SourceDex.FindFeatureTree(featureTreeName);
        if (featureTree is null)
        {
            return null;
        }

        var templateNode = featureTree.FindByName(skillName);
        return templateNode is null ? null : IntakeSkillNode(templateNode, GridPoint.Zero, featureTree.Name);
    }

    private void PlaceResearchBranchChildren(
        ResearchBranchNode branchNode,
        BinarySkillNode parentNode,
        GridPoint anchorLocation)
    {
        if (branchNode.Left is not null)
        {
            AddLeftChild(parentNode, branchNode.Left.Node);
            SetNodeLocation(branchNode.Left.Node, GetBranchNodeLocation(anchorLocation, branchNode.Left.Delta));
            PlaceResearchBranchChildren(branchNode.Left, branchNode.Left.Node, anchorLocation);
        }

        if (branchNode.Right is not null)
        {
            AddRightChild(parentNode, branchNode.Right.Node);
            SetNodeLocation(branchNode.Right.Node, GetBranchNodeLocation(anchorLocation, branchNode.Right.Delta));
            PlaceResearchBranchChildren(branchNode.Right, branchNode.Right.Node, anchorLocation);
        }
    }

    private void EnsureNodeDoesNotAlreadyBelongToTree(BinarySkillNode node)
    {
        if (Contains(node))
        {
            throw new InvalidOperationException("The skill node already belongs to this skill tree.");
        }
    }

    private void EnsureNodeBelongsToTree(BinarySkillNode node)
    {
        if (!Contains(node))
        {
            throw new InvalidOperationException("The skill node must belong to this skill tree.");
        }
    }

    private void EnsureNodeLocationCanBeRegistered(BinarySkillNode node)
    {
        if (node.NodeLocation is not GridPoint location)
        {
            return;
        }

        if (!IsValidGridLocation(location))
        {
            throw new ArgumentOutOfRangeException(nameof(location), "Grid coordinates must be zero or positive.");
        }

        if (_nodesByLocation.TryGetValue(location, out var occupiedNode) && !ReferenceEquals(occupiedNode, node))
        {
            throw new InvalidOperationException("That grid location is already occupied by another skill node.");
        }
    }

    private void RegisterExistingNodeLocation(BinarySkillNode node)
    {
        if (node.NodeLocation is GridPoint location)
        {
            RegisterNodeLocation(node, location);
        }
    }

    private void RegisterNodeLocation(BinarySkillNode node, GridPoint location)
    {
        if (!IsValidGridLocation(location))
        {
            throw new ArgumentOutOfRangeException(nameof(location), "Grid coordinates must be zero or positive.");
        }

        if (node.NodeLocation is GridPoint existingLocation && existingLocation != location)
        {
            throw new InvalidOperationException("A skill node can only be placed at one grid location.");
        }

        if (_nodesByLocation.TryGetValue(location, out var occupiedNode) && !ReferenceEquals(occupiedNode, node))
        {
            throw new InvalidOperationException("That grid location is already occupied by another skill node.");
        }

        node.SetNodeLocation(location);
        _nodesByLocation[location] = node;
    }

    private void ClearSubtreeLocations(BinarySkillNode node)
    {
        foreach (var current in node.TraverseDepthFirst())
        {
            if (current.NodeLocation is GridPoint location)
            {
                _nodesByLocation.Remove(location);
                current.ClearNodeLocation();
            }
        }
    }

    private GridPoint RequireNodeLocation(BinarySkillNode node)
    {
        EnsureNodeBelongsToTree(node);

        if (node.NodeLocation is not GridPoint location)
        {
            throw new InvalidOperationException("The skill node must be placed on the grid before it can spawn children.");
        }

        return location;
    }

    public static bool IsValidGridLocation(GridPoint location)
    {
        return location.X >= 0 &&
               location.Y >= 0 &&
               Math.Abs(location.X - location.Y) <= MaxLateralDifference;
    }

    private static GridPoint GetBranchNodeLocation(GridPoint anchorLocation, GridPoint branchDelta)
    {
        return new GridPoint(anchorLocation.X + branchDelta.X, anchorLocation.Y + branchDelta.Y);
    }
}
