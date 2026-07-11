using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private readonly List<Silo> _silos = [];

    public IReadOnlyList<Silo> GetSilos() => _silos;

    internal bool TryTransferPlowAlgaeToAdjacentSilo(Plow plow)
    {
        if (!ReferenceEquals(plow.Cave, this) ||
            plow.Location is not { } location)
        {
            return false;
        }

        var availableAlgae = plow.GetInventory().GetValueOrDefault(ResourceName.Algae, 0);
        if (availableAlgae <= 0)
        {
            return false;
        }

        foreach (var adjacentLocation in EnumerateAdjacentFootprintLocations(location, plow.GetRotatedSize()))
        {
            if (GetSoilTile(adjacentLocation) is not null)
            {
                continue;
            }

            var tile = GetTile(adjacentLocation);
            if (tile?.Built is not Silo silo)
            {
                continue;
            }

            var accepted = silo.Deposit(ResourceName.Algae, availableAlgae);
            if (accepted > 0)
            {
                plow.Withdraw(ResourceName.Algae, accepted);
            }

            return accepted > 0;
        }

        return false;
    }

    private void AttachGarageToAdjacentSilos(Garage garage)
    {
        if (garage.Location is not { } location)
        {
            return;
        }

        foreach (var silo in EnumerateAdjacentSilos(location, garage.Size))
        {
            garage.AddAdjacentSilo(silo);
        }

        garage.TryOffloadAlgaeToAdjacentSilos();
    }

    private void DetachGarageFromAdjacentSilos(Garage garage)
    {
        foreach (var silo in garage.AdjacentSilos.ToArray())
        {
            garage.RemoveAdjacentSilo(silo);
        }
    }

    private void AttachSiloToAdjacentNetwork(Silo silo)
    {
        if (silo.Location is not { } location)
        {
            return;
        }

        var rebalanceConnectedSilos = false;
        foreach (var adjacentSilo in EnumerateAdjacentSilos(location, silo.Size))
        {
            if (ReferenceEquals(adjacentSilo, silo))
            {
                continue;
            }

            silo.AddAdjacentSilo(adjacentSilo);
            adjacentSilo.AddAdjacentSilo(silo);
            rebalanceConnectedSilos = true;
        }

        var adjacentGarages = new List<Garage>();
        foreach (var garage in EnumerateAdjacentGarages(location, silo.Size))
        {
            garage.AddAdjacentSilo(silo);
            adjacentGarages.Add(garage);
        }

        if (rebalanceConnectedSilos)
        {
            silo.RebalanceAfterConnection();
        }

        for (var index = 0; index < adjacentGarages.Count; index++)
        {
            adjacentGarages[index].TryOffloadAlgaeToAdjacentSilos();
        }
    }

    private void DetachSiloFromAdjacentNetwork(Silo silo)
    {
        foreach (var adjacentSilo in silo.AdjacentSilos.ToArray())
        {
            adjacentSilo.RemoveAdjacentSilo(silo);
            silo.RemoveAdjacentSilo(adjacentSilo);
        }

        for (var index = 0; index < _garages.Count; index++)
        {
            _garages[index].RemoveAdjacentSilo(silo);
        }
    }

    private IEnumerable<Silo> EnumerateAdjacentSilos(GridPoint location, GridPoint size)
    {
        var yielded = new HashSet<Silo>();
        foreach (var adjacentLocation in EnumerateAdjacentFootprintLocations(location, size))
        {
            var tile = GetTile(adjacentLocation);
            if (tile?.Built is Silo silo && yielded.Add(silo))
            {
                yield return silo;
            }
        }
    }

    private IEnumerable<Garage> EnumerateAdjacentGarages(GridPoint location, GridPoint size)
    {
        var yielded = new HashSet<Garage>();
        foreach (var adjacentLocation in EnumerateAdjacentFootprintLocations(location, size))
        {
            var tile = GetTile(adjacentLocation);
            if (tile?.Built is Garage garage && yielded.Add(garage))
            {
                yield return garage;
            }
        }
    }

    private static IEnumerable<GridPoint> EnumerateAdjacentFootprintLocations(GridPoint location, GridPoint size)
    {
        for (var y = location.Y - 1; y <= location.Y + size.Y; y++)
        {
            for (var x = location.X - 1; x <= location.X + size.X; x++)
            {
                var point = new GridPoint(x, y);
                if (!IsInsideFootprint(point, location, size))
                {
                    yield return point;
                }
            }
        }
    }
}
