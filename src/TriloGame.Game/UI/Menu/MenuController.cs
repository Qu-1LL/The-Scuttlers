using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;
using TriloGame.Game.UI.ViewModels;

namespace TriloGame.Game.UI.Menu;

public sealed partial class MenuController
{
    private const string TabBuildings = "buildings";
    private const string TabAssignments = "assignments";
    private const string TabSelected = "selected";
    private const int AssignmentRowHeight = 76;
    private const int AssignmentRowGap = 10;

    private Point _pointerPoint;

    public object? SelectedObject { get; private set; }

    public string ActiveTab { get; private set; } = TabBuildings;

    public bool PanelOpen { get; private set; } = true;

    public Factory? HoveredBuildOption { get; private set; }

    public Factory? SelectedBuildOption { get; private set; }

    public string AssignmentFilter { get; private set; } = "miner";

    public float BuildGridScroll { get; private set; }

    public float AssignmentActiveScroll { get; private set; }

    public float AssignmentUnassignedScroll { get; private set; }

    public float GetOpenPanelWidth(Point viewport)
    {
        return PanelOpen ? GetMetrics(viewport).PanelWidth : 0f;
    }

    public void OpenPanel(string? tab = null)
    {
        if (tab is TabBuildings or TabAssignments or TabSelected)
        {
            ActiveTab = tab;
        }

        NormalizeActiveTab();
        PanelOpen = true;
    }

    public void ClosePanel()
    {
        PanelOpen = false;
    }

    public void TogglePanel()
    {
        if (PanelOpen)
        {
            ClosePanel();
            return;
        }

        OpenPanel();
    }

    public void ResetState()
    {
        SelectedObject = null;
        ActiveTab = TabBuildings;
        PanelOpen = true;
        HoveredBuildOption = null;
        SelectedBuildOption = null;
        AssignmentFilter = "miner";
        BuildGridScroll = 0f;
        AssignmentActiveScroll = 0f;
        AssignmentUnassignedScroll = 0f;
    }

    public void SetSelectedObject(object? selectedObject)
    {
        SelectedObject = selectedObject;
        NormalizeActiveTab();
    }

    public bool CoversScreenPoint(Point point, Point viewport)
    {
        var layout = GetLayout(viewport, null);
        return !PanelOpen
            ? layout.MenuButton.Contains(point)
            : layout.PanelBounds.Contains(point);
    }

    public bool HandleWheel(Point point, int delta, Point viewport, GameSession session)
    {
        _pointerPoint = point;
        if (!PanelOpen)
        {
            return CoversScreenPoint(point, viewport);
        }

        var layout = GetLayout(viewport, session);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        if (ActiveTab == TabBuildings && layout.BuildGridFrameBounds.Contains(point))
        {
            BuildGridScroll = Clamp(BuildGridScroll + delta, 0f, layout.BuildGridMaxScroll);
        }
        else if (ActiveTab == TabAssignments)
        {
            if (layout.AssignmentActiveBounds.Contains(point))
            {
                AssignmentActiveScroll = Clamp(AssignmentActiveScroll + delta, 0f, layout.AssignmentActiveMaxScroll);
            }
            else if (layout.AssignmentUnassignedBounds.Contains(point))
            {
                AssignmentUnassignedScroll = Clamp(AssignmentUnassignedScroll + delta, 0f, layout.AssignmentUnassignedMaxScroll);
            }
        }

        return true;
    }

    public void UpdateHover(Point point, Point viewport, GameSession session)
    {
        _pointerPoint = point;
        if (!PanelOpen || ActiveTab != TabBuildings)
        {
            HoveredBuildOption = null;
            return;
        }

        var layout = GetLayout(viewport, session);
        HoveredBuildOption = layout.BuildCards
            .FirstOrDefault(card => card.Bounds.Contains(point))
            .Factory;
    }

    public bool HandleClick(Point point, Point viewport, GameApp? game, GameSession session)
    {
        _pointerPoint = point;
        var layout = GetLayout(viewport, session);
        if (!PanelOpen)
        {
            if (layout.MenuButton.Contains(point))
            {
                game?.PlayUiSelectSound();
                OpenPanel();
                return true;
            }

            return false;
        }

        if (layout.CollapseButton.Contains(point))
        {
            game?.PlayUiSelectSound();
            ClosePanel();
            return true;
        }

        foreach (var tab in layout.Tabs)
        {
            if (!tab.Bounds.Contains(point))
            {
                continue;
            }

            game?.PlayUiSelectSound();
            ActiveTab = tab.Key;
            NormalizeActiveTab();
            return true;
        }

        if (ActiveTab == TabBuildings)
        {
            foreach (var card in layout.BuildCards)
            {
                if (!card.Bounds.Contains(point))
                {
                    continue;
                }

                game?.PlayUiSelectSound();
                SelectedBuildOption = card.Factory;
                HoveredBuildOption = card.Factory;
                if (game is null)
                {
                    return true;
                }

                StartBuildingPlacement(card.Factory, game, session);
                return true;
            }
        }
        else if (ActiveTab == TabAssignments)
        {
            foreach (var filter in layout.AssignmentFilters)
            {
                if (!filter.Bounds.Contains(point))
                {
                    continue;
                }

                game?.PlayUiSelectSound();
                AssignmentFilter = filter.Key;
                return true;
            }

            foreach (var row in layout.ActiveAssignmentRows)
            {
                if (row.Bounds.Contains(point))
                {
                    game?.PlayUiSelectSound();
                    return TransferCreatureAssignment(row.FromAssignment, row.ToAssignment, session);
                }
            }

            foreach (var row in layout.UnassignedAssignmentRows)
            {
                if (row.Bounds.Contains(point))
                {
                    game?.PlayUiSelectSound();
                    return TransferCreatureAssignment(row.FromAssignment, row.ToAssignment, session);
                }
            }
        }
        else if (ActiveTab == TabSelected && layout.DeleteSelectedBounds.Contains(point))
        {
            game?.PlayUiSelectSound();
            return DeleteSelectedObject();
        }

        return layout.PanelBounds.Contains(point);
    }

