using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public readonly record struct SoilAreaPatchPlacement(SoilPatch SoilPatch, GridPoint Location);

public sealed class SoilArea : Building
{
    private readonly Dictionary<SoilPatch, GridPoint> _patchOffsets = [];
    private readonly HashSet<SoilPatch> _soilPatches = [];
    private readonly HashSet<SoilTile> _soilTiles = [];

    public SoilArea(GameSession session)
        : base("Soil Area", new GridPoint(1, 1), [[1]], session, false, false)
    {
        TextureKey = "SoilTile_0";
        Description = "A grouped soil area made from soil patches placed together.";
    }

    public IReadOnlyCollection<SoilPatch> SoilPatches => _soilPatches;

    public IReadOnlyCollection<SoilTile> SoilTiles => _soilTiles;

    public Ranch? Ranch { get; internal set; }

    public bool IsStillValid => _soilPatches.Count > 0 && TileArray.Count > 0 && Cave is not null;

    public bool AreAllPatchesBuilt(Cave cave)
    {
        foreach (var soilPatch in _soilPatches)
        {
            if (soilPatch.Cave != cave || soilPatch.Location is null)
            {
                return false;
            }
        }

        return _soilPatches.Count > 0;
    }

    public bool Contains(SoilPatch soilPatch) => _soilPatches.Contains(soilPatch);

    public IReadOnlyList<SoilAreaPatchPlacement> GetPatchPlacements(GridPoint areaLocation)
    {
        var placements = new List<SoilAreaPatchPlacement>(_soilPatches.Count);
        foreach (var soilPatch in _soilPatches)
        {
            var offset = _patchOffsets.TryGetValue(soilPatch, out var storedOffset)
                ? storedOffset
                : GridPoint.Zero;
            placements.Add(new SoilAreaPatchPlacement(
                soilPatch,
                new GridPoint(areaLocation.X + offset.X, areaLocation.Y + offset.Y)));
        }

        placements.Sort(static (left, right) =>
        {
            var yComparison = left.Location.Y.CompareTo(right.Location.Y);
            return yComparison != 0 ? yComparison : left.Location.X.CompareTo(right.Location.X);
        });
        return placements;
    }

    public override int Tick(Cave cave)
    {
        var advancedTiles = 0;
        foreach (var soilPatch in _soilPatches)
        {
            advancedTiles += soilPatch.Tick(cave);
        }

        return advancedTiles;
    }

    public override bool RemoveFromGame(object? source = null)
    {
        var removed = false;
        foreach (var soilPatch in _soilPatches.ToArray())
        {
            if (soilPatch.Cave is not null)
            {
                removed |= soilPatch.RemoveFromGame(source ?? "soilAreaRemove");
            }
        }

        return removed;
    }

    internal bool AddSoilPatch(SoilPatch soilPatch)
    {
        return AddSoilPatch(soilPatch, TryGetLiveLocalOffset(soilPatch, out var liveOffset) ? liveOffset : GridPoint.Zero);
    }

    internal bool AddSoilPatch(SoilPatch soilPatch, GridPoint localOffset)
    {
        if (ReferenceEquals(soilPatch.SoilArea, this) && _soilPatches.Contains(soilPatch))
        {
            _patchOffsets[soilPatch] = localOffset;
            RefreshPlannedFootprint();
            return false;
        }

        soilPatch.SoilArea?.RemoveSoilPatch(soilPatch);
        soilPatch.SoilArea = this;
        _soilPatches.Add(soilPatch);
        _patchOffsets[soilPatch] = localOffset;
        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            _soilTiles.Add(soilPatch.SoilTiles[index]);
        }

