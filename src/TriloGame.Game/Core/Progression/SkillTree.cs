using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
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

    public int GetNodeUnlockCost(BinarySkillNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.NodeLocation is not GridPoint location)
        {
            throw new InvalidOperationException("Only placed skill tree nodes have an unlock cost.");
        }

        return CalculateNodeUnlockCost(location);
    }

    public IReadOnlyDictionary<string, int> GetNodeUnlockCosts(BinarySkillNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return BuildNodeUnlockCosts(GetNodeUnlockCost(node));
    }

    public bool CanPurchaseNode(GameSession session, BinarySkillNode node, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        if (!Contains(node) || node.NodeLocation is not GridPoint location)
        {
            failureReason = "Only placed skill tree nodes can be unlocked.";
            return false;
        }

        if (node.IsUnlocked)
        {
            failureReason = "That skill node is already unlocked.";
            return false;
        }

        if (node.Prerequisite is not null && !node.Prerequisite.IsUnlocked)
        {
            failureReason = "The previous skill node must be unlocked first.";
            return false;
        }

        var missingPrerequisites = GetMissingFeatureTreePrerequisiteSkillNames(node);
        if (missingPrerequisites.Count > 0)
        {
            failureReason = BuildMissingFeatureTreePrerequisiteFailureReason(missingPrerequisites);
            return false;
        }

        var costs = BuildNodeUnlockCosts(CalculateNodeUnlockCost(location));
        if (ResourceCostComparer.TryFindFirstShortfall(session.Resources, costs, out var shortfall))
        {
            failureReason = BuildMissingStoredResourceFailureReason(shortfall);
            return false;
        }

        failureReason = null;
        return true;
    }

    public bool TryPurchaseNode(GameSession session, BinarySkillNode node, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        if (!CanPurchaseNode(session, node, out failureReason))
        {
            return false;
        }

        var costs = GetNodeUnlockCosts(node);
        if (!TrySpendStoredResources(session, costs, out failureReason))
        {
            return false;
        }

        if (!node.TryUnlock(session))
        {
            failureReason = "That skill node could not be unlocked.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public IReadOnlyList<string> GetMissingFeatureTreePrerequisiteSkillNames(BinarySkillNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (string.IsNullOrWhiteSpace(node.SourceFeatureTreeName))
        {
            return [];
        }

        var missingPrerequisites = new List<string>();
        for (var current = node.SourceSkillNode.Parent; current is not null; current = current.Parent)
        {
            var localPrerequisite = FindBySourceSkill(node.SourceFeatureTreeName, current.Name);
            if (localPrerequisite is not null && localPrerequisite.IsUnlocked)
            {
                continue;
            }

            missingPrerequisites.Add(current.Name);
        }

        missingPrerequisites.Reverse();
        return missingPrerequisites;
    }

    public static int CalculateNodeUnlockCost(GridPoint location)
    {
        if (!IsValidGridLocation(location))
        {
            throw new ArgumentOutOfRangeException(nameof(location), "Grid coordinates must be zero or positive.");
        }

        return checked((location.X + location.Y) * 100);
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

    private static string BuildMissingFeatureTreePrerequisiteFailureReason(IReadOnlyList<string> missingPrerequisites)
    {
        return missingPrerequisites.Count == 1
            ? $"Unlock prerequisite skill {missingPrerequisites[0]} first."
            : $"Unlock prerequisite skills {string.Join(", ", missingPrerequisites)} first.";
    }

    private static Dictionary<string, int> BuildNodeUnlockCosts(int sandstoneCost)
    {
        var costs = new Dictionary<string, int>(1, StringComparer.Ordinal)
        {
            [OreType.SANDSTONE.Name] = sandstoneCost
        };
        return costs;
    }

    private static string BuildMissingStoredResourceFailureReason(ResourceShortfall shortfall)
    {
        return $"Need {shortfall.MissingAmount} more {shortfall.ResourceType.ToLowerInvariant()} to unlock this node.";
    }

    private static bool TrySpendStoredResources(
        GameSession session,
        IReadOnlyDictionary<string, int> costs,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(costs);

        var hasPositiveCost = false;
        foreach (var pair in costs)
        {
            if (pair.Value > 0 && !string.IsNullOrWhiteSpace(pair.Key))
            {
                hasPositiveCost = true;
                break;
            }
        }

        if (!hasPositiveCost)
        {
            failureReason = null;
            return true;
        }

        var cave = session.Cave;
        if (cave is null)
        {
            failureReason = "The session must have a cave before skill nodes can be purchased.";
            return false;
        }

        var storages = GetStorageBuildings(cave);
        if (storages.Count == 0)
        {
            failureReason = "The colony has no storage buildings holding materials.";
            return false;
        }

        var storedResources = GetTotalStoredResources(storages);
        if (ResourceCostComparer.TryFindFirstShortfall(storedResources, costs, out var shortfall))
        {
            failureReason = BuildMissingStoredResourceFailureReason(shortfall);
            return false;
        }

        foreach (var pair in costs)
        {
            if (pair.Value <= 0 || string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (!SpendResourceAcrossStorages(storages, pair.Key, pair.Value))
            {
                failureReason = $"Unable to spend {pair.Value} {pair.Key.ToLowerInvariant()} from storage.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private static List<IStorage> GetStorageBuildings(TriloGame.Game.Core.World.Cave cave)
    {
        var storages = new List<IStorage>();
        foreach (var building in cave.GetBuildingList())
        {
            if (building is IStorage storage)
            {
                storages.Add(storage);
            }
        }

        return storages;
    }

    private static Dictionary<string, int> GetTotalStoredResources(IReadOnlyList<IStorage> storages)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var storage in storages)
        {
            foreach (var pair in storage.GetInventory())
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                totals[pair.Key] = totals.GetValueOrDefault(pair.Key, 0) + pair.Value;
            }
        }

        return totals;
    }

    private static bool SpendResourceAcrossStorages(IReadOnlyList<IStorage> storages, string resourceType, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(resourceType))
        {
            return true;
        }

        var remaining = amount;
        for (var index = 0; index < storages.Count && remaining > 0; index++)
        {
            var available = storages[index].GetInventory().GetValueOrDefault(resourceType, 0);
            if (available <= 0)
            {
                continue;
            }

            var storagesRemainingWithResource = CountStoragesContainingResource(storages, resourceType, index);
            var targetShare = remaining / storagesRemainingWithResource;
            if ((remaining % storagesRemainingWithResource) != 0)
            {
                targetShare++;
            }

            var withdrawn = storages[index].Withdraw(resourceType, Math.Min(available, targetShare));
            remaining -= withdrawn;
        }

        return remaining <= 0;
    }

    private static int CountStoragesContainingResource(
        IReadOnlyList<IStorage> storages,
        string resourceType,
        int startIndex)
    {
        var count = 0;
        for (var index = startIndex; index < storages.Count; index++)
        {
            if (storages[index].GetInventory().GetValueOrDefault(resourceType, 0) > 0)
            {
                count++;
            }
        }

        return Math.Max(1, count);
    }
}
