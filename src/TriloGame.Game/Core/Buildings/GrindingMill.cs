using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class GrindingMill : Building, IProcessingBuilding, IProcessingOutputAssignmentBuilding
{
    private const int ResourceCapacity = 500;
    private static readonly IReadOnlyList<ProcessingResourceDefinition> InputResourceDefinitions =
    [
        new(ResourceName.Algae, AmountPerProcess: 1, Capacity: ResourceCapacity)
    ];
    private static readonly IReadOnlyList<ProcessingResourceDefinition> OutputResourceDefinitions =
    [
        new(ResourceName.AlgaeMeal, AmountPerProcess: 1, Capacity: ResourceCapacity)
    ];
    private readonly Dictionary<ResourceName, int> _inputs = [];
    private readonly Dictionary<ResourceName, int> _outputs = [];
    private readonly Dictionary<Trilobite, ResourceName> _outputCollectors = [];

    public GrindingMill(GameSession session)
        : base("Grinding Mill", new GridPoint(2, 3), [[0, 0], [0, 0], [0, 0]], session, false)
    {
        TextureKey = "GrindingMill";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Description = "Processes algae into Algae Meal. Holds 500 algae input and 500 Algae Meal output.";
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

    public int GetInputCapacity(ResourceName resourceType) => GetCapacity(InputResourceDefinitions, resourceType);

    public int GetOutputCapacity(ResourceName resourceType) => GetCapacity(OutputResourceDefinitions, resourceType);

    public int GetInputSpace(ResourceName resourceType)
    {
        return System.Math.Max(0, GetInputCapacity(resourceType) - GetInputAmount(resourceType));
    }

    public int GetOutputSpace(ResourceName resourceType)
    {
        return System.Math.Max(0, GetOutputCapacity(resourceType) - GetOutputAmount(resourceType));
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

    // Only reserve a new collector when the current output can fill every collector's full load.
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

        var requiredOutput = GetAssignedOutputCarryingCapacity(resourceType) + collector.InventoryCapacity;
        return GetOutputAmount(resourceType) >= requiredOutput;
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
        var accepted = System.Math.Min(GetInputSpace(resourceType), amount);
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

    // Convert one complete input batch only when every output batch has reserved capacity.
    public override int Tick(World.Cave cave)
    {
        if (Session.TickCount % ProcessingIntervalTicks != 0 || !CanProcessBatch())
        {
            return 0;
        }

        ConsumeInputBatch();
        ProduceOutputBatch();
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

    private bool CanProcessBatch()
    {
        for (var index = 0; index < InputResourceDefinitions.Count; index++)
        {
            var input = InputResourceDefinitions[index];
            if (GetInputAmount(input.ResourceType) < input.AmountPerProcess)
            {
                return false;
            }
        }

        for (var index = 0; index < OutputResourceDefinitions.Count; index++)
        {
            var output = OutputResourceDefinitions[index];
            if (GetOutputSpace(output.ResourceType) < output.AmountPerProcess)
            {
                return false;
            }
        }

        return true;
    }

    private void ConsumeInputBatch()
    {
        for (var index = 0; index < InputResourceDefinitions.Count; index++)
        {
            var input = InputResourceDefinitions[index];
            _inputs[input.ResourceType] -= input.AmountPerProcess;
            EmitResourceChanged(input.ResourceType, -input.AmountPerProcess);
        }
    }

    private void ProduceOutputBatch()
    {
        for (var index = 0; index < OutputResourceDefinitions.Count; index++)
        {
            var output = OutputResourceDefinitions[index];
            _outputs.TryAdd(output.ResourceType, 0);
            _outputs[output.ResourceType] += output.AmountPerProcess;
            EmitResourceChanged(output.ResourceType, output.AmountPerProcess);
        }
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

    private static int GetCapacity(IReadOnlyList<ProcessingResourceDefinition> definitions, ResourceName resourceType)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            if (definitions[index].ResourceType == resourceType)
            {
                return definitions[index].Capacity;
            }
        }

        return 0;
    }

    private static bool HasOutputDefinition(ResourceName resourceType)
    {
        for (var index = 0; index < OutputResourceDefinitions.Count; index++)
        {
            if (OutputResourceDefinitions[index].ResourceType == resourceType)
            {
                return true;
            }
        }

        return false;
    }

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