        RefreshPlannedFootprint();
        return true;
    }

    internal bool RemoveSoilPatch(SoilPatch soilPatch)
    {
        if (!_soilPatches.Remove(soilPatch))
        {
            return false;
        }

        for (var index = 0; index < soilPatch.SoilTiles.Count; index++)
        {
            _soilTiles.Remove(soilPatch.SoilTiles[index]);
        }

        if (ReferenceEquals(soilPatch.SoilArea, this))
        {
            soilPatch.SoilArea = null;
        }

        _patchOffsets.Remove(soilPatch);
        RefreshSelectionFootprint();
        return true;
    }

    internal void RebuildPatchOffsetsFromLiveLocations()
    {
        if (!TryGetLiveBounds(out var minX, out var minY, out _, out _))
        {
            RefreshPlannedFootprint();
            return;
        }

        foreach (var soilPatch in _soilPatches)
        {
            if (soilPatch.Location is not { } location)
            {
                continue;
            }

            _patchOffsets[soilPatch] = new GridPoint(location.X - minX, location.Y - minY);
        }

        RefreshSelectionFootprint();
    }

    internal bool TryGetLiveBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;
        foreach (var soilPatch in _soilPatches)
        {
            if (soilPatch.Location is not { } location)
            {
                continue;
            }

            minX = Math.Min(minX, location.X);
            minY = Math.Min(minY, location.Y);
            maxX = Math.Max(maxX, location.X + soilPatch.Size.X - 1);
            maxY = Math.Max(maxY, location.Y + soilPatch.Size.Y - 1);
        }

        return minX != int.MaxValue;
    }

    internal void RefreshSelectionFootprint(Ranch? ranchFilter = null)
    {
        var tiles = new List<Tile>();
        var seen = new HashSet<Tile>();
        Cave? cave = null;
        foreach (var soilPatch in _soilPatches)
        {
            cave ??= soilPatch.Cave;
            if (soilPatch.Cave is null)
            {
                continue;
            }

            if (ranchFilter is not null && !ReferenceEquals(soilPatch.Ranch, ranchFilter))
            {
                continue;
            }

            for (var index = 0; index < soilPatch.TileArray.Count; index++)
            {
                var tile = soilPatch.TileArray[index];
                if (seen.Add(tile))
                {
                    tiles.Add(tile);
                }
            }
        }

        Cave = cave;
        TileArray = tiles;
        if (tiles.Count == 0)
        {
            Location = null;
            RefreshPlannedFootprint();
            return;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        for (var index = 0; index < tiles.Count; index++)
        {
            var point = tiles[index].Coordinates;
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        Location = new GridPoint(minX, minY);
        Size = new GridPoint((maxX - minX) + 1, (maxY - minY) + 1);
        DisplayBaseSize = Size;
        OpenMap = BuildOpenMap(tiles, Location.Value, Size);
        Description = $"A soil area with {_soilPatches.Count} patch{(_soilPatches.Count == 1 ? string.Empty : "es")}.";
    }

    private bool TryGetLiveLocalOffset(SoilPatch soilPatch, out GridPoint localOffset)
    {
        localOffset = GridPoint.Zero;
        if (soilPatch.Location is not { } location)
        {
            return false;
        }

        if (!TryGetLiveBounds(out var minX, out var minY, out _, out _))
        {
            return false;
        }

        localOffset = new GridPoint(location.X - minX, location.Y - minY);
        return true;
    }

    private void RefreshPlannedFootprint()
    {
        RefreshRecipe();
        if (_patchOffsets.Count == 0)
        {
            Size = new GridPoint(1, 1);
            DisplayBaseSize = Size;
            OpenMap = [[1]];
            Description = "A grouped soil area made from soil patches placed together.";
            return;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var pair in _patchOffsets)
        {
            var soilPatch = pair.Key;
            var offset = pair.Value;
            minX = Math.Min(minX, offset.X);
            minY = Math.Min(minY, offset.Y);
            maxX = Math.Max(maxX, offset.X + soilPatch.Size.X - 1);
            maxY = Math.Max(maxY, offset.Y + soilPatch.Size.Y - 1);
        }

        Size = new GridPoint((maxX - minX) + 1, (maxY - minY) + 1);
        DisplayBaseSize = Size;
        OpenMap = BuildFilledOpenMap(Size);
        Description = $"A soil area with {_soilPatches.Count} patch{(_soilPatches.Count == 1 ? string.Empty : "es")}.";
    }

    private void RefreshRecipe()
    {
        var resourceAmounts = new Dictionary<ResourceName, int>();
        var categoryAmounts = new Dictionary<ResourceCategory, int>();
        foreach (var soilPatch in _soilPatches)
        {
            var patchRecipe = soilPatch.GetRecipe();
            if (patchRecipe is null)
            {
                continue;
            }

            foreach (var requirement in patchRecipe)
            {
                if (requirement.SpecificResource is { } specificResource)
                {
                    resourceAmounts[specificResource] = resourceAmounts.GetValueOrDefault(specificResource) + requirement.Amount;
                    continue;
                }

                var category = requirement.Category!.Value;
                categoryAmounts[category] = categoryAmounts.GetValueOrDefault(category) + requirement.Amount;
            }
        }

        var recipe = new List<ResourceRequirement>(resourceAmounts.Count + categoryAmounts.Count);
        foreach (var resourceType in Enum.GetValues<ResourceName>())
        {
            if (resourceAmounts.TryGetValue(resourceType, out var amount) && amount > 0)
            {
                recipe.Add(ResourceRequirement.ForResource(resourceType, amount));
            }
        }

        foreach (var category in Enum.GetValues<ResourceCategory>())
        {
            if (categoryAmounts.TryGetValue(category, out var amount) && amount > 0)
            {
                recipe.Add(ResourceRequirement.ForCategory(category, amount));
            }
        }

        Recipe = recipe;
    }

    private static int[][] BuildFilledOpenMap(GridPoint size)
    {
        var map = new int[size.Y][];
        for (var row = 0; row < size.Y; row++)
        {
            map[row] = new int[size.X];
            Array.Fill(map[row], 1);
        }

        return map;
    }

    private static int[][] BuildOpenMap(IReadOnlyList<Tile> tiles, GridPoint location, GridPoint size)
    {
        var map = new int[size.Y][];
        for (var row = 0; row < size.Y; row++)
        {
            map[row] = new int[size.X];
            Array.Fill(map[row], 2);
        }

        for (var index = 0; index < tiles.Count; index++)
        {
            var point = tiles[index].Coordinates;
            map[point.Y - location.Y][point.X - location.X] = 1;
        }

        return map;
    }
}
