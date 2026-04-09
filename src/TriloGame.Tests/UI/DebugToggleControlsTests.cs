using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class DebugToggleControlsTests
{
    [Fact]
    public void HandleClick_TogglesDisableEnemySpawnsInsideSecondToggle_WhenOpalIsDisabled()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
        var clickPoint = new Point(rows[2].Center.X, rows[2].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, disableEnemySpawns, noCostBuildPlacement);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.True(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesNoCostBuildInsideSecondToggle_WhenOpalIsDisabled()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
        var clickPoint = new Point(rows[1].Center.X, rows[1].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, disableEnemySpawns, noCostBuildPlacement);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(disableEnemySpawns);
        Assert.True(noCostBuildPlacement);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_WhenMenuClosed_DoesNothing()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
        var clickPoint = new Point(rows[0].Center.X, rows[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, false, showRoleLabels, freezeOpal, disableEnemySpawns, noCostBuildPlacement);

        Assert.False(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.Equal(0, playCount);
    }
}
