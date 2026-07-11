using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

internal static class BuildingPlacementDragPlanner
{
    public static List<GridPoint> BuildLocations(GridPoint start, GridPoint end, GridPoint footprintSize)
    {
        var xStep = System.Math.Max(1, footprintSize.X);
        var yStep = System.Math.Max(1, footprintSize.Y);

        if (start.X == end.X)
        {
            return BuildLine(start.X, start.Y, end.Y, yStep, vertical: true);
        }

        if (start.Y == end.Y)
        {
            return BuildLine(start.Y, start.X, end.X, xStep, vertical: false);
        }

        var minX = System.Math.Min(start.X, end.X);
        var maxX = System.Math.Max(start.X, end.X);
        var minY = System.Math.Min(start.Y, end.Y);
        var maxY = System.Math.Max(start.Y, end.Y);
        var widthCount = ((maxX - minX) / xStep) + 1;
        var heightCount = ((maxY - minY) / yStep) + 1;
        var locations = new List<GridPoint>(widthCount * heightCount);

        for (var y = minY; y <= maxY; y += yStep)
        {
            for (var x = minX; x <= maxX; x += xStep)
            {
                locations.Add(new GridPoint(x, y));
            }
        }

        return locations;
    }

    private static List<GridPoint> BuildLine(int fixedCoordinate, int start, int end, int step, bool vertical)
    {
        var min = System.Math.Min(start, end);
        var max = System.Math.Max(start, end);
        var count = ((max - min) / step) + 1;
        var locations = new List<GridPoint>(count);

        for (var coordinate = min; coordinate <= max; coordinate += step)
        {
            locations.Add(vertical
                ? new GridPoint(fixedCoordinate, coordinate)
                : new GridPoint(coordinate, fixedCoordinate));
        }

        return locations;
    }
}
