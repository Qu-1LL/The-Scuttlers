using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public enum WallSelectionMode
{
    HorizontalRow,
    VerticalRow,
    Group
}

public sealed class WallSelection : Building
{
    private readonly Wall[] _walls;

    public WallSelection(Wall anchorWall, WallSelectionMode mode, IEnumerable<Wall> walls)
        : base(GetName(mode), new GridPoint(1, 1), [[1]], anchorWall.Session, false)
    {
        AnchorWall = anchorWall;
        Mode = mode;
        TextureKey = anchorWall.TextureKey;
        _walls = walls.Distinct().ToArray();
        Description = GetDescription(mode);
        RefreshSelectionFootprint();
    }

    public Wall AnchorWall { get; }

    public WallSelectionMode Mode { get; }

    public IReadOnlyList<Wall> Walls => _walls;

    public bool IsStillValid => _walls.Any(wall => wall.Cave is not null) && TileArray.Count > 0;

    public bool Contains(Wall wall)
    {
        for (var index = 0; index < _walls.Length; index++)
        {
            if (ReferenceEquals(_walls[index], wall))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSameAnchor(Wall wall) => ReferenceEquals(AnchorWall, wall);

    public override bool RemoveFromGame(object? source = null)
    {
        var removed = false;
        for (var index = 0; index < _walls.Length; index++)
        {
            var wall = _walls[index];
            if (wall.Cave is not null)
            {
                removed |= wall.RemoveFromGame(source ?? "wallSelectionRemove");
            }
        }

        return removed;
    }

    // Wall selections are transient outlines over the current live wall footprints.
    public void RefreshSelectionFootprint()
    {
        var tiles = new List<Tile>();
        var seen = new HashSet<Tile>();
        Cave? cave = null;
        for (var wallIndex = 0; wallIndex < _walls.Length; wallIndex++)
        {
            var wall = _walls[wallIndex];
            cave ??= wall.Cave;
            for (var tileIndex = 0; tileIndex < wall.TileArray.Count; tileIndex++)
            {
                var tile = wall.TileArray[tileIndex];
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

    private static string GetName(WallSelectionMode mode)
    {
        return mode switch
        {
            WallSelectionMode.HorizontalRow => "Wall Row",
            WallSelectionMode.VerticalRow => "Wall Column",
            _ => "Wall Group"
        };
    }

    private static string GetDescription(WallSelectionMode mode)
    {
        return mode switch
        {
            WallSelectionMode.HorizontalRow => "A selected horizontal wall row.",
            WallSelectionMode.VerticalRow => "A selected vertical wall column.",
            _ => "A selected contiguous wall group."
        };
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
