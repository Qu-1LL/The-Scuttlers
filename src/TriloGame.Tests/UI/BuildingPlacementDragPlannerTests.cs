using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class BuildingPlacementDragPlannerTests
{
    [Fact]
    public void BuildLocations_WithHorizontalDrag_ReturnsLineSteppedByFootprintWidth()
    {
        var locations = BuildingPlacementDragPlanner.BuildLocations(
            new GridPoint(2, 5),
            new GridPoint(8, 5),
            new GridPoint(3, 2));

        Assert.Equal(
            [new GridPoint(2, 5), new GridPoint(5, 5), new GridPoint(8, 5)],
            locations);
    }

    [Fact]
    public void BuildLocations_WithVerticalDrag_ReturnsLineSteppedByFootprintHeight()
    {
        var locations = BuildingPlacementDragPlanner.BuildLocations(
            new GridPoint(4, 9),
            new GridPoint(4, 3),
            new GridPoint(2, 3));

        Assert.Equal(
            [new GridPoint(4, 3), new GridPoint(4, 6), new GridPoint(4, 9)],
            locations);
    }

    [Fact]
    public void BuildLocations_WithDiagonalDrag_ReturnsFilledBoxSteppedByFootprint()
    {
        var locations = BuildingPlacementDragPlanner.BuildLocations(
            new GridPoint(1, 1),
            new GridPoint(5, 4),
            new GridPoint(2, 3));

        Assert.Equal(
            [
                new GridPoint(1, 1),
                new GridPoint(3, 1),
                new GridPoint(5, 1),
                new GridPoint(1, 4),
                new GridPoint(3, 4),
                new GridPoint(5, 4)
            ],
            locations);
    }
}
