using TriloGame.Game.Audio;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Scaffolding : Building
{
    private readonly Dictionary<Creature, ResourceReservation> _materialReservations = [];
    private readonly HashSet<Creature> _assignments = [];

    public Scaffolding(GameSession session, Building targetBuilding, Dictionary<string, int>? recipeOverride = null)
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
            : new Dictionary<string, int>(recipeOverride, StringComparer.Ordinal);
        RecipeDeposited = RecipeRequired.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        ConstructionRequired = BuildConstructionRequirement(RecipeRequired);
        Description = $"A construction site for {targetBuilding.Name}.";
        SetDisplayRotationTurns(targetBuilding.GetDisplayRotationTurns());
    }

    public Building TargetBuilding { get; }

    public Dictionary<string, int> RecipeRequired { get; }

    public Dictionary<string, int> RecipeDeposited { get; }

    public bool RecipeComplete { get; private set; }

    public int ConstructionProgress { get; private set; }

    public int ConstructionRequired { get; }

    public bool ConstructionComplete { get; private set; }

    public bool CompletionPending { get; private set; }

    public override int[][] RotateMap()
    {
        TargetBuilding.RotateMap();
        Size = TargetBuilding.Size;
        OpenMap = BuildScaffoldOpenMap(TargetBuilding.OpenMap);
        SetDisplayRotationTurns(GetDisplayRotationTurns());
        return OpenMap;
    }

    public IReadOnlyCollection<Creature> GetAssignments() => _assignments;

    public void Assign(Creature creature) => _assignments.Add(creature);

    public void RemoveAssignment(Creature creature) => _assignments.Remove(creature);

    public int GetVolume() => _assignments.Count;

    public ResourceReservation? GetMaterialReservation(Creature creature)
    {
        return _materialReservations.GetValueOrDefault(creature);
    }

    public int GetReservedAmount(string resourceType, Creature? excludeCreature = null)
    {
        return _materialReservations
            .Where(pair => pair.Key != excludeCreature && string.Equals(pair.Value.ResourceType, resourceType, StringComparison.Ordinal))
            .Sum(pair => pair.Value.Amount);
    }

    public int GetRemainingRequirement(string resourceType)
    {
        return !RecipeRequired.ContainsKey(resourceType)
            ? 0
            : System.Math.Max(0, RecipeRequired[resourceType] - RecipeDeposited.GetValueOrDefault(resourceType, 0));
    }

    public int GetUnreservedRemainingRequirement(string resourceType, Creature? excludeCreature = null)
    {
        return System.Math.Max(0, GetRemainingRequirement(resourceType) - GetReservedAmount(resourceType, excludeCreature));
    }

    public IReadOnlyList<string> GetNeededResourceTypes(bool includeReservations = false, Creature? excludeCreature = null)
    {
        return RecipeRequired.Keys
            .Where(resourceType => includeReservations
                ? GetUnreservedRemainingRequirement(resourceType, excludeCreature) > 0
                : GetRemainingRequirement(resourceType) > 0)
            .ToArray();
    }

    public bool NeedsAnyResource(bool includeReservations = false, Creature? excludeCreature = null)
    {
        return GetNeededResourceTypes(includeReservations, excludeCreature).Count > 0;
    }

    public int ReserveMaterial(Creature creature, string resourceType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        ReleaseMaterialReservation(creature);
        var reserved = System.Math.Min(amount, GetUnreservedRemainingRequirement(resourceType, creature));
        if (reserved <= 0)
        {
            return 0;
        }

        _materialReservations[creature] = new ResourceReservation(resourceType, reserved);
        return reserved;
    }

    public ResourceReservation? ReleaseMaterialReservation(Creature creature)
    {
        if (!_materialReservations.TryGetValue(creature, out var reservation))
        {
            return null;
        }

        _materialReservations.Remove(creature);
        return reservation;
    }

    public bool NeedsResource(string resourceType, bool includeReservations = false, Creature? excludeCreature = null)
    {
        return includeReservations
            ? GetUnreservedRemainingRequirement(resourceType, excludeCreature) > 0
            : GetRemainingRequirement(resourceType) > 0;
    }

    public int Deposit(string resourceType, int amount, Creature? creature = null)
    {
        if (creature is not null)
        {
            ReleaseMaterialReservation(creature);
        }

        if (amount <= 0 || !NeedsResource(resourceType))
        {
            TryCompleteConstruction(creature);
            return 0;
        }

        var accepted = System.Math.Min(amount, GetRemainingRequirement(resourceType));
        if (accepted <= 0)
        {
            TryCompleteConstruction(creature);
            return 0;
        }

        RecipeDeposited[resourceType] += accepted;
        UpdateRecipeCompleteState();
        TryCompleteConstruction(creature);
        return accepted;
    }

    public bool IsRecipeComplete() => UpdateRecipeCompleteState();

    public int GetConstructionRemaining() => System.Math.Max(0, ConstructionRequired - ConstructionProgress);

    public bool NeedsConstructionWork() => GetConstructionRemaining() > 0;

    public bool IsConstructionComplete() => UpdateConstructionCompleteState();

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
        return CompletionPending || !IsRecipeComplete() || !IsConstructionComplete();
    }

    public bool TryCompleteConstruction(object? source = null)
    {
        if (!IsRecipeComplete() || !IsConstructionComplete())
        {
            CompletionPending = false;
            return false;
        }

        CompletionPending = true;
        return CompleteConstruction(source);
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        _assignments.Clear();
        _materialReservations.Clear();
        CompletionPending = false;
    }

    public bool CompleteConstruction(object? source = null)
    {
        if (!IsRecipeComplete() || !IsConstructionComplete() || Cave is null || Location is null)
        {
            CompletionPending = false;
            return false;
        }

        var cave = Cave;
        var location = Location.Value;
        var displayRotationTurns = GetDisplayRotationTurns();
        TargetBuilding.SetDisplayRotationTurns(displayRotationTurns);

        if (!cave.RemoveBuilding(this, source ?? "scaffoldingComplete"))
        {
            return false;
        }

        if (cave.Build(TargetBuilding, location))
        {
            CompletionPending = false;
            Session.RequestAudioCue(GameAudioCue.BuildingFinished);
            return true;
        }

        SetDisplayRotationTurns(displayRotationTurns);
        cave.Build(this, location);
        CompletionPending = true;
        return false;
    }

    private bool UpdateRecipeCompleteState()
    {
        RecipeComplete = RecipeRequired.All(pair => RecipeDeposited.GetValueOrDefault(pair.Key, 0) >= pair.Value);
        return RecipeComplete;
    }

    private bool UpdateConstructionCompleteState()
    {
        ConstructionComplete = ConstructionProgress >= ConstructionRequired;
        return ConstructionComplete;
    }

    private static int[][] BuildScaffoldOpenMap(int[][] targetOpenMap)
    {
        return targetOpenMap
            .Select(row => row.Select(cell => cell > 1 ? cell : 0).ToArray())
            .ToArray();
    }

    private static int BuildConstructionRequirement(Dictionary<string, int> recipeRequired)
    {
        var requiredWork = 0;
        foreach (var (resourceType, amount) in recipeRequired)
        {
            var oreIndex = Economy.OreType.GetOres()
                .Select((ore, index) => new { ore.Name, Index = index + 1 })
                .FirstOrDefault(entry => string.Equals(entry.Name, resourceType, StringComparison.Ordinal))?.Index ?? 1;
            requiredWork += amount * oreIndex;
        }

        return System.Math.Max(1, requiredWork);
    }
}
