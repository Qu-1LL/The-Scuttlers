using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.UI.Selection;

public static class BuildingSelectionResolver
{
    public static Building Resolve(Building clickedBuilding, object? currentSelection)
    {
        return clickedBuilding switch
        {
            Wall wall => WallBuildingSelection.Resolve(wall, currentSelection),
            _ => RanchBuildingSelection.Resolve(clickedBuilding, currentSelection)
        };
    }
}
