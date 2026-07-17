using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    public bool CanPlaceVehicle(Vehicle vehicle, GridPoint location)
    {
        return CanPlaceVehicle(vehicle, location, vehicle);
    }

    public bool SpawnVehicle(Vehicle vehicle, GridPoint location)
    {
        if (_vehicles.Contains(vehicle) ||
            vehicle.Cave is not null ||
            !CanPlaceVehicle(vehicle, location, vehicle))
        {
            return false;
        }

        var tiles = BuildVehicleTileArray(vehicle, location);
        _vehicles.Add(vehicle);
        vehicle.AttachToCave(this, location, tiles);
        RegisterVehicleOccupancy(vehicle);
        MarkVehicleBfsFieldsDirty(vehicle.TileArray);
        return true;
    }

    public bool MoveVehicle(Vehicle vehicle, GridPoint destination)
    {
        if (!_vehicles.Contains(vehicle) || vehicle.Location is null)
        {
            return false;
        }

        var moveDistance = GridPoint.ManhattanDistance(vehicle.Location.Value, destination);
        if (moveDistance > 1 ||
            !CanPlaceVehicle(vehicle, destination, vehicle))
        {
            return false;
        }

        var oldTiles = vehicle.TileArray.ToArray();
        UnregisterVehicleOccupancy(vehicle);
        var newTiles = BuildVehicleTileArray(vehicle, destination);
        vehicle.MoveWithinCave(destination, newTiles);
        RegisterVehicleOccupancy(vehicle);
        MarkVehicleBfsFieldsDirty(oldTiles);
        MarkVehicleBfsFieldsDirty(newTiles);
        return true;
    }

    public bool RemoveVehicle(Vehicle vehicle, object? source = null)
    {
        if (!_vehicles.Remove(vehicle))
        {
            return false;
        }

        var dirtyTiles = vehicle.TileArray.ToArray();
        UnregisterVehicleOccupancy(vehicle);
        vehicle.CleanupBeforeRemoval(source);
        vehicle.DetachFromCave();
        MarkVehicleBfsFieldsDirty(dirtyTiles);
        return true;
    }

    public Vehicle? GetVehicleAtTileKey(string? tileKey)
    {
        return !string.IsNullOrWhiteSpace(tileKey) && _vehicleOccupancy.TryGetValue(tileKey, out var vehicle)
            ? vehicle
            : null;
    }

    public Vehicle? GetVehicleAtTile(GridPoint location)
    {
        return GetVehicleAtTileKey(location.ToString());
    }

    public void TickVehicles()
    {
        var count = _vehicles.Count;
        for (var index = 0; index < count && index < _vehicles.Count; index++)
        {
            var vehicle = _vehicles[index];
            if (vehicle is IDriveable)
            {
                continue;
            }

            vehicle.Move();
        }
    }

    private bool CanPlaceVehicle(Vehicle vehicle, GridPoint location, Vehicle? ignoreVehicle)
    {
        foreach (var point in vehicle.EnumerateOccupiedPoints(location))
        {
            var tile = GetTile(point);
            if (tile is null || string.Equals(tile.Base, "wall", StringComparison.Ordinal))
            {
                return false;
            }

            var occupyingVehicle = GetVehicleAtTileKey(tile.Key);
            if (occupyingVehicle is not null && !ReferenceEquals(occupyingVehicle, ignoreVehicle))
            {
                return false;
            }

            if (HasCreatureBodyInVehicleCell(vehicle, point))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasCreatureBodyInVehicleCell(Vehicle vehicle, GridPoint cell)
    {
        for (var index = 0; index < _trilobiteList.Count; index++)
        {
            var creature = _trilobiteList[index];
            if (!creature.IsHostedOnVehicle(vehicle) && CircleOverlapsCell(creature, cell))
            {
                return true;
            }
        }

        for (var index = 0; index < _enemyList.Count; index++)
        {
            if (CircleOverlapsCell(_enemyList[index], cell))
            {
                return true;
            }
        }

        return false;
    }

    private List<Tile> BuildVehicleTileArray(Vehicle vehicle, GridPoint location)
    {
        var tiles = new List<Tile>();
        foreach (var point in vehicle.EnumerateOccupiedPoints(location))
        {
            var tile = GetTile(point);
            if (tile is not null)
            {
                tiles.Add(tile);
            }
        }

        return tiles;
    }

    private void RegisterVehicleOccupancy(Vehicle vehicle)
    {
        foreach (var tile in vehicle.TileArray)
        {
            _vehicleOccupancy[tile.Key] = vehicle;
        }
    }

    private void UnregisterVehicleOccupancy(Vehicle vehicle)
    {
        foreach (var tile in vehicle.TileArray)
        {
            if (_vehicleOccupancy.TryGetValue(tile.Key, out var occupyingVehicle) &&
                ReferenceEquals(occupyingVehicle, vehicle))
            {
                _vehicleOccupancy.Remove(tile.Key);
            }
        }
    }

    private void MarkVehicleBfsFieldsDirty(IEnumerable<Tile> tiles)
    {
        var dirtyKeys = new List<string>();
        foreach (var tile in tiles)
        {
            dirtyKeys.Add(tile.Key);
        }

        GetBfsFieldObject("colony")?.MarkTilesDirty(dirtyKeys);
    }
}
