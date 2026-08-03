using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.Core.Pathfinding;

// Describes how a building's traversal field is seeded and who owns its maintenance timing.
public enum BuildingNavigationSeedMode
{
    None,
    InteriorPassableOwnedTiles,
    AdjacentExteriorPassableTiles
}

public enum BuildingNavigationMaintenanceMode
{
    None,
    Synchronous,
    Asynchronous
}

// A published field is immutable from the simulation's point of view. The arrays are copied on
// construction so the worker can never share its mutable repair buffers with the main thread.
public sealed class BuildingNavigationFieldSnapshot
{
    public BuildingNavigationFieldSnapshot(
        int buildingRuntimeId,
        long generation,
        long topologyVersion,
        long reachabilityVersion,
        BuildingNavigationSeedMode seedMode,
        IReadOnlyList<int> distanceByTileId,
        IReadOnlyList<int> nextStepTileIdByTileId,
        IReadOnlyList<bool> passableByTileId,
        IReadOnlyList<int>? footprintTileIds = null,
        IReadOnlyList<int>? seedTileIds = null)
    {
        BuildingRuntimeId = buildingRuntimeId;
        Generation = generation;
        TopologyVersion = topologyVersion;
        ReachabilityVersion = reachabilityVersion;
        SeedMode = seedMode;
        DistanceByTileId = distanceByTileId.ToArray();
        NextStepTileIdByTileId = nextStepTileIdByTileId.ToArray();
        PassableByTileId = passableByTileId.ToArray();
        FootprintTileIds = footprintTileIds?.ToArray() ?? [];
        SeedTileIds = seedTileIds?.ToArray() ?? [];
    }

    public int BuildingRuntimeId { get; }

    public long Generation { get; }

    public long TopologyVersion { get; }

    public long ReachabilityVersion { get; }

    public BuildingNavigationSeedMode SeedMode { get; }

    public IReadOnlyList<int> DistanceByTileId { get; }

    public IReadOnlyList<int> NextStepTileIdByTileId { get; }

    public IReadOnlyList<bool> PassableByTileId { get; }

    public IReadOnlyList<int> FootprintTileIds { get; }

    public IReadOnlyList<int> SeedTileIds { get; }

    public int GetDistance(int tileId)
    {
        return tileId < 0 || tileId >= DistanceByTileId.Count ||
               tileId >= PassableByTileId.Count || !PassableByTileId[tileId]
            ? int.MaxValue
            : DistanceByTileId[tileId];
    }

    public int GetNextStepTileId(int tileId)
    {
        return tileId < 0 || tileId >= NextStepTileIdByTileId.Count ||
               tileId >= PassableByTileId.Count || !PassableByTileId[tileId]
            ? -1
            : NextStepTileIdByTileId[tileId];
    }

    public bool HasSameFootprint(BuildingNavigationFieldSnapshot other)
    {
        return SeedMode == other.SeedMode &&
               new HashSet<int>(FootprintTileIds).SetEquals(other.FootprintTileIds);
    }

    public bool HasMatchingFootprint(BuildingNavigationFieldSnapshot other)
    {
        return new HashSet<int>(FootprintTileIds).SetEquals(other.FootprintTileIds);
    }

    public BuildingNavigationFieldSnapshot RebindBuilding(int buildingRuntimeId, long generation)
    {
        return new BuildingNavigationFieldSnapshot(
            buildingRuntimeId,
            generation,
            TopologyVersion,
            ReachabilityVersion,
            SeedMode,
            DistanceByTileId,
            NextStepTileIdByTileId,
            PassableByTileId,
            FootprintTileIds,
            SeedTileIds);
    }
}

public sealed class BuildingNavigationTileSnapshot
{
    public BuildingNavigationTileSnapshot(
        int id,
        string key,
        bool passableForCreatures,
        bool reachable,
        IReadOnlyList<int> neighborIds)
    {
        Id = id;
        Key = key;
        PassableForCreatures = passableForCreatures;
        Reachable = reachable;
        NeighborIds = neighborIds.ToArray();
    }

    public int Id { get; }

    public string Key { get; }

    public bool PassableForCreatures { get; }

    public bool Reachable { get; }

    public IReadOnlyList<int> NeighborIds { get; }
}

