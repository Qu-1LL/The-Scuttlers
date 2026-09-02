using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;

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

        ClearFarmerFoodSource();
        _farmerProcessingBuilding = null;
        return ranch.TryBeginGarageWait(this);
    }

    // Stored food is the farmer's fallback after ranch and algae-farm work are unavailable.
    private bool TryNavigateStoredFood()
    {
        var source = FindStoredFoodSource();
        if (!source.HasValue)
        {
            ClearFarmerFoodSource();
            return QueueFarmerState(FarmerState.WaitForFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        if (!TryAssignFarmerFoodSource(source.Value.Source, source.Value.ResourceType))
        {
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        _farmerFoodSource = source.Value.Source;
        _farmerFoodResource = source.Value.ResourceType;
        if (IsAtResourceStorageSource(source.Value.Source))
        {
            return WithdrawStoredFood(source.Value.Source, source.Value.ResourceType);
        }

        if (!NavigateToBuilding(source.Value.Source))
        {
            ClearFarmerFoodSource();
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoReachablePath, result: false);
        }

        return QueueFarmerState(FarmerState.MoveToStoredFood);
    }

    private bool AdvanceFarmerMoveToStoredFood()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (HasInventory())
        {
            return GetCarriedFarmerFoodResource().HasValue
                ? AdvanceFarmerMoveToQueen()
                : QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.InventoryBlocked, result: false);
        }

        // A newly available algae farm preempts this lowest-priority food delivery.
        if (SelectAlgaeFarm() is not null)
        {
            ClearFarmerFoodSource();
            return AdvanceFarmerSelectFarm();
        }

        var source = _farmerFoodSource;
        var resourceType = _farmerFoodResource;
        if (source is null ||
            !resourceType.HasValue ||
            source.Cave != Cave ||
            GetFarmerFoodSourceAmount(source, resourceType.Value) <= 0)
        {
            ClearFarmerFoodSource();
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        if (IsAtResourceStorageSource(source))
        {
            return WithdrawStoredFood(source, resourceType.Value);
        }

        if (!NavigateToBuilding(source))
        {
            ClearFarmerFoodSource();
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoReachablePath, result: false);
        }

        return QueueFarmerState(FarmerState.MoveToStoredFood);
    }

    // Prefer the most nutritious stored food before selecting lower-value alternatives.
    private (Building Source, ResourceName ResourceType)? FindStoredFoodSource()
    {
        var pieSource = FindStoredFoodSource(ResourceName.AlgaePie);
        if (pieSource is not null)
        {
            return (pieSource, ResourceName.AlgaePie);
        }

        var mealSource = FindStoredFoodSource(ResourceName.AlgaeMeal);
        if (mealSource is not null)
        {
            return (mealSource, ResourceName.AlgaeMeal);
        }

        return FindStoredFoodSource(ResourceName.Algae) is { } algaeSource
            ? (algaeSource, ResourceName.Algae)
            : null;
    }

    private Building? FindStoredFoodSource(ResourceName resourceType)
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
                GetFarmerFoodSourceAmount(building, resourceType) <= 0 ||
                !CanAssignFarmerFoodSource(building, resourceType) ||
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

    private bool WithdrawStoredFood(Building source, ResourceName resourceType)
    {
        var requested = System.Math.Min(GetInventorySpace(), GetFarmerFoodSourceAmount(source, resourceType));
        var withdrawn = WithdrawFarmerFood(source, resourceType, requested);
        if (withdrawn <= 0)
        {
            ClearFarmerFoodSource();
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        var accepted = AddToInventory(resourceType, withdrawn);
        if (accepted != withdrawn)
        {
            throw new InvalidOperationException("Farmer food withdrawal exceeded the available carry capacity.");
        }

        ClearFarmerFoodSource();
        if (accepted <= 0)
        {
            return QueueFarmerState(FarmerState.SelectFarm, WorkerRoleFailureReason.NoWork, result: false);
        }

        return AdvanceFarmerMoveToQueen();
    }

    // Processing buildings expose only their outputs as farmer food sources, never their inputs.
    private static int GetFarmerFoodSourceAmount(Building source, ResourceName resourceType)
    {
        return source switch
        {
            IProcessingBuilding processing => processing.GetOutputAmount(resourceType),
            IResourceStorage storage => storage.GetStoredAmount(resourceType),
            _ => 0
        };
    }

    private static int WithdrawFarmerFood(Building source, ResourceName resourceType, int amount)
    {
        return source switch
        {
            IProcessingBuilding processing => processing.WithdrawOutput(resourceType, amount),
            IResourceStorage storage => storage.Withdraw(resourceType, amount),
            _ => 0
        };
    }

    private bool CanAssignFarmerFoodSource(Building source, ResourceName resourceType)
    {
        return source is not IProcessingOutputAssignmentBuilding processing ||
               processing.CanAssignOutputCollector(this, resourceType);
    }

    private bool TryAssignFarmerFoodSource(Building source, ResourceName resourceType)
    {
        return source is not IProcessingOutputAssignmentBuilding processing ||
               processing.TryAssignOutputCollector(this, resourceType);
    }

    // Release the processor's reserved output load whenever this farmer changes food targets.
    private void ClearFarmerFoodSource()
    {
        if (_farmerFoodSource is IProcessingOutputAssignmentBuilding processing)
        {
            processing.ReleaseOutputCollector(this);
        }

        _farmerFoodSource = null;
        _farmerFoodResource = null;
    }

    // Route food into the least-filled compatible processor before feeding the queen.
    private bool TryNavigateProcessingBuilding(ResourceName resourceType)
    {
        var processingBuilding = FindAvailableProcessingBuilding(resourceType);
        if (processingBuilding is null)
        {
            _farmerProcessingBuilding = null;
            return false;
        }

        _farmerProcessingBuilding = processingBuilding;
        if (IsAtBuildingInteractionTile(processingBuilding))
        {
            return DepositFarmerResourceAtProcessingBuilding(processingBuilding, resourceType);
        }

        if (!NavigateToBuilding(processingBuilding))
        {
            _farmerProcessingBuilding = null;
            return false;
        }

        return QueueFarmerState(FarmerState.MoveToProcessingBuilding);
    }

    private bool AdvanceFarmerMoveToProcessingBuilding()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var resourceType = GetCarriedFarmerFoodResource();
        if (!resourceType.HasValue)
        {
            _farmerProcessingBuilding = null;
            return AdvanceFarmerMoveToQueen();
        }

        var processingBuilding = _farmerProcessingBuilding;
        if (processingBuilding is not IProcessingBuilding processing ||
            processingBuilding.Cave != Cave ||
            processing.GetInputSpace(resourceType.Value) <= 0)
        {
            _farmerProcessingBuilding = null;
            return AdvanceFarmerMoveToQueen();
        }

        if (IsAtBuildingInteractionTile(processingBuilding))
        {
            return DepositFarmerResourceAtProcessingBuilding(processingBuilding, resourceType.Value);
        }

        if (!NavigateToBuilding(processingBuilding))
        {
            _farmerProcessingBuilding = null;
            return AdvanceFarmerMoveToQueen();
        }

        return QueueFarmerState(FarmerState.MoveToProcessingBuilding);
    }

    private Building? FindAvailableProcessingBuilding(ResourceName resourceType)
    {
        if (Cave is null)
        {
            return null;
        }

        Building? best = null;
        var bestInputAmount = int.MaxValue;
        var bestDistance = int.MaxValue;
        var bestKey = string.Empty;
        var buildings = Cave.GetBuildingList();
        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (building is not IProcessingBuilding processing ||
                processing.GetInputSpace(resourceType) <= 0 ||
                !CanReachResourceStorage(building))
            {
                continue;
            }

            var inputAmount = processing.GetInputAmount(resourceType);
            var distance = Cave.GetBuildingBfsFieldValue(building, Location);
            var key = GetOwnedBuildingSelectionKey(building);
            if (best is null ||
                inputAmount < bestInputAmount ||
                (inputAmount == bestInputAmount && distance < bestDistance) ||
                (inputAmount == bestInputAmount && distance == bestDistance && string.CompareOrdinal(key, bestKey) < 0))
            {
                best = building;
                bestInputAmount = inputAmount;
                bestDistance = distance;
                bestKey = key;
            }
        }

        return best;
    }

    private bool DepositFarmerResourceAtProcessingBuilding(Building processingBuilding, ResourceName resourceType)
    {
        var processing = (IProcessingBuilding)processingBuilding;
        var accepted = processing.DepositInput(resourceType, Inventory.GetAmount(resourceType));
        _farmerProcessingBuilding = null;
        if (accepted <= 0)
        {
            return AdvanceFarmerMoveToQueen();
        }

        RemoveFromInventory(resourceType, accepted);
        SetActivity(CreatureActivity.Depositing);
        Session.RequestAudioCue(
            GameAudioCue.CreatureDeposit,
            Position,
            AudioCueRequest.CreatureEffectFootprintTiles);

        return GetCarriedFarmerFoodResource().HasValue
            ? AdvanceFarmerMoveToQueen()
            : AdvanceFarmerSelectFarm();
    }
}
