using Microsoft.Xna.Framework;
using System.Linq;
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
        Assert.Equal(0f, menu.BuildPreviewDescriptionScroll);
        Assert.Equal(0f, menu.SelectedDescriptionScroll);
    }

    [Fact]
    public void HandleClick_CollapseButtonClosesPanelAndGearReopensIt()
    {
        var menu = new MenuController();
        var session = new GameSession();
        var viewport = new Point(1440, 900);

        var collapseResult = menu.HandleClick(new Point(959, 37), viewport, session);

        Assert.True(collapseResult.Consumed);
        Assert.True(collapseResult.PlaySelectSound);
        Assert.False(menu.PanelOpen);

        var gearResult = menu.HandleClick(new Point(1402, 37), viewport, session);

        Assert.True(gearResult.Consumed);
        Assert.True(gearResult.PlaySelectSound);
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

    [Fact]
    public void HandleWheel_ScrollsBuildPreviewDescriptionWhenPreviewTextOverflows()
    {
        var session = new GameSession();
        session.UnlockedBuildings.Add(new Factory(game => new LongDescriptionBuilding(game), session));

        var menu = new MenuController();
        menu.OpenPanel("buildings");

        var viewport = new Point(960, 260);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var textLayout = layout!.GetType().GetProperty("BuildPreviewDescriptionLayout")!.GetValue(layout)!;
        var viewportBounds = (Rectangle)textLayout.GetType().GetProperty("ViewportBounds")!.GetValue(textLayout)!;
        var maxScroll = (float)textLayout.GetType().GetProperty("MaxScroll")!.GetValue(textLayout)!;
        Assert.True(maxScroll > 0f);

        var handled = menu.HandleWheel(viewportBounds.Center, 90, viewport, session);

        Assert.True(handled);
        Assert.True(menu.BuildPreviewDescriptionScroll > 0f);
    }

    [Fact]
    public void SelectedTab_DescriptionViewportStartsBelowAssignedBuildingMetadata()
    {
        var session = new GameSession();
        var building = new LongDescriptionBuilding(session);
        var menu = new MenuController();
        var viewport = new Point(960, 420);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.SetSelectedObject(building);
        menu.OpenPanel("selected");

        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var textLayout = layout!.GetType().GetProperty("SelectedDescriptionLayout")!.GetValue(layout)!;
        var viewportBounds = (Rectangle)textLayout.GetType().GetProperty("ViewportBounds")!.GetValue(textLayout)!;

        Assert.True(viewportBounds.Y >= 160);
    }

    [Fact]
    public void HandleWheel_ScrollsSelectedDescriptionWhenBodyTextOverflows()
    {
        var session = new GameSession();
        var building = new LongDescriptionBuilding(session);
        var menu = new MenuController();
        var viewport = new Point(960, 420);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.SetSelectedObject(building);
        menu.OpenPanel("selected");

        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var textLayout = layout!.GetType().GetProperty("SelectedDescriptionLayout")!.GetValue(layout)!;
        var viewportBounds = (Rectangle)textLayout.GetType().GetProperty("ViewportBounds")!.GetValue(textLayout)!;
        var maxScroll = (float)textLayout.GetType().GetProperty("MaxScroll")!.GetValue(textLayout)!;
        Assert.True(maxScroll > 0f);

        var handled = menu.HandleWheel(viewportBounds.Center, 90, viewport, session);

        Assert.True(handled);
        Assert.True(menu.SelectedDescriptionScroll > 0f);
    }

    [Fact]
    public void HandleClick_DeleteSelectedBuilding_ClearsSelectionAfterRemoval()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var miningPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var menu = new MenuController();
        var viewport = new Point(1440, 900);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.OpenPanel("selected");
        menu.SetSelectedObject(miningPost);

        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var deleteBounds = (Rectangle)layout!.GetType().GetProperty("DeleteSelectedBounds")!.GetValue(layout)!;

        var result = menu.HandleClick(deleteBounds.Center, viewport, session);

        Assert.True(result.Consumed);
        Assert.True(result.PlaySelectSound);
        Assert.Null(menu.SelectedObject);
        Assert.Null(miningPost.Cave);
    }

    [Fact]
    public void HandleClick_BuildingCardReturnsPlacementRequestWithoutHostCoupling()
    {
        var session = new GameSession();
        session.UnlockedBuildings.Add(new Factory(game => new AlgaeFarm(game), session));
        var menu = new MenuController();
        var viewport = new Point(1440, 900);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.OpenPanel("buildings");
        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var buildCards = (System.Collections.IEnumerable)layout!.GetType().GetProperty("BuildCards")!.GetValue(layout)!;
        var firstCard = buildCards.Cast<object>().First();
        var bounds = (Rectangle)firstCard.GetType().GetProperty("Bounds")!.GetValue(firstCard)!;

        var result = menu.HandleClick(bounds.Center, viewport, session);

        Assert.True(result.Consumed);
        Assert.True(result.PlaySelectSound);
        Assert.NotNull(result.BuildingPlacement);
        Assert.IsType<AlgaeFarm>(result.BuildingPlacement!.TargetBuilding);
    }

    [Fact]
    public void SelectedBuildingAssignmentCount_UsesCurrentBuildingAssignments()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var miningPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var algaeFarm = TestWorldFactory.BuildAlgaeFarm(cave, session, new GridPoint(12, 6));
        var firstMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(2, 6), "Miner A", "miner");
        var secondMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Miner B", "miner");
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(12, 6), "Farmer", "farmer");
        var storage = new Storage(session);

        miningPost.Assign(firstMiner, null);
        miningPost.Assign(secondMiner, null);
        Assert.True(algaeFarm.Assign(farmer));

        var getAssignmentCount = typeof(MenuController).GetMethod(
            "GetSelectedBuildingAssignmentCount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getAssignmentCount);
        Assert.Equal(2, (int)getAssignmentCount!.Invoke(null, [miningPost])!);
        Assert.Equal(1, (int)getAssignmentCount.Invoke(null, [algaeFarm])!);
        Assert.Equal(0, (int)getAssignmentCount.Invoke(null, [storage])!);
    }

    private sealed class LongDescriptionBuilding : Building
    {
        public LongDescriptionBuilding(GameSession session)
            : base("Archivist Spire", new GridPoint(2, 2), [[1, 1], [1, 1]], session, hasStation: false)
        {
            Description = string.Join(' ', Enumerable.Repeat(
                "A deliberately long construction brief that should force the colony menu to expose a scrollable text viewport instead of clipping or overlapping nearby UI.",
                20));
            TextureKey = "Storage";
        }
    }
}
