using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Menu;

namespace TriloGame.Tests.UI;

public sealed class MenuControllerTests
{
    [Fact]
    public void HandleWheel_ScrollsBuildGridFromFramePaddingHitArea()
    {
        var session = new GameSession();
        for (var index = 0; index < 24; index++)
        {
            session.UnlockedBuildings.Add(new Factory(game => new AlgaeFarm(game), session));
        }

        var menu = new MenuController();
        menu.OpenPanel();

        var viewport = new Point(1440, 900);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var buildGridFrameBounds = (Rectangle)layout!.GetType().GetProperty("BuildGridFrameBounds")!.GetValue(layout)!;
        var framePaddingPoint = new Point(buildGridFrameBounds.X + 8, buildGridFrameBounds.Y + 8);

        var handled = menu.HandleWheel(framePaddingPoint, 90, viewport, session);

        Assert.True(handled);
        Assert.True(menu.BuildGridScroll > 0f);
    }

    [Fact]
    public void ResetState_RestoresDefaultMenuValues()
    {
        var menu = new MenuController();

        menu.OpenPanel("assignments");
        menu.SetSelectedObject(new object());

        menu.ResetState();

        Assert.True(menu.PanelOpen);
        Assert.Null(menu.SelectedObject);
        Assert.Equal("buildings", menu.ActiveTab);
        Assert.Null(menu.HoveredBuildOption);
        Assert.Null(menu.SelectedBuildOption);
        Assert.Equal("miner", menu.AssignmentFilter);
        Assert.Equal(0f, menu.BuildGridScroll);
        Assert.Equal(0f, menu.AssignmentActiveScroll);
        Assert.Equal(0f, menu.AssignmentUnassignedScroll);
    }

    [Fact]
    public void HandleClick_CollapseButtonClosesPanelAndGearReopensIt()
    {
        var menu = new MenuController();
        var session = new GameSession();
        var viewport = new Point(1440, 900);

        var collapseHandled = menu.HandleClick(new Point(959, 37), viewport, null!, session);

        Assert.True(collapseHandled);
        Assert.False(menu.PanelOpen);

        var gearHandled = menu.HandleClick(new Point(1402, 37), viewport, null!, session);

        Assert.True(gearHandled);
        Assert.True(menu.PanelOpen);
    }

    [Fact]
    public void TogglePanel_FlipsOpenState()
    {
        var menu = new MenuController();

        menu.TogglePanel();
        Assert.False(menu.PanelOpen);

        menu.TogglePanel();
        Assert.True(menu.PanelOpen);
    }

    [Fact]
    public void ResetState_CancelsTrilobiteRenameMode()
    {
        var session = new GameSession();
        var trilobite = new Trilobite("Jeffery", GridPoint.Zero, session);
        var menu = new MenuController();

        menu.SetSelectedObject(trilobite);
        Assert.True(menu.BeginRenameSelectedTrilobite());

        menu.ResetState();

        Assert.False(menu.IsRenamingSelectedTrilobite);
    }

    [Fact]
    public void CommitRenameSelectedTrilobite_UpdatesCreatureName()
    {
        var session = new GameSession();
        var trilobite = new Trilobite("Jeffery", GridPoint.Zero, session);
        var menu = new MenuController();

        menu.SetSelectedObject(trilobite);
        Assert.True(menu.BeginRenameSelectedTrilobite());

        typeof(MenuController)
            .GetField("_renameBuffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(menu, "Captain Jeff");

        var committed = menu.CommitRenameSelectedTrilobite();

        Assert.True(committed);
        Assert.Equal("Captain Jeff", trilobite.Name);
        Assert.False(menu.IsRenamingSelectedTrilobite);
    }

    [Fact]
    public void HandleWheel_ScrollsSelectedMiningPostInventory()
    {
        var session = new GameSession();
        var miningPost = new MiningPost(session);
        for (var index = 0; index < 18; index++)
        {
            miningPost.Deposit($"Resource {index}", index + 1);
        }

        var menu = new MenuController();
        menu.SetSelectedObject(miningPost);
        menu.OpenPanel("selected");

        var viewport = new Point(960, 420);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var inventoryFrameBounds = (Rectangle?)layout!.GetType().GetProperty("SelectedInventoryFrameBounds")!.GetValue(layout);
        Assert.True(inventoryFrameBounds.HasValue);
        var scrollPoint = new Point(inventoryFrameBounds.Value.X + 8, inventoryFrameBounds.Value.Y + 8);

        var handled = menu.HandleWheel(scrollPoint, 90, viewport, session);

        Assert.True(handled);
        Assert.True(menu.SelectedInventoryScroll > 0f);
    }
}
