using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering;

public static class BuildingHighlightFootprint
{
    // Selection and scaffold highlights include solid and passable cells, but skip optional/excluded markers.
    public static IEnumerable<GridPoint> EnumerateTiles(Building building)
    {
        if (building.Location is not { } location)
        {
            yield break;
        }

        foreach (var tile in EnumerateTiles(location, building.OpenMap))
        {
            yield return tile;
        }
    }

    public static IEnumerable<GridPoint> EnumerateTiles(GridPoint location, int[][] openMap)
    {
        for (var y = 0; y < openMap.Length; y++)
        {
            var row = openMap[y];
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x] > 1)
                {
                    continue;
                }

                yield return new GridPoint(location.X + x, location.Y + y);
            }
        }
    }
}
