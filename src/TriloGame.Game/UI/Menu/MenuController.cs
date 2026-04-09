using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Input;
using TriloGame.Game.UI.ViewModels;

namespace TriloGame.Game.UI.Menu;

public sealed partial class MenuController
{
    private const string TabBuildings = "buildings";
    private const string TabAssignments = "assignments";
    private const string TabSelected = "selected";
    private const int AssignmentRowHeight = 76;
    private const int AssignmentRowGap = 10;
    private const int RenameMaxLength = 24;

    private Point _pointerPoint;
    private GumUiRenderer? _gumUi;
    private bool _renamingSelectedTrilobite;
    private string _renameBuffer = string.Empty;

    public object? SelectedObject { get; private set; }

    public string ActiveTab { get; private set; } = TabBuildings;

    public bool PanelOpen { get; private set; } = true;

    public Factory? HoveredBuildOption { get; private set; }

    public Factory? SelectedBuildOption { get; private set; }

    public string AssignmentFilter { get; private set; } = "miner";

    public float BuildGridScroll { get; private set; }

    public float AssignmentActiveScroll { get; private set; }

    public float AssignmentUnassignedScroll { get; private set; }

    public float SelectedInventoryScroll { get; private set; }

    public bool IsRenamingSelectedTrilobite => _renamingSelectedTrilobite;

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
        CancelRenameSelectedTrilobite();
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
        CancelRenameSelectedTrilobite();
        SelectedObject = null;
        ActiveTab = TabBuildings;
        PanelOpen = true;
        HoveredBuildOption = null;
        SelectedBuildOption = null;
        AssignmentFilter = "miner";
        BuildGridScroll = 0f;
        AssignmentActiveScroll = 0f;
        AssignmentUnassignedScroll = 0f;
        SelectedInventoryScroll = 0f;
    }

    public void SetSelectedObject(object? selectedObject)
    {
        if (!ReferenceEquals(SelectedObject, selectedObject))
        {
            CancelRenameSelectedTrilobite();
            SelectedInventoryScroll = 0f;
        }

        SelectedObject = selectedObject;
        if (SelectedObject is not Trilobite)
        {
            CancelRenameSelectedTrilobite();
        }

        NormalizeActiveTab();
    }

    public bool BeginRenameSelectedTrilobite()
    {
        if (SelectedObject is not Trilobite trilobite)
        {
            return false;
        }

        _renamingSelectedTrilobite = true;
        _renameBuffer = trilobite.Name;
        return true;
    }

    public void CancelRenameSelectedTrilobite()
    {
        _renamingSelectedTrilobite = false;
        _renameBuffer = string.Empty;
    }

    public bool CommitRenameSelectedTrilobite()
    {
        if (SelectedObject is not Trilobite trilobite)
        {
            CancelRenameSelectedTrilobite();
            return false;
        }

        var trimmed = _renameBuffer.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        trilobite.Rename(trimmed);
        _renameBuffer = trilobite.Name;
        _renamingSelectedTrilobite = false;
        return true;
    }

    public bool HandleRenameInput(InputController input)
    {
        if (!_renamingSelectedTrilobite)
        {
            return false;
        }

        if (input.KeyPressed(Keys.Escape))
        {
            CancelRenameSelectedTrilobite();
            return true;
        }

        if (input.KeyPressed(Keys.Enter))
        {
            CommitRenameSelectedTrilobite();
            return true;
        }

        if (input.KeyPressed(Keys.Back))
        {
            if (_renameBuffer.Length > 0)
            {
                _renameBuffer = _renameBuffer[..^1];
            }

            return true;
        }

        foreach (var key in input.CurrentKeyboard.GetPressedKeys())
        {
            if (!input.PreviousKeyboard.IsKeyUp(key))
            {
                continue;
            }

            if (!TryConvertKeyToCharacter(key, input.CurrentKeyboard, out var character))
            {
                continue;
            }

            if (_renameBuffer.Length >= RenameMaxLength)
            {
                return true;
            }

            _renameBuffer += character;
            return true;
        }

        return false;
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
        else if (ActiveTab == TabSelected && layout.SelectedInventoryFrameBounds?.Contains(point) == true)
        {
            SelectedInventoryScroll = Clamp(SelectedInventoryScroll + delta, 0f, layout.SelectedInventoryMaxScroll);
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
            if (tab.Key != TabSelected)
            {
                CancelRenameSelectedTrilobite();
            }

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
        else if (ActiveTab == TabSelected)
        {
            if (_renamingSelectedTrilobite)
            {
                if (layout.SelectedRenameFieldBounds.Contains(point))
                {
                    return true;
                }

                if (layout.SelectedRenamePrimaryButtonBounds?.Contains(point) == true)
                {
                    game?.PlayUiSelectSound();
                    CommitRenameSelectedTrilobite();
                    return true;
                }

                if (layout.SelectedRenameSecondaryButtonBounds?.Contains(point) == true)
                {
                    game?.PlayUiSelectSound();
                    CancelRenameSelectedTrilobite();
                    return true;
                }
            }
            else if (SelectedObject is Trilobite && layout.SelectedRenamePrimaryButtonBounds?.Contains(point) == true)
            {
                game?.PlayUiSelectSound();
                BeginRenameSelectedTrilobite();
                return true;
            }

            if (layout.DeleteSelectedBounds.Contains(point))
            {
                game?.PlayUiSelectSound();
                return DeleteSelectedObject();
            }
        }

        return layout.PanelBounds.Contains(point);
    }

    public void Draw(RenderingContext context, Point viewport, GameApp game, GameSession session, GumUiRenderer gumUi)
    {
        _gumUi = gumUi;
        DrawInternal(context, viewport, game, session);
    }

    private void DrawInternal(RenderingContext context, Point viewport, GameApp game, GameSession session)
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

    private bool HasRenderer => _gumUi is not null;

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
        var removed = SelectedObject switch
        {
            Creature creature => creature.TakeDamage(Math.Max(1, creature.Health), "menuKill") > 0,
            Building building => building.RemoveFromGame("menuDelete"),
            _ => false
        };

        if (removed)
        {
            SetSelectedObject(null);
        }

        return removed;
    }

    private bool TransferCreatureAssignment(string fromAssignment, string toAssignment, GameSession session)
    {
        if (string.Equals(fromAssignment, toAssignment, StringComparison.Ordinal))
        {
            return false;
        }

        var creature = session.Cave?.Trilobites.FirstOrDefault(trilo => trilo.Assignment == fromAssignment);
        if (creature is null)
        {
            return false;
        }

        creature.Assignment = toAssignment;
        creature.RestartBehavior();
        return true;
    }

    private static bool TryConvertKeyToCharacter(Keys key, KeyboardState keyboard, out char character)
    {
        var shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (key is >= Keys.A and <= Keys.Z)
        {
            character = (char)('a' + (key - Keys.A));
            if (shiftHeld)
            {
                character = char.ToUpperInvariant(character);
            }

            return true;
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            character = (char)('0' + (key - Keys.D0));
            return true;
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            character = (char)('0' + (key - Keys.NumPad0));
            return true;
        }

        character = key switch
        {
            Keys.Space => ' ',
            Keys.OemMinus => shiftHeld ? '_' : '-',
            Keys.OemPlus => shiftHeld ? '+' : '=',
            Keys.OemPeriod => shiftHeld ? '>' : '.',
            Keys.OemComma => shiftHeld ? '<' : ',',
            Keys.OemQuestion => shiftHeld ? '?' : '/',
            Keys.OemSemicolon => shiftHeld ? ':' : ';',
            Keys.OemQuotes => shiftHeld ? '"' : '\'',
            _ => '\0'
        };

        return character != '\0';
    }
}
