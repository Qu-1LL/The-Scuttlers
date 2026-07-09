using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Silo : Building, IResourceStorage
{
    private readonly Dictionary<ResourceName, int> _inventory = new()
    {
        [ResourceName.Algae] = 0
    };
    private readonly HashSet<Silo> _adjacentSilos = [];
    private int _rebalanceScopeDepth;

    public Silo(GameSession session)
        : base("Silo", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Silo";
        Recipe = new Dictionary<ResourceName, int>
        {
            [ResourceName.Sandstone] = 20
        };
        Capacity = 5000;
        Description = $"A high-capacity algae silo that stores up to {Capacity} algae and balances with adjacent silos.";
    }

    public int Capacity { get; }

    public IReadOnlyCollection<Silo> AdjacentSilos => _adjacentSilos;

    public IReadOnlyDictionary<ResourceName, int> GetInventory() => _inventory;

    public IReadOnlyDictionary<ResourceName, int> GetStoredResources() => _inventory;

    public int GetInventoryTotal() => _inventory[ResourceName.Algae];

    public int GetInventorySpace() => Math.Max(0, Capacity - GetInventoryTotal());

    public int Deposit(ResourceName resourceType, int amount)
    {
        if (!IsAcceptedResource(resourceType) || amount <= 0)
        {
            return 0;
        }

        var accepted = AddStoredAlgae(amount);
        if (accepted > 0 && !IsRebalanceSuppressed)
        {
            RebalanceAfterAddition();
        }

        return accepted;
    }

    public int Withdraw(ResourceName resourceType, int amount)
    {
        if (!IsAcceptedResource(resourceType) || amount <= 0)
        {
            return 0;
        }

        var taken = RemoveStoredAlgae(amount);
        if (taken > 0 && !IsRebalanceSuppressed)
        {
            RebalanceAfterRemoval();
        }

        return taken;
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        _adjacentSilos.Clear();
        var storedAlgae = _inventory[ResourceName.Algae];
        if (storedAlgae > 0)
        {
            EmitStorageInventoryChanged(ResourceName.Algae, -storedAlgae);
        }

        _inventory[ResourceName.Algae] = 0;
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
            RebalanceAfterRemoval();
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
        return resourceType == ResourceName.Algae;
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

    private int AddStoredAlgae(int amount)
    {
        var accepted = Math.Min(GetInventorySpace(), amount);
        if (accepted > 0)
        {
            _inventory[ResourceName.Algae] += accepted;
            EmitStorageInventoryChanged(ResourceName.Algae, accepted);
        }

        return accepted;
    }

    private int RemoveStoredAlgae(int amount)
    {
        var taken = Math.Min(GetInventoryTotal(), amount);
        if (taken > 0)
        {
            _inventory[ResourceName.Algae] -= taken;
            EmitStorageInventoryChanged(ResourceName.Algae, -taken);
        }

        return taken;
    }

    private void RebalanceAfterAddition()
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

                while (donor.TryPushToLowestAdjacentSilo(out var recipient))
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

    private void RebalanceAfterRemoval()
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

                while (receiver.TryPullFromHighestAdjacentSilo(out var donor))
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

    private bool TryPushToLowestAdjacentSilo(out Silo? recipient)
    {
        recipient = SelectLowestAdjacentSiloWithSpace();
        if (recipient is null)
        {
            return false;
        }

        var difference = GetInventoryTotal() - recipient.GetInventoryTotal();
        if (difference <= 1)
        {
            return false;
        }

        var transferAmount = Math.Min(recipient.GetInventorySpace(), difference / 2);
        return transferAmount > 0 && TransferStoredAlgaeTo(recipient, transferAmount);
    }

    private bool TryPullFromHighestAdjacentSilo(out Silo? donor)
    {
        donor = SelectHighestAdjacentSiloWithAlgae();
        if (donor is null)
        {
            return false;
        }

        var difference = donor.GetInventoryTotal() - GetInventoryTotal();
        if (difference <= 1)
        {
            return false;
        }

        var transferAmount = Math.Min(GetInventorySpace(), difference / 2);
        return transferAmount > 0 && donor.TransferStoredAlgaeTo(this, transferAmount);
    }

    private Silo? SelectLowestAdjacentSiloWithSpace()
    {
        Silo? best = null;
        foreach (var adjacentSilo in _adjacentSilos)
        {
            if (adjacentSilo.Cave != Cave || adjacentSilo.GetInventorySpace() <= 0)
            {
                continue;
            }

            if (best is null ||
                adjacentSilo.GetInventoryTotal() < best.GetInventoryTotal() ||
                (adjacentSilo.GetInventoryTotal() == best.GetInventoryTotal() && CompareStableOrder(adjacentSilo, best) < 0))
            {
                best = adjacentSilo;
            }
        }

        return best;
    }

    private Silo? SelectHighestAdjacentSiloWithAlgae()
    {
        Silo? best = null;
        foreach (var adjacentSilo in _adjacentSilos)
        {
            if (adjacentSilo.Cave != Cave || adjacentSilo.GetInventoryTotal() <= 0)
            {
                continue;
            }

            if (best is null ||
                adjacentSilo.GetInventoryTotal() > best.GetInventoryTotal() ||
                (adjacentSilo.GetInventoryTotal() == best.GetInventoryTotal() && CompareStableOrder(adjacentSilo, best) < 0))
            {
                best = adjacentSilo;
            }
        }

        return best;
    }

    private bool TransferStoredAlgaeTo(Silo recipient, int amount)
    {
        if (ReferenceEquals(recipient, this) || amount <= 0)
        {
            return false;
        }

        BeginRebalanceScope();
        recipient.BeginRebalanceScope();
        try
        {
            var removed = RemoveStoredAlgae(amount);
            if (removed <= 0)
            {
                return false;
            }

            var accepted = recipient.AddStoredAlgae(removed);
            if (accepted < removed)
            {
                AddStoredAlgae(removed - accepted);
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
