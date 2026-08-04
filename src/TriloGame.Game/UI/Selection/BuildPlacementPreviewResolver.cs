using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Selection;

public static class BuildPlacementPreviewResolver
{
    public static List<GridPoint> ResolveLocations(Building targetBuilding, GridPoint hoveredTile, GridPoint? dragStart = null)
    {
        return BuildPlacementDragLayout.BuildLocations(targetBuilding, dragStart ?? hoveredTile, hoveredTile);
    }
}
