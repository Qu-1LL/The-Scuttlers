using TriloGame.Game.Audio;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Scaffolding : Building
{
    private sealed class RequirementProgress
    {
        public RequirementProgress(ResourceRequirement requirement)
        {
            Requirement = requirement;
        }

        public ResourceRequirement Requirement { get; }

        public int Deposited { get; set; }
    }

    private readonly List<RequirementProgress> _recipeProgress = [];
    private readonly Dictionary<ResourceName, int> _depositedResources = [];
    private readonly Dictionary<Creature, ScaffoldMaterialReservation> _materialReservations = [];
    private readonly HashSet<Creature> _assignments = [];

    public Scaffolding(GameSession session, Building targetBuilding, IReadOnlyList<ResourceRequirement>? recipeOverride = null)
        : base(
            $"{targetBuilding.Name} Scaffolding",
            targetBuilding.Size,
            BuildScaffoldOpenMap(targetBuilding.OpenMap),
            session,
            false)
    {
        TargetBuilding = targetBuilding;
        TextureKey = "Scaffold";
        RecipeRequired = recipeOverride is null
            ? targetBuilding.GetRecipe() ?? throw new InvalidOperationException($"Scaffolding requires a valid recipe for {targetBuilding.Name}.")
            : [.. recipeOverride];

        for (var index = 0; index < RecipeRequired.Count; index++)
        {
            _recipeProgress.Add(new RequirementProgress(RecipeRequired[index]));
        }

        ConstructionRequired = BuildConstructionRequirement(RecipeRequired);
        Description = $"A construction site for {targetBuilding.Name}.";
        SetDisplayRotationTurns(targetBuilding.GetDisplayRotationTurns());
    }

    public Building TargetBuilding { get; }

    public override bool MaintainsNavigationField => true;

    // Builders work from the exterior ring; the scaffold footprint is the construction target,
    // not a location they need to occupy while delivering materials or applying work.
    public override BuildingNavigationSeedMode NavigationSeedMode =>
        BuildingNavigationSeedMode.AdjacentExteriorPassableTiles;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Asynchronous;

    public IReadOnlyList<ResourceRequirement> RecipeRequired { get; }

    public bool RecipeComplete { get; private set; }

    public int ConstructionProgress { get; private set; }

    public int ConstructionRequired { get; }

    public bool ConstructionComplete { get; private set; }

    public bool ResourceComplete { get; private set; }

    public bool CompletionPending { get; private set; }

    public bool BuildFirst { get; private set; }

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions()
    {
        var width = DisplayBaseSize.X;
        var height = DisplayBaseSize.Y;
        return
        [
            CreateEdgeZone("North work edge", new GridPoint(0, -1), new GridPoint(width, 1), width, horizontal: true),
            CreateEdgeZone("East work edge", new GridPoint(width, 0), new GridPoint(1, height), height, horizontal: false),
            CreateEdgeZone("South work edge", new GridPoint(0, height), new GridPoint(width, 1), width, horizontal: true),
            CreateEdgeZone("West work edge", new GridPoint(-1, 0), new GridPoint(1, height), height, horizontal: false)
        ];
    }

    private static InteractionZoneDefinition CreateEdgeZone(
        string name,
        GridPoint origin,
        GridPoint size,
        int slotCount,
        bool horizontal)
    {
        var slots = new GridPoint[slotCount];
        for (var index = 0; index < slotCount; index++)
        {
            slots[index] = horizontal
                ? new GridPoint(origin.X + index, origin.Y)
                : new GridPoint(origin.X, origin.Y + index);
        }

        return new InteractionZoneDefinition(name, InteractionZonePurpose.Construction, origin, size, slots);
    }

    public override int[][] RotateMap()
    {
        TargetBuilding.RotateMap();
        Size = TargetBuilding.Size;
        OpenMap = BuildScaffoldOpenMap(TargetBuilding.OpenMap);
        SetDisplayRotationTurns(GetDisplayRotationTurns());
        return OpenMap;
    }

    public IReadOnlyCollection<Creature> GetAssignments() => _assignments;

    public IReadOnlyDictionary<ResourceName, int> GetDepositedResources() => _depositedResources;

    public void Assign(Creature creature) => _assignments.Add(creature);

    public void RemoveAssignment(Creature creature) => _assignments.Remove(creature);

    public int GetVolume() => _assignments.Count;

    public bool ToggleBuildFirst()
    {
        if (!IsInProgress())
        {
            return false;
        }

        BuildFirst = !BuildFirst;
        return true;
    }

    public int GetRequiredBuilderCount(int carryCapacity)
    {
        if (!IsInProgress())
        {
            return 0;
        }

        if (!NeedsAnyResource())
        {
            return NeedsConstructionWork() ? 1 : 0;
        }

        var remaining = 0;
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            remaining += GetRemainingRequirement(index);
        }

        return Math.Max(1, (remaining + Math.Max(1, carryCapacity) - 1) / Math.Max(1, carryCapacity));
    }

    public bool CanAssignBuilder(Creature creature, int carryCapacity)
    {
        return IsInProgress() &&
               (_assignments.Contains(creature) ||
                _assignments.Count < GetRequiredBuilderCount(carryCapacity));
    }

    public int GetTotalDepositedAmount()
    {
        var total = 0;
        foreach (var pair in _depositedResources)
        {
            total += pair.Value;
        }

        return total;
    }

    public ScaffoldMaterialReservation? GetMaterialReservation(Creature creature)
    {
        return _materialReservations.TryGetValue(creature, out var reservation)
            ? reservation
            : null;
    }

    public int GetReservedAmount(ResourceName resourceType, Creature? excludeCreature = null)
    {
        var reservedAmount = 0;
        foreach (var pair in _materialReservations)
        {
            if (pair.Key == excludeCreature || pair.Value.ResourceType != resourceType)
            {
                continue;
            }

            reservedAmount += pair.Value.Amount;
        }

        return reservedAmount;
    }

    public int GetReservedAmount(int requirementIndex, Creature? excludeCreature = null)
    {
        var reservedAmount = 0;
        foreach (var pair in _materialReservations)
        {
            if (pair.Key == excludeCreature || pair.Value.RequirementIndex != requirementIndex)
            {
                continue;
            }

            reservedAmount += pair.Value.Amount;
        }

        return reservedAmount;
    }

    public int GetRemainingRequirement(int requirementIndex)
    {
        if (!IsValidRequirementIndex(requirementIndex))
        {
            return 0;
        }

        var progress = _recipeProgress[requirementIndex];
        return System.Math.Max(0, progress.Requirement.Amount - progress.Deposited);
    }

    public int GetRemainingRequirement(ResourceName resourceType)
    {
        var remaining = 0;
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            if (_recipeProgress[index].Requirement.Matches(resourceType))
            {
                remaining += GetRemainingRequirement(index);
            }
        }

        return remaining;
    }

    public int GetUnreservedRemainingRequirement(int requirementIndex, Creature? excludeCreature = null)
    {
        return System.Math.Max(0, GetRemainingRequirement(requirementIndex) - GetReservedAmount(requirementIndex, excludeCreature));
    }

    public int GetUnreservedRemainingRequirement(ResourceName resourceType, Creature? excludeCreature = null)
    {
        var remaining = 0;
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            if (_recipeProgress[index].Requirement.Matches(resourceType))
            {
                remaining += GetUnreservedRemainingRequirement(index, excludeCreature);
            }
        }

        return remaining;
    }

    public IReadOnlyList<ScaffoldRequirementNeed> GetNeededRequirements(bool includeReservations = false, Creature? excludeCreature = null)
    {
        var neededRequirements = new List<ScaffoldRequirementNeed>(_recipeProgress.Count);
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            var remaining = includeReservations
                ? GetUnreservedRemainingRequirement(index, excludeCreature)
                : GetRemainingRequirement(index);
            if (remaining <= 0)
            {
                continue;
            }

            neededRequirements.Add(new ScaffoldRequirementNeed(index, _recipeProgress[index].Requirement, remaining));
        }

        return neededRequirements;
    }

    public bool NeedsAnyResource(bool includeReservations = false, Creature? excludeCreature = null)
    {
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            var remaining = includeReservations
                ? GetUnreservedRemainingRequirement(index, excludeCreature)
                : GetRemainingRequirement(index);
            if (remaining > 0)
            {
                return true;
            }
        }

        return false;
    }

    public int ReserveMaterial(Creature creature, int requirementIndex, ResourceName resourceType, int amount)
    {
        if (amount <= 0 || !IsValidRequirementIndex(requirementIndex))
        {
            return 0;
        }

        var requirement = _recipeProgress[requirementIndex].Requirement;
        if (!requirement.Matches(resourceType))
        {
            return 0;
        }

        ReleaseMaterialReservation(creature);
        var reserved = System.Math.Min(amount, GetUnreservedRemainingRequirement(requirementIndex, creature));
        if (reserved <= 0)
        {
            return 0;
        }

        _materialReservations[creature] = new ScaffoldMaterialReservation(requirementIndex, requirement, resourceType, reserved);
        return reserved;
    }

    public int ReserveMaterial(Creature creature, ResourceName resourceType, int amount)
    {
        var requirementIndex = FindPreferredRequirementIndex(resourceType, includeReservations: true, creature);
        return requirementIndex < 0
            ? 0
            : ReserveMaterial(creature, requirementIndex, resourceType, amount);
    }

    public ScaffoldMaterialReservation? ReleaseMaterialReservation(Creature creature)
    {
        if (!_materialReservations.TryGetValue(creature, out var reservation))
        {
            return null;
        }

        _materialReservations.Remove(creature);
        return reservation;
    }

    public bool NeedsResource(ResourceName resourceType, bool includeReservations = false, Creature? excludeCreature = null)
    {
        return FindPreferredRequirementIndex(resourceType, includeReservations, excludeCreature) >= 0;
    }

    public int Deposit(ResourceName resourceType, int amount, Creature? creature = null)
    {
        var releasedReservation = creature is not null
            ? ReleaseMaterialReservation(creature)
            : null;

        if (amount <= 0 || !NeedsResource(resourceType))
        {
            TryCompleteConstruction(creature);
            return 0;
        }

        var accepted = DepositIntoMatchingRequirements(resourceType, amount, releasedReservation);
        if (accepted <= 0)
        {
            TryCompleteConstruction(creature);
            return 0;
        }

        UpdateRecipeCompleteState();
        TryCompleteConstruction(creature);
        return accepted;
    }

    public bool IsRecipeComplete() => UpdateRecipeCompleteState();

    public int GetConstructionRemaining() => System.Math.Max(0, ConstructionRequired - ConstructionProgress);

    public bool NeedsConstructionWork() => GetConstructionRemaining() > 0;

    public bool IsConstructionComplete() => UpdateConstructionCompleteState();

    public bool IsResourceComplete() => UpdateResourceCompleteState();

    public int ApplyConstructionWork(int amount, Creature? creature = null)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var applied = System.Math.Min(amount, GetConstructionRemaining());
        if (applied <= 0)
        {
            TryCompleteConstruction(creature);
            return 0;
        }

        ConstructionProgress += applied;
        UpdateConstructionCompleteState();
        TryCompleteConstruction(creature);
        return applied;
    }

    public bool IsInProgress()
    {
        return CompletionPending || !IsResourceComplete();
    }

    public bool TryCompleteConstruction(object? source = null)
    {
        if (!IsResourceComplete())
        {
            CompletionPending = false;
            return false;
        }

        CompletionPending = true;
        return CompleteConstruction(source);
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        base.CleanupBeforeRemoval(source);
        _assignments.Clear();
        _depositedResources.Clear();
        _materialReservations.Clear();
        CompletionPending = false;
    }

    public bool CompleteConstruction(object? source = null)
    {
        if (!IsResourceComplete() || Cave is null || Location is null)
        {
            CompletionPending = false;
            return false;
        }

        if (Cave.HasCreatureOverlappingSolidCells(TargetBuilding, Location.Value))
        {
            CompletionPending = true;
            return false;
        }

        var cave = Cave;
        var location = Location.Value;
        var displayRotationTurns = GetDisplayRotationTurns();
        TargetBuilding.SetDisplayRotationTurns(displayRotationTurns);

        if (cave.ReplaceBuilding(this, TargetBuilding, location, source ?? "scaffoldingComplete"))
        {
            CompletionPending = false;
            Session.RequestAudioCue(
                GameAudioCue.BuildingFinished,
                WorldPoint.FromGridPoint(TargetBuilding.GetCenter()),
                Math.Max(1f, TargetBuilding.Size.X * TargetBuilding.Size.Y));
            return true;
        }

        CompletionPending = true;
        return false;
    }

    public override int Tick(World.Cave cave)
    {
        return TryCompleteConstruction("scaffoldingTick") ? 1 : 0;
    }

    private bool UpdateRecipeCompleteState()
    {
        RecipeComplete = true;
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            if (_recipeProgress[index].Deposited < _recipeProgress[index].Requirement.Amount)
            {
                RecipeComplete = false;
                break;
            }
        }

        return RecipeComplete;
    }

    private bool UpdateConstructionCompleteState()
    {
        ConstructionComplete = ConstructionProgress >= ConstructionRequired;
        return ConstructionComplete;
    }

    private bool UpdateResourceCompleteState()
    {
        ResourceComplete = UpdateRecipeCompleteState() && UpdateConstructionCompleteState();
        return ResourceComplete;
    }

    private int DepositIntoMatchingRequirements(
        ResourceName resourceType,
        int amount,
        ScaffoldMaterialReservation? releasedReservation)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var specificPreferredIndex = releasedReservation.HasValue && releasedReservation.Value.Requirement.IsSpecificResource
            ? releasedReservation.Value.RequirementIndex
            : -1;
        var categoryPreferredIndex = releasedReservation.HasValue && releasedReservation.Value.Requirement.IsCategory
            ? releasedReservation.Value.RequirementIndex
            : -1;

        var accepted = DepositIntoRequirements(resourceType, amount, specificPreferredIndex, requireExactResource: true);
        if (accepted >= amount)
        {
            return accepted;
        }

        return accepted + DepositIntoRequirements(
            resourceType,
            amount - accepted,
            categoryPreferredIndex,
            requireExactResource: false);
    }

    private int DepositIntoRequirements(ResourceName resourceType, int amount, int preferredIndex, bool requireExactResource)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var accepted = 0;
        if (preferredIndex >= 0 && RequirementMatchesPriority(preferredIndex, resourceType, requireExactResource))
        {
            accepted += DepositIntoRequirement(preferredIndex, resourceType, amount);
        }

        if (accepted >= amount)
        {
            return accepted;
        }

        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            if (index == preferredIndex || !RequirementMatchesPriority(index, resourceType, requireExactResource))
            {
                continue;
            }

            accepted += DepositIntoRequirement(index, resourceType, amount - accepted);
            if (accepted >= amount)
            {
                break;
            }
        }

        return accepted;
    }

    private int DepositIntoRequirement(int requirementIndex, ResourceName resourceType, int amount)
    {
        if (amount <= 0 || !IsValidRequirementIndex(requirementIndex))
        {
            return 0;
        }

        var progress = _recipeProgress[requirementIndex];
        if (!progress.Requirement.Matches(resourceType))
        {
            return 0;
        }

        var accepted = System.Math.Min(amount, GetRemainingRequirement(requirementIndex));
        if (accepted <= 0)
        {
            return 0;
        }

        progress.Deposited += accepted;
        _depositedResources[resourceType] = _depositedResources.GetValueOrDefault(resourceType) + accepted;
        return accepted;
    }

    private bool RequirementMatchesPriority(int requirementIndex, ResourceName resourceType, bool requireExactResource)
    {
        if (!IsValidRequirementIndex(requirementIndex))
        {
            return false;
        }

        var requirement = _recipeProgress[requirementIndex].Requirement;
        if (requireExactResource)
        {
            return requirement.Requires(resourceType) && GetRemainingRequirement(requirementIndex) > 0;
        }

        return requirement.IsCategory &&
               requirement.Matches(resourceType) &&
               GetRemainingRequirement(requirementIndex) > 0;
    }

    private int FindPreferredRequirementIndex(ResourceName resourceType, bool includeReservations, Creature? excludeCreature)
    {
        var exactMatch = FindMatchingRequirementIndex(resourceType, includeReservations, excludeCreature, requireExactResource: true);
        return exactMatch >= 0
            ? exactMatch
            : FindMatchingRequirementIndex(resourceType, includeReservations, excludeCreature, requireExactResource: false);
    }

    private int FindMatchingRequirementIndex(
        ResourceName resourceType,
        bool includeReservations,
        Creature? excludeCreature,
        bool requireExactResource)
    {
        for (var index = 0; index < _recipeProgress.Count; index++)
        {
            if (!RequirementMatchesPriority(index, resourceType, requireExactResource))
            {
                continue;
            }

            var remaining = includeReservations
                ? GetUnreservedRemainingRequirement(index, excludeCreature)
                : GetRemainingRequirement(index);
            if (remaining > 0)
            {
                return index;
            }
        }

        return -1;
    }

    private bool IsValidRequirementIndex(int requirementIndex)
    {
        return requirementIndex >= 0 && requirementIndex < _recipeProgress.Count;
    }

    private static int[][] BuildScaffoldOpenMap(int[][] targetOpenMap)
    {
        var openMap = new int[targetOpenMap.Length][];
        for (var row = 0; row < targetOpenMap.Length; row++)
        {
            openMap[row] = new int[targetOpenMap[row].Length];
            for (var column = 0; column < targetOpenMap[row].Length; column++)
            {
                openMap[row][column] = targetOpenMap[row][column] > 1 ? targetOpenMap[row][column] : 1;
            }
        }

        return openMap;
    }

    // Only live scaffold-owned tiles block completion; excluded cells stay out of the occupancy check.
    private static int BuildConstructionRequirement(IReadOnlyList<ResourceRequirement> recipeRequired)
    {
        var requiredWork = 0;
        for (var index = 0; index < recipeRequired.Count; index++)
        {
            var requirement = recipeRequired[index];
            requiredWork += requirement.Amount * GetConstructionWeight(requirement);
        }

        return System.Math.Max(1, requiredWork);
    }

    private static int GetConstructionWeight(ResourceRequirement requirement)
    {
        if (requirement.SpecificResource is { } specificResource)
        {
            return GetResourceConstructionWeight(specificResource);
        }

        foreach (var matchingResourceType in Enum.GetValues<ResourceName>())
        {
            if (requirement.Matches(matchingResourceType))
            {
                return GetResourceConstructionWeight(matchingResourceType);
            }
        }

        return 1;
    }

    private static int GetResourceConstructionWeight(ResourceName resourceType)
    {
        var ores = Economy.OreType.GetOres();
        for (var index = 0; index < ores.Count; index++)
        {
            if (ores[index].Resource == resourceType)
            {
                return index + 1;
            }
        }

        return 1;
    }
}
