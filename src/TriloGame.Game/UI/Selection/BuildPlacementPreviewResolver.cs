using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class BuildPlacementPreviewResolver
{
    // Resolve the snapped placement anchors for the current hovered tile and optional drag origin.
    public static List<GridPoint> ResolveLocations(Building targetBuilding, GridPoint hoveredTile, GridPoint? dragStart = null)
    {
        if (targetBuilding is IBuildPlacementDragTarget dragTarget)
        {
            return BuildPlacementDragLayout.BuildLocations(dragTarget, dragStart ?? hoveredTile, hoveredTile);
        }

        return [hoveredTile];
    }
}