    public void Draw(RenderingContext context, Point viewport, GameApp game, GameSession session)
    {
        var layout = GetLayout(viewport, session);
        if (!PanelOpen)
        {
            var menuHovered = layout.MenuButton.Contains(_pointerPoint);
            DrawIconButton(
                context,
                layout.MenuButton,
                menuHovered ? new Color(47, 63, 78) : new Color(32, 46, 58),
                menuHovered ? new Color(180, 219, 233) : new Color(107, 151, 169),
                menuHovered ? new Color(233, 247, 252) : new Color(200, 226, 236),
                DrawGearIcon);
            return;
        }

        DrawPanelFrame(context, layout.PanelBounds);
        var headerTextX = layout.CollapseButton.Right + (int)MathF.Round(12f * layout.LayoutScale);
        var headerTextWidth = Math.Max(64, layout.PanelBounds.Right - headerTextX - layout.ContentPadding);
        var collapseHovered = layout.CollapseButton.Contains(_pointerPoint);
        DrawIconButton(
            context,
            layout.CollapseButton,
            collapseHovered ? new Color(28, 52, 69) : new Color(19, 39, 54),
            collapseHovered ? new Color(174, 224, 237) : new Color(101, 154, 173),
            collapseHovered ? Color.White : new Color(213, 235, 243),
            DrawBackArrowIcon);
        DrawTextFitted(
            context,
            "Colony Menu",
            new Rectangle(headerTextX, layout.PanelBounds.Y + (int)MathF.Round(16f * layout.LayoutScale), headerTextWidth, (int)MathF.Round(30f * layout.LayoutScale)),
            Color.White,
            large: true);
        DrawText(
            context,
            "Build structures and manage colony assignments.",
            new Vector2(headerTextX, layout.PanelBounds.Y + MathF.Round(50f * layout.LayoutScale)),
            new Color(141, 183, 199));

        foreach (var tab in layout.Tabs)
        {
            var active = ActiveTab == tab.Key;
            var hovered = tab.Bounds.Contains(_pointerPoint);
            DrawTabButton(context, tab.Bounds, tab.Label, active, hovered);
        }

        if (ActiveTab == TabAssignments)
        {
            DrawAssignmentsTab(context, layout, session);
        }
        else if (ActiveTab == TabSelected && SelectedObject is not null)
        {
            DrawSelectedTab(context, layout);
        }
        else
        {
            DrawBuildingsTab(context, layout, session);
        }
    }

    private IReadOnlyList<Factory> GetBuildableOptions(GameSession session)
    {
        if (SelectedObject is Creature creature)
        {
            var buildables = creature.GetBuildable();
            if (buildables.Count > 0)
            {
                return buildables;
            }
        }

        return session.UnlockedBuildings;
    }

    private void SyncBuildSelection(IReadOnlyList<Factory> options)
    {
        if (SelectedBuildOption is null || options.All(factory => factory.Name != SelectedBuildOption.Name))
        {
            SelectedBuildOption = options.FirstOrDefault();
        }

        if (HoveredBuildOption is not null && options.All(factory => factory.Name != HoveredBuildOption.Name))
        {
            HoveredBuildOption = null;
        }
    }

    private IReadOnlyList<AssignmentEntryViewModel> BuildAssignmentEntries(IReadOnlyList<Trilobite> creatures)
    {
        return creatures.Count == 0 ? [] : [new AssignmentEntryViewModel(creatures.Count, creatures)];
    }

    private void NormalizeActiveTab()
    {
        var availableTabKeys = GetAvailableTabs()
            .Select(tab => tab.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (!availableTabKeys.Contains(ActiveTab))
        {
            ActiveTab = TabBuildings;
        }
    }

    private IReadOnlyList<(string Key, string Label)> GetAvailableTabs()
    {
        var tabs = new List<(string Key, string Label)>
        {
            (TabBuildings, "Buildings"),
            (TabAssignments, "Assignments")
        };

        if (SelectedObject is not null)
        {
            tabs.Add((TabSelected, "Selected"));
        }

        return tabs;
    }

    private void StartBuildingPlacement(Factory factory, GameApp game, GameSession session)
    {
        var targetBuilding = factory.Build(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        game.BeginBuildingPlacement(scaffolding);
    }

    private bool DeleteSelectedObject()
    {
        return SelectedObject switch
        {
            Creature creature => creature.RemoveFromGame("menuDelete"),
            Building building => building.RemoveFromGame("menuDelete"),
            _ => false
        };
    }

    private bool TransferCreatureAssignment(string fromAssignment, string toAssignment, GameSession session)
    {
        var creature = session.Cave?.Trilobites.FirstOrDefault(trilo => trilo.Assignment == fromAssignment);
        if (creature is null)
        {
            return false;
        }

        creature.Assignment = toAssignment;
        creature.ClearActionQueue();
        creature.GetBehavior()?.Invoke();
        return true;
    }
}
