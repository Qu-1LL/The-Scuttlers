using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class RanchBuildingSelectionTests
{
    [Fact]
    public void Resolve_FirstSoilPatchClickSelectsItsEntireSoilArea()
    {
        var (_, area, patches) = BuildGroupedSoilAreaRanch();
        var selected = RanchBuildingSelection.Resolve(patches[1, 1], null);

        Assert.Same(area, selected);
        Assert.Equal(area.SoilTiles.Count, area.TileArray.Count);
        Assert.Equal(36, area.TileArray.Count);
    }

    [Fact]
    public void Resolve_SecondClickOnTheSameAreaSelectsOnlyTheClickedSoilPatch()
    {
        var (_, area, patches) = BuildGroupedSoilAreaRanch();
        var firstSelection = RanchBuildingSelection.Resolve(patches[2, 1], null);
        var secondSelection = RanchBuildingSelection.Resolve(patches[2, 1], firstSelection);

        Assert.Same(area, firstSelection);
        Assert.Same(patches[2, 1], secondSelection);
        Assert.Equal(4, patches[2, 1].TileArray.Count);
    }

    [Fact]
    public void Resolve_AfterSelectingAPatchReturnsToTheSoilAreaOnTheNextClick()
    {
        var (_, area, patches) = BuildGroupedSoilAreaRanch();
        var patch = patches[0, 0];

        var soilArea = RanchBuildingSelection.Resolve(patch, null);
        var soilPatch = RanchBuildingSelection.Resolve(patch, soilArea);
        var soilAreaAgain = RanchBuildingSelection.Resolve(patch, soilPatch);

        Assert.Same(area, soilArea);
        Assert.Same(patch, soilPatch);
        Assert.Same(area, soilAreaAgain);
    }

    [Fact]
    public void Resolve_KeepsExplicitGarageSelectionStable()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var session = cave.Session;
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));

        var selection = RanchBuildingSelection.Resolve(garage, garage);

        Assert.Same(garage, selection);
    }

    private static (Cave Cave, SoilArea Area, SoilPatch[,] Patches) BuildGroupedSoilAreaRanch()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 20, new GridPoint(10, 0));
        var session = cave.Session;
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var area = new SoilArea(session);
        var patches = new SoilPatch[3, 3];
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var patch = new SoilPatch(session);
                area.AddSoilPatch(patch);
                Assert.True(cave.Build(patch, new GridPoint(4 + (x * 2), 6 + (y * 2))));
                patches[x, y] = patch;
            }
        }

        Assert.Single(cave.GetRanches());
        return (cave, area, patches);
    }
}
