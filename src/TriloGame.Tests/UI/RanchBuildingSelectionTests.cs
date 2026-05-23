using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
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
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));

        var selection = RanchBuildingSelection.Resolve(soilPatch, null);

        Assert.Same(soilPatch.Ranch, selection);
    }

    [Fact]
    public void Resolve_ReturnsClickedBuilding_WhenRanchIsAlreadySelected()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var session = cave.Session;
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 6));
        Assert.NotNull(soilPatch.Ranch);

        var selection = RanchBuildingSelection.Resolve(soilPatch, soilPatch.Ranch);

        Assert.Same(soilPatch.SoilArea, selection);
    }

    [Fact]
    public void Resolve_ReturnsAreaRow_WhenTopEdgePatchIsClickedWhileAreaIsSelected()
    {
        var (cave, _, patches) = BuildGroupedSoilAreaRanch();
        var topEdgePatch = patches[1, 0];
        var area = topEdgePatch.SoilArea!;

        var selection = RanchBuildingSelection.Resolve(topEdgePatch, area);

        var rowSelection = Assert.IsType<SoilAreaSelection>(selection);
        Assert.Equal(SoilAreaSelectionMode.Row, rowSelection.Mode);
        Assert.Same(topEdgePatch, rowSelection.AnchorPatch);
        Assert.Equal([patches[0, 0], patches[1, 0], patches[2, 0]], rowSelection.SoilPatches);
        Assert.All(rowSelection.SoilPatches, patch => Assert.Same(cave.GetRanches()[0], patch.Ranch));
    }

    [Fact]
    public void Resolve_ReturnsAreaColumn_WhenLeftEdgePatchIsClickedWhileAreaIsSelected()
    {
        var (_, _, patches) = BuildGroupedSoilAreaRanch();
        var leftEdgePatch = patches[0, 1];
        var area = leftEdgePatch.SoilArea!;

        var selection = RanchBuildingSelection.Resolve(leftEdgePatch, area);

        var columnSelection = Assert.IsType<SoilAreaSelection>(selection);
        Assert.Equal(SoilAreaSelectionMode.Column, columnSelection.Mode);
        Assert.Same(leftEdgePatch, columnSelection.AnchorPatch);
        Assert.Equal([patches[0, 0], patches[0, 1], patches[0, 2]], columnSelection.SoilPatches);
    }

    [Fact]
    public void Resolve_CyclesCornerPatchThroughRowColumnAndArea()
    {
        var (_, _, patches) = BuildGroupedSoilAreaRanch();
        var cornerPatch = patches[0, 0];
        var area = cornerPatch.SoilArea!;

        var row = Assert.IsType<SoilAreaSelection>(RanchBuildingSelection.Resolve(cornerPatch, area));
        var column = Assert.IsType<SoilAreaSelection>(RanchBuildingSelection.Resolve(cornerPatch, row));
        var areaAgain = RanchBuildingSelection.Resolve(cornerPatch, column);

        Assert.Equal(SoilAreaSelectionMode.Row, row.Mode);
        Assert.Equal(SoilAreaSelectionMode.Column, column.Mode);
        Assert.Same(area, areaAgain);
    }

    [Fact]
    public void Resolve_ReturnsArea_WhenInteriorPatchOrSingleRowPatchIsClickedWhileAreaIsSelected()
    {
        var (_, _, patches) = BuildGroupedSoilAreaRanch();
        var interiorPatch = patches[1, 1];
        var area = interiorPatch.SoilArea!;

        Assert.Same(area, RanchBuildingSelection.Resolve(interiorPatch, area));

        var (_, _, rowPatches) = BuildGroupedSoilAreaRanch(width: 3, height: 1);
        var singleRowMiddle = rowPatches[1, 0];
        Assert.Same(singleRowMiddle.SoilArea, RanchBuildingSelection.Resolve(singleRowMiddle, singleRowMiddle.SoilArea));
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

    private static (Cave Cave, SoilArea Area, SoilPatch[,] Patches) BuildGroupedSoilAreaRanch(int width = 3, int height = 3)
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 20, new GridPoint(10, 0));
        var session = cave.Session;
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(2, 6));
        var area = new SoilArea(session);
        var patches = new SoilPatch[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var patch = new SoilPatch(session);
                area.AddSoilPatch(patch);
                Assert.True(cave.Build(patch, new GridPoint(4 + (x * 2), 6 + (y * 2))));
                patches[x, y] = patch;
            }
        }

        Assert.Same(area, patches[0, 0].SoilArea);
        Assert.Single(cave.GetRanches());
        return (cave, area, patches);
    }
}
