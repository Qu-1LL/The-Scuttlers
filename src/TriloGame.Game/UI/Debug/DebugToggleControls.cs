using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Debug;

public sealed class DebugToggleControls
{
    private readonly Action<bool> _setRoleLabels;
    private readonly Action<bool> _setFreezeOpal;
    private readonly Action<bool> _setDisableEnemySpawns;
    private readonly Action<bool> _setNoCostBuildPlacement;
    private readonly Action _playUiSelectSound;

    public DebugToggleControls(
        Action<bool> setRoleLabels,
        Action<bool> setFreezeOpal,
        Action<bool> setDisableEnemySpawns,
        Action<bool> setNoCostBuildPlacement,
        Action playUiSelectSound)
    {
        _setRoleLabels = setRoleLabels;
        _setFreezeOpal = setFreezeOpal;
        _setDisableEnemySpawns = setDisableEnemySpawns;
        _setNoCostBuildPlacement = setNoCostBuildPlacement;
        _playUiSelectSound = playUiSelectSound;
    }

    public bool HandleClick(
        Point viewport,
        Point point,
        bool debugMenuOpen,
        bool showRoleLabels,
        bool freezeOpalProgression,
        bool disableEnemySpawns,
        bool noCostBuildPlacement)
    {
        if (!debugMenuOpen)
        {
            return false;
        }

        var rows = GetToggleRows(viewport);
        if (rows[0].Contains(point))
        {
            _setRoleLabels(!showRoleLabels);
            _playUiSelectSound();
            return true;
        }

        if (GameConstants.EnableOpal && rows[1].Contains(point))
        {
            _setFreezeOpal(!freezeOpalProgression);
            _playUiSelectSound();
            return true;
        }

        var noCostBuildIndex = GameConstants.EnableOpal ? 2 : 1;
        if (rows[noCostBuildIndex].Contains(point))
        {
            _setNoCostBuildPlacement(!noCostBuildPlacement);
            _playUiSelectSound();
            return true;
        }

        var disableEnemyIndex = GameConstants.EnableOpal ? 3 : 2;
        if (rows[disableEnemyIndex].Contains(point))
        {
            _setDisableEnemySpawns(!disableEnemySpawns);
            _playUiSelectSound();
            return true;
        }

        return false;
    }

    public void Draw(
        GumUiRenderer gumUi,
        Point viewport,
        bool debugMenuOpen,
        bool showRoleLabels,
        bool freezeOpalProgression,
        bool disableEnemySpawns,
        bool noCostBuildPlacement,
        Point pointer)
    {
        if (!debugMenuOpen)
        {
            return;
        }

        var rows = GetToggleRows(viewport);
        DrawToggle(gumUi, rows[0], "Show Role Labels", showRoleLabels, rows[0].Contains(pointer));
        if (GameConstants.EnableOpal)
        {
            DrawToggle(gumUi, rows[1], "Freeze Opal", freezeOpalProgression, rows[1].Contains(pointer));
        }

        var noCostBuildIndex = GameConstants.EnableOpal ? 2 : 1;
        DrawToggle(gumUi, rows[noCostBuildIndex], "No Cost Build", noCostBuildPlacement, rows[noCostBuildIndex].Contains(pointer));

        var disableEnemyIndex = GameConstants.EnableOpal ? 3 : 2;
        DrawToggle(gumUi, rows[disableEnemyIndex], "Disable Enemy Spawns", disableEnemySpawns, rows[disableEnemyIndex].Contains(pointer));
    }

    private static IReadOnlyList<Rectangle> GetToggleRows(Point viewport)
    {
        var layout = DebugMenuLayout.Build(viewport);
        return DebugMenuLayout.SplitRow(layout.VisualRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
    }

    private static void DrawToggle(GumUiRenderer gumUi, Rectangle bounds, string text, bool isChecked, bool hovered)
    {
        var fill = hovered ? new Color(26, 41, 54) : new Color(20, 33, 45);
        var border = hovered ? new Color(120, 158, 176) : new Color(82, 112, 128);
        gumUi.AddFilledRectangle(bounds, fill);
        gumUi.AddRectangleOutline(bounds, border, 2);

        var checkBounds = new Rectangle(bounds.X + 10, bounds.Y + 8, 20, 20);
        var checkFill = isChecked ? new Color(38, 171, 190) : new Color(10, 19, 28);
        var checkBorder = isChecked ? new Color(162, 233, 241) : new Color(97, 126, 141);
        gumUi.AddFilledRectangle(checkBounds, checkFill);
        gumUi.AddRectangleOutline(checkBounds, checkBorder, 2);

        if (isChecked)
        {
            gumUi.AddFilledRectangle(new Rectangle(checkBounds.X + 5, checkBounds.Y + 5, 10, 10), new Color(231, 247, 252));
        }

        var textBounds = new Rectangle(checkBounds.Right + 8, bounds.Y + 3, Math.Max(0, bounds.Width - (checkBounds.Right - bounds.X) - 16), Math.Max(0, bounds.Height - 6));
        var metrics = GumTextLayout.GetMetrics(GumTextStyle.Compact);
        var wrappedText = GumTextLayout.Wrap([text], textBounds.Width, 2, GumTextStyle.Compact);
        gumUi.AddText(
            textBounds,
            string.Join('\n', wrappedText),
            hovered ? new Color(236, 246, 251) : new Color(216, 228, 235),
            HorizontalAlignment.Left,
            VerticalAlignment.Center,
            metrics.FontSize,
            maxLines: wrappedText.Count);
    }
}
