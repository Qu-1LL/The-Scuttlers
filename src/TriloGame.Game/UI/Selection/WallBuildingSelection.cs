using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class WallBuildingSelection
{
    public static Building Resolve(Wall clickedWall, object? currentSelection)
    {
        if (currentSelection is WallSelection wallSelection && wallSelection.Contains(clickedWall))
        {
            return ResolveSelectedWall(clickedWall, wallSelection);
        }

        if (ReferenceEquals(currentSelection, clickedWall))
        {
            return CreatePreferredRowSelection(clickedWall);
        }

        return clickedWall;
    }

    private static Building ResolveSelectedWall(Wall clickedWall, WallSelection selection)
    {
        if (selection.Mode == WallSelectionMode.Group)
        {
            selection.RefreshSelectionFootprint();
            return selection;
        }

        if (selection.Mode == WallSelectionMode.HorizontalRow &&
            selection.IsSameAnchor(clickedWall) &&
            HasLineAlongAxis(clickedWall, horizontal: false))
        {
            return CreateRowSelection(clickedWall, WallSelectionMode.VerticalRow);
        }

        return CreateGroupSelection(clickedWall);
    }

    private static WallSelection CreatePreferredRowSelection(Wall anchorWall)
    {
        var horizontalLength = GetLineLength(anchorWall, horizontal: true);
        var verticalLength = GetLineLength(anchorWall, horizontal: false);
        var mode = verticalLength > horizontalLength
            ? WallSelectionMode.VerticalRow
            : WallSelectionMode.HorizontalRow;
        return CreateRowSelection(anchorWall, mode);
    }

    private static WallSelection CreateRowSelection(Wall anchorWall, WallSelectionMode mode)
    {
        var walls = GetAlignedWalls(anchorWall, mode == WallSelectionMode.HorizontalRow);
        return new WallSelection(anchorWall, mode, walls);
    }

    private static WallSelection CreateGroupSelection(Wall anchorWall)
    {
        var walls = GetConnectedWalls(anchorWall);
        return new WallSelection(anchorWall, WallSelectionMode.Group, walls);
    }

    private static bool HasLineAlongAxis(Wall anchorWall, bool horizontal)
    {
        return GetLineLength(anchorWall, horizontal) > 1;
    }

    private static int GetLineLength(Wall anchorWall, bool horizontal)
    {
        return GetAlignedWalls(anchorWall, horizontal).Count;
    }

    private static List<Wall> GetAlignedWalls(Wall anchorWall, bool horizontal)
    {
        var deltaX = horizontal ? 1 : 0;
        var deltaY = horizontal ? 0 : 1;
        var walls = new List<Wall>();
        var reverse = new List<Wall>();

        var current = GetAdjacentWall(anchorWall, -deltaX, -deltaY);
        while (current is not null)
        {
            reverse.Add(current);
            current = GetAdjacentWall(current, -deltaX, -deltaY);
        }

        for (var index = reverse.Count - 1; index >= 0; index--)
        {
            walls.Add(reverse[index]);
        }

        walls.Add(anchorWall);
        current = GetAdjacentWall(anchorWall, deltaX, deltaY);
        while (current is not null)
        {
            walls.Add(current);
            current = GetAdjacentWall(current, deltaX, deltaY);
        }

        return walls;
    }

    // Flood-fill contiguous walls so delete/highlight operate on the same connected group.
    private static List<Wall> GetConnectedWalls(Wall anchorWall)
    {
        var walls = new List<Wall>();
        if (anchorWall.TileArray.Count == 0)
        {
            return walls;
        }

        var frontier = new Queue<Wall>();
        var seen = new HashSet<Wall>();
        frontier.Enqueue(anchorWall);
        seen.Add(anchorWall);

        while (frontier.Count > 0)
        {
            var wall = frontier.Dequeue();
            walls.Add(wall);
            var tile = wall.TileArray[0];
            foreach (var neighborTile in tile.Neighbors)
            {
                if (neighborTile.Built is Wall neighborWall &&
                    neighborWall.TileArray.Count > 0 &&
                    seen.Add(neighborWall))
                {
                    frontier.Enqueue(neighborWall);
                }
            }
        }

        walls.Sort(static (left, right) =>
        {
            var leftLocation = left.Location!.Value;
            var rightLocation = right.Location!.Value;
            var yComparison = leftLocation.Y.CompareTo(rightLocation.Y);
            return yComparison != 0 ? yComparison : leftLocation.X.CompareTo(rightLocation.X);
        });
        return walls;
    }

    private static Wall? GetAdjacentWall(Wall sourceWall, int deltaX, int deltaY)
    {
        if (sourceWall.Cave is null || sourceWall.Location is not { } location)
        {
            return null;
        }

        var adjacentTile = sourceWall.Cave.GetTile(new GridPoint(location.X + deltaX, location.Y + deltaY));
        return adjacentTile?.Built as Wall;
    }
}
