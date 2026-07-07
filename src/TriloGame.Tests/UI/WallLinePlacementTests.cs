using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class WallLinePlacementTests
{
    [Fact]
    public void ResolveEnd_SnapsHorizontallyWhenHorizontalDragIsLonger()
    {
        var end = WallLinePlacement.ResolveEnd(new GridPoint(4, 4), new GridPoint(9, 6));

        Assert.Equal(new GridPoint(9, 4), end);
    }

    [Fact]
    public void ResolveEnd_SnapsVerticallyWhenVerticalDragIsLonger()
    {
        var end = WallLinePlacement.ResolveEnd(new GridPoint(4, 4), new GridPoint(6, 10));

        Assert.Equal(new GridPoint(4, 10), end);
    }

    [Fact]
    public void BuildLine_ReturnsInclusiveHorizontalTilesWhenDraggingLeft()
    {
        var line = WallLinePlacement.BuildLine(new GridPoint(6, 4), new GridPoint(2, 5));

        Assert.Equal(
        [
            new GridPoint(6, 4),
            new GridPoint(5, 4),
            new GridPoint(4, 4),
            new GridPoint(3, 4),
            new GridPoint(2, 4)
        ], line);
    }

    [Fact]
    public void BuildLine_ReturnsInclusiveVerticalTilesWhenDraggingUp()
    {
        var line = WallLinePlacement.BuildLine(new GridPoint(4, 6), new GridPoint(5, 2));

        Assert.Equal(
        [
            new GridPoint(4, 6),
            new GridPoint(4, 5),
            new GridPoint(4, 4),
            new GridPoint(4, 3),
            new GridPoint(4, 2)
        ], line);
    }
}
