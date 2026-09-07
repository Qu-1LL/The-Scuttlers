namespace TriloGame.Game.Core.Economy;

// Declares one shared processor buffer for resources with a matching classification.
public readonly record struct ProcessingResourceDefinition(
    string Label,
    string ClassificationKey,
    string ClassificationValue,
    int AmountPerProcess,
    int Capacity)
{
    public bool Matches(ResourceName resourceType)
    {
        return ItemCatalog.HasClassification(resourceType, ClassificationKey, ClassificationValue);
    }
}

public interface IProcessor
{
    IReadOnlyList<ProcessingResourceDefinition> InputDefinitions { get; }

    IReadOnlyList<ProcessingResourceDefinition> OutputDefinitions { get; }

    int ProcessingIntervalTicks { get; }

    IReadOnlyDictionary<ResourceName, int> GetInputResources();

    IReadOnlyDictionary<ResourceName, int> GetOutputResources();

    int GetInputAmount(ResourceName resourceType);

    int GetOutputAmount(ResourceName resourceType);

    int GetInputAmount(ProcessingResourceDefinition definition);

    int GetOutputAmount(ProcessingResourceDefinition definition);

    int GetInputCapacity(ResourceName resourceType);

    int GetOutputCapacity(ResourceName resourceType);

    int GetInputCapacity(ProcessingResourceDefinition definition);

    int GetOutputCapacity(ProcessingResourceDefinition definition);

    int GetInputSpace(ResourceName resourceType);

    int GetOutputSpace(ResourceName resourceType);

    int GetInputSpace(ProcessingResourceDefinition definition);

    int GetOutputSpace(ProcessingResourceDefinition definition);

    int DepositInput(ResourceName resourceType, int amount);

    int WithdrawOutput(ResourceName resourceType, int amount);
}

// Kept as a compatibility marker while callers transition to the classification-based processor contract.
public interface IProcessingBuilding : IProcessor
{
}
