using System.Numerics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public abstract class StationBuilding : Building, IStationBuilding
{
    private readonly Dictionary<Creature, int> _assignedStationIndices = [];
    private readonly StationSlot[] _stations;

    protected StationBuilding(
        string name,
        GridPoint size,
        int[][] openMap,
        GameSession session,
        string textureKey,
        string description,
        int fighterAssignmentPriority,
        IEnumerable<StationSlot> stations)
        : base(name, size, openMap, session, true)
    {
        TextureKey = textureKey;
        Description = description;
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        FighterAssignmentPriority = fighterAssignmentPriority;
        _stations = stations.ToArray();
    }

    public IReadOnlyList<StationSlot> Stations => _stations;

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Asynchronous;

    public int Capacity => _stations.Length;

    public int FighterAssignmentPriority { get; }

    public IReadOnlyCollection<Creature> Assignments => _assignedStationIndices.Keys;

    protected virtual bool TracksAssignments => false;

    public bool HasAssignmentSlot(Creature? creature = null)
    {
        return (creature is not null && _assignedStationIndices.ContainsKey(creature)) ||
               _assignedStationIndices.Count < Capacity;
    }

    public bool CanAssign(Creature creature) => HasAssignmentSlot(creature);

    public bool IsAssigned(Creature creature) => _assignedStationIndices.ContainsKey(creature);

    public int? GetAssignedStationIndex(Creature creature)
    {
        return _assignedStationIndices.TryGetValue(creature, out var stationIndex)
            ? stationIndex
            : null;
    }

    public int GetVolume() => _assignedStationIndices.Count;

    public virtual bool Assign(Creature creature)
    {
        if (!HasAssignmentSlot(creature))
        {
            return false;
        }

        if (_assignedStationIndices.ContainsKey(creature))
        {
            if (TracksAssignments)
            {
                TrackCreature(creature);
            }

            return true;
        }

        var occupiedIndices = _assignedStationIndices.Values.ToHashSet();
        for (var stationIndex = 0; stationIndex < _stations.Length; stationIndex++)
        {
            if (occupiedIndices.Contains(stationIndex))
            {
                continue;
            }

            _assignedStationIndices[creature] = stationIndex;
            if (TracksAssignments)
            {
                TrackCreature(creature);
            }

            Cave?.SyncStationAssignmentCount(this, _assignedStationIndices.Count);
            return true;
        }

        return false;
    }

    public virtual bool RemoveAssignment(Creature creature)
    {
        if (!_assignedStationIndices.Remove(creature))
        {
            return false;
        }

        if (TracksAssignments)
        {
            UntrackCreature(creature);
        }

        Cave?.SyncStationAssignmentCount(this, _assignedStationIndices.Count);
        return true;
    }

    public override void TrackedCreatureDied(Creature creature)
    {
        if (TracksAssignments)
        {
            RemoveAssignment(creature);
        }
    }

    public virtual GridPoint? GetPreferredAccessTile(GridPoint preferredLocation)
    {
        GridPoint? bestTile = null;
        var bestDistance = int.MaxValue;
        string? bestKey = null;
        for (var stationIndex = 0; stationIndex < _stations.Length; stationIndex++)
        {
            if (!TryGetStationTile(stationIndex, out var tileLocation))
            {
                continue;
            }

            var tile = Cave?.GetTile(tileLocation);
            if (tile is null || !tile.CreatureFits() || Cave?.IsTileReachable(tile) != true || tile.EnemyOccupant is not null)
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(preferredLocation, tileLocation);
            if (bestTile is null ||
                distance < bestDistance ||
                (distance == bestDistance && string.CompareOrdinal(tile.Key, bestKey) < 0))
            {
                bestTile = tileLocation;
                bestDistance = distance;
                bestKey = tile.Key;
            }
        }

        return bestTile;
    }

    public virtual GridPoint? GetAssignedNavigationTile(Creature creature, GridPoint preferredLocation)
    {
        return TryGetAssignedStationTile(creature, out var assignedTile)
            ? assignedTile
            : GetPreferredAccessTile(preferredLocation);
    }

    public virtual bool IsCreatureAtNavigationTarget(Creature creature)
    {
        return creature.IsTrackedInTileSystem &&
               TryGetAssignedStationTile(creature, out var assignedTile) &&
               creature.Location == assignedTile;
    }

    public virtual bool IsCreatureStationed(Creature creature)
    {
        return IsCreatureAtNavigationTarget(creature);
    }

    public virtual bool TryStationCreature(Creature creature)
    {
        return IsCreatureStationed(creature);
    }

    public virtual bool TryRestoreCreatureToTileSystem(Creature creature)
    {
        return creature.IsTrackedInTileSystem;
    }

    public virtual bool TryGetAssignedWorldPosition(Creature creature, out Vector2 worldPosition)
    {
        if (TryGetAssignedStationTile(creature, out var assignedTile))
        {
            worldPosition = new Vector2(
                assignedTile.X * TileConstants.TileSize,
                assignedTile.Y * TileConstants.TileSize);
            return true;
        }

        if (GetAssignedStationIndex(creature) is { } stationIndex && TryGetLocalPixelOffset(stationIndex, out var localPixelOffset))
        {
            worldPosition = ResolveWorldPosition(localPixelOffset);
            return true;
        }

        worldPosition = Vector2.Zero;
        return false;
    }

    protected bool TryGetAssignedStationTile(Creature creature, out GridPoint tileLocation)
    {
        if (GetAssignedStationIndex(creature) is { } stationIndex)
        {
            return TryGetStationTile(stationIndex, out tileLocation);
        }

        tileLocation = default;
        return false;
    }

    protected bool TryGetLocalPixelOffset(int stationIndex, out Vector2 localPixelOffset)
    {
        if (stationIndex < 0 || stationIndex >= _stations.Length || !_stations[stationIndex].LocalPixelOffset.HasValue)
        {
            localPixelOffset = Vector2.Zero;
            return false;
        }

        localPixelOffset = _stations[stationIndex].LocalPixelOffset!.Value;
        return true;
    }

    protected bool TryGetStationTile(int stationIndex, out GridPoint tileLocation)
    {
        if (Location is null ||
            stationIndex < 0 ||
            stationIndex >= _stations.Length ||
            !_stations[stationIndex].TileOffset.HasValue)
        {
            tileLocation = default;
            return false;
        }

        tileLocation = new GridPoint(
            Location.Value.X + _stations[stationIndex].TileOffset!.Value.X,
            Location.Value.Y + _stations[stationIndex].TileOffset!.Value.Y);
        return true;
    }

    protected Vector2 ResolveWorldPosition(Vector2 localPixelOffset)
    {
        var location = Location ?? GridPoint.Zero;
        var topLeftWorld = new Vector2(
            (location.X * TileConstants.TileSize) - TileConstants.TileHalfSize,
            (location.Y * TileConstants.TileSize) - TileConstants.TileHalfSize);
        return topLeftWorld + localPixelOffset;
    }

    protected IEnumerable<World.Tile> EnumerateOwnedTiles()
    {
        foreach (var tile in TileArray)
        {
            if (ReferenceEquals(tile.Built, this))
            {
                yield return tile;
            }
        }
    }

    protected static StationSlot[] CreateTileStations(int[][] openMap)
    {
        var stations = new List<StationSlot>();
        for (var y = 0; y < openMap.Length; y++)
        {
            for (var x = 0; x < openMap[y].Length; x++)
            {
                if (openMap[y][x] >= 1)
                {
                    stations.Add(new StationSlot(new GridPoint(x, y), null));
                }
            }
        }

        return stations.ToArray();
    }
}
