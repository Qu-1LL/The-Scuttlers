using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private static readonly GridPoint[] SoilNeighborDirections =
    [
        new GridPoint(1, 0),
        new GridPoint(0, 1),
        new GridPoint(-1, 0),
        new GridPoint(0, -1)
    ];

    private readonly List<Garage> _garages = [];
    private readonly List<SoilPatch> _soilPatches = [];
    private readonly List<SoilTile> _soilTiles = [];
    private readonly Dictionary<GridPoint, SoilTile> _soilTileLookup = [];
    private readonly List<Silo> _silos = [];

    public IReadOnlyList<Garage> GetGarages() => _garages;

    public IReadOnlyList<SoilPatch> GetSoilPatches() => _soilPatches;

    public IReadOnlyList<SoilTile> GetSoilTiles() => _soilTiles;

    public IReadOnlyList<Silo> GetSilos() => _silos;

    public SoilTile? GetSoilTile(GridPoint location)
    {
        return _soilTileLookup.TryGetValue(location, out var soilTile)
            ? soilTile
            : null;
    }

    private void OnSoilAndStorageBuildingBuilt(Building building)
    {
        switch (building)
        {
            case SoilPatch soilPatch:
                RegisterSoilPatchTiles(soilPatch);
                MergeAdjacentSoilAreas(soilPatch);
                break;
            case Garage garage:
                AttachGarageToAdjacentSilos(garage);
                break;
            case Silo silo:
                AttachSiloToAdjacentNetwork(silo);
                break;
        }
    }

    private void OnSoilAndStorageBuildingRemoved(Building building)
    {
        switch (building)
        {
            case SoilPatch soilPatch:
                UnregisterSoilPatchTiles(soilPatch);
                soilPatch.SoilArea?.RemoveSoilPatch(soilPatch);
                break;
            case Garage garage:
                DetachGarageFromAdjacentSilos(garage);
                break;
            case Silo silo:
                DetachSiloFromAdjacentNetwork(silo);
                break;
        }
    }

    private void RegisterSoilPatchTiles(SoilPatch soilPatch)
    {
        soilPatch.SoilArea ??= new SoilArea(Session);
        soilPatch.SoilArea.AddSoilPatch(soilPatch);
        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            var soilTile = soilPatch.SoilTiles[index];
            var worldLocation = soilTile.WorldLocation;
            if (worldLocation is null)
            {
                continue;
            }

            _soilTiles.Add(soilTile);
            _soilTileLookup[worldLocation.Value] = soilTile;
        }

        soilPatch.SoilArea.RefreshSelectionFootprint();
    }

    private void UnregisterSoilPatchTiles(SoilPatch soilPatch)
    {
        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            var soilTile = soilPatch.SoilTiles[index];
            _soilTiles.Remove(soilTile);
            if (soilTile.WorldLocation is { } worldLocation)
            {
                _soilTileLookup.Remove(worldLocation);
            }
        }
    }

    private void MergeAdjacentSoilAreas(SoilPatch soilPatch)
    {
        var mergedArea = soilPatch.SoilArea ?? new SoilArea(Session);
        mergedArea.AddSoilPatch(soilPatch);

        var adjacentAreas = new List<SoilArea>();
        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            foreach (var adjacentTile in GetAdjacentSoilTiles(soilPatch.SoilTiles[index]))
            {
                var adjacentArea = adjacentTile.ParentPatch.SoilArea;
                if (adjacentArea is null ||
                    ReferenceEquals(adjacentArea, mergedArea) ||
                    adjacentAreas.Contains(adjacentArea))
                {
                    continue;
                }

                adjacentAreas.Add(adjacentArea);
            }
        }

        for (var areaIndex = 0; areaIndex < adjacentAreas.Count; areaIndex++)
        {
            foreach (var adjacentPatch in adjacentAreas[areaIndex].SoilPatches.ToArray())
            {
                mergedArea.AddSoilPatch(adjacentPatch);
            }
        }

        mergedArea.RebuildPatchOffsetsFromLiveLocations();
        mergedArea.RefreshSelectionFootprint();
    }

    private IEnumerable<SoilTile> GetAdjacentSoilTiles(SoilTile soilTile)
    {
        if (soilTile.WorldLocation is not { } location)
        {
            yield break;
        }

        for (var index = 0; index < SoilNeighborDirections.Length; index++)
        {
            var adjacentLocation = new GridPoint(
                location.X + SoilNeighborDirections[index].X,
                location.Y + SoilNeighborDirections[index].Y);
            if (_soilTileLookup.TryGetValue(adjacentLocation, out var adjacentSoilTile))
            {
                yield return adjacentSoilTile;
            }
        }
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

    private static bool IsInsideFootprint(GridPoint point, GridPoint location, GridPoint size)
    {
        return point.X >= location.X &&
               point.X < location.X + size.X &&
               point.Y >= location.Y &&
               point.Y < location.Y + size.Y;
    }
}
