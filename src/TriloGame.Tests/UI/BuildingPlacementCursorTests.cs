using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class BuildingPlacementCursorTests
{
    [Fact]
    public void GetPlacementCandidate_UsesScaffoldingUnlessNoCostPlacementIsEnabled()
    {
        var session = new GameSession();
        var factory = new Factory(game => new AlgaeFarm(game), session);
        var cursor = new BuildingPlacementCursor(factory, session);

        Assert.Same(cursor.Scaffolding, cursor.GetPlacementCandidate(noCostPlacement: false));
        Assert.Same(cursor.TargetBuilding, cursor.GetPlacementCandidate(noCostPlacement: true));
    }

    [Fact]
    public void CreatePlacementCandidate_ReturnsFreshBuildingWithCursorRotation()
    {
        var session = new GameSession();
        var factory = new Factory(game => new AlgaeFarm(game), session);
        var cursor = new BuildingPlacementCursor(factory, session);
        cursor.RotateClockwise();

        var first = cursor.CreatePlacementCandidate(noCostPlacement: false);
        var second = cursor.CreatePlacementCandidate(noCostPlacement: false);
        var noCost = cursor.CreatePlacementCandidate(noCostPlacement: true);

        Assert.NotSame(first, second);
        var firstScaffold = Assert.IsType<Scaffolding>(first);
        Assert.IsType<Scaffolding>(second);
        Assert.IsType<AlgaeFarm>(noCost);
        Assert.Equal(1, firstScaffold.GetDisplayRotationTurns());
        Assert.Equal(new GridPoint(3, 2), firstScaffold.Size);
        Assert.Equal(new GridPoint(3, 2), noCost.Size);
    }

    [Fact]
    public void RefreshAfterSuccessfulPlacement_CreatesFreshPlacementAndPreservesRotation()
    {
        var session = new GameSession();
        var factory = new Factory(game => new AlgaeFarm(game), session);
        var cursor = new BuildingPlacementCursor(factory, session);
        cursor.RotateClockwise();
        var placedScaffolding = cursor.Scaffolding;
        var placedTarget = cursor.TargetBuilding;

        cursor.RefreshAfterSuccessfulPlacement();

        Assert.NotSame(placedScaffolding, cursor.Scaffolding);
        Assert.NotSame(placedTarget, cursor.TargetBuilding);
        Assert.IsType<AlgaeFarm>(cursor.TargetBuilding);
        Assert.Equal(1, cursor.GetDisplayRotationTurns());
        Assert.Equal(new GridPoint(3, 2), cursor.Scaffolding.Size);
        Assert.Equal(new GridPoint(3, 2), cursor.TargetBuilding.Size);
    }
}
