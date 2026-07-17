namespace TriloGame.Game.Core.Economy;

public interface IInventoryCarrier
{
    Inventory Inventory { get; }

    int InventoryCapacity { get; }

    bool HasInventory();

    int GetInventorySpace();

    int AddToInventory(ResourceName resourceType, int amount);

    int RemoveFromInventory(int amount);

    int RemoveFromInventory(ResourceName resourceType, int amount);

    void ClearInventory();
}
