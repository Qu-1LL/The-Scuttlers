using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.UI.Selection;

public static class RanchBuildingSelection
{
    private const int SoilPatchStep = 2;

    public static Building Resolve(Building clickedBuilding, object? currentSelection)
    {
        if (clickedBuilding is SoilPatch soilPatch)
        {
            return ResolveSoilPatchSelection(soilPatch, currentSelection);
        }

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

    private static Building ResolveSoilPatchSelection(SoilPatch soilPatch, object? currentSelection)
    {
        var ranch = soilPatch.Ranch;
        if (ranch is null || soilPatch.SoilArea is not { } soilArea)
        {
            return soilPatch;
        }

        if (currentSelection is SoilAreaSelection selection && ReferenceEquals(selection.SoilArea, soilArea))
        {
            if (selection.IsSameAnchor(soilArea, soilPatch))
            {
                if (selection.Mode == SoilAreaSelectionMode.Row && IsCornerPatch(soilArea, ranch, soilPatch))
                {
                    return CreateColumnSelection(soilArea, ranch, soilPatch);
                }

                return soilArea;
            }

            return soilArea;
        }

        if (ReferenceEquals(currentSelection, soilArea))
        {
            return ResolveSoilAreaDrilldown(soilArea, ranch, soilPatch);
        }

        if (ReferenceEquals(currentSelection, ranch) ||
            currentSelection is SoilArea otherArea && ReferenceEquals(otherArea.Ranch, ranch) ||
            currentSelection is SoilAreaSelection otherSelection && ReferenceEquals(otherSelection.SoilArea.Ranch, ranch))
        {
            soilArea.RefreshSelectionFootprint(ranch);
            return soilArea;
        }

        return ranch;
    }

    private static Building ResolveSoilAreaDrilldown(SoilArea soilArea, Ranch ranch, SoilPatch soilPatch)
    {
        var hasLeft = HasAdjacentPatch(soilArea, ranch, soilPatch, -SoilPatchStep, 0);
        var hasRight = HasAdjacentPatch(soilArea, ranch, soilPatch, SoilPatchStep, 0);
        var hasUp = HasAdjacentPatch(soilArea, ranch, soilPatch, 0, -SoilPatchStep);
        var hasDown = HasAdjacentPatch(soilArea, ranch, soilPatch, 0, SoilPatchStep);

        if (hasLeft && hasRight && hasUp && hasDown)
        {
            return soilArea;
        }

        var horizontalNeighbors = (hasLeft ? 1 : 0) + (hasRight ? 1 : 0);
        var verticalNeighbors = (hasUp ? 1 : 0) + (hasDown ? 1 : 0);
        if (horizontalNeighbors == 2 && verticalNeighbors == 1)
        {
            return CreateRowSelection(soilArea, ranch, soilPatch);
        }

        if (verticalNeighbors == 2 && horizontalNeighbors == 1)
        {
            return CreateColumnSelection(soilArea, ranch, soilPatch);
        }

        if (horizontalNeighbors == 1 && verticalNeighbors == 1)
        {
            return CreateRowSelection(soilArea, ranch, soilPatch);
        }

        return soilArea;
    }

    private static bool IsCornerPatch(SoilArea soilArea, Ranch ranch, SoilPatch soilPatch)
    {
        var horizontalNeighbors =
            (HasAdjacentPatch(soilArea, ranch, soilPatch, -SoilPatchStep, 0) ? 1 : 0) +
            (HasAdjacentPatch(soilArea, ranch, soilPatch, SoilPatchStep, 0) ? 1 : 0);
        var verticalNeighbors =
            (HasAdjacentPatch(soilArea, ranch, soilPatch, 0, -SoilPatchStep) ? 1 : 0) +
            (HasAdjacentPatch(soilArea, ranch, soilPatch, 0, SoilPatchStep) ? 1 : 0);
        return horizontalNeighbors == 1 && verticalNeighbors == 1;
    }

    private static SoilAreaSelection CreateRowSelection(SoilArea soilArea, Ranch ranch, SoilPatch anchorPatch)
    {
        var rowPatches = GetAlignedPatches(soilArea, ranch, anchorPatch, alignByRow: true);
        return new SoilAreaSelection(soilArea, anchorPatch, SoilAreaSelectionMode.Row, rowPatches);
    }

    private static SoilAreaSelection CreateColumnSelection(SoilArea soilArea, Ranch ranch, SoilPatch anchorPatch)
    {
        var columnPatches = GetAlignedPatches(soilArea, ranch, anchorPatch, alignByRow: false);
        return new SoilAreaSelection(soilArea, anchorPatch, SoilAreaSelectionMode.Column, columnPatches);
    }

    private static List<SoilPatch> GetAlignedPatches(SoilArea soilArea, Ranch ranch, SoilPatch anchorPatch, bool alignByRow)
    {
        var patches = new List<SoilPatch>();
        if (anchorPatch.Location is not { } anchorLocation)
        {
            return patches;
        }

        foreach (var soilPatch in soilArea.SoilPatches)
        {
            if (soilPatch.Location is not { } location || !ReferenceEquals(soilPatch.Ranch, ranch))
            {
                continue;
            }

            if ((alignByRow && location.Y == anchorLocation.Y) ||
                (!alignByRow && location.X == anchorLocation.X))
            {
                patches.Add(soilPatch);
            }
        }

        patches.Sort(static (left, right) =>
        {
            var leftLocation = left.Location!.Value;
            var rightLocation = right.Location!.Value;
            var yComparison = leftLocation.Y.CompareTo(rightLocation.Y);
            return yComparison != 0 ? yComparison : leftLocation.X.CompareTo(rightLocation.X);
        });
        return patches;
    }

    private static bool HasAdjacentPatch(SoilArea soilArea, Ranch ranch, SoilPatch soilPatch, int dx, int dy)
    {
        if (soilPatch.Location is not { } location)
        {
            return false;
        }

        var neighborLocation = new Shared.Math.GridPoint(location.X + dx, location.Y + dy);
        foreach (var candidate in soilArea.SoilPatches)
        {
            if (candidate.Location == neighborLocation && ReferenceEquals(candidate.Ranch, ranch))
            {
                return true;
            }
        }

        return false;
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
