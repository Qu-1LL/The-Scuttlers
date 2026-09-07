using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class GrindingMill : Building, IProcessor, IProcessingOutputAssignmentBuilding
{
    private const int ResourceCapacity = 500;
    private static readonly IReadOnlyList<ProcessingResourceDefinition> InputResourceDefinitions =
    [
        ProcessingResourceDefinitions.ForFoodType(
            "RAW PLANTS",
            ResourceClassificationValues.Raw,
            amountPerProcess: 1,
            capacity: ResourceCapacity)
    ];
    private static readonly IReadOnlyList<ProcessingResourceDefinition> OutputResourceDefinitions =
    [
        ProcessingResourceDefinitions.ForFoodType(
            "MEALS",
            ResourceClassificationValues.Meal,
            amountPerProcess: 1,
            capacity: ResourceCapacity)
    ];
    private readonly Dictionary<ResourceName, int> _inputs = [];
    private readonly Dictionary<ResourceName, int> _outputs = [];
    private readonly Dictionary<Trilobite, ResourceName> _outputCollectors = [];

    public GrindingMill(GameSession session)
        : base("Grinding Mill", new GridPoint(2, 3), [[0, 0], [0, 0], [0, 0]], session, false)
    {
        TextureKey = "GrindingMill";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Description = "Grinds any raw plant into its matching meal. Holds 500 raw plants and 500 meals.";
    }

    public IReadOnlyList<ProcessingResourceDefinition> InputDefinitions => InputResourceDefinitions;

    public IReadOnlyList<ProcessingResourceDefinition> OutputDefinitions => OutputResourceDefinitions;

    public int ProcessingIntervalTicks => 5;

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationSeedMode NavigationSeedMode => BuildingNavigationSeedMode.AdjacentExteriorPassableTiles;

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

    // Convert one complete plant batch only when its matching meal has room.
    public override int Tick(World.Cave cave)
    {
        if (Session.TickCount % ProcessingIntervalTicks != 0 ||
            !TryGetProcessableBatch(out var inputResource, out var outputResource))
        {
            return 0;
        }

        ConsumeInputBatch(inputResource);
        ProduceOutputBatch(outputResource);
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

    private bool TryGetProcessableBatch(out ResourceName inputResource, out ResourceName outputResource)
    {
        inputResource = default;
        outputResource = default;
        var resources = ItemCatalog.GetStockpileOrder();
        for (var index = 0; index < resources.Count; index++)
        {
            var candidate = resources[index].Resource;
            if (!TryGetDefinition(InputResourceDefinitions, candidate, out var input) ||
                GetInputAmount(candidate) < input.AmountPerProcess ||
                !ItemCatalog.TryGetRelatedPlantResource(candidate, ResourceClassificationValues.Meal, out var matchingMeal) ||
                GetOutputSpace(matchingMeal) < input.AmountPerProcess)
            {
                continue;
            }

            inputResource = candidate;
            outputResource = matchingMeal;
            return true;
        }

        return false;
    }

    private void ConsumeInputBatch(ResourceName inputResource)
    {
        _inputs[inputResource]--;
        EmitResourceChanged(inputResource, -1);
    }

    private void ProduceOutputBatch(ResourceName outputResource)
    {
        _outputs.TryAdd(outputResource, 0);
        _outputs[outputResource]++;
        EmitResourceChanged(outputResource, 1);
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
