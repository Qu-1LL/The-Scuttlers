using TriloGame.Game.Core.Economy;

namespace TriloGame.Game.Core.Buildings;

public interface IStorage
{
    int Capacity { get; }

    // Return the current per-resource inventory snapshot.
    IReadOnlyDictionary<ResourceName, int> GetInventory();

    // Sum all stored resources across every entry in the inventory.
    int GetInventoryTotal();

    // Report how much storage capacity remains available.
    int GetInventorySpace();

    // Add as much of the requested resource as this storage can accept.
    int Deposit(ResourceName resourceType, int amount);

    // Remove up to the requested amount of the selected resource.
    int Withdraw(ResourceName resourceType, int amount);
}
