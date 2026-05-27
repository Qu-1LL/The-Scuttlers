using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public enum SoilAreaSelectionMode
{
    Row,
    Column
}

public sealed class SoilAreaSelection : Building
{
    private readonly SoilPatch[] _soilPatches;

    public SoilAreaSelection(SoilArea soilArea, SoilPatch anchorPatch, SoilAreaSelectionMode mode, IEnumerable<SoilPatch> soilPatches)
        : base(mode == SoilAreaSelectionMode.Row ? "Soil Area Row" : "Soil Area Column", new GridPoint(1, 1), [[1]], soilArea.Session, false)
    {
        SoilArea = soilArea;
        AnchorPatch = anchorPatch;
        Mode = mode;
        TextureKey = "SoilTile_0";
        _soilPatches = soilPatches.Distinct().ToArray();
        Description = mode == SoilAreaSelectionMode.Row
            ? "A selected row inside a soil area."
            : "A selected column inside a soil area.";
        RefreshSelectionFootprint();
    }

    public SoilArea SoilArea { get; }

    public SoilPatch AnchorPatch { get; }

    public SoilAreaSelectionMode Mode { get; }

    public IReadOnlyList<SoilPatch> SoilPatches => _soilPatches;

    public bool IsStillValid => _soilPatches.Any(soilPatch => soilPatch.Cave is not null) && TileArray.Count > 0;

    public bool IsSameAnchor(SoilArea soilArea, SoilPatch soilPatch)
    {
        return ReferenceEquals(SoilArea, soilArea) && ReferenceEquals(AnchorPatch, soilPatch);
    }

    public override bool RemoveFromGame(object? source = null)
    {
        var removed = false;
        for (var index = 0; index < _soilPatches.Length; index++)
        {
            var soilPatch = _soilPatches[index];
            if (soilPatch.Cave is not null)
            {
                removed |= soilPatch.RemoveFromGame(source ?? "soilAreaSelectionRemove");
            }
        }

        return removed;
    }

    // Row and column selections are transient outlines over the live soil patch footprints.
    public void RefreshSelectionFootprint()
    {
        var tiles = new List<Tile>();
        var seen = new HashSet<Tile>();
        Cave? cave = null;
        for (var patchIndex = 0; patchIndex < _soilPatches.Length; patchIndex++)
        {
            var soilPatch = _soilPatches[patchIndex];
            cave ??= soilPatch.Cave;
            for (var tileIndex = 0; tileIndex < soilPatch.TileArray.Count; tileIndex++)
            {
                var tile = soilPatch.TileArray[tileIndex];
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
            Size = new GridPoint(1, 1);
            DisplayBaseSize = Size;
            OpenMap = [[1]];
            return;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        for (var index = 0; index < tiles.Count; index++)
        {
            var point = tiles[index].Coordinates;
            minX = System.Math.Min(minX, point.X);
            minY = System.Math.Min(minY, point.Y);
            maxX = System.Math.Max(maxX, point.X);
            maxY = System.Math.Max(maxY, point.Y);
        }

        Location = new GridPoint(minX, minY);
        Size = new GridPoint((maxX - minX) + 1, (maxY - minY) + 1);
        DisplayBaseSize = Size;
        OpenMap = BuildOpenMap(tiles, Location.Value, Size);
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
