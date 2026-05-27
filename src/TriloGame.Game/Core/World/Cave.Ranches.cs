using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
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
    private readonly List<Ranch> _ranches = [];

    public IReadOnlyList<Garage> GetGarages() => _garages;

    public IReadOnlyList<SoilPatch> GetSoilPatches() => _soilPatches;

    public IReadOnlyList<SoilTile> GetSoilTiles() => _soilTiles;

    public IReadOnlyList<Ranch> GetRanches() => _ranches;

    public SoilTile? GetSoilTile(GridPoint location)
    {
        return _soilTileLookup.TryGetValue(location, out var soilTile)
            ? soilTile
            : null;
    }

    public bool CanBuildSoilArea(SoilArea soilArea, GridPoint location, bool preserveReachability = false)
    {
        var placements = soilArea.GetPatchPlacements(location);
        if (placements.Count == 0)
        {
            return false;
        }

        var occupied = new HashSet<GridPoint>();
        for (var placementIndex = 0; placementIndex < placements.Count; placementIndex++)
        {
            var placement = placements[placementIndex];
            for (var x = 0; x < placement.SoilPatch.Size.X; x++)
            {
                for (var y = 0; y < placement.SoilPatch.Size.Y; y++)
                {
                    if (!occupied.Add(new GridPoint(placement.Location.X + x, placement.Location.Y + y)))
                    {
                        return false;
                    }
                }
            }

            if (!CanBuild(placement.SoilPatch, placement.Location, preserveReachability))
            {
                return false;
            }
        }

        return true;
    }

    public bool BuildSoilArea(SoilArea soilArea, GridPoint location)
    {
        if (!CanBuildSoilArea(soilArea, location))
        {
            return false;
        }

        var placements = soilArea.GetPatchPlacements(location);
        var builtPatches = new List<Building>(placements.Count);
        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            PlaceBuildingUnchecked(placement.SoilPatch, placement.Location);
            builtPatches.Add(placement.SoilPatch);
        }

        soilArea.RebuildPatchOffsetsFromLiveLocations();
        var mergedArea = MergeAdjacentCompatibleSoilAreas(soilArea);
        AttachSoilAreaToRanch(mergedArea);
        FinalizeBuiltBuildings(builtPatches);
        return true;
    }

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
            var worldLocation = soilTile.WorldLocation;
            _soilTiles.Remove(soilTile);
            if (worldLocation is not null)
            {
                _soilTileLookup.Remove(worldLocation.Value);
            }
        }
    }

    private bool CanPlaceRanchBuilding(Building building, GridPoint location)
    {
        return building switch
        {
            SoilPatch => CanPlaceSoilPatch(location),
            // Garages can sit beside an existing ranch; they stay idle until ranchless soil reaches them.
            Garage => true,
            _ => true
        };
    }

    private bool CanPlaceSoilPatch(GridPoint location)
    {
        return GetTile(location) is not null;
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
            case SoilPatch soilPatch:
                if (soilPatch.SoilArea is { } soilArea)
                {
                    if (!soilArea.AreAllPatchesBuilt(this))
                    {
                        return;
                    }

                    var mergedArea = MergeAdjacentCompatibleSoilAreas(soilArea);
                    AttachSoilAreaToRanch(mergedArea);
                    break;
                }

                AttachSoilPatchToRanch(soilPatch);
                break;
            case Garage garage:
                AttachGarageToRanch(garage);
                AttachGarageToAdjacentSilos(garage);
                break;
            case Silo silo:
                AttachSiloToAdjacentNetwork(silo);
                break;
        }
    }

    private void OnRanchBuildingRemoved(Building building)
    {
        switch (building)
        {
            case SoilPatch soilPatch:
                RemoveSoilPatchFromRanch(soilPatch);
                soilPatch.SoilArea?.RemoveSoilPatch(soilPatch);
                break;
            case Garage garage:
                DetachGarageFromAdjacentSilos(garage);
                RemoveGarageFromRanch(garage);
                break;
            case Silo silo:
                DetachSiloFromAdjacentNetwork(silo);
                break;
        }
    }

    private void AttachSoilPatchToRanch(SoilPatch soilPatch)
    {
        if (soilPatch.SoilArea is { } soilArea)
        {
            AttachSoilAreaToRanch(soilArea);
            return;
        }

        var targetRanch = FindAdjacentRanchForSoilPatch(soilPatch);
        if (targetRanch is null)
        {
            return;
        }

        var changed = false;
        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            changed |= targetRanch.AddSoil(soilPatch.SoilTiles[index]);
        }

        if (changed)
        {
            targetRanch.RefreshSelectionFootprint();
            targetRanch.RebuildPlowPath();
        }
    }

    // Only rectangular soil areas can join a ranch through soil adjacency.
    private bool AttachSoilAreaToRanch(SoilArea soilArea)
    {
        if (!soilArea.AreAllPatchesBuilt(this))
        {
            return false;
        }

        var mergedArea = MergeAdjacentCompatibleSoilAreas(soilArea);
        if (!IsRectangularSoilArea(mergedArea))
        {
            return false;
        }

        var targetRanch = mergedArea.Ranch;
        if (targetRanch is null)
        {
            var adjacentGarage = FindAdjacentGarageForSoilArea(mergedArea);
            if (adjacentGarage is null)
            {
                return false;
            }

            targetRanch = adjacentGarage.Ranch ?? CreateRanch(adjacentGarage);
        }

        var changed = AddSoilAreaToRanch(targetRanch, mergedArea);
        if (!changed)
        {
            return false;
        }

        targetRanch.RefreshSelectionFootprint();
        targetRanch.RebuildPlowPath();
        return true;
    }

    private Garage? FindAdjacentGarageForSoilArea(SoilArea soilArea)
    {
        foreach (var soilPatch in GetSoilAreaPatchesInStableOrder(soilArea))
        {
            for (var tileIndex = 0; tileIndex < soilPatch.SoilTiles.Count; tileIndex++)
            {
                var soilTile = soilPatch.SoilTiles[tileIndex];
                foreach (var adjacentGarage in GetAdjacentGarages(soilTile))
                {
                    return adjacentGarage;
                }
            }
        }

        return null;
    }

    private Ranch? FindAdjacentRanchForSoilPatch(SoilPatch soilPatch)
    {
        for (var tileIndex = 0; tileIndex < soilPatch.SoilTiles.Count; tileIndex++)
        {
            var soilTile = soilPatch.SoilTiles[tileIndex];
            foreach (var adjacentGarage in GetAdjacentGarages(soilTile))
            {
                return adjacentGarage.Ranch ?? CreateRanch(adjacentGarage);
            }

            foreach (var adjacentSoil in GetAdjacentSoilTiles(soilTile))
            {
                if (adjacentSoil.Ranch is not null)
                {
                    return adjacentSoil.Ranch;
                }
            }
        }

        return null;
    }

    private void AttachGarageToRanch(Garage garage)
    {
        var ranch = garage.Ranch ?? CreateRanch(garage);
        var changed = false;
        foreach (var soilArea in GetAdjacentSoilAreas(garage))
        {
            changed |= AttachSoilAreaToRanch(soilArea);
        }

        if (!changed)
        {
            ranch.RefreshSelectionFootprint();
            ranch.RebuildPlowPath();
        }
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

    private SoilArea MergeAdjacentCompatibleSoilAreas(SoilArea soilArea)
    {
        var result = soilArea;
        var merged = true;
        while (merged)
        {
            merged = false;
            foreach (var adjacentArea in GetAdjacentSoilAreas(result))
            {
                if (!CanMergeSoilAreas(result, adjacentArea))
                {
                    continue;
                }

                result = MergeSoilAreas(result, adjacentArea);
                merged = true;
                break;
            }
        }

        return result;
    }

    private IEnumerable<SoilArea> GetAdjacentSoilAreas(SoilArea soilArea)
    {
        var yielded = new HashSet<SoilArea>();
        foreach (var soilPatch in soilArea.SoilPatches)
        {
            for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
            {
                foreach (var adjacentSoil in GetAdjacentSoilTiles(soilPatch.SoilTiles[index]))
                {
                    var adjacentArea = adjacentSoil.ParentPatch.SoilArea;
                    if (adjacentArea is not null &&
                        !ReferenceEquals(adjacentArea, soilArea) &&
                        yielded.Add(adjacentArea))
                    {
                        yield return adjacentArea;
                    }
                }
            }
        }
    }

    private static bool CanMergeSoilAreas(SoilArea left, SoilArea right)
    {
        var ranchLinkedCount = (left.Ranch is not null ? 1 : 0) + (right.Ranch is not null ? 1 : 0);
        if (ranchLinkedCount > 1 ||
            !left.TryGetLiveBounds(out var leftMinX, out var leftMinY, out var leftMaxX, out var leftMaxY) ||
            !right.TryGetLiveBounds(out var rightMinX, out var rightMinY, out var rightMaxX, out var rightMaxY))
        {
            return false;
        }

        var sameVerticalSpan = leftMinY == rightMinY && leftMaxY == rightMaxY;
        var sameHorizontalSpan = leftMinX == rightMinX && leftMaxX == rightMaxX;
        return (sameVerticalSpan && (leftMaxX + 1 == rightMinX || rightMaxX + 1 == leftMinX)) ||
               (sameHorizontalSpan && (leftMaxY + 1 == rightMinY || rightMaxY + 1 == leftMinY));
    }

    private static SoilArea MergeSoilAreas(SoilArea left, SoilArea right)
    {
        var target = left.Ranch is not null
            ? left
            : right.Ranch is not null ? right : left;
        var source = ReferenceEquals(target, left) ? right : left;
        foreach (var soilPatch in source.SoilPatches.ToArray())
        {
            target.AddSoilPatch(soilPatch);
        }

        source.Ranch = null;
        target.RebuildPatchOffsetsFromLiveLocations();
        source.RefreshSelectionFootprint();
        return target;
    }

    private bool AddSoilAreaToRanch(Ranch targetRanch, SoilArea soilArea)
    {
        var changed = false;
        foreach (var soilPatch in GetSoilAreaPatchesInStableOrder(soilArea))
        {
            if (soilPatch.Cave != this)
            {
                continue;
            }

            for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
            {
                var soilTile = soilPatch.SoilTiles[index];
                if (soilTile.Ranch is not null && !ReferenceEquals(soilTile.Ranch, targetRanch))
                {
                    continue;
                }

                changed |= targetRanch.AddSoil(soilTile);
            }
        }

        return changed;
    }

    private static bool IsRectangularSoilArea(SoilArea soilArea)
    {
        return soilArea.TryGetLiveBounds(out var minX, out var minY, out var maxX, out var maxY) &&
               soilArea.SoilTiles.Count == ((maxX - minX) + 1) * ((maxY - minY) + 1);
    }

    private IEnumerable<SoilPatch> GetSoilAreaPatchesInStableOrder(SoilArea soilArea)
    {
        if (soilArea.Location is not { } areaLocation)
        {
            foreach (var soilPatch in soilArea.SoilPatches)
            {
                yield return soilPatch;
            }

            yield break;
        }

        var placements = soilArea.GetPatchPlacements(areaLocation);
        for (var index = 0; index < placements.Count; index++)
        {
            yield return placements[index].SoilPatch;
        }
    }

    private IEnumerable<SoilArea> GetAdjacentSoilAreas(Building building)
    {
        var yielded = new HashSet<SoilArea>();
        foreach (var soilTile in GetAdjacentSoilTiles(building))
        {
            var adjacentArea = soilTile.ParentPatch.SoilArea;
            if (adjacentArea is not null && yielded.Add(adjacentArea))
            {
                yield return adjacentArea;
            }
        }
    }

    private void RemoveSoilPatchFromRanch(SoilPatch soilPatch)
    {
        var ranch = soilPatch.Ranch;
        if (ranch is null)
        {
            return;
        }

        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            ranch.RemoveSoil(soilPatch.SoilTiles[index]);
        }

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
        ReattachRanchlessSoilsToReachableRanches();
    }

    // After soil disappears, keep only the soil component that still reaches the garage.
    private void PruneDisconnectedSoils(Ranch ranch)
    {
        var garage = ranch.Garage;
        if (garage is null)
        {
            _ranches.Remove(ranch);
            ranch.Dissolve();
            return;
        }

        var connected = new HashSet<SoilTile>();
        var queue = new Queue<SoilTile>();
        foreach (var soilTile in GetAdjacentSoilTiles(garage))
        {
            if (ReferenceEquals(soilTile.Ranch, ranch) && connected.Add(soilTile))
            {
                queue.Enqueue(soilTile);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetAdjacentSoilTiles(current))
            {
                if (ReferenceEquals(neighbor.Ranch, ranch) && connected.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (var soilTile in ranch.SoilTiles.ToArray())
        {
            if (!connected.Contains(soilTile))
            {
                ranch.RemoveSoil(soilTile);
            }
        }

        ranch.RefreshSelectionFootprint();
        ranch.RebuildPlowPath();
    }

    // Soil left behind by a removed garage can rejoin any remaining garage-backed ranch it reaches.
    private void ReattachRanchlessSoilsToReachableRanches()
    {
        var passChanged = true;
        while (passChanged)
        {
            passChanged = false;
            foreach (var soilArea in GetSoilAreas())
            {
                if (AttachSoilAreaToRanch(soilArea))
                {
                    passChanged = true;
                }
            }
        }
    }

    private IEnumerable<SoilArea> GetSoilAreas()
    {
        var yielded = new HashSet<SoilArea>();
        for (var index = 0; index < _soilPatches.Count; index++)
        {
            var soilArea = _soilPatches[index].SoilArea;
            if (soilArea is not null && yielded.Add(soilArea))
            {
                yield return soilArea;
            }
        }
    }

    private IEnumerable<SoilTile> GetAdjacentSoilTiles(Building building)
    {
        var yielded = new HashSet<SoilTile>();
        foreach (var tile in building.TileArray)
        {
            foreach (var direction in SoilNeighborDirections)
            {
                var location = new GridPoint(tile.Coordinates.X + direction.X, tile.Coordinates.Y + direction.Y);
                var soilTile = GetSoilTile(location);
                if (soilTile is not null && yielded.Add(soilTile))
                {
                    yield return soilTile;
                }
            }
        }
    }

    private IEnumerable<SoilTile> GetAdjacentSoilTiles(SoilTile soilTile)
    {
        var worldLocation = soilTile.WorldLocation;
        if (worldLocation is null)
        {
            yield break;
        }

        foreach (var direction in SoilNeighborDirections)
        {
            var neighbor = GetSoilTile(new GridPoint(worldLocation.Value.X + direction.X, worldLocation.Value.Y + direction.Y));
            if (neighbor is not null)
            {
                yield return neighbor;
            }
        }
    }

    private IEnumerable<Garage> GetAdjacentGarages(SoilTile soilTile)
    {
        var worldLocation = soilTile.WorldLocation;
        if (worldLocation is null)
        {
            yield break;
        }

        var yielded = new HashSet<Garage>();
        foreach (var direction in SoilNeighborDirections)
        {
            var tile = GetTile(new GridPoint(worldLocation.Value.X + direction.X, worldLocation.Value.Y + direction.Y));
            if (tile?.Built is Garage garage && yielded.Add(garage))
            {
                yield return garage;
            }
        }
    }
}
