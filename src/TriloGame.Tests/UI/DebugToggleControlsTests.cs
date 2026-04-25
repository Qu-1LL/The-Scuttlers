using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class DebugToggleControlsTests
{
    [Fact]
    public void HandleClick_TogglesDisableEnemySpawnsInsideThirdToggle()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[2].Center.X, rows[2].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.True(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesNoCostBuildInsideSecondToggle()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[1].Center.X, rows[1].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.True(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesInfiniteDraftInsideFourthToggle()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[3].Center.X, rows[3].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.True(infiniteDraft);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_WhenMenuClosed_DoesNothing()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[0].Center.X, rows[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, false, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft);

        Assert.False(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.Equal(0, playCount);
    }
}
