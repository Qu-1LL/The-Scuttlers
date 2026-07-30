using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed partial class Trilobite
{
    private GridPoint? _farmerHarvestTarget;

    private bool AdvanceFarmerRole()
    {
        return FarmerState switch
        {
            FarmerState.Idle => AdvanceFarmerSelectFarm(),
            FarmerState.SelectFarm => AdvanceFarmerSelectFarm(),
            FarmerState.MoveToFarmSlot => AdvanceFarmerMoveToFarmSlot(),
            FarmerState.Harvest => _farmerHarvestTarget.HasValue
                ? AdvanceFarmerHarvest(_farmerHarvestTarget.Value)
                : QueueFarmerState(FarmerState.MoveToFarmSlot, WorkerRoleFailureReason.TargetInvalid, result: false),
            FarmerState.MoveToQueen => AdvanceFarmerMoveToQueen(),
            FarmerState.FeedQueen => AdvanceFarmerFeedQueen(),
            FarmerState.WaitForFarm => QueueFarmerState(FarmerState.Idle, WorkerRoleFailureReason.NoWork, result: false),
            _ => QueueFarmerState(FarmerState.Idle, WorkerRoleFailureReason.TargetInvalid, result: false)
        };
    }

    public int FeedQueenAlgae(Queen queen)
    {
        if (!HasInventory() || Inventory.Type != ResourceName.Algae)
        {
            return 0;
        }

        var result = queen.FeedAlgae(Inventory.Amount, this, Cave);
        if (result.Accepted <= 0)
        {
            return 0;
        }

        RemoveFromInventory(result.Accepted);
        return result.Accepted;
    }

    public List<AlgaeFarm> GetAlgaeFarmPriorityList()
    {
        return EnumerateAlgaeFarmCandidates(GetAssignedAlgaeFarm())
            .Where(CanReachAlgaeFarm)
            .ToList();
    }

    private bool CanSearchForAlgaeFarm(AlgaeFarm? preferredFarm = null)
    {
        return Cave is not null &&
               ((preferredFarm is not null && preferredFarm.HasAssignmentSlot(this)) || Cave.HasOpenAlgaeFarms);
    }

    private bool IsSelectableAlgaeFarm(AlgaeFarm? farm, ISet<AlgaeFarm>? excludedFarms = null)
    {
        return farm is not null &&
               farm.Location is not null &&
               farm.TileArray.Count > 0 &&
               farm.HasAssignmentSlot(this) &&
               excludedFarms?.Contains(farm) != true;
    }

    private bool CanReachAlgaeFarm(AlgaeFarm farm)
    {
        return Cave is not null &&
               (farm.IsLocationOnFarm(Location) ||
                ReferenceEquals(Cave.GetNearestAlgaeFarm(Location), farm) ||
                Cave.GetBuildingBfsFieldValue(farm, Location) != int.MaxValue);
    }

    private IEnumerable<AlgaeFarm> EnumerateAlgaeFarmCandidates(AlgaeFarm? preferredFarm = null, ISet<AlgaeFarm>? excludedFarms = null)
    {
        if (Cave is null)
        {
            yield break;
        }

        excludedFarms ??= new HashSet<AlgaeFarm>();
        var visited = new HashSet<AlgaeFarm>();

        if (IsSelectableAlgaeFarm(preferredFarm, excludedFarms) && visited.Add(preferredFarm!))
        {
            yield return preferredFarm!;
        }

        if (!CanSearchForAlgaeFarm(preferredFarm))
        {
            yield break;
        }

        var nearestFarm = Cave.GetNearestAlgaeFarm(Location);
        var queue = new Queue<AlgaeFarm>();
        if (IsSelectableAlgaeFarm(nearestFarm, excludedFarms) && visited.Add(nearestFarm!))
        {
            queue.Enqueue(nearestFarm!);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var neighbor in Cave.GetAdjacentAlgaeFarms(current))
            {
                if (IsSelectableAlgaeFarm(neighbor, excludedFarms) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (visited.Count > 0)
        {
            yield break;
        }

        foreach (var farm in GetAlgaeFarms()
                     .Where(farm => IsSelectableAlgaeFarm(farm, excludedFarms))
                     .OrderBy(farm => GetOwnedBuildingSelectionKey(farm), StringComparer.Ordinal))
        {
            if (visited.Add(farm))
            {
                yield return farm;
            }
        }
    }

    internal AlgaeFarm? SelectAlgaeFarm(AlgaeFarm? preferredFarm = null, ISet<AlgaeFarm>? excludedFarms = null)
    {
        foreach (var farm in EnumerateAlgaeFarmCandidates(preferredFarm, excludedFarms))
        {
            if (CanReachAlgaeFarm(farm))
            {
                return farm;
            }
        }

        return null;
    }

    public bool TryNavigateAlgaeFarms(ISet<AlgaeFarm>? excludedFarms = null)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        excludedFarms ??= new HashSet<AlgaeFarm>();
        var farm = SelectAlgaeFarm(GetAssignedAlgaeFarm(), excludedFarms);
        if (farm is null)
        {
            ReleaseAssignedBuilding();
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        SetAssignedBuilding(farm);
        if (!farm.Assign(this))
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            return TryNavigateAlgaeFarms(excludedFarms);
        }

        if (ReservedZone is { Purpose: InteractionZonePurpose.Work } reservedWorkZone &&
            ReferenceEquals(reservedWorkZone.Owner, farm) &&
            IsAtReservedInteractionSlot())
        {
            return AdvanceFarmerMoveToFarmSlot();
        }

        if (!NavigateToInteractionZone(farm, InteractionZonePurpose.Work))
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            return TryNavigateAlgaeFarms(excludedFarms);
        }

        QueueFarmerState(FarmerState.MoveToFarmSlot);
        return true;
    }

    private bool AdvanceFarmerSelectFarm()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (HasInventory())
        {
            if (Inventory.Type == ResourceName.Algae)
            {
                return AdvanceFarmerMoveToQueen();
            }

            ClearInventory();
        }

        if (SelectAlgaeFarm(GetAssignedAlgaeFarm()) is null)
        {
            ReleaseAssignedBuilding();
            return false;
        }

        return TryNavigateAlgaeFarms();
    }

    private bool AdvanceFarmerMoveToFarmSlot()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var farm = GetAssignedAlgaeFarm();
        if (farm is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.Work } workZone ||
            !ReferenceEquals(workZone.Owner, farm) ||
            !IsAtReservedInteractionSlot())
        {
            if (!NavigateToInteractionZone(farm, InteractionZonePurpose.Work))
            {
                ReleaseAssignedBuilding();
                QueueFarmerState(FarmerState.SelectFarm);
                return false;
            }

            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return true;
        }

        var nextLocation = farm.GetNextTraversalLocation(Location);
        if (nextLocation is null)
        {
            ReleaseAssignedBuilding();
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (!TryMoveInteractionReservation(nextLocation.Value))
        {
            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return false;
        }

        QueueFarmerHarvest(nextLocation.Value);
        return true;
    }

    private bool AdvanceFarmerHarvest(GridPoint nextLocation)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var farm = GetAssignedAlgaeFarm();
        if (farm is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (!IsAtReservedInteractionSlot() || CurrentCell != nextLocation)
        {
            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return false;
        }

        SetActivity(CreatureActivity.Working);
        if (!farm.TryHarvest(this))
        {
            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return true;
        }

        ClearTaskQueue();
        return AdvanceFarmerMoveToQueen();
    }

    private bool AdvanceFarmerMoveToQueen()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (!HasInventory() || Inventory.Type != ResourceName.Algae)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (queen.CanBeFedBy(this))
        {
            return AdvanceFarmerFeedQueen();
        }

        ClearTaskQueue();
        if (!NavigateToInteractionZone(queen, InteractionZonePurpose.Feeding, clearExisting: false))
        {
            QueueFarmerState(FarmerState.MoveToQueen);
            return false;
        }

        QueueFarmerState(FarmerState.FeedQueen);
        return true;
    }

    private bool AdvanceFarmerMoveToQueenStep(GridPoint nextLocation)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var moved = PerformMove(nextLocation);
        if (!moved)
        {
            return AdvanceFarmerMoveToQueen();
        }

        return true;
    }

    private bool AdvanceFarmerFeedQueen()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (!queen.CanBeFedBy(this))
        {
            return AdvanceFarmerMoveToQueen();
        }

        SetActivity(CreatureActivity.Feeding);
        var fed = FeedQueenAlgae(queen);
        if (fed <= 0)
        {
            QueueFarmerState(FarmerState.MoveToQueen);
            return false;
        }

        return AdvanceFarmerSelectFarm();
    }
}
