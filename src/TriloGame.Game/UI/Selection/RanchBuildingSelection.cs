using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.UI.Selection;

public static class RanchBuildingSelection
{
    public static Building Resolve(Building clickedBuilding, object? currentSelection)
    {
        if (clickedBuilding is SoilPatch soilPatch && soilPatch.SoilArea is { } soilArea)
        {
            soilArea.RefreshSelectionFootprint();
            return ReferenceEquals(currentSelection, soilArea) ? soilPatch : soilArea;
        }

        var ranch = GetRanch(clickedBuilding);
        if (ranch is null ||
            ReferenceEquals(currentSelection, ranch) ||
            ReferenceEquals(currentSelection, clickedBuilding))
        {
            return clickedBuilding;
        }

        return ranch;
    }

    public static Ranch? GetRanch(Building building)
    {
        return building switch
        {
            SoilPatch soilPatch => soilPatch.Ranch,
            Garage garage => garage.Ranch,
            _ => null
        };
    }
}
