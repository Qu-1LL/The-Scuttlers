using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.UI.Selection;

public static class RanchBuildingSelection
{
    public static Building Resolve(Building clickedBuilding, object? currentSelection)
    {
        var ranch = GetRanch(clickedBuilding);
        if (ranch is null)
        {
            return clickedBuilding;
        }

        if (ReferenceEquals(currentSelection, ranch) || ReferenceEquals(currentSelection, clickedBuilding))
        {
            return clickedBuilding;
        }

        return ranch;
    }

    public static Ranch? GetRanch(Building building)
    {
        return building switch
        {
            Soil soil => soil.Ranch,
            Garage garage => garage.Ranch,
            _ => null
        };
    }
}
