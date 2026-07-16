using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private readonly object _dirtyNavigationTilesGate = new();
    private readonly HashSet<string> _dirtyNavigationTileKeys = new(StringComparer.Ordinal);
    private bool _asyncBuildingBfsMaintenanceEnabled;

    internal void EnableAsyncBuildingBfsMaintenance()
    {
        _asyncBuildingBfsMaintenanceEnabled = true;
    }

    internal bool UsesAsyncBuildingBfsMaintenance => _asyncBuildingBfsMaintenanceEnabled;

    internal int PendingNavigationDirtyTileCount
    {
        get
        {
            lock (_dirtyNavigationTilesGate)
            {
                return _dirtyNavigationTileKeys.Count;
            }
        }
    }

    // Record topology changes centrally so one worker batch can coalesce nearby edits.
    public void HandleNavigationTopologyChanged(
        IEnumerable<string>? dirtyKeys = null,
        IEnumerable<Building>? dirtyBuildings = null,
        IEnumerable<Creature>? dirtyCreatures = null)
    {
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in dirtyKeys ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                keySet.Add(key);
            }
        }

        var keyArray = keySet.ToArray();
        if (_asyncBuildingBfsMaintenanceEnabled)
        {
            if (keyArray.Length > 0 || dirtyBuildings is not null || dirtyCreatures is not null)
            {
                RebalanceCriticalBfsFields(keyArray, dirtyBuildings, dirtyCreatures);
            }

            lock (_dirtyNavigationTilesGate)
            {
                for (var index = 0; index < keyArray.Length; index++)
                {
                    _dirtyNavigationTileKeys.Add(keyArray[index]);
                }
            }

            if (keyArray.Length > 0)
            {
                MarkNavigableBuildingFieldsPending();
            }
            return;
        }

        RebalanceCriticalBfsFields(keyArray, dirtyBuildings, dirtyCreatures);
        RebalanceNonCriticalBuildingFields(keyArray);
    }

    // Keep the last published field values untouched until the worker publishes a replacement.
    private void MarkNavigableBuildingFieldsPending()
    {
        foreach (var building in _buildingList)
        {
            if (ReferenceEquals(building, _queenBuilding) ||
                !building.Navigable ||
                building.Location is null ||
                building.TileArray.Count == 0)
            {
                continue;
            }

            GetBuildingBfsFieldObject(building)?.MarkMaintenancePending(TopologyVersion, ReachabilityVersion);
        }
    }

    // Critical fields remain available to synchronous/headless Core callers.
    public void RebalanceCriticalBfsFields(
        IEnumerable<string>? dirtyKeys = null,
        IEnumerable<Building>? dirtyBuildings = null,
        IEnumerable<Creature>? dirtyCreatures = null)
    {
        RebalanceBfsField("colony", dirtyKeys, dirtyBuildings, dirtyCreatures);
        if (Session.Danger)
        {
            RebalanceBfsField("enemy", dirtyKeys, dirtyBuildings, dirtyCreatures);
        }

        var queen = GetQueenBuilding();
        if (queen is null || queen.TileArray.Count == 0)
        {
            return;
        }

        var queenField = GetBuildingBfsFieldObject(queen);
        if (queenField is null)
        {
            return;
        }

        queenField.MarkDirty(dirtyKeys, dirtyBuildings, dirtyCreatures);
        if (queenField.HasCoverage())
        {
            queenField.Rebalance(dirtyKeys);
        }
        else
        {
            queenField.Rebuild();
        }
    }

    // Bootstrap and headless Core tests do not own a Runtime worker, so keep fields complete.
    private void RebalanceNonCriticalBuildingFields(IEnumerable<string> dirtyKeys)
    {
        foreach (var building in _buildingList)
        {
            if (ReferenceEquals(building, _queenBuilding) ||
                !building.Navigable ||
                building.Location is null ||
                building.TileArray.Count == 0)
            {
                continue;
            }

            var field = GetBuildingBfsFieldObject(building);
            if (field is null)
            {
                continue;
            }

            field.MarkDirty(dirtyKeys, [], []);
            if (field.HasCoverage())
            {
                field.Rebalance(dirtyKeys);
            }
            else
            {
                field.Rebuild();
            }
        }
    }

    // Copy and clear the global journal on the simulation thread before background work starts.
    internal BuildingBfsMaintenanceBatch? TakeBuildingBfsMaintenanceBatch()
    {
        string[] dirtyKeys;
        lock (_dirtyNavigationTilesGate)
        {
            if (_dirtyNavigationTileKeys.Count == 0)
            {
                return null;
            }

            dirtyKeys = _dirtyNavigationTileKeys.ToArray();
            _dirtyNavigationTileKeys.Clear();
        }

        var workItems = new List<BuildingBfsMaintenanceWork>(_buildingList.Count);
        BuildingBfsNavigationSnapshot? navigation = null;
        foreach (var building in _buildingList)
        {
            if (ReferenceEquals(building, _queenBuilding) ||
                !building.Navigable ||
                building.Location is null ||
                building.TileArray.Count == 0)
            {
                continue;
            }

            var field = GetBuildingBfsFieldObject(building);
            if (field is null)
            {
                continue;
            }

            navigation ??= field.CreateBuildingMaintenanceNavigationSnapshot();
            var snapshot = field.CreateBuildingMaintenanceSnapshot(
                navigation,
                dirtyKeys,
                !field.HasCoverage());
            if (snapshot is not null)
            {
                workItems.Add(new BuildingBfsMaintenanceWork(building, snapshot));
            }
        }

        return workItems.Count == 0
            ? null
            : new BuildingBfsMaintenanceBatch(
                workItems,
                TopologyVersion,
                ReachabilityVersion);
    }

    internal void PublishBuildingBfsMaintenanceBatch(
        BuildingBfsMaintenanceBatch batch,
        BuildingBfsMaintenanceBatchResult result)
    {
        for (var index = 0; index < batch.WorkItems.Count && index < result.Results.Count; index++)
        {
            var work = batch.WorkItems[index];
            var building = work.Building;
            if (!Buildings.Contains(building) || !ReferenceEquals(building.Cave, this))
            {
                continue;
            }

            var field = GetBuildingBfsFieldObject(building);
            if (field is null)
            {
                continue;
            }

            field.ApplyBuildingMaintenanceResult(
                result.Results[index],
                batch.TopologyVersion,
                batch.ReachabilityVersion);

            if (batch.TopologyVersion != TopologyVersion ||
                batch.ReachabilityVersion != ReachabilityVersion)
            {
                field.MarkMaintenancePending(TopologyVersion, ReachabilityVersion);
            }
        }
    }

    public bool IsBuildingBfsFieldCurrent(Building building)
    {
        return GetBuildingBfsFieldObject(building)?.IsCurrentFor(TopologyVersion, ReachabilityVersion) == true;
    }

    public int GetBuildingBfsFieldValueOrManhattanDistance(Building building, GridPoint location)
    {
        if (!building.Navigable)
        {
            return int.MaxValue;
        }

        var field = GetBuildingBfsFieldObject(building);
        if (field is null)
        {
            return int.MaxValue;
        }

        if (!_asyncBuildingBfsMaintenanceEnabled)
        {
            return field.GetFieldValue(location);
        }

        // Keep the last published field live while the worker computes a replacement.
        if (field.HasCoverage())
        {
            return field.GetFieldValue(location, refresh: false);
        }

        return building.Location is null
            ? int.MaxValue
            : GridPoint.ManhattanDistance(location, building.Location.Value);
    }

    private TBuilding? GetNearestBuildingByField<TBuilding>(IReadOnlyList<TBuilding> buildings, GridPoint location)
        where TBuilding : Building
    {
        TBuilding? nearest = null;
        var nearestDistance = int.MaxValue;
        var nearestKey = string.Empty;

        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (!building.Navigable || building.Location is null || building.TileArray.Count == 0)
            {
                continue;
            }

            var distance = GetBuildingBfsFieldValueOrManhattanDistance(building, location);
            var key = building.Location.Value.ToString();
            if (nearest is null ||
                distance < nearestDistance ||
                (distance == nearestDistance && string.CompareOrdinal(key, nearestKey) < 0))
            {
                nearest = building;
                nearestDistance = distance;
                nearestKey = key;
            }
        }

        return nearest;
    }

    private int GetNearestBuildingDistanceByField<TBuilding>(IReadOnlyList<TBuilding> buildings, GridPoint location)
        where TBuilding : Building
    {
        var building = GetNearestBuildingByField(buildings, location);
        return building is null ? int.MaxValue : GetBuildingBfsFieldValueOrManhattanDistance(building, location);
    }

    // Rare assignment searches use the same per-building field distances as nearest-building lookup.
    public List<TBuilding> GetBuildingsByFieldDistance<TBuilding>(IReadOnlyList<TBuilding> buildings, GridPoint location)
        where TBuilding : Building
    {
        var ordered = new List<TBuilding>(buildings.Count);
        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (!building.Navigable || building.Location is null || building.TileArray.Count == 0)
            {
                continue;
            }

            var distance = GetBuildingBfsFieldValueOrManhattanDistance(building, location);
            var key = building.Location.Value.ToString();
            var insertIndex = ordered.Count;
            for (var candidateIndex = 0; candidateIndex < ordered.Count; candidateIndex++)
            {
                var candidate = ordered[candidateIndex];
                var candidateDistance = GetBuildingBfsFieldValueOrManhattanDistance(candidate, location);
                if (distance < candidateDistance ||
                    (distance == candidateDistance && string.CompareOrdinal(key, candidate.Location!.Value.ToString()) < 0))
                {
                    insertIndex = candidateIndex;
                    break;
                }
            }

            ordered.Insert(insertIndex, building);
        }

        return ordered;
    }
}
