using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Silo : Building, IResourceStorage
{
    private readonly Dictionary<ResourceName, int> _inventory = [];
    private readonly HashSet<Silo> _adjacentSilos = [];
    private int _rebalanceScopeDepth;

    public Silo(GameSession session)
        : base("Silo", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Silo";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Capacity = 5000;
        Description = $"A high-capacity plant silo that stores up to {Capacity} raw plant resources and balances with adjacent silos.";
    }

    public int Capacity { get; }

    public IReadOnlyCollection<Silo> AdjacentSilos => _adjacentSilos;

    public IReadOnlyDictionary<ResourceName, int> GetInventory() => _inventory;

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationSeedMode NavigationSeedMode => BuildingNavigationSeedMode.AdjacentExteriorPassableTiles;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Asynchronous;

    public IReadOnlyDictionary<ResourceName, int> GetStoredResources() => _inventory;

    public int GetStoredAmount(ResourceName resourceType) => _inventory.GetValueOrDefault(resourceType, 0);

    public int GetStoredAmount(ResourceCategory resourceCategory)
    {
        return ResourceInventoryHelper.GetStoredAmount(resourceCategory, GetStoredAmount);
    }

    public ResourceStorageMatch? FindStoredResource(ResourceRequirement requirement, int maxAmount)
    {
        return ResourceInventoryHelper.FindStoredResource(requirement, maxAmount, GetStoredAmount);
    }

    public int GetInventoryTotal()
    {
        var total = 0;
        foreach (var pair in _inventory)
        {
            total += pair.Value;
        }

        return total;
    }

    public int GetInventorySpace() => Math.Max(0, Capacity - GetInventoryTotal());

    public int Deposit(ResourceName resourceType, int amount)
    {
        if (!IsAcceptedResource(resourceType) || amount <= 0)
        {
            return 0;
        }

        var accepted = AddStoredResource(resourceType, amount);
        if (accepted > 0 && !IsRebalanceSuppressed)
        {
            RebalanceAfterAddition(resourceType);
        }

        return accepted;
    }

    public int Withdraw(ResourceName resourceType, int amount)
    {
        if (!IsAcceptedResource(resourceType) || amount <= 0)
        {
            return 0;
        }

        var taken = RemoveStoredResource(resourceType, amount);
        if (taken > 0 && !IsRebalanceSuppressed)
        {
            RebalanceAfterRemoval(resourceType);
        }

        return taken;
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        _adjacentSilos.Clear();
        foreach (var pair in _inventory)
        {
            if (pair.Value > 0)
            {
                EmitStorageInventoryChanged(pair.Key, -pair.Value);
            }
        }

        _inventory.Clear();
        base.CleanupBeforeRemoval(source);
    }

    internal void AddAdjacentSilo(Silo silo)
    {
        if (!ReferenceEquals(silo, this))
        {
            _adjacentSilos.Add(silo);
        }
    }

    internal void RemoveAdjacentSilo(Silo silo)
    {
        _adjacentSilos.Remove(silo);
    }

    internal void RebalanceAfterConnection()
    {
        if (_adjacentSilos.Count > 0 && Cave is not null)
        {
            var plantTypes = GrowableResourceType.GetAll();
            for (var index = 0; index < plantTypes.Count; index++)
            {
                RebalanceAfterRemoval(plantTypes[index].Resource);
            }
        }
    }

    internal static int CompareStableOrder(Silo left, Silo right)
    {
        var leftLocation = left.Location ?? GridPoint.Zero;
        var rightLocation = right.Location ?? GridPoint.Zero;
        var yComparison = leftLocation.Y.CompareTo(rightLocation.Y);
        return yComparison != 0
            ? yComparison
            : leftLocation.X.CompareTo(rightLocation.X);
    }

    private bool IsRebalanceSuppressed => _rebalanceScopeDepth > 0;

    private static bool IsAcceptedResource(ResourceName resourceType)
    {
        return ItemCatalog.GetClassification(resourceType, ResourceClassificationKeys.PlantType) is not null &&
               ItemCatalog.HasClassification(resourceType, ResourceClassificationKeys.FoodType, ResourceClassificationValues.Raw);
    }

    private void BeginRebalanceScope()
    {
        _rebalanceScopeDepth++;
    }

    private void EndRebalanceScope()
    {
        if (_rebalanceScopeDepth > 0)
        {
            _rebalanceScopeDepth--;
        }
    }

    private int AddStoredResource(ResourceName resourceType, int amount)
    {
        var accepted = Math.Min(GetInventorySpace(), amount);
        if (accepted > 0)
        {
            _inventory.TryAdd(resourceType, 0);
            _inventory[resourceType] += accepted;
            EmitStorageInventoryChanged(resourceType, accepted);
        }

        return accepted;
    }

    private int RemoveStoredResource(ResourceName resourceType, int amount)
    {
        var taken = Math.Min(GetStoredAmount(resourceType), amount);
        if (taken > 0)
        {
            _inventory[resourceType] -= taken;
            EmitStorageInventoryChanged(resourceType, -taken);
        }

        return taken;
    }

    private void RebalanceAfterAddition(ResourceName resourceType)
    {
        if (Cave is null || _adjacentSilos.Count == 0 || IsRebalanceSuppressed)
        {
            return;
        }

        BeginRebalanceScope();
        try
        {
            var queue = new Queue<Silo>();
            var queued = new HashSet<Silo>();
            EnqueueForRebalance(queue, queued, this);

            while (queue.Count > 0)
            {
                var donor = queue.Dequeue();
                queued.Remove(donor);
                if (donor.Cave is null)
                {
                    continue;
                }

                while (donor.TryPushToLowestAdjacentSilo(resourceType, out var recipient))
                {
                    EnqueueForRebalance(queue, queued, donor);
                    EnqueueForRebalance(queue, queued, recipient!);
                    donor.EnqueueAdjacentSilos(queue, queued);
                    recipient!.EnqueueAdjacentSilos(queue, queued);
                }
            }
        }
        finally
        {
            EndRebalanceScope();
        }
    }

    private void RebalanceAfterRemoval(ResourceName resourceType)
    {
        if (Cave is null || _adjacentSilos.Count == 0 || IsRebalanceSuppressed)
        {
            return;
        }

        BeginRebalanceScope();
        try
        {
            var queue = new Queue<Silo>();
            var queued = new HashSet<Silo>();
            EnqueueForRebalance(queue, queued, this);

            while (queue.Count > 0)
            {
                var receiver = queue.Dequeue();
                queued.Remove(receiver);
                if (receiver.Cave is null)
                {
                    continue;
                }

                while (receiver.TryPullFromHighestAdjacentSilo(resourceType, out var donor))
                {
                    EnqueueForRebalance(queue, queued, receiver);
                    EnqueueForRebalance(queue, queued, donor!);
                    receiver.EnqueueAdjacentSilos(queue, queued);
                    donor!.EnqueueAdjacentSilos(queue, queued);
                }
            }
        }
        finally
        {
            EndRebalanceScope();
        }
    }

    private void EnqueueAdjacentSilos(Queue<Silo> queue, HashSet<Silo> queued)
    {
        foreach (var adjacentSilo in _adjacentSilos)
        {
            EnqueueForRebalance(queue, queued, adjacentSilo);
        }
    }

    private static void EnqueueForRebalance(Queue<Silo> queue, HashSet<Silo> queued, Silo silo)
    {
        if (silo.Cave is null || !queued.Add(silo))
        {
            return;
        }

        queue.Enqueue(silo);
    }

    private bool TryPushToLowestAdjacentSilo(ResourceName resourceType, out Silo? recipient)
    {
        recipient = SelectLowestAdjacentSiloWithSpace(resourceType);
        if (recipient is null)
        {
            return false;
        }

        var difference = GetStoredAmount(resourceType) - recipient.GetStoredAmount(resourceType);
        if (difference <= 1)
        {
            return false;
        }

        var transferAmount = Math.Min(recipient.GetInventorySpace(), difference / 2);
        return transferAmount > 0 && TransferStoredResourceTo(recipient, resourceType, transferAmount);
    }

    private bool TryPullFromHighestAdjacentSilo(ResourceName resourceType, out Silo? donor)
    {
        donor = SelectHighestAdjacentSiloWithResource(resourceType);
        if (donor is null)
        {
            return false;
        }

        var difference = donor.GetStoredAmount(resourceType) - GetStoredAmount(resourceType);
        if (difference <= 1)
        {
            return false;
        }

        var transferAmount = Math.Min(GetInventorySpace(), difference / 2);
        return transferAmount > 0 && donor.TransferStoredResourceTo(this, resourceType, transferAmount);
    }

    private Silo? SelectLowestAdjacentSiloWithSpace(ResourceName resourceType)
    {
        Silo? best = null;
        foreach (var adjacentSilo in _adjacentSilos)
        {
            if (adjacentSilo.Cave != Cave || adjacentSilo.GetInventorySpace() <= 0)
            {
                continue;
            }

            if (best is null ||
                adjacentSilo.GetStoredAmount(resourceType) < best.GetStoredAmount(resourceType) ||
                (adjacentSilo.GetStoredAmount(resourceType) == best.GetStoredAmount(resourceType) && CompareStableOrder(adjacentSilo, best) < 0))
            {
                best = adjacentSilo;
            }
        }

        return best;
    }

    private Silo? SelectHighestAdjacentSiloWithResource(ResourceName resourceType)
    {
        Silo? best = null;
        foreach (var adjacentSilo in _adjacentSilos)
        {
            if (adjacentSilo.Cave != Cave || adjacentSilo.GetStoredAmount(resourceType) <= 0)
            {
                continue;
            }

            if (best is null ||
                adjacentSilo.GetStoredAmount(resourceType) > best.GetStoredAmount(resourceType) ||
                (adjacentSilo.GetStoredAmount(resourceType) == best.GetStoredAmount(resourceType) && CompareStableOrder(adjacentSilo, best) < 0))
            {
                best = adjacentSilo;
            }
        }

        return best;
    }

    private bool TransferStoredResourceTo(Silo recipient, ResourceName resourceType, int amount)
    {
        if (ReferenceEquals(recipient, this) || amount <= 0)
        {
            return false;
        }

        BeginRebalanceScope();
        recipient.BeginRebalanceScope();
        try
        {
            var removed = RemoveStoredResource(resourceType, amount);
            if (removed <= 0)
            {
                return false;
            }

            var accepted = recipient.AddStoredResource(resourceType, removed);
            if (accepted < removed)
            {
                AddStoredResource(resourceType, removed - accepted);
            }

            return accepted > 0;
        }
        finally
        {
            recipient.EndRebalanceScope();
            EndRebalanceScope();
        }
    }

    private void EmitStorageInventoryChanged(ResourceName resourceType, int resourceDelta)
    {
        if (resourceDelta == 0)
        {
            return;
        }

        Session.Emit(
            GameEvents.StorageInventoryChanged,
            new GameEventPayload(
                Cave,
                null,
                Location,
                null,
                resourceType,
                this,
                resourceDelta));
    }
}
