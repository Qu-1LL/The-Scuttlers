using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
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
    public void ClearBuildSelection_PersistsAfterLayoutUntilPlayerChoosesAnotherOption()
    {
        var session = new GameSession();
        var firstFactory = new Factory(game => new Storage(game), session);
        var secondFactory = new Factory(game => new MiningPost(game), session);
        session.UnlockedBuildings.Add(firstFactory);
        session.UnlockedBuildings.Add(secondFactory);
        var menu = new MenuController();
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var viewport = new Point(1440, 900);

        _ = getLayout!.Invoke(menu, [viewport, session]);
        Assert.Same(firstFactory, menu.SelectedBuildOption);

        menu.ClearBuildSelection();
        var clearedLayout = getLayout.Invoke(menu, [viewport, session]);
        var buildCards = ((System.Collections.IEnumerable)clearedLayout!.GetType().GetProperty("BuildCards")!.GetValue(clearedLayout)!)
            .Cast<object>()
            .ToArray();
        var secondCardBounds = (Rectangle)buildCards[1].GetType().GetProperty("Bounds")!.GetValue(buildCards[1])!;

        Assert.Null(menu.SelectedBuildOption);
        Assert.Null(menu.HoveredBuildOption);

        var handled = menu.HandleClick(secondCardBounds.Center, viewport, null!, session);

        Assert.True(handled);
        Assert.Same(secondFactory, menu.SelectedBuildOption);
    }

    [Fact]
    public void OpenBuildingsPanel_ReopensPanelOnBuildingsTab()
    {
        var menu = new MenuController();

        menu.OpenPanel("assignments");
        menu.ClosePanel();
        menu.OpenBuildingsPanel();

        Assert.True(menu.PanelOpen);
        Assert.Equal("buildings", menu.ActiveTab);
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
    public void SetSelectedObject_CreatureSelectionSwitchesToSelectedTab()
    {
        var session = new GameSession();
        var menu = new MenuController();

        menu.OpenPanel("assignments");
        menu.SetSelectedObject(new Enemy("Ant", GridPoint.Zero, session));

        Assert.Equal("selected", menu.ActiveTab);
    }

    [Fact]
    public void SetSelectedObject_BuildingSelectionSwitchesToSelectedTab()
    {
        var session = new GameSession();
        var menu = new MenuController();

        menu.OpenPanel("assignments");
        menu.SetSelectedObject(new Storage(session));

        Assert.Equal("selected", menu.ActiveTab);
    }

    [Fact]
    public void SetSelectedObject_VehicleSelectionSwitchesToSelectedTab()
    {
        var session = new GameSession();
        var menu = new MenuController();

        menu.OpenPanel("assignments");
        menu.SetSelectedObject(new Plow(session));

        Assert.Equal("selected", menu.ActiveTab);
    }

    [Fact]
    public void GetLayout_SelectedPlowInventoryUpdatesWithoutReselecting()
    {
        var session = new GameSession();
        var plow = new Plow(session);
        var menu = new MenuController();
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.OpenPanel("selected");
        menu.SetSelectedObject(plow);

        var initialLayout = getLayout!.Invoke(menu, [new Point(960, 420), session]);
        var initialEntries = (System.Collections.IEnumerable)initialLayout!.GetType().GetProperty("SelectedInventoryEntries")!.GetValue(initialLayout)!;
        Assert.Empty(initialEntries.Cast<object>());

        Assert.Equal(25, plow.Deposit(OreType.ALGAE.Name, 25));
        Assert.Equal(10, plow.Deposit(OreType.SANDSTONE.Name, 10));

        var refreshedLayout = getLayout.Invoke(menu, [new Point(960, 420), session]);
        var refreshedEntries = (System.Collections.IEnumerable)refreshedLayout!.GetType().GetProperty("SelectedInventoryEntries")!.GetValue(refreshedLayout)!;

        Assert.Equal(2, refreshedEntries.Cast<object>().Count());
        Assert.True(((Rectangle?)refreshedLayout.GetType().GetProperty("SelectedInventoryFrameBounds")!.GetValue(refreshedLayout)).HasValue);
    }

    [Fact]
    public void GetSelectedDetailText_SelectedPlowUsesStoredSummary()
    {
        var session = new GameSession();
        var plow = new Plow(session);
        var getDetailText = typeof(MenuController).GetMethod(
            "GetSelectedDetailText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getDetailText);
        Assert.Equal(25, plow.Deposit(OreType.ALGAE.Name, 25));

        var detailText = (string)getDetailText!.Invoke(null, [plow])!;

        Assert.Equal("Stored: 25/400", detailText);
    }

    [Fact]
    public void GetSelectedDetailText_SelectedScaffoldingUsesConstructionProgress()
    {
        var session = new GameSession();
        var scaffolding = new Scaffolding(session, new Storage(session));
        scaffolding.ApplyConstructionWork(7);
        var getDetailText = typeof(MenuController).GetMethod(
            "GetSelectedDetailText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getDetailText);

        var detailText = (string)getDetailText!.Invoke(null, [scaffolding])!;

        Assert.Equal($"Construction: 7/{scaffolding.ConstructionRequired}", detailText);
    }

    [Fact]
    public void GetSelectedSupplementalText_SelectedScaffoldingShowsZeroAssignments()
    {
        var session = new GameSession();
        var scaffolding = new Scaffolding(session, new Storage(session));
        var getSupplementalText = typeof(MenuController).GetMethod(
            "GetSelectedSupplementalText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getSupplementalText);

        var supplementalText = (string)getSupplementalText!.Invoke(null, [scaffolding])!;

        Assert.Equal("Assigned Trilobites: 0", supplementalText);
    }

    [Fact]
    public void GetLayout_SelectedScaffoldingShowsRequiredAndInputResources()
    {
        var session = new GameSession();
        var scaffolding = new Scaffolding(session, new Storage(session));
        scaffolding.Deposit("Sandstone", 7);
        scaffolding.ApplyConstructionWork(7);

        var menu = new MenuController();
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.OpenPanel("selected");
        menu.SetSelectedObject(scaffolding);

        var layout = getLayout!.Invoke(menu, [new Point(1440, 900), session]);
        var frameBounds = (Rectangle?)layout!.GetType().GetProperty("SelectedScaffoldingResourcesFrameBounds")!.GetValue(layout);
        var requiredEntries = ((System.Collections.IEnumerable)layout.GetType().GetProperty("SelectedScaffoldingRequiredEntries")!.GetValue(layout)!).Cast<object>().ToArray();
        var inputEntries = ((System.Collections.IEnumerable)layout.GetType().GetProperty("SelectedScaffoldingInputEntries")!.GetValue(layout)!).Cast<object>().ToArray();
        var descriptionBounds = (Rectangle)layout.GetType().GetProperty("SelectedDescriptionBounds")!.GetValue(layout)!;
        var deleteBounds = (Rectangle)layout.GetType().GetProperty("DeleteSelectedBounds")!.GetValue(layout)!;

        Assert.True(frameBounds.HasValue);
        Assert.Single(requiredEntries);
        Assert.Single(inputEntries);
        Assert.Equal("Sandstone", GetStringProperty(requiredEntries[0], "ResourceType"));
        Assert.Equal("Sandstone", GetStringProperty(inputEntries[0], "ResourceType"));
        Assert.Equal(20, GetIntProperty(requiredEntries[0], "Quantity"));
        Assert.Equal(7, GetIntProperty(inputEntries[0], "Quantity"));
        Assert.True(descriptionBounds.Top > frameBounds.Value.Bottom);
        Assert.True(frameBounds.Value.Bottom <= deleteBounds.Y);
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

        var handled = menu.HandleClick(deleteBounds.Center, viewport, null!, session);

        Assert.True(handled);
        Assert.Null(menu.SelectedObject);
        Assert.Null(miningPost.Cave);
    }

    [Fact]
    public void HandleClick_DeleteSelectedVehicle_ClearsSelectionAfterRemoval()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var plow = new Plow(session);
        var menu = new MenuController();
        var viewport = new Point(1440, 900);
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));
        menu.OpenPanel("selected");
        menu.SetSelectedObject(plow);

        var layout = getLayout!.Invoke(menu, [viewport, session]);
        var deleteBounds = (Rectangle)layout!.GetType().GetProperty("DeleteSelectedBounds")!.GetValue(layout)!;

        var handled = menu.HandleClick(deleteBounds.Center, viewport, null!, session);

        Assert.True(handled);
        Assert.Null(menu.SelectedObject);
        Assert.Null(plow.Cave);
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

    [Fact]
    public void GetLayout_SelectedDescriptionStartsBelowSupplementalBuildingInfo()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var miningPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var firstMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(2, 6), "Miner A", "miner");
        var secondMiner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Miner B", "miner");
        var menu = new MenuController();
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        miningPost.Assign(firstMiner, null);
        miningPost.Assign(secondMiner, null);
        menu.OpenPanel("selected");
        menu.SetSelectedObject(miningPost);

        var layout = getLayout!.Invoke(menu, [new Point(1440, 900), session]);
        var selectedBounds = (Rectangle)layout!.GetType().GetProperty("SelectedBounds")!.GetValue(layout)!;
        var descriptionBounds = (Rectangle)layout.GetType().GetProperty("SelectedDescriptionBounds")!.GetValue(layout)!;

        Assert.True(descriptionBounds.Top > selectedBounds.Y + 144);
    }

    [Fact]
    public void GetLayout_SelectedInventoryFrameStopsAboveDeleteButton()
    {
        var session = new GameSession();
        var miningPost = new MiningPost(session);
        var menu = new MenuController();
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        menu.OpenPanel("selected");
        menu.SetSelectedObject(miningPost);

        var layout = getLayout!.Invoke(menu, [new Point(960, 420), session]);
        var inventoryFrameBounds = (Rectangle?)layout!.GetType().GetProperty("SelectedInventoryFrameBounds")!.GetValue(layout);
        var deleteBounds = (Rectangle)layout.GetType().GetProperty("DeleteSelectedBounds")!.GetValue(layout)!;

        Assert.True(inventoryFrameBounds.HasValue);
        if (inventoryFrameBounds.Value.Bottom > deleteBounds.Y)
        {
            throw new Xunit.Sdk.XunitException(
                $"Inventory frame {inventoryFrameBounds.Value} should stay above delete button {deleteBounds}.");
        }
    }

    [Fact]
    public void GetLayout_BuildPreviewKeepsBodyTextBelowImageAndWiderThanIntroColumn()
    {
        var session = new GameSession();
        session.UnlockedBuildings.Add(new Factory(game => new Storage(game), session));
        var menu = new MenuController();
        var getLayout = typeof(MenuController).GetMethod("GetLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        var layout = getLayout!.Invoke(menu, [new Point(1440, 900), session]);
        var previewImageBounds = (Rectangle)layout!.GetType().GetProperty("PreviewImageBounds")!.GetValue(layout)!;
        var previewIntroBounds = (Rectangle?)layout.GetType().GetProperty("PreviewDescriptionIntroBounds")!.GetValue(layout);
        var previewBodyBounds = (Rectangle)layout.GetType().GetProperty("PreviewDescriptionBodyBounds")!.GetValue(layout)!;
        var previewTitleBounds = (Rectangle)layout.GetType().GetProperty("PreviewTitleBounds")!.GetValue(layout)!;

        Assert.True(previewIntroBounds.HasValue);
        Assert.True(previewTitleBounds.Right <= previewImageBounds.Left);
        Assert.True(previewBodyBounds.Top > previewImageBounds.Bottom);
        Assert.True(previewBodyBounds.Width > previewIntroBounds.Value.Width);
    }

    [Fact]
    public void SelectedDescriptionText_UsesModelDescriptionsInsteadOfRemovalCopy()
    {
        var session = new GameSession();
        var getDescription = typeof(MenuController).GetMethod(
            "GetSelectedDescriptionText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getDescription);

        var buildingDescription = (string)getDescription!.Invoke(null, [new Storage(session)])!;
        var trilobiteDescription = (string)getDescription.Invoke(null, [new Trilobite("Jeffery", GridPoint.Zero, session)])!;
        var vehicleDescription = (string)getDescription.Invoke(null, [new Plow(session)])!;

        Assert.Equal(new Storage(session).Description, buildingDescription);
        Assert.Equal(new Trilobite("Jeffery", GridPoint.Zero, session).Description, trilobiteDescription);
        Assert.Equal(new Plow(session).Description, vehicleDescription);
        Assert.DoesNotContain("delete", buildingDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kill", trilobiteDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delete", vehicleDescription, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetIntProperty(object value, string propertyName)
    {
        return (int)value.GetType().GetProperty(propertyName)!.GetValue(value)!;
    }

    private static string GetStringProperty(object value, string propertyName)
    {
        return (string)value.GetType().GetProperty(propertyName)!.GetValue(value)!;
    }
}
