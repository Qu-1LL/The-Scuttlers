using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Debug;

public sealed class DebugToggleControls
{
    private readonly Action<bool> _setRoleLabels;
    private readonly Action<bool> _setDisableEnemySpawns;
    private readonly Action<bool> _setNoCostBuildPlacement;
    private readonly Action<bool> _setInfiniteDraft;
    private readonly Action<bool> _setRevealMap;
    private readonly Action<bool> _setShowHitboxes;
    private readonly Action<bool> _setShowZones;
    private readonly Action _playUiSelectSound;

    public DebugToggleControls(
        Action<bool> setRoleLabels,
        Action<bool> setDisableEnemySpawns,
        Action<bool> setNoCostBuildPlacement,
        Action<bool> setInfiniteDraft,
        Action<bool> setRevealMap,
        Action playUiSelectSound)
        : this(
            setRoleLabels,
            setDisableEnemySpawns,
            setNoCostBuildPlacement,
            setInfiniteDraft,
            setRevealMap,
            static _ => { },
            static _ => { },
            playUiSelectSound)
    {
    }

    public DebugToggleControls(
        Action<bool> setRoleLabels,
        Action<bool> setDisableEnemySpawns,
        Action<bool> setNoCostBuildPlacement,
        Action<bool> setInfiniteDraft,
        Action<bool> setRevealMap,
        Action<bool> setShowHitboxes,
        Action<bool> setShowZones,
        Action playUiSelectSound)
    {
        _setRoleLabels = setRoleLabels;
        _setDisableEnemySpawns = setDisableEnemySpawns;
        _setNoCostBuildPlacement = setNoCostBuildPlacement;
        _setInfiniteDraft = setInfiniteDraft;
        _setRevealMap = setRevealMap;
        _setShowHitboxes = setShowHitboxes;
        _setShowZones = setShowZones;
        _playUiSelectSound = playUiSelectSound;
    }

    public bool HandleClick(
        Point viewport,
        Point point,
        bool debugMenuOpen,
        bool showRoleLabels,
        bool disableEnemySpawns,
        bool noCostBuildPlacement,
        bool infiniteDraft,
        bool revealMap,
        bool showHitboxes,
        bool showZones)
    {
        if (!debugMenuOpen)
        {
            return false;
        }

        var bounds = BuildToggleBounds(viewport);
        if (bounds.ShowRoleLabels.Contains(point))
        {
            _setRoleLabels(!showRoleLabels);
            _playUiSelectSound();
            return true;
        }

        if (bounds.NoCostBuild.Contains(point))
        {
            _setNoCostBuildPlacement(!noCostBuildPlacement);
            _playUiSelectSound();
            return true;
        }

        if (bounds.DisableEnemySpawns.Contains(point))
        {
            _setDisableEnemySpawns(!disableEnemySpawns);
            _playUiSelectSound();
            return true;
        }

        if (bounds.InfiniteDraft.Contains(point))
        {
            _setInfiniteDraft(!infiniteDraft);
            _playUiSelectSound();
            return true;
        }

        if (bounds.RevealMap.Contains(point))
        {
            _setRevealMap(!revealMap);
            _playUiSelectSound();
            return true;
        }

        if (bounds.ShowHitboxes.Contains(point))
        {
            _setShowHitboxes(!showHitboxes);
            _playUiSelectSound();
            return true;
        }

        if (bounds.ShowZones.Contains(point))
        {
            _setShowZones(!showZones);
            _playUiSelectSound();
            return true;
        }

        return false;
    }

    public bool HandleClick(
        Point viewport,
        Point point,
        bool debugMenuOpen,
        bool showRoleLabels,
        bool disableEnemySpawns,
        bool noCostBuildPlacement,
        bool infiniteDraft,
        bool revealMap)
    {
        return HandleClick(
            viewport,
            point,
            debugMenuOpen,
            showRoleLabels,
            disableEnemySpawns,
            noCostBuildPlacement,
            infiniteDraft,
            revealMap,
            showHitboxes: false,
            showZones: false);
    }

    public void Draw(
        GumUiRenderer gumUi,
        Point viewport,
        bool debugMenuOpen,
        bool showRoleLabels,
        bool disableEnemySpawns,
        bool noCostBuildPlacement,
        bool infiniteDraft,
        bool revealMap,
        bool showHitboxes,
        bool showZones,
        Point pointer)
    {
        if (!debugMenuOpen)
        {
            return;
        }

        var bounds = BuildToggleBounds(viewport);
        DrawToggle(gumUi, bounds.ShowRoleLabels, "Show Role Labels", showRoleLabels, bounds.ShowRoleLabels.Contains(pointer));
        DrawToggle(gumUi, bounds.NoCostBuild, "No Cost Build", noCostBuildPlacement, bounds.NoCostBuild.Contains(pointer));
        DrawToggle(gumUi, bounds.DisableEnemySpawns, "Disable Enemy Spawns", disableEnemySpawns, bounds.DisableEnemySpawns.Contains(pointer));
        DrawToggle(gumUi, bounds.InfiniteDraft, "Infinite Draft", infiniteDraft, bounds.InfiniteDraft.Contains(pointer));
        DrawToggle(gumUi, bounds.RevealMap, "Reveal Map", revealMap, bounds.RevealMap.Contains(pointer));
        DrawToggle(gumUi, bounds.ShowHitboxes, "Show Hitboxes", showHitboxes, bounds.ShowHitboxes.Contains(pointer));
        DrawToggle(gumUi, bounds.ShowZones, "Show Zones", showZones, bounds.ShowZones.Contains(pointer));
    }

    private static DebugToggleBounds BuildToggleBounds(Point viewport)
    {
        var layout = DebugMenuLayout.Build(viewport);
        var topRow = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, 4, layout.ButtonGap);
        var bottomRow = DebugMenuLayout.SplitRow(layout.VisualBottomRowBounds, 3, layout.ButtonGap);
        return new DebugToggleBounds(
            ShowRoleLabels: topRow[0],
            NoCostBuild: topRow[1],
            DisableEnemySpawns: topRow[2],
            InfiniteDraft: topRow[3],
            RevealMap: bottomRow[0],
            ShowHitboxes: bottomRow[1],
            ShowZones: bottomRow[2]);
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

    private readonly record struct DebugToggleBounds(
        Rectangle ShowRoleLabels,
        Rectangle NoCostBuild,
        Rectangle DisableEnemySpawns,
        Rectangle InfiniteDraft,
        Rectangle RevealMap,
        Rectangle ShowHitboxes,
        Rectangle ShowZones);
}
