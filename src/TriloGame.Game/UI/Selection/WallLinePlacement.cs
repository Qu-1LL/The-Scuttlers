using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class WallLinePlacement
{
    // Snap wall drags to the axis that leaves the cursor closest to the preview line.
    public static GridPoint ResolveEnd(GridPoint start, GridPoint hoveredTile)
    {
        return BuildPlacementDragLayout.ResolveAxisLineEnd(start, hoveredTile);
    }

    // Expand the snapped endpoints into one tile per wall segment.
    public static List<GridPoint> BuildLine(GridPoint start, GridPoint hoveredTile)
    {
        return BuildPlacementDragLayout.BuildAxisLine(start, hoveredTile);
    }
}
