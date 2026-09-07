using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Bakery : Building, IProcessor, IProcessingOutputAssignmentBuilding
{
    private const int ResourceCapacity = 250;
    private static readonly IReadOnlyList<ProcessingResourceDefinition> InputResourceDefinitions =
    [
        ProcessingResourceDefinitions.ForFoodType(
            "RAW PLANTS",
            ResourceClassificationValues.Raw,
            amountPerProcess: 1,
            capacity: ResourceCapacity),
        ProcessingResourceDefinitions.ForFoodType(
            "MEALS",
            ResourceClassificationValues.Meal,
            amountPerProcess: 1,
            capacity: ResourceCapacity)
    ];
    private static readonly IReadOnlyList<ProcessingResourceDefinition> OutputResourceDefinitions =
    [
        ProcessingResourceDefinitions.ForFoodType(
            "PIES",
            ResourceClassificationValues.Pie,
            amountPerProcess: 1,
            capacity: ResourceCapacity)
    ];
    private readonly Dictionary<ResourceName, int> _inputs = [];
    private readonly Dictionary<ResourceName, int> _outputs = [];
    private readonly Dictionary<Trilobite, ResourceName> _outputCollectors = [];

    public Bakery(GameSession session)
        : base("Bakery", new GridPoint(2, 3), [[1, 0], [1, 0], [0, 0]], session, hasStation: true)
    {
        TextureKey = "Bakery";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Description = "Bakes each raw plant with its matching meal into a pie. Holds 250 raw plants, 250 meals, and 250 pies.";
    }

    public IReadOnlyList<ProcessingResourceDefinition> InputDefinitions => InputResourceDefinitions;

    public IReadOnlyList<ProcessingResourceDefinition> OutputDefinitions => OutputResourceDefinitions;

    public int ProcessingIntervalTicks => 5;

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Asynchronous;

    public IReadOnlyDictionary<ResourceName, int> GetInputResources() => _inputs;

    public IReadOnlyDictionary<ResourceName, int> GetOutputResources() => _outputs;

    public int GetInputAmount(ResourceName resourceType) => _inputs.GetValueOrDefault(resourceType, 0);

    public int GetOutputAmount(ResourceName resourceType) => _outputs.GetValueOrDefault(resourceType, 0);

    public int GetInputAmount(ProcessingResourceDefinition definition) => GetClassifiedAmount(_inputs, definition);

    public int GetOutputAmount(ProcessingResourceDefinition definition) => GetClassifiedAmount(_outputs, definition);

    public int GetInputCapacity(ResourceName resourceType) =>
        TryGetDefinition(InputResourceDefinitions, resourceType, out var definition) ? definition.Capacity : 0;

    public int GetOutputCapacity(ResourceName resourceType) =>
        TryGetDefinition(OutputResourceDefinitions, resourceType, out var definition) ? definition.Capacity : 0;

    public int GetInputCapacity(ProcessingResourceDefinition definition) =>
        ContainsDefinition(InputResourceDefinitions, definition) ? definition.Capacity : 0;

    public int GetOutputCapacity(ProcessingResourceDefinition definition) =>
        ContainsDefinition(OutputResourceDefinitions, definition) ? definition.Capacity : 0;

    public int GetInputSpace(ResourceName resourceType) =>
        TryGetDefinition(InputResourceDefinitions, resourceType, out var definition) ? GetInputSpace(definition) : 0;

    public int GetOutputSpace(ResourceName resourceType) =>
        TryGetDefinition(OutputResourceDefinitions, resourceType, out var definition) ? GetOutputSpace(definition) : 0;

    public int GetInputSpace(ProcessingResourceDefinition definition)
    {
        return System.Math.Max(0, GetInputCapacity(definition) - GetInputAmount(definition));
    }

    public int GetOutputSpace(ProcessingResourceDefinition definition)
    {
        return System.Math.Max(0, GetOutputCapacity(definition) - GetOutputAmount(definition));
    }

    public int GetOutputCollectorCount(ResourceName resourceType)
    {
        var count = 0;
        foreach (var assignment in _outputCollectors)
        {
            if (assignment.Value == resourceType)
            {
                count++;
            }
        }

        return count;
    }

    public int GetAssignedOutputCarryingCapacity(ResourceName resourceType)
    {
        var capacity = 0;
        foreach (var assignment in _outputCollectors)
        {
            if (assignment.Value == resourceType)
            {
                capacity += assignment.Key.InventoryCapacity;
            }
        }

        return capacity;
    }

    // Reserve one collector per output resource as soon as food is available.
    public bool CanAssignOutputCollector(Trilobite collector, ResourceName resourceType)
    {
        if (!HasOutputDefinition(resourceType))
        {
            return false;
        }

        if (_outputCollectors.TryGetValue(collector, out var assignedResource))
        {
            return assignedResource == resourceType;
        }

        return GetOutputAmount(resourceType) > 0 && GetOutputCollectorCount(resourceType) == 0;
    }

    public bool TryAssignOutputCollector(Trilobite collector, ResourceName resourceType)
    {
        if (!CanAssignOutputCollector(collector, resourceType))
        {
            return false;
        }

        if (_outputCollectors.ContainsKey(collector))
        {
            return true;
        }

        _outputCollectors.Add(collector, resourceType);
        TrackCreature(collector);
        return true;
    }

    public bool ReleaseOutputCollector(Trilobite collector)
    {
        if (!_outputCollectors.Remove(collector))
        {
            return false;
        }

        UntrackCreature(collector);
        return true;
    }

    public int DepositInput(ResourceName resourceType, int amount)
    {
        if (!TryGetDefinition(InputResourceDefinitions, resourceType, out var definition))
        {
            return 0;
        }

        var accepted = System.Math.Min(GetInputSpace(definition), amount);
        if (accepted <= 0)
        {
            return 0;
        }

        _inputs.TryAdd(resourceType, 0);
        _inputs[resourceType] += accepted;
        EmitResourceChanged(resourceType, accepted);
        return accepted;
    }

    public int WithdrawOutput(ResourceName resourceType, int amount)
    {
        var taken = System.Math.Min(GetOutputAmount(resourceType), amount);
        if (taken <= 0)
        {
            return 0;
        }

        _outputs[resourceType] -= taken;
        EmitResourceChanged(resourceType, -taken);
        return taken;
    }

    // Match a raw plant to its own meal so mixed plant inputs can never produce the wrong pie.
    public override int Tick(World.Cave cave)
    {
        if (Session.TickCount % ProcessingIntervalTicks != 0 ||
            !TryGetProcessableBatch(out var plantResource, out var mealResource, out var pieResource))
        {
            return 0;
        }

        ConsumeInputBatch(plantResource, mealResource);
        ProduceOutputBatch(pieResource);
        return 1;
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        while (_outputCollectors.Count > 0)
        {
            using var collectors = _outputCollectors.GetEnumerator();
            collectors.MoveNext();
            ReleaseOutputCollector(collectors.Current.Key);
        }

        ClearResources(_inputs);
        ClearResources(_outputs);
        base.CleanupBeforeRemoval(source);
    }

    public override void TrackedCreatureDied(Creature creature)
    {
        if (creature is Trilobite collector)
        {
            ReleaseOutputCollector(collector);
        }
    }

    private bool TryGetProcessableBatch(
        out ResourceName plantResource,
        out ResourceName mealResource,
        out ResourceName pieResource)
    {
        plantResource = default;
        mealResource = default;
        pieResource = default;
        var resources = ItemCatalog.GetStockpileOrder();
        for (var index = 0; index < resources.Count; index++)
        {
            var candidate = resources[index].Resource;
            if (!TryGetDefinition(InputResourceDefinitions, candidate, out var plantInput) ||
                !ItemCatalog.HasClassification(candidate, ResourceClassificationKeys.FoodType, ResourceClassificationValues.Raw) ||
                GetInputAmount(candidate) < plantInput.AmountPerProcess ||
                !ItemCatalog.TryGetRelatedPlantResource(candidate, ResourceClassificationValues.Meal, out var matchingMeal) ||
                !TryGetDefinition(InputResourceDefinitions, matchingMeal, out var mealInput) ||
                GetInputAmount(matchingMeal) < mealInput.AmountPerProcess ||
                !ItemCatalog.TryGetRelatedPlantResource(candidate, ResourceClassificationValues.Pie, out var matchingPie) ||
                GetOutputSpace(matchingPie) < plantInput.AmountPerProcess)
            {
                continue;
            }

            plantResource = candidate;
            mealResource = matchingMeal;
            pieResource = matchingPie;
            return true;
        }

        return false;
    }

    private void ConsumeInputBatch(ResourceName plantResource, ResourceName mealResource)
    {
        _inputs[plantResource]--;
        EmitResourceChanged(plantResource, -1);
        _inputs[mealResource]--;
        EmitResourceChanged(mealResource, -1);
    }

    private void ProduceOutputBatch(ResourceName pieResource)
    {
        _outputs.TryAdd(pieResource, 0);
        _outputs[pieResource]++;
        EmitResourceChanged(pieResource, 1);
    }

    private void ClearResources(Dictionary<ResourceName, int> resources)
    {
        foreach (var pair in resources)
        {
            if (pair.Value > 0)
            {
                EmitResourceChanged(pair.Key, -pair.Value);
            }
        }

        resources.Clear();
    }

    private static int GetClassifiedAmount(
        IReadOnlyDictionary<ResourceName, int> resources,
        ProcessingResourceDefinition definition)
    {
        var amount = 0;
        foreach (var pair in resources)
        {
            if (pair.Value > 0 && definition.Matches(pair.Key))
            {
                amount += pair.Value;
            }
        }

        return amount;
    }

    private static bool ContainsDefinition(
        IReadOnlyList<ProcessingResourceDefinition> definitions,
        ProcessingResourceDefinition definition)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            if (definitions[index] == definition)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDefinition(
        IReadOnlyList<ProcessingResourceDefinition> definitions,
        ResourceName resourceType,
        out ProcessingResourceDefinition definition)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            if (definitions[index].Matches(resourceType))
            {
                definition = definitions[index];
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static bool HasOutputDefinition(ResourceName resourceType) =>
        TryGetDefinition(OutputResourceDefinitions, resourceType, out _);

    private void EmitResourceChanged(ResourceName resourceType, int resourceDelta)
    {
        if (resourceDelta == 0)
        {
            return;
        }

        Session.Emit(
            GameEvents.StorageInventoryChanged,
            new GameEventPayload(Cave, null, Location, null, resourceType, this, resourceDelta));
    }
}
