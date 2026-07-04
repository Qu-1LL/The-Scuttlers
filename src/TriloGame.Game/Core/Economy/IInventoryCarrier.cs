namespace TriloGame.Game.Core.Economy;

public interface IInventoryCarrier
{
    Inventory Inventory { get; }

    int InventoryCapacity { get; }

    bool HasInventory();

    int GetInventorySpace();

    int AddToInventory(string resourceType, int amount);

    int RemoveFromInventory(int amount);

    void ClearInventory();
}
