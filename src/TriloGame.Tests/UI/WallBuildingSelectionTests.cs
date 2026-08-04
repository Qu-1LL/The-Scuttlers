using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Menu;
using TriloGame.Game.UI.Selection;
using Microsoft.Xna.Framework;

namespace TriloGame.Tests.UI;

public sealed class WallBuildingSelectionTests
{
    [Fact]
    public void Resolve_ReturnsClickedWall_WhenWallIsClickedFirst()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 24, new GridPoint(2, 2));
        var wall = TestWorldFactory.BuildWall(cave, cave.Session, new GridPoint(10, 10));

        var selection = WallBuildingSelection.Resolve(wall, null);

        Assert.Same(wall, selection);
    }

    [Fact]
    public void Resolve_CyclesCrossCenterThroughHorizontalThenVerticalRow()
    {
        var walls = BuildCrossWalls();

        var horizontal = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Center, walls.Center));
        var vertical = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Center, horizontal));

        Assert.Equal(WallSelectionMode.HorizontalRow, horizontal.Mode);
        Assert.Equal([walls.Left, walls.Center, walls.Right], horizontal.Walls);
        Assert.Equal(WallSelectionMode.VerticalRow, vertical.Mode);
        Assert.Equal([walls.Up, walls.Center, walls.Down], vertical.Walls);
    }

    [Fact]
    public void Resolve_SelectingWallFromRowPromotesToContiguousGroup()
    {
        var walls = BuildCrossWalls();
        var horizontal = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Center, walls.Center));

        var group = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Right, horizontal));

        Assert.Equal(WallSelectionMode.Group, group.Mode);
        Assert.Equal([walls.Up, walls.Left, walls.Center, walls.Right, walls.Down], group.Walls);
    }

    [Fact]
    public void RemoveFromGame_RemovesEveryWallInSelectedGroup()
    {
        var walls = BuildCrossWalls();
        var horizontal = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Center, walls.Center));
        var group = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Left, horizontal));

        var removed = group.RemoveFromGame("test");

        Assert.True(removed);
        Assert.Empty(walls.Cave.GetWalls());
        Assert.All(group.Walls, wall => Assert.Null(wall.Cave));
    }

    [Fact]
    public void MenuDelete_RemovesEveryWallInSelectedGroup()
    {
        var walls = BuildCrossWalls();
        var horizontal = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Center, walls.Center));
        var group = Assert.IsType<WallSelection>(WallBuildingSelection.Resolve(walls.Right, horizontal));
        var menu = new MenuController();
        var viewport = new Point(1440, 900);

        menu.OpenPanel("selected");
        menu.SetSelectedObject(group);

        var deleteBounds = menu.GetLayout(viewport, walls.Cave.Session).Selected.DeleteBounds;

        var handled = menu.HandleClick(deleteBounds.Center, viewport, null!, walls.Cave.Session);

        Assert.True(handled);
        Assert.Null(menu.SelectedObject);
        Assert.Empty(walls.Cave.GetWalls());
    }

    private static (TriloGame.Game.Core.World.Cave Cave, Wall Left, Wall Center, Wall Right, Wall Up, Wall Down) BuildCrossWalls()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 24, new GridPoint(2, 2));
        var session = cave.Session;
        var center = TestWorldFactory.BuildWall(cave, session, new GridPoint(10, 10));
        var left = TestWorldFactory.BuildWall(cave, session, new GridPoint(9, 10));
        var right = TestWorldFactory.BuildWall(cave, session, new GridPoint(11, 10));
        var up = TestWorldFactory.BuildWall(cave, session, new GridPoint(10, 9));
        var down = TestWorldFactory.BuildWall(cave, session, new GridPoint(10, 11));
        return (cave, left, center, right, up, down);
    }
}
