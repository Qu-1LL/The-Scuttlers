using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private readonly List<Garage> _garages = [];
    private readonly List<Soil> _soilTiles = [];
    private readonly List<Ranch> _ranches = [];

    public IReadOnlyList<Garage> GetGarages() => _garages;

    public IReadOnlyList<Soil> GetSoilTiles() => _soilTiles;

    public IReadOnlyList<Ranch> GetRanches() => _ranches;

    public void TickRanches()
    {
        var count = _ranches.Count;
        for (var index = 0; index < count && index < _ranches.Count; index++)
        {
            var ranch = _ranches[index];
            if (ranch.HasAssignmentSlot())
            {
                TryAssignAvailableFarmerToRanch(ranch);
            }

            ranch.Tick(this);
        }
    }

    private bool CanPlaceRanchBuilding(Building building, GridPoint location)
    {
        return building switch
        {
            Soil => CanPlaceSoil(location),
            Garage garage => CanPlaceGarage(garage, location),
            _ => true
        };
    }

    private bool CanPlaceSoil(GridPoint location)
    {
        return GetTile(location) is not null;
    }

    // A garage can only anchor soil that is not already claimed by another garage-backed ranch.
    private bool CanPlaceGarage(Garage garage, GridPoint location)
    {
        foreach (var neighbor in EnumerateFootprintNeighborTiles(location, garage.Size))
        {
            if (neighbor.Built is Soil soil && soil.Ranch?.Garage is not null)
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<Tile> EnumerateFootprintNeighborTiles(GridPoint location, GridPoint size)
    {
        var yielded = new HashSet<Tile>();
        for (var x = 0; x < size.X; x++)
        {
            for (var y = 0; y < size.Y; y++)
            {
                var tile = GetTile(new GridPoint(location.X + x, location.Y + y));
                if (tile is null)
                {
                    continue;
                }

                foreach (var neighbor in tile.Neighbors)
                {
                    if (IsInsideFootprint(neighbor.Coordinates, location, size) || !yielded.Add(neighbor))
                    {
                        continue;
                    }

                    yield return neighbor;
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

    private void OnRanchBuildingBuilt(Building building)
    {
        switch (building)
        {
            case Soil soil:
                AttachSoilToRanch(soil);
                break;
            case Garage garage:
                AttachGarageToRanch(garage);
                break;
        }
    }

    private void OnRanchBuildingRemoved(Building building)
    {
        switch (building)
        {
            case Soil soil:
                RemoveSoilFromRanch(soil);
                break;
            case Garage garage:
                RemoveGarageFromRanch(garage);
                break;
        }
    }

    private void AttachSoilToRanch(Soil soil)
    {
        var targetRanch = FindAdjacentRanchForSoil(soil);
        if (targetRanch is null)
        {
            return;
        }

        targetRanch.AddSoil(soil);
        var adjacentSoils = GetAdjacentSoils(soil).ToArray();
        for (var index = 0; index < adjacentSoils.Length; index++)
        {
            var neighborSoil = adjacentSoils[index];
            if (neighborSoil.Ranch is null)
            {
                AbsorbConnectedRanchlessSoils(targetRanch, neighborSoil);
            }
        }

        targetRanch.RefreshSelectionFootprint();
    }

    private Ranch? FindAdjacentRanchForSoil(Soil soil)
    {
        foreach (var neighbor in soil.TileArray.SelectMany(tile => tile.Neighbors))
        {
            switch (neighbor.Built)
            {
                case Garage garage:
                    return garage.Ranch ?? CreateRanch(garage);
                case Soil neighborSoil when neighborSoil.Ranch is not null:
                    return neighborSoil.Ranch;
            }
        }

        return null;
    }

    private void AttachGarageToRanch(Garage garage)
    {
        var ranch = garage.Ranch ?? CreateRanch(garage);
        var adjacentSoils = GetAdjacentSoils(garage).ToArray();
        for (var index = 0; index < adjacentSoils.Length; index++)
        {
            var soil = adjacentSoils[index];
            if (soil.Ranch is null)
            {
                AbsorbConnectedRanchlessSoils(ranch, soil);
            }
            else if (!ReferenceEquals(soil.Ranch, ranch))
            {
                MergeRanches(ranch, soil.Ranch);
            }
        }

        ranch.RefreshSelectionFootprint();
    }

    private Ranch CreateRanch(Garage garage)
    {
        var ranch = new Ranch(Session);
        ranch.SetGarage(garage);
        _ranches.Add(ranch);
        TryAssignAvailableFarmerToRanch(ranch);
        return ranch;
    }

    private bool TryAssignAvailableFarmerToRanch(Ranch ranch)
    {
        if (!ranch.HasAssignmentSlot())
        {
            return false;
        }

        for (var index = 0; index < _trilobiteList.Count; index++)
        {
            var trilobite = _trilobiteList[index];
            if (!trilobite.IsFarmer() ||
                trilobite.Cave != this ||
                trilobite.Health <= 0 ||
                trilobite.HasInventory())
            {
                continue;
            }

            var assignedRanch = trilobite.GetAssignedRanch();
            if (assignedRanch is not null && !ReferenceEquals(assignedRanch, ranch))
            {
                continue;
            }

            trilobite.SetAssignedBuilding(ranch);
            if (!ranch.Assign(trilobite))
            {
                trilobite.ReleaseAssignedBuilding();
                continue;
            }

            trilobite.RestartBehavior();
            return true;
        }

        return false;
    }

    private void MergeRanches(Ranch target, Ranch other)
    {
        if (ReferenceEquals(target, other))
        {
            return;
        }

        var otherGarage = other.Garage;
        if (otherGarage is not null)
        {
            if (target.Garage is null)
            {
                other.ClearGarage(otherGarage);
                target.SetGarage(otherGarage);
            }
            else if (!ReferenceEquals(target.Garage, otherGarage))
            {
                throw new InvalidOperationException("A ranch cannot be connected to more than one garage.");
            }
        }

        foreach (var soil in other.SoilTiles.ToArray())
        {
            other.RemoveSoil(soil);
            target.AddSoil(soil);
        }

        _ranches.Remove(other);
        other.Dissolve();
        target.RefreshSelectionFootprint();
    }

    private void AbsorbConnectedRanchlessSoils(Ranch target, Soil start)
    {
        if (start.Ranch is not null)
        {
            return;
        }

        var queue = new Queue<Soil>();
        var visited = new HashSet<Soil>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Cave != this || current.Ranch is not null)
            {
                continue;
            }

            target.AddSoil(current);
            foreach (var neighbor in GetAdjacentSoils(current))
            {
                if (neighbor.Ranch is null && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private void RemoveSoilFromRanch(Soil soil)
    {
        var ranch = soil.Ranch;
        if (ranch is null)
        {
            return;
        }

        ranch.RemoveSoil(soil);
        PruneDisconnectedSoils(ranch);
    }

    private void RemoveGarageFromRanch(Garage garage)
    {
        var ranch = garage.Ranch;
        if (ranch is null)
        {
            return;
        }

        _ranches.Remove(ranch);
        ranch.Dissolve();
    }

    // After a soil tile disappears, keep only the soil component that still reaches the garage.
    private void PruneDisconnectedSoils(Ranch ranch)
    {
        var garage = ranch.Garage;
        if (garage is null)
        {
            _ranches.Remove(ranch);
            ranch.Dissolve();
            return;
        }

        var connected = new HashSet<Soil>();
        var queue = new Queue<Soil>();
        foreach (var soil in GetAdjacentSoils(garage))
        {
            if (soil.Ranch is not null && ranch.Contains(soil) && connected.Add(soil))
            {
                queue.Enqueue(soil);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetAdjacentSoils(current))
            {
                if (neighbor.Ranch is not null && ranch.Contains(neighbor) && connected.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (var soil in ranch.SoilTiles.ToArray())
        {
            if (!connected.Contains(soil))
            {
                ranch.RemoveSoil(soil);
            }
        }

        ranch.RefreshSelectionFootprint();
    }

    private static IEnumerable<Soil> GetAdjacentSoils(Soil soil)
    {
        if (soil.TileArray.Count == 0)
        {
            yield break;
        }

        foreach (var neighbor in soil.TileArray[0].Neighbors)
        {
            if (neighbor.Built is Soil neighborSoil)
            {
                yield return neighborSoil;
            }
        }
    }

    private static IEnumerable<Soil> GetAdjacentSoils(Garage garage)
    {
        var yielded = new HashSet<Soil>();
        foreach (var tile in garage.TileArray)
        {
            foreach (var neighbor in tile.Neighbors)
            {
                if (neighbor.Built is Soil soil && yielded.Add(soil))
                {
                    yield return soil;
                }
            }
        }
    }
}
