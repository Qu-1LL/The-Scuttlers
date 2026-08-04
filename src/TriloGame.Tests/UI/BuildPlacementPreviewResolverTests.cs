using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class BuildPlacementPreviewResolverTests
{
    [Fact]
    public void ResolveLocations_ReturnsHoveredTileForNonDraggableBuilding_EvenWhenDragged()
    {
        var session = new GameSession();

        var locations = BuildPlacementPreviewResolver.ResolveLocations(
            new Garage(session),
            new GridPoint(6, 8),
            new GridPoint(2, 3));

        Assert.Equal([new GridPoint(6, 8)], locations);
    }

    [Fact]
    public void Buildings_ExposeOnlyTheirSupportedDragPlacementKinds()
    {
        var session = new GameSession();

        Assert.Equal(BuildPlacementDragKind.None, new Garage(session).DragPlacementKind);
        Assert.Equal(BuildPlacementDragKind.FootprintGrid, new SoilPatch(session).DragPlacementKind);
        Assert.Equal(BuildPlacementDragKind.AxisLine, new Wall(session).DragPlacementKind);
    }


    [Fact]
    public void ResolveLocations_UsesFootprintGridForSoilPatches()
    {
        var session = new GameSession();

        var locations = BuildPlacementPreviewResolver.ResolveLocations(
            new SoilPatch(session),
            new GridPoint(5, 5),
            new GridPoint(2, 2));

        Assert.Equal(
        [
            new GridPoint(2, 2),
            new GridPoint(4, 2),
            new GridPoint(2, 4),
            new GridPoint(4, 4)
        ], locations);
    }
}
