using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class RanchBuildingSelectionTests
{
    [Fact]
    public void Resolve_ReturnsRanch_WhenRanchMemberIsClickedFirst()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var session = cave.Session;
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var soil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));

        var selection = RanchBuildingSelection.Resolve(soil, null);

        Assert.Same(soil.Ranch, selection);
    }

    [Fact]
    public void Resolve_ReturnsClickedBuilding_WhenRanchIsAlreadySelected()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var session = cave.Session;
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var soil = TestWorldFactory.BuildSoil(cave, session, new GridPoint(4, 6));
        Assert.NotNull(soil.Ranch);

        var selection = RanchBuildingSelection.Resolve(soil, soil.Ranch);

        Assert.Same(soil, selection);
    }

    [Fact]
    public void Resolve_KeepsExplicitBuildingSelectionStable()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var session = cave.Session;
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));

        var selection = RanchBuildingSelection.Resolve(garage, garage);

        Assert.Same(garage, selection);
    }
}
