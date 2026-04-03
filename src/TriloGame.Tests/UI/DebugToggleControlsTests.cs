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
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => disableEnemySpawns = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, GameConstants.EnableOpal ? 3 : 2, layout.ButtonGap);
        var clickPoint = new Point(rows[1].Center.X, rows[1].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, freezeOpal, disableEnemySpawns);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.True(disableEnemySpawns);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_WhenMenuClosed_DoesNothing()
    {
        var showRoleLabels = false;
        var freezeOpal = false;
        var disableEnemySpawns = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => freezeOpal = value,
            value => disableEnemySpawns = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualRowBounds, GameConstants.EnableOpal ? 3 : 2, layout.ButtonGap);
        var clickPoint = new Point(rows[0].Center.X, rows[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, false, showRoleLabels, freezeOpal, disableEnemySpawns);

        Assert.False(handled);
        Assert.False(showRoleLabels);
        Assert.False(freezeOpal);
        Assert.False(disableEnemySpawns);
        Assert.Equal(0, playCount);
    }
}
