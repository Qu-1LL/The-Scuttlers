namespace TriloGame.Game.Core.Economy;

public interface IResourceStorage
{
    int Capacity { get; }

    IReadOnlyDictionary<string, int> GetStoredResources();

    int GetInventoryTotal();

    int GetInventorySpace();

    int Deposit(string resourceType, int amount);

    int Withdraw(string resourceType, int amount);
}
