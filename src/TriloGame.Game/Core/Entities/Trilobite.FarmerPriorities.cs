using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Interaction;

namespace TriloGame.Game.Core.Entities;

public sealed partial class Trilobite
{
    // Ranch work is the farmer's top priority and owns its waiting/plow lifecycle.
    private bool AdvanceFarmerRanch(Ranch ranch)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (!ranch.IsAssigned(this))
        {
            ReleaseAssignedBuilding();
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.TargetInvalid, result: false);
        }

        _farmerAlgaeStorageSource = null;
        ReleaseInteractionReservation();
        return ranch.TryBeginGarageWait(this);
    }

    // Stored algae is the farmer's fallback after ranch and algae-farm work are unavailable.
    private bool TryNavigateStoredAlgae()
    {
        var source = FindStoredAlgaeSource();
        if (source is null)
        {
            _farmerAlgaeStorageSource = null;
            return QueueFarmerState(FarmerState.WaitForFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        _farmerAlgaeStorageSource = source;
        if (IsAtResourceStorageSource(source))
        {
            return WithdrawStoredAlgae(source);
        }

        if (!NavigateToInteractionZone(source, InteractionZonePurpose.ResourceTransfer))
        {
            _farmerAlgaeStorageSource = null;
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoReachablePath, result: false);
        }

        return QueueFarmerState(FarmerState.MoveToStoredAlgae);
    }

    private bool AdvanceFarmerMoveToStoredAlgae()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (HasInventory())
        {
            return Inventory.Type == ResourceName.Algae
                ? AdvanceFarmerMoveToQueen()
                : QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.InventoryBlocked, result: false);
        }

        // A newly available algae farm preempts this lowest-priority storage delivery.
        if (SelectAlgaeFarm() is not null)
        {
            _farmerAlgaeStorageSource = null;
            ReleaseInteractionReservation();
            return AdvanceFarmerSelectFarm();
        }

        var source = _farmerAlgaeStorageSource;
        if (source is null ||
            source.Cave != Cave ||
            source is not IResourceStorage storage ||
            storage.GetStoredAmount(ResourceName.Algae) <= 0)
        {
            _farmerAlgaeStorageSource = null;
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        if (IsAtResourceStorageSource(source))
        {
            return WithdrawStoredAlgae(source);
        }

        if (!NavigateToInteractionZone(source, InteractionZonePurpose.ResourceTransfer))
        {
            _farmerAlgaeStorageSource = null;
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoReachablePath, result: false);
        }

        return QueueFarmerState(FarmerState.MoveToStoredAlgae);
    }

    private Building? FindStoredAlgaeSource()
    {
        if (Cave is null)
        {
            return null;
        }

        Building? best = null;
        var bestDistance = int.MaxValue;
        var bestKey = string.Empty;
        var buildings = Cave.GetBuildingList();
        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (building is MiningPost ||
                building is not IResourceStorage storage ||
                storage.GetStoredAmount(ResourceName.Algae) <= 0 ||
                !CanReachResourceStorage(building))
            {
                continue;
            }

            var distance = Cave.GetBuildingBfsFieldValue(building, Location);
            var key = GetOwnedBuildingSelectionKey(building);
            if (best is null ||
                distance < bestDistance ||
                (distance == bestDistance && string.CompareOrdinal(key, bestKey) < 0))
            {
                best = building;
                bestDistance = distance;
                bestKey = key;
            }
        }

        return best;
    }

    private bool WithdrawStoredAlgae(Building source)
    {
        if (source is not IResourceStorage storage)
        {
            _farmerAlgaeStorageSource = null;
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.TargetInvalid, result: false);
        }

        var requested = System.Math.Min(GetInventorySpace(), storage.GetStoredAmount(ResourceName.Algae));
        var withdrawn = storage.Withdraw(ResourceName.Algae, requested);
        var accepted = AddToInventory(ResourceName.Algae, withdrawn);
        if (accepted < withdrawn)
        {
            storage.Deposit(ResourceName.Algae, withdrawn - accepted);
        }

        _farmerAlgaeStorageSource = null;
        ReleaseInteractionReservation();
        if (accepted <= 0)
        {
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        return AdvanceFarmerMoveToQueen();
    }
}
