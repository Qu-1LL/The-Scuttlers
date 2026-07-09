namespace TriloGame.Game.Core.Economy;

public interface IResourceStorage
{
    int Capacity { get; }

    IReadOnlyDictionary<ResourceName, int> GetStoredResources();

    int GetStoredAmount(ResourceName resourceType);

    int GetStoredAmount(ResourceCategory resourceCategory);

    ResourceStorageMatch? FindStoredResource(ResourceRequirement requirement, int maxAmount);

    int GetInventoryTotal();

    int GetInventorySpace();

    int Deposit(ResourceName resourceType, int amount);

    int Withdraw(ResourceName resourceType, int amount);
}
