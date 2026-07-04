using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class BuildPlacementPreviewResolver
{
    public static List<GridPoint> ResolveLocations(Building targetBuilding, GridPoint hoveredTile, GridPoint? dragStart = null)
    {
        if (targetBuilding is IBuildPlacementDragTarget dragTarget)
        {
            return BuildPlacementDragLayout.BuildLocations(dragTarget, dragStart ?? hoveredTile, hoveredTile);
        }

        return dragStart.HasValue
            ? BuildingPlacementDragPlanner.BuildLocations(dragStart.Value, hoveredTile, targetBuilding.Size)
            : [hoveredTile];
    }
}
