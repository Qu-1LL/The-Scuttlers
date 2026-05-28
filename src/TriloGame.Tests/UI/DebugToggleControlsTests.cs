using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class DebugToggleControlsTests
{
    [Fact]
    public void HandleClick_TogglesDisableEnemySpawnsInsideDisableEnemyToggle()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var allowManualMining = false;
        var toggleMapVisibility = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => allowManualMining = value,
            value => toggleMapVisibility = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var topRow = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
        var disableEnemyIndex = GameConstants.EnableOpal ? 3 : 2;
        var clickPoint = new Point(topRow[disableEnemyIndex].Center.X, topRow[disableEnemyIndex].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, allowManualMining, toggleMapVisibility, disableEnemySpawns, noCostBuildPlacement);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(allowManualMining);
        Assert.False(toggleMapVisibility);
        Assert.True(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesNoCostBuildInsideNoCostBuildToggle()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var allowManualMining = false;
        var toggleMapVisibility = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => allowManualMining = value,
            value => toggleMapVisibility = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var topRow = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
        var noCostBuildIndex = GameConstants.EnableOpal ? 2 : 1;
        var clickPoint = new Point(topRow[noCostBuildIndex].Center.X, topRow[noCostBuildIndex].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, allowManualMining, toggleMapVisibility, disableEnemySpawns, noCostBuildPlacement);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(allowManualMining);
        Assert.False(toggleMapVisibility);
        Assert.False(disableEnemySpawns);
        Assert.True(noCostBuildPlacement);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesAllowManualMiningInsideManualMiningToggle()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var allowManualMining = false;
        var toggleMapVisibility = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => allowManualMining = value,
            value => toggleMapVisibility = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var bottomRow = DebugMenuLayout.SplitRow(layout.VisualBottomRowBounds, 2, layout.ButtonGap);
        var clickPoint = new Point(bottomRow[0].Center.X, bottomRow[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, allowManualMining, toggleMapVisibility, disableEnemySpawns, noCostBuildPlacement);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.True(allowManualMining);
        Assert.False(toggleMapVisibility);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesMapVisibilityInsideMapVisibilityToggle()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var allowManualMining = false;
        var toggleMapVisibility = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => allowManualMining = value,
            value => toggleMapVisibility = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var bottomRow = DebugMenuLayout.SplitRow(layout.VisualBottomRowBounds, 2, layout.ButtonGap);
        var clickPoint = new Point(bottomRow[1].Center.X, bottomRow[1].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, allowManualMining, toggleMapVisibility, disableEnemySpawns, noCostBuildPlacement);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(allowManualMining);
        Assert.True(toggleMapVisibility);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_WhenMenuClosed_DoesNothing()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var allowManualMining = false;
        var toggleMapVisibility = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => allowManualMining = value,
            value => toggleMapVisibility = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var topRow = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, GameConstants.EnableOpal ? 4 : 3, layout.ButtonGap);
        var clickPoint = new Point(topRow[0].Center.X, topRow[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, false, showRoleLabels, freezeOpal, allowManualMining, toggleMapVisibility, disableEnemySpawns, noCostBuildPlacement);

        Assert.False(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(allowManualMining);
        Assert.False(toggleMapVisibility);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.Equal(0, playCount);
    }
}
