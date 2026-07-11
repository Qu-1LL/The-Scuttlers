using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class BuildPlacementDragLayout
{
    public static List<GridPoint> BuildLocations(IBuildPlacementDragTarget dragTarget, GridPoint start, GridPoint hoveredTile)
    {
        return dragTarget.DragPlacementKind switch
        {
            BuildPlacementDragKind.AxisLine => BuildAxisLine(start, hoveredTile),
            BuildPlacementDragKind.FootprintGrid => BuildFootprintGrid(start, hoveredTile, dragTarget.DragPlacementStep),
            _ => [hoveredTile]
        };
    }

    public static GridPoint ResolveAxisLineEnd(GridPoint start, GridPoint hoveredTile)
    {
        var horizontalDistance = Math.Abs(hoveredTile.Y - start.Y);
        var verticalDistance = Math.Abs(hoveredTile.X - start.X);
        return horizontalDistance <= verticalDistance
            ? new GridPoint(hoveredTile.X, start.Y)
            : new GridPoint(start.X, hoveredTile.Y);
    }

    public static List<GridPoint> BuildAxisLine(GridPoint start, GridPoint hoveredTile)
    {
        var end = ResolveAxisLineEnd(start, hoveredTile);
        var locations = new List<GridPoint>(Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) + 1);

        if (start.X == end.X)
        {
            var stepY = end.Y >= start.Y ? 1 : -1;
            for (var y = start.Y; ; y += stepY)
            {
                locations.Add(new GridPoint(start.X, y));
                if (y == end.Y)
                {
                    break;
                }
            }

            return locations;
        }

        var stepX = end.X >= start.X ? 1 : -1;
        for (var x = start.X; ; x += stepX)
        {
            locations.Add(new GridPoint(x, start.Y));
            if (x == end.X)
            {
                break;
            }
        }

        return locations;
    }

    public static List<GridPoint> BuildFootprintGrid(GridPoint start, GridPoint hoveredTile, GridPoint step)
    {
        var stepWidth = Math.Max(1, step.X);
        var stepHeight = Math.Max(1, step.Y);
        var stepX = hoveredTile.X >= start.X ? stepWidth : -stepWidth;
        var stepY = hoveredTile.Y >= start.Y ? stepHeight : -stepHeight;
        var countX = (Math.Abs(hoveredTile.X - start.X) / stepWidth) + 1;
        var countY = (Math.Abs(hoveredTile.Y - start.Y) / stepHeight) + 1;
        var locations = new List<GridPoint>(countX * countY);

        for (var y = 0; y < countY; y++)
        {
            for (var x = 0; x < countX; x++)
            {
                locations.Add(new GridPoint(
                    start.X + (x * stepX),
                    start.Y + (y * stepY)));
            }
        }

        return locations;
    }
}
