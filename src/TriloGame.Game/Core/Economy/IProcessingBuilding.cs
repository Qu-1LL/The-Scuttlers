namespace TriloGame.Game.Core.Economy;

// Declares one resource's batch requirement and isolated capacity for a processor.
public readonly record struct ProcessingResourceDefinition(ResourceName ResourceType, int AmountPerProcess, int Capacity);

public interface IProcessingBuilding
{
    IReadOnlyList<ProcessingResourceDefinition> InputDefinitions { get; }

    IReadOnlyList<ProcessingResourceDefinition> OutputDefinitions { get; }

    int ProcessingIntervalTicks { get; }

    IReadOnlyDictionary<ResourceName, int> GetInputResources();

    IReadOnlyDictionary<ResourceName, int> GetOutputResources();

    int GetInputAmount(ResourceName resourceType);

    int GetOutputAmount(ResourceName resourceType);

    int GetInputCapacity(ResourceName resourceType);

    int GetOutputCapacity(ResourceName resourceType);

    int GetInputSpace(ResourceName resourceType);

    int GetOutputSpace(ResourceName resourceType);

    int DepositInput(ResourceName resourceType, int amount);

    int WithdrawOutput(ResourceName resourceType, int amount);
}