public sealed class BuildingNavigationBuildingSnapshot
{
    public BuildingNavigationBuildingSnapshot(
        int runtimeId,
        int placementOrder,
        string name,
        BuildingNavigationSeedMode seedMode,
        BuildingNavigationMaintenanceMode maintenanceMode,
        IReadOnlyList<int> footprintTileIds,
        IReadOnlyList<int> seedTileIds,
        BuildingNavigationFieldSnapshot? inheritedField = null)
    {
        RuntimeId = runtimeId;
        PlacementOrder = placementOrder;
        Name = name;
        SeedMode = seedMode;
        MaintenanceMode = maintenanceMode;
        FootprintTileIds = footprintTileIds.ToArray();
        SeedTileIds = seedTileIds.ToArray();
        InheritedField = inheritedField;
    }

    public int RuntimeId { get; }

    public int PlacementOrder { get; }

    public string Name { get; }

    public BuildingNavigationSeedMode SeedMode { get; }

    public BuildingNavigationMaintenanceMode MaintenanceMode { get; }

    public IReadOnlyList<int> FootprintTileIds { get; }

    public IReadOnlyList<int> SeedTileIds { get; }

    public BuildingNavigationFieldSnapshot? InheritedField { get; }
}

// This is a main-thread-created value object. The runtime worker receives it instead of a Cave,
// Tile, Building, or mutable BfsField reference.
public sealed class BuildingNavigationTopologySnapshot
{
    public BuildingNavigationTopologySnapshot(
        long topologyVersion,
        long reachabilityVersion,
        int tileCapacity,
        IReadOnlyList<BuildingNavigationTileSnapshot> tiles,
        IReadOnlyList<BuildingNavigationBuildingSnapshot> buildings)
    {
        TopologyVersion = topologyVersion;
        ReachabilityVersion = reachabilityVersion;
        TileCapacity = tileCapacity;
        Tiles = tiles.ToArray();
        Buildings = buildings.ToArray();
    }

    public long TopologyVersion { get; }

    public long ReachabilityVersion { get; }

    public int TileCapacity { get; }

    public IReadOnlyList<BuildingNavigationTileSnapshot> Tiles { get; }

    public IReadOnlyList<BuildingNavigationBuildingSnapshot> Buildings { get; }
}

// Incremental topology transport for normal mutations. Tile records are copied on the main
// thread, while the worker keeps all unchanged topology in its private mirror.
public sealed class BuildingNavigationTopologyDelta
{
    public BuildingNavigationTopologyDelta(
        long topologyVersion,
        long reachabilityVersion,
        int tileCapacity,
        IReadOnlyList<BuildingNavigationTileSnapshot> tileUpdates,
        IReadOnlyList<string> removedTileKeys,
        IReadOnlyList<int> dirtyTileIds,
        IReadOnlyList<BuildingNavigationBuildingSnapshot>? buildingUpdates = null)
    {
        TopologyVersion = topologyVersion;
        ReachabilityVersion = reachabilityVersion;
        TileCapacity = tileCapacity;
        TileUpdates = tileUpdates.ToArray();
        RemovedTileKeys = removedTileKeys.ToArray();
        DirtyTileIds = dirtyTileIds.ToArray();
        HasBuildingChanges = buildingUpdates is not null;
        BuildingUpdates = buildingUpdates?.ToArray() ?? [];
    }

    public long TopologyVersion { get; }

    public long ReachabilityVersion { get; }

    public int TileCapacity { get; }

    public IReadOnlyList<BuildingNavigationTileSnapshot> TileUpdates { get; }

    public IReadOnlyList<string> RemovedTileKeys { get; }

    public IReadOnlyList<int> DirtyTileIds { get; }

    // Present only for structural changes; when present it is the complete active async set.
    public IReadOnlyList<BuildingNavigationBuildingSnapshot> BuildingUpdates { get; }

    public bool HasBuildingChanges { get; }
}

public readonly record struct BuildingNavigationTopologyChange(IReadOnlyList<string> DirtyTileKeys, bool StructuralChange);

// Core exposes only this narrow attachment seam; background ownership remains in Runtime.
public interface IBuildingNavigationFieldService
{
    bool IsAttached { get; }
}
