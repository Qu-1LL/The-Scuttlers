using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private readonly MiningTileSelectionState _miningTileSelection = new();
    private readonly MiningOrderMenuController _miningOrderMenu = new();
    private readonly MiningOrderMenuRenderer _miningOrderMenuRenderer = new();
    private readonly MiningTileHoverTooltipRenderer _miningTileHoverTooltipRenderer = new();
    private bool _selectionDragAppend;

    private bool ControlHeld()
    {
        return _input.KeyHeld(Keys.LeftControl) || _input.KeyHeld(Keys.RightControl);
    }

    private void ClearMiningTileSelection(bool closeMenu = true)
    {
        _miningTileSelection.Clear();
        if (closeMenu)
        {
            _miningOrderMenu.Close();
        }
    }

    private void ClearObjectSelection()
    {
        _selectedObject = null;
        _selectedTrilobites.Clear();
        _menu.SetSelectedObject(null);
        _roleRadialMenu = null;
        ClearPendingManualMove();
    }

    private bool HasSelectedMiningTiles()
    {
        return _miningTileSelection.HasSelection;
    }

    private bool CanSelectMiningTile(Tile tile)
    {
        var cave = _session.Cave;
        return cave is not null &&
               (Building.IsMineableType(tile.Base) || !cave.IsTileRevealed(tile));
    }

    private void SelectMiningTile(Tile tile, bool append, bool toggleIfAlreadySelected)
    {
        _miningTileSelection.Select(tile.Key, append, toggleIfAlreadySelected);
        if (!_miningTileSelection.HasSelection)
        {
            _miningOrderMenu.Close();
        }
    }

    private void SelectMiningTiles(IEnumerable<string> tileKeys, bool append)
    {
        _miningTileSelection.SelectMany(tileKeys, append);
    }

    private bool TryHandleMiningTileSelectionClick(Tile tile)
    {
        if (!CanSelectMiningTile(tile))
        {
            return false;
        }

        ClearObjectSelection();
        SelectMiningTile(tile, ControlHeld(), ControlHeld());
        PlayUiSelectSound();
        return true;
    }

    private bool TryFinalizeMiningTileSelectionBox()
    {
        if (_selectionBoxBounds is null)
        {
            return false;
        }

        var cave = _session.Cave;
        IReadOnlyList<string> selectedKeys = cave is null
            ? []
            : MiningTileRectangleSelector.SelectTileKeys(
                cave,
                _selectionBoxBounds.Value,
                _camera.ScreenToWorld,
                GetTileScreenBounds,
                CanSelectMiningTile);
        if (selectedKeys.Count == 0)
        {
            if (!_selectionDragAppend)
            {
                ClearMiningTileSelection();
            }

            return false;
        }

        ClearObjectSelection();
        SelectMiningTiles(selectedKeys, _selectionDragAppend);
        return true;
    }

    private void OpenMiningOrderMenu(Point point)
    {
        var cave = _session.Cave;
        if (cave is null || !_miningTileSelection.HasSelection)
        {
            _miningOrderMenu.Close();
            return;
        }

        var miners = cave.Trilobites
            .Where(trilobite => trilobite.Cave == cave && string.Equals(trilobite.Assignment, "miner", StringComparison.Ordinal))
            .OrderBy(trilobite => trilobite.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _miningOrderMenu.Open(point.ToVector2(), miners);
    }

    private bool HandleMiningOrderMenuWheel(Point point, int wheelDelta)
    {
        if (!_miningOrderMenu.IsOpen)
        {
            return false;
        }

        var layout = BuildMiningOrderMenuLayout();
        var result = _miningOrderMenu.HandleWheel(point, layout.PanelBounds, layout.ListViewportBounds, layout.MaxScroll, wheelDelta);
        return result.Consumed;
    }

    private bool TryHandleMiningOrderMenuClick(Point point)
    {
        if (!_miningOrderMenu.IsOpen)
        {
            return false;
        }

        var layout = BuildMiningOrderMenuLayout();
        var result = _miningOrderMenu.HandleClick(point, layout.Rows, layout.PanelBounds, layout.SendButtonBounds, ControlHeld());
        if (result.PlaySelectSound)
        {
            PlayUiSelectSound();
        }

        if (result.Outcome == MiningOrderMenuOutcome.SendRequested)
        {
            DispatchSelectedMinersToMiningTiles();
        }

        return result.Consumed;
    }

    private void DispatchSelectedMinersToMiningTiles()
    {
        var cave = _session.Cave;
        if (cave is null || !_miningOrderMenu.IsOpen)
        {
            return;
        }

        MineOrderExecutor.Dispatch(cave, _miningOrderMenu.GetSelectedMiners(), _miningTileSelection.TileKeys);
        ClearMiningTileSelection();
    }

    private void DrawMiningTileSelection()
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            return;
        }

        var outlineThickness = Math.Max(2, (int)MathF.Round(_camera.CurrentScale * 2f));
        foreach (var tileKey in _miningTileSelection.TileKeys)
        {
            var tile = cave.GetTile(tileKey);
            if (tile is null)
            {
                continue;
            }

            DrawScreenBorder(GetTileScreenBounds(tile.Coordinates), new Color(146, 233, 138, 220), outlineThickness);
        }
    }

    private void DrawMiningTileHoverLabel()
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            return;
        }

        var tile = GetTileAtScreenPoint(_input.MousePoint);
        if (tile is null || !CanSelectMiningTile(tile) || !cave.IsTileRevealed(tile))
        {
            return;
        }

        var gameplayBounds = SelectionFocusLayout.GetGameplayBounds(Window.ClientBounds.Size, _menu.GetOpenPanelWidth(Window.ClientBounds.Size));
        _miningTileHoverTooltipRenderer.Draw(_gumUiRenderer, tile, _input.MousePoint, gameplayBounds);
    }

    private void DrawMiningOrderMenu()
    {
        if (!_miningOrderMenu.IsOpen)
        {
            return;
        }

        SyncMiningOrderMenuMiners();
        if (!_miningOrderMenu.IsOpen)
        {
            return;
        }

        var layout = BuildMiningOrderMenuLayout();
        _miningOrderMenuRenderer.Draw(
            _gumUiRenderer,
            layout,
            _miningOrderMenu,
            _input.MousePoint,
            _miningTileSelection.Count,
            _miningTileSelection.HasSelection);
    }

    private MiningOrderMenuLayout BuildMiningOrderMenuLayout()
    {
        var viewport = Window.ClientBounds.Size;
        return MiningOrderMenuLayout.Build(_miningOrderMenu, viewport, _menu.GetOpenPanelWidth(viewport));
    }

    private Rectangle GetTileScreenBounds(Shared.Math.GridPoint point)
    {
        var centerWorld = new Vector2(point.X * TileConstants.TileSize, point.Y * TileConstants.TileSize);
        var topLeftWorld = centerWorld - new Vector2(TileConstants.TileHalfSize, TileConstants.TileHalfSize);
        var bottomRightWorld = centerWorld + new Vector2(TileConstants.TileHalfSize, TileConstants.TileHalfSize);

        var topLeftScreen = _camera.WorldToScreen(topLeftWorld);
        var bottomRightScreen = _camera.WorldToScreen(bottomRightWorld);

        var left = (int)MathF.Floor(topLeftScreen.X);
        var top = (int)MathF.Floor(topLeftScreen.Y);
        var right = (int)MathF.Ceiling(bottomRightScreen.X);
        var bottom = (int)MathF.Ceiling(bottomRightScreen.Y);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private void DrawTileSelectionEdge(TileSelectionEdge edge, Color color, int thickness)
    {
        var bounds = GetTileScreenBounds(edge.Tile);
        Rectangle edgeBounds;
        switch (edge.Side)
        {
            case TileSelectionEdgeSide.Top:
                edgeBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness);
                break;
            case TileSelectionEdgeSide.Right:
                edgeBounds = new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);
                break;
            case TileSelectionEdgeSide.Bottom:
                edgeBounds = new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
                break;
            default:
                edgeBounds = new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height);
                break;
        }

        _gumUiRenderer.AddFilledRectangle(edgeBounds, color);
    }

    private void SyncMiningOrderMenuMiners()
    {
        var cave = _session.Cave;
        if (cave is null || !_miningOrderMenu.IsOpen)
        {
            return;
        }

        var activeMiners = cave.Trilobites
            .Where(trilobite => trilobite.Cave == cave && string.Equals(trilobite.Assignment, "miner", StringComparison.Ordinal))
            .OrderBy(trilobite => trilobite.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _miningOrderMenu.SyncMiners(activeMiners);
    }

}
