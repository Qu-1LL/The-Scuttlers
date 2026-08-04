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

    // One object per scrollable area, each owning its offset, its measured extent and the identity of
    // the content it is showing. What this replaced was six loose floats plus a separate
    // _buildPreviewScrollKey string, with the clamp written out at each wheel site and the reset
    // written out again in two more places - four edits in four files for every new scrollable panel,
    // and a forgotten reset showing up as a panel that opens already scrolled into someone else's
    // content. Track() now does that reset as a consequence of pointing a region at new content.
    private readonly ScrollRegion _buildGridScroll = new();
    private readonly ScrollRegion _buildPreviewDescriptionScroll = new();
    private readonly ScrollRegion _assignmentActiveScroll = new();
    private readonly ScrollRegion _assignmentUnassignedScroll = new();
    private readonly ScrollRegion _selectedInventoryScroll = new();
    private readonly ScrollRegion _selectedDescriptionScroll = new();

    public object? SelectedObject { get; private set; }

    public string ActiveTab { get; private set; } = TabBuildings;

    public bool PanelOpen { get; private set; } = true;

    public Factory? HoveredBuildOption { get; private set; }

    public Factory? SelectedBuildOption { get; private set; }

    public string AssignmentFilter { get; private set; } = "miner";

    public float BuildGridScroll => _buildGridScroll.Offset;

    public float AssignmentActiveScroll => _assignmentActiveScroll.Offset;

    public float AssignmentUnassignedScroll => _assignmentUnassignedScroll.Offset;

    public float SelectedInventoryScroll => _selectedInventoryScroll.Offset;

    public float BuildPreviewDescriptionScroll => _buildPreviewDescriptionScroll.Offset;

    public float SelectedDescriptionScroll => _selectedDescriptionScroll.Offset;

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
        foreach (var region in EnumerateScrollRegions())
        {
            region.Reset();
        }
    }

    private IEnumerable<ScrollRegion> EnumerateScrollRegions()
    {
        yield return _buildGridScroll;
        yield return _buildPreviewDescriptionScroll;
        yield return _assignmentActiveScroll;
        yield return _assignmentUnassignedScroll;
        yield return _selectedInventoryScroll;
        yield return _selectedDescriptionScroll;
    }

    public void SetSelectedObject(object? selectedObject)
    {
        if (!ReferenceEquals(SelectedObject, selectedObject))
        {
            CancelRenameSelectedTrilobite();
            _selectedInventoryScroll.ScrollTo(0f);
            _selectedDescriptionScroll.ScrollTo(0f);
        }

        SelectedObject = selectedObject;
        if (SelectedObject is not Trilobite)
        {
            CancelRenameSelectedTrilobite();
        }

        if (SelectedObject is not null)
        {
            ActiveTab = TabSelected;
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
            ? layout.Chrome.MenuButton.Contains(point)
            : layout.Chrome.PanelBounds.Contains(point);
    }

    public bool HandleWheel(Point point, int delta, Point viewport, GameSession session)
    {
        _pointerPoint = point;
        if (!PanelOpen)
        {
            return CoversScreenPoint(point, viewport);
        }

        var layout = GetLayout(viewport, session);
        if (!layout.Chrome.PanelBounds.Contains(point))
        {
            return false;
        }

        // Each region clamps itself against the extent layout measured for it, so the wheel handler
        // no longer repeats a Clamp against a matching MaxScroll field at every site.
        if (ActiveTab == TabBuildings && layout.Build.DescriptionLayout.ViewportBounds.Contains(point))
        {
            _buildPreviewDescriptionScroll.ScrollBy(delta);
        }
        else if (ActiveTab == TabBuildings && layout.Build.GridFrameBounds.Contains(point))
        {
            _buildGridScroll.ScrollBy(delta);
        }
        else if (ActiveTab == TabAssignments)
        {
            if (layout.Assignments.ActiveBounds.Contains(point))
            {
                _assignmentActiveScroll.ScrollBy(delta);
            }
            else if (layout.Assignments.UnassignedBounds.Contains(point))
            {
                _assignmentUnassignedScroll.ScrollBy(delta);
            }
        }
        else if (ActiveTab == TabSelected && layout.Selected.DescriptionLayout.ViewportBounds.Contains(point))
        {
            _selectedDescriptionScroll.ScrollBy(delta);
        }
        else if (ActiveTab == TabSelected && layout.Selected.InventoryFrameBounds?.Contains(point) == true)
        {
            _selectedInventoryScroll.ScrollBy(delta);
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
        HoveredBuildOption = layout.Build.Cards
            .FirstOrDefault(card => card.Bounds.Contains(point))
            .Factory;
    }

    public MenuInteractionResult HandleClick(Point point, Point viewport, GameSession session)
    {
        _pointerPoint = point;
        var layout = GetLayout(viewport, session);
        if (!PanelOpen)
        {
            if (layout.Chrome.MenuButton.Contains(point))
            {
                OpenPanel();
                return MenuInteractionResult.ConsumedWithSelectSound;
            }

            return MenuInteractionResult.NotHandled;
        }

        if (layout.Chrome.CollapseButton.Contains(point))
        {
            ClosePanel();
            return MenuInteractionResult.ConsumedWithSelectSound;
        }

        foreach (var tab in layout.Chrome.Tabs)
        {
            if (!tab.Bounds.Contains(point))
            {
                continue;
            }

            if (tab.Key != TabSelected)
            {
                CancelRenameSelectedTrilobite();
            }

            ActiveTab = tab.Key;
            NormalizeActiveTab();
            return MenuInteractionResult.ConsumedWithSelectSound;
        }

        if (ActiveTab == TabBuildings)
        {
            foreach (var card in layout.Build.Cards)
            {
                if (!card.Bounds.Contains(point))
                {
                    continue;
                }

                SelectedBuildOption = card.Factory;
                HoveredBuildOption = card.Factory;
                return MenuInteractionResult.RequestBuildingPlacement(
                    new BuildingPlacementRequest(card.Factory, CreateBuildingPlacement(card.Factory, session)));
            }
        }
        else if (ActiveTab == TabAssignments)
        {
            foreach (var filter in layout.Assignments.Filters)
            {
                if (!filter.Bounds.Contains(point))
                {
                    continue;
                }

                AssignmentFilter = filter.Key;
                return MenuInteractionResult.ConsumedWithSelectSound;
            }

            foreach (var row in layout.Assignments.ActiveRows)
            {
                if (row.Bounds.Contains(point))
                {
                    return MenuInteractionResult.WithSelectSound(TransferCreatureAssignment(row.FromAssignment, row.ToAssignment, session));
                }
            }

            foreach (var row in layout.Assignments.UnassignedRows)
            {
                if (row.Bounds.Contains(point))
                {
                    return MenuInteractionResult.WithSelectSound(TransferCreatureAssignment(row.FromAssignment, row.ToAssignment, session));
                }
            }
        }
        else if (ActiveTab == TabSelected)
        {
            if (_renamingSelectedTrilobite)
            {
                if (layout.Selected.RenameFieldBounds.Contains(point))
                {
                    return MenuInteractionResult.ConsumedSilently;
                }

                if (layout.Selected.RenamePrimaryButtonBounds?.Contains(point) == true)
                {
                    CommitRenameSelectedTrilobite();
                    return MenuInteractionResult.ConsumedWithSelectSound;
                }

                if (layout.Selected.RenameSecondaryButtonBounds?.Contains(point) == true)
                {
                    CancelRenameSelectedTrilobite();
                    return MenuInteractionResult.ConsumedWithSelectSound;
                }
            }
            else if (SelectedObject is Trilobite && layout.Selected.RenamePrimaryButtonBounds?.Contains(point) == true)
            {
                BeginRenameSelectedTrilobite();
                return MenuInteractionResult.ConsumedWithSelectSound;
            }

            if (SelectedObject is Scaffolding scaffolding &&
                layout.Selected.BuildFirstBounds?.Contains(point) == true)
            {
                return MenuInteractionResult.WithSelectSound(scaffolding.ToggleBuildFirst());
            }

            if (layout.Selected.DeleteBounds.Contains(point))
            {
                return MenuInteractionResult.WithSelectSound(DeleteSelectedObject());
            }
        }

        return layout.Chrome.PanelBounds.Contains(point)
            ? MenuInteractionResult.ConsumedSilently
            : MenuInteractionResult.NotHandled;
    }

    public bool HandleClick(Point point, Point viewport, object? _, GameSession session)
    {
        return HandleClick(point, viewport, session).Consumed;
    }

    public void Draw(RenderingContext context, Point viewport, GameSession session, GumUiRenderer gumUi)
    {
        _gumUi = gumUi;
        DrawInternal(context, viewport, session);
    }

    private void DrawInternal(RenderingContext context, Point viewport, GameSession session)
    {
        var layout = GetLayout(viewport, session);
        if (!PanelOpen)
        {
            var menuHovered = layout.Chrome.MenuButton.Contains(_pointerPoint);
            DrawIconButton(
                context,
                layout.Chrome.MenuButton,
                menuHovered ? new Color(47, 63, 78) : new Color(32, 46, 58),
                menuHovered ? new Color(180, 219, 233) : new Color(107, 151, 169),
                menuHovered ? new Color(233, 247, 252) : new Color(200, 226, 236),
                DrawGearIcon);
            return;
        }

        DrawPanelFrame(context, layout.Chrome.PanelBounds);
        var headerTextX = layout.Chrome.CollapseButton.Right + (int)MathF.Round(12f * layout.Chrome.LayoutScale);
        var headerTextWidth = Math.Max(64, layout.Chrome.PanelBounds.Right - headerTextX - layout.Chrome.ContentPadding);
        var collapseHovered = layout.Chrome.CollapseButton.Contains(_pointerPoint);
        DrawIconButton(
            context,
            layout.Chrome.CollapseButton,
            collapseHovered ? new Color(28, 52, 69) : new Color(19, 39, 54),
            collapseHovered ? new Color(174, 224, 237) : new Color(101, 154, 173),
            collapseHovered ? Color.White : new Color(213, 235, 243),
            DrawBackArrowIcon);
        DrawTextFitted(
            context,
            "Colony Menu",
            new Rectangle(headerTextX, layout.Chrome.PanelBounds.Y + (int)MathF.Round(16f * layout.Chrome.LayoutScale), headerTextWidth, (int)MathF.Round(30f * layout.Chrome.LayoutScale)),
            Color.White,
            large: true);
        DrawText(
            context,
            "Build structures and manage colony assignments.",
            new Vector2(headerTextX, layout.Chrome.PanelBounds.Y + MathF.Round(50f * layout.Chrome.LayoutScale)),
            new Color(141, 183, 199));

        foreach (var tab in layout.Chrome.Tabs)
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
        if (SelectedBuildOption is null || !ContainsVisibleBuildOption(options, SelectedBuildOption.Name))
        {
            SelectedBuildOption = FindFirstVisibleBuildOption(options);
        }

        if (HoveredBuildOption is not null && !ContainsVisibleBuildOption(options, HoveredBuildOption.Name))
        {
            HoveredBuildOption = null;
        }
    }

    // Temporarily suppress glitchy ranch/storage-adjacent buildings from the build menu.
    private static bool IsBuildOptionTemporarilyHidden(Factory factory)
    {
        return factory.Name is "Soil Patch" or "Garage" or "Silo";
    }

    private static Factory? FindFirstVisibleBuildOption(IReadOnlyList<Factory> options)
    {
        for (var index = 0; index < options.Count; index++)
        {
            var factory = options[index];
            if (!IsBuildOptionTemporarilyHidden(factory))
            {
                return factory;
            }
        }

        return null;
    }

    private static bool ContainsVisibleBuildOption(IReadOnlyList<Factory> options, string factoryName)
    {
        for (var index = 0; index < options.Count; index++)
        {
            var factory = options[index];
            if (!IsBuildOptionTemporarilyHidden(factory) &&
                string.Equals(factory.Name, factoryName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

    private static Scaffolding CreateBuildingPlacement(Factory factory, GameSession session)
    {
        var targetBuilding = factory.Build(session);
        return new Scaffolding(session, targetBuilding);
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

        return creature.ChangeAssignment(toAssignment);
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
