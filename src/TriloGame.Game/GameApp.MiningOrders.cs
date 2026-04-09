using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private enum SelectionDragMode
    {
        Trilobites,
        MiningTiles
    }

    private readonly List<string> _selectedMiningTileKeys = [];
    private SelectionDragMode? _selectionDragMode;
    private bool _selectionDragAppend;
    private MiningOrderMenuState? _miningOrderMenu;

    private bool ControlHeld()
    {
        return _input.KeyHeld(Keys.LeftControl) || _input.KeyHeld(Keys.RightControl);
    }

    private SelectionDragMode? ResolveSelectionDragMode(Point point)
    {
        if (!ShouldStartSelectionDrag(point))
        {
            return null;
        }

        if (TryHitTrilobite(point, out _))
        {
            return SelectionDragMode.Trilobites;
        }

        var tile = GetTileAtScreenPoint(point);
        if (tile is not null && CanSelectMiningTile(tile))
        {
            return SelectionDragMode.MiningTiles;
        }

        return SelectionDragMode.Trilobites;
    }

    private void ClearMiningTileSelection(bool closeMenu = true)
    {
        _selectedMiningTileKeys.Clear();
        if (closeMenu)
        {
            _miningOrderMenu = null;
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
        return _selectedMiningTileKeys.Count > 0;
    }

    private bool CanSelectMiningTile(Tile tile)
    {
        var cave = _session.Cave;
        return cave is not null &&
               (cave.HasOpal(tile) || Building.IsMineableType(tile.Base) || !cave.IsTileRevealed(tile));
    }

    private bool TryBeginLeftMiningSelectionDrag(Point point)
    {
        if (_roleRadialMenu is not null || _miningOrderMenu is not null || !ShouldStartSelectionDrag(point))
        {
            return false;
        }

        var tile = GetTileAtScreenPoint(point);
        if (tile is null || !CanSelectMiningTile(tile))
        {
            return false;
        }

        _selectionDragAppend = ControlHeld();
        _selectionDragMode = SelectionDragMode.MiningTiles;
        _selectionDragActive = true;
        _selectionBoxBounds = CreateScreenRectangle(point, point);
        return true;
    }

    private void SelectMiningTile(Tile tile, bool append, bool toggleIfAlreadySelected)
    {
        if (!append)
        {
            _selectedMiningTileKeys.Clear();
        }

        if (toggleIfAlreadySelected && _selectedMiningTileKeys.Remove(tile.Key))
        {
            if (_selectedMiningTileKeys.Count == 0)
            {
                _miningOrderMenu = null;
            }

            return;
        }

        if (!_selectedMiningTileKeys.Contains(tile.Key, StringComparer.Ordinal))
        {
            _selectedMiningTileKeys.Add(tile.Key);
        }
    }

    private void SelectMiningTiles(IEnumerable<string> tileKeys, bool append)
    {
        if (!append)
        {
            _selectedMiningTileKeys.Clear();
        }

        foreach (var tileKey in tileKeys)
        {
            if (!_selectedMiningTileKeys.Contains(tileKey, StringComparer.Ordinal))
            {
                _selectedMiningTileKeys.Add(tileKey);
            }
        }
    }

    private IReadOnlyList<string> GetMiningTilesInScreenRectangle(Rectangle selection)
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            return [];
        }

        var selectedKeys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var topLeft = _camera.ScreenToWorld(new Point(selection.Left, selection.Top));
        var bottomRight = _camera.ScreenToWorld(new Point(selection.Right, selection.Bottom));
        var minTileX = (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X) / TileConstants.TileSize) - 1;
        var minTileY = (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y) / TileConstants.TileSize) - 1;
        var maxTileX = (int)MathF.Ceiling(MathF.Max(topLeft.X, bottomRight.X) / TileConstants.TileSize) + 1;
        var maxTileY = (int)MathF.Ceiling(MathF.Max(topLeft.Y, bottomRight.Y) / TileConstants.TileSize) + 1;

        for (var y = minTileY; y <= maxTileY; y++)
        {
            for (var x = minTileX; x <= maxTileX; x++)
            {
                var tile = cave.GetTile(new Shared.Math.GridPoint(x, y).ToString());
                if (tile is null || !CanSelectMiningTile(tile))
                {
                    continue;
                }

                var tileBounds = GetTileScreenBounds(tile.Coordinates);
                if (!selection.Intersects(tileBounds) || !seen.Add(tile.Key))
                {
                    continue;
                }

                selectedKeys.Add(tile.Key);
            }
        }

        return selectedKeys;
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

    private void FinalizeMiningTileSelectionBox()
    {
        if (_selectionBoxBounds is null)
        {
            return;
        }

        var selectedKeys = GetMiningTilesInScreenRectangle(_selectionBoxBounds.Value);
        if (selectedKeys.Count == 0)
        {
            if (!_selectionDragAppend)
            {
                ClearMiningTileSelection();
            }

            return;
        }

        ClearObjectSelection();
        SelectMiningTiles(selectedKeys, _selectionDragAppend);
    }

    private void OpenMiningOrderMenu(Point point)
    {
        var cave = _session.Cave;
        if (cave is null || _selectedMiningTileKeys.Count == 0)
        {
            _miningOrderMenu = null;
            return;
        }

        var miners = cave.Trilobites
            .Where(trilobite => trilobite.Cave == cave && string.Equals(trilobite.Assignment, "miner", StringComparison.Ordinal))
            .OrderBy(trilobite => trilobite.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _miningOrderMenu = new MiningOrderMenuState(point.ToVector2(), miners);
        foreach (var miner in miners)
        {
            _miningOrderMenu.SelectedMiners.Add(miner);
        }
    }

    private bool HandleMiningOrderMenuWheel(Point point, int wheelDelta)
    {
        if (_miningOrderMenu is null)
        {
            return false;
        }

        var layout = BuildMiningOrderMenuLayout(_miningOrderMenu);
        if (!layout.ListViewportBounds.Contains(point))
        {
            return layout.PanelBounds.Contains(point);
        }

        _miningOrderMenu.Scroll = Math.Clamp(_miningOrderMenu.Scroll + wheelDelta, 0f, layout.MaxScroll);
        return true;
    }

    private bool TryHandleMiningOrderMenuClick(Point point)
    {
        if (_miningOrderMenu is null)
        {
            return false;
        }

        var layout = BuildMiningOrderMenuLayout(_miningOrderMenu);
        if (!layout.PanelBounds.Contains(point))
        {
            return false;
        }

        foreach (var row in layout.Rows)
        {
            if (!row.Bounds.Contains(point))
            {
                continue;
            }

            PlayUiSelectSound();
            if (ControlHeld())
            {
                if (!_miningOrderMenu.SelectedMiners.Add(row.Miner))
                {
                    _miningOrderMenu.SelectedMiners.Remove(row.Miner);
                }
            }
            else
            {
                _miningOrderMenu.SelectedMiners.Clear();
                _miningOrderMenu.SelectedMiners.Add(row.Miner);
            }

            return true;
        }

        if (layout.SendButtonBounds.Contains(point))
        {
            PlayUiSelectSound();
            DispatchSelectedMinersToMiningTiles();
            return true;
        }

        return true;
    }

    private void DispatchSelectedMinersToMiningTiles()
    {
        var cave = _session.Cave;
        if (cave is null || _miningOrderMenu is null)
        {
            return;
        }

        var selectedMiners = _miningOrderMenu.SelectedMiners
            .Where(miner => miner.Cave == cave && string.Equals(miner.Assignment, "miner", StringComparison.Ordinal))
            .ToArray();
        var plans = MineOrderPlanner.BuildPlans(cave, selectedMiners, _selectedMiningTileKeys);
        foreach (var miner in selectedMiners)
        {
            if (plans.TryGetValue(miner, out var tileKeys))
            {
                miner.SetManualMineOrders(tileKeys);
            }
            else
            {
                miner.ClearManualMineOrders(restartBehavior: true);
            }
        }

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
        foreach (var tileKey in _selectedMiningTileKeys)
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

        var lines = new List<string> { GetTileDisplayName(tile) };
        if (cave.HasOpal(tile))
        {
            var opal = cave.GetOpalNode(tile);
            if (opal is not null)
            {
                lines.Add($"Yield: {opal.RemainingYield}");
            }
        }
        else if (tile.IsOreTile())
        {
            lines.Add($"Yield: {tile.ResourceYield}");
        }

        const int tooltipPaddingX = 14;
        const int tooltipPaddingY = 8;
        const int tooltipLineGap = 2;
        var maxWidth = 0;
        foreach (var line in lines)
        {
            maxWidth = Math.Max(maxWidth, GumTextLayout.Measure(line, GumTextStyle.Small).X);
        }

        var lineHeight = GumTextLayout.GetMetrics(GumTextStyle.Small).LineHeight;
        var labelHeight = (lineHeight * lines.Count) + Math.Max(0, (lines.Count - 1) * tooltipLineGap);
        var bounds = new Rectangle(
            _input.MousePoint.X + 14,
            _input.MousePoint.Y - (labelHeight + 20),
            Math.Max(132, maxWidth + (tooltipPaddingX * 2) + 8),
            labelHeight + (tooltipPaddingY * 2));
        var gameplayBounds = SelectionFocusLayout.GetGameplayBounds(Window.ClientBounds.Size, _menu.GetOpenPanelWidth(Window.ClientBounds.Size));
        if (bounds.Right > gameplayBounds.Right)
        {
            bounds.X = gameplayBounds.Right - bounds.Width;
        }

        if (bounds.Y < gameplayBounds.Top)
        {
            bounds.Y = _input.MousePoint.Y + 14;
        }

        DrawRoundedScreenFrame(bounds, new Color(7, 15, 22, 230), new Color(143, 205, 226), 2, 12);
        var textBounds = new Rectangle(bounds.X + tooltipPaddingX, bounds.Y + tooltipPaddingY, Math.Max(0, bounds.Width - (tooltipPaddingX * 2)), lineHeight);
        for (var index = 0; index < lines.Count; index++)
        {
            var lineBounds = new Rectangle(textBounds.X, textBounds.Y + (index * (lineHeight + tooltipLineGap)), textBounds.Width, lineHeight);
            DrawScreenTextFittedLeft(lines[index], lineBounds, Color.White, _rendering.SmallFont, minScale: 1f);
        }
    }

    private void DrawMiningOrderMenu()
    {
        if (_miningOrderMenu is null)
        {
            return;
        }

        SyncMiningOrderMenuMiners();
        if (_miningOrderMenu is null)
        {
            return;
        }

        var layout = BuildMiningOrderMenuLayout(_miningOrderMenu);
        var pointer = _input.MousePoint;
        DrawRoundedScreenFrame(layout.PanelBounds, new Color(8, 19, 29, 247), new Color(77, 122, 140), 3, 16);
        DrawScreenTextFittedCentered("Send Miners", layout.HeaderBounds, Color.White, _rendering.UiFont, minScale: 0.72f);
        DrawScreenTextFittedCentered(
            $"{_selectedMiningTileKeys.Count} target tiles",
            layout.SubtitleBounds,
            new Color(180, 214, 226),
            _rendering.SmallFont,
            minScale: 0.72f);

        DrawRoundedScreenFrame(layout.ListViewportBounds, new Color(10, 22, 32), new Color(74, 114, 132), 2, 12);
        if (layout.Rows.Count == 0)
        {
            DrawScreenTextFittedCentered("No miners available", layout.ListViewportBounds, new Color(171, 198, 208), _rendering.SmallFont, minScale: 0.72f);
        }

        foreach (var row in layout.Rows)
        {
            var hovered = row.Bounds.Contains(pointer);
            var selected = _miningOrderMenu.SelectedMiners.Contains(row.Miner);
            var fill = selected
                ? hovered ? new Color(39, 86, 109) : new Color(33, 75, 95)
                : hovered ? new Color(20, 48, 68) : new Color(13, 33, 48);
            var border = selected
                ? hovered ? new Color(160, 221, 237) : new Color(140, 207, 224)
                : hovered ? new Color(76, 116, 136) : new Color(53, 88, 106);
            DrawRoundedScreenFrame(row.Bounds, fill, border, 2, 12);
            DrawScreenTextFittedLeft(
                row.Miner.Name,
                new Rectangle(row.Bounds.X + 12, row.Bounds.Y + 4, row.Bounds.Width - 24, row.Bounds.Height - 8),
                Color.White,
                _rendering.SmallFont,
                minScale: 0.72f);
        }

        if (layout.ScrollbarTrackBounds is { } track && layout.ScrollbarThumbBounds is { } thumb)
        {
            DrawRoundedScreenFrame(track, new Color(9, 19, 28), new Color(39, 64, 79), 2, 6);
            DrawRoundedScreenFrame(thumb, new Color(109, 170, 192), new Color(191, 230, 244), 2, 6);
        }

        var sendHovered = layout.SendButtonBounds.Contains(pointer);
        var sendEnabled = _miningOrderMenu.SelectedMiners.Count > 0 && _selectedMiningTileKeys.Count > 0;
        var fillColor = sendEnabled
            ? sendHovered ? new Color(194, 171, 122) : new Color(170, 148, 102)
            : new Color(44, 53, 61);
        var borderColor = sendEnabled
            ? sendHovered ? new Color(255, 232, 184) : new Color(235, 210, 158)
            : new Color(89, 100, 109);
        var textColor = sendEnabled ? new Color(10, 23, 34) : new Color(160, 171, 178);
        DrawRoundedScreenFrame(layout.SendButtonBounds, fillColor, borderColor, 2, 12);
        DrawScreenTextFittedCentered("Send Miners", layout.SendButtonBounds, textColor, _rendering.SmallFont, minScale: 0.72f);
    }

    private MiningOrderMenuLayout BuildMiningOrderMenuLayout(MiningOrderMenuState state)
    {
        var gameplayBounds = SelectionFocusLayout.GetGameplayBounds(Window.ClientBounds.Size, _menu.GetOpenPanelWidth(Window.ClientBounds.Size));
        const int panelWidth = 300;
        const int rowHeight = 36;
        const int rowGap = 6;
        const int headerHeight = 32;
        const int subtitleHeight = 20;
        const int sendHeight = 42;
        const int viewportHeight = 240;

        var listBounds = new Rectangle(0, 0, panelWidth - 32, viewportHeight);
        var contentHeight = state.Miners.Length == 0 ? 0 : (state.Miners.Length * rowHeight) + (Math.Max(0, state.Miners.Length - 1) * rowGap);
        var maxScroll = Math.Max(0f, contentHeight - listBounds.Height);
        state.Scroll = Math.Clamp(state.Scroll, 0f, maxScroll);

        var panelHeight = 16 + headerHeight + subtitleHeight + 12 + viewportHeight + 14 + sendHeight + 16;
        var panelX = (int)MathF.Round(Math.Clamp(state.AnchorScreen.X, gameplayBounds.Left + 8f, gameplayBounds.Right - panelWidth - 8f));
        var panelY = (int)MathF.Round(Math.Clamp(state.AnchorScreen.Y, gameplayBounds.Top + 8f, gameplayBounds.Bottom - panelHeight - 8f));
        var panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        var headerBounds = new Rectangle(panelBounds.X + 16, panelBounds.Y + 14, panelBounds.Width - 32, headerHeight);
        var subtitleBounds = new Rectangle(panelBounds.X + 16, headerBounds.Bottom + 2, panelBounds.Width - 32, subtitleHeight);
        var viewportBounds = new Rectangle(panelBounds.X + 16, subtitleBounds.Bottom + 10, panelBounds.Width - 32, viewportHeight);
        var sendButtonBounds = new Rectangle(panelBounds.X + 16, viewportBounds.Bottom + 14, panelBounds.Width - 32, sendHeight);

        var rows = new List<MiningOrderMinerRow>(state.Miners.Length);
        var rowY = viewportBounds.Y - (int)MathF.Round(state.Scroll);
        foreach (var miner in state.Miners)
        {
            var bounds = new Rectangle(viewportBounds.X + 6, rowY, viewportBounds.Width - 18, rowHeight);
            if (bounds.Bottom >= viewportBounds.Top && bounds.Top <= viewportBounds.Bottom)
            {
                rows.Add(new MiningOrderMinerRow(miner, bounds));
            }

            rowY += rowHeight + rowGap;
        }

        Rectangle? trackBounds = null;
        Rectangle? thumbBounds = null;
        if (maxScroll > 0f)
        {
            var trackHeight = viewportBounds.Height;
            var thumbHeight = Math.Max(32f, (viewportBounds.Height / (float)contentHeight) * trackHeight);
            var travel = Math.Max(0f, trackHeight - thumbHeight);
            var ratio = state.Scroll / maxScroll;
            var thumbY = viewportBounds.Y + (int)MathF.Round(ratio * travel);
            trackBounds = new Rectangle(viewportBounds.Right - 6, viewportBounds.Y, 6, trackHeight);
            thumbBounds = new Rectangle(viewportBounds.Right - 6, thumbY, 6, (int)MathF.Round(thumbHeight));
        }

        return new MiningOrderMenuLayout(panelBounds, headerBounds, subtitleBounds, viewportBounds, rows, maxScroll, trackBounds, thumbBounds, sendButtonBounds);
    }

    private string GetTileDisplayName(Tile tile)
    {
        if (_session.Cave?.HasOpal(tile) == true)
        {
            return "Opal";
        }

        return tile.Base switch
        {
            "wall" => "Wall",
            "empty" => "Unknown",
            _ => tile.Base
        };
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

    private sealed class MiningOrderMenuState
    {
        public MiningOrderMenuState(Vector2 anchorScreen, Trilobite[] miners)
        {
            AnchorScreen = anchorScreen;
            Miners = miners;
        }

        public Vector2 AnchorScreen { get; }

        public Trilobite[] Miners { get; }

        public HashSet<Trilobite> SelectedMiners { get; } = [];

        public float Scroll { get; set; }
    }

    private void SyncMiningOrderMenuMiners()
    {
        var cave = _session.Cave;
        if (cave is null || _miningOrderMenu is null)
        {
            return;
        }

        var activeMiners = cave.Trilobites
            .Where(trilobite => trilobite.Cave == cave && string.Equals(trilobite.Assignment, "miner", StringComparison.Ordinal))
            .OrderBy(trilobite => trilobite.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (activeMiners.Length == _miningOrderMenu.Miners.Length &&
            activeMiners.SequenceEqual(_miningOrderMenu.Miners))
        {
            return;
        }

        var selectedMinerNames = _miningOrderMenu.SelectedMiners.Select(miner => miner.Name).ToHashSet(StringComparer.Ordinal);
        _miningOrderMenu = new MiningOrderMenuState(_miningOrderMenu.AnchorScreen, activeMiners)
        {
            Scroll = _miningOrderMenu.Scroll
        };
        foreach (var miner in activeMiners)
        {
            if (selectedMinerNames.Contains(miner.Name))
            {
                _miningOrderMenu.SelectedMiners.Add(miner);
            }
        }

        if (_miningOrderMenu.SelectedMiners.Count == 0)
        {
            foreach (var miner in activeMiners)
            {
                _miningOrderMenu.SelectedMiners.Add(miner);
            }
        }
    }

    private readonly record struct MiningOrderMinerRow(Trilobite Miner, Rectangle Bounds);

    private readonly record struct MiningOrderMenuLayout(
        Rectangle PanelBounds,
        Rectangle HeaderBounds,
        Rectangle SubtitleBounds,
        Rectangle ListViewportBounds,
        IReadOnlyList<MiningOrderMinerRow> Rows,
        float MaxScroll,
        Rectangle? ScrollbarTrackBounds,
        Rectangle? ScrollbarThumbBounds,
        Rectangle SendButtonBounds);
}
