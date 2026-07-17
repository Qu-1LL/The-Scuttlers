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
        var revealMap = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            value => revealMap = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[2].Center.X, rows[2].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft, revealMap);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.True(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.False(revealMap);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesNoCostBuildInsideSecondToggle()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var revealMap = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            value => revealMap = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[1].Center.X, rows[1].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft, revealMap);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.True(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.False(revealMap);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesInfiniteDraftInsideFourthToggle()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var revealMap = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            value => revealMap = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[3].Center.X, rows[3].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft, revealMap);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.True(infiniteDraft);
        Assert.False(revealMap);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_TogglesRevealMapInsideSecondRow()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var revealMap = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            value => revealMap = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualBottomRowBounds, 3, layout.ButtonGap);
        var clickPoint = new Point(rows[0].Center.X, rows[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, true, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft, revealMap);

        Assert.True(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.True(revealMap);
        Assert.Equal(1, playCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void HandleClick_TogglesContinuousWorldDebugControls(int bottomRowIndex)
    {
        var showHitboxes = false;
        var showZones = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            static _ => { },
            static _ => { },
            static _ => { },
            static _ => { },
            static _ => { },
            value => showHitboxes = value,
            value => showZones = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualBottomRowBounds, 3, layout.ButtonGap);
        var clickPoint = rows[bottomRowIndex].Center;

        var handled = controls.HandleClick(
            viewport,
            clickPoint,
            debugMenuOpen: true,
            showRoleLabels: false,
            disableEnemySpawns: false,
            noCostBuildPlacement: false,
            infiniteDraft: false,
            revealMap: false,
            showHitboxes,
            showZones);

        Assert.True(handled);
        Assert.Equal(bottomRowIndex == 1, showHitboxes);
        Assert.Equal(bottomRowIndex == 2, showZones);
        Assert.Equal(1, playCount);
    }

    [Fact]
    public void HandleClick_WhenMenuClosed_DoesNothing()
    {
        var showRoleLabels = false;
        var disableEnemySpawns = false;
        var noCostBuildPlacement = false;
        var infiniteDraft = false;
        var revealMap = false;
        var playCount = 0;
        var controls = new DebugToggleControls(
            value => showRoleLabels = value,
            value => disableEnemySpawns = value,
            value => noCostBuildPlacement = value,
            value => infiniteDraft = value,
            value => revealMap = value,
            () => playCount++);
        var viewport = new Point(1440, 900);
        var layout = DebugMenuLayout.Build(viewport);
        var rows = DebugMenuLayout.SplitRow(layout.VisualTopRowBounds, 4, layout.ButtonGap);
        var clickPoint = new Point(rows[0].Center.X, rows[0].Center.Y);

        var handled = controls.HandleClick(viewport, clickPoint, false, showRoleLabels, disableEnemySpawns, noCostBuildPlacement, infiniteDraft, revealMap);

        Assert.False(handled);
        Assert.False(showRoleLabels);
        Assert.False(disableEnemySpawns);
        Assert.False(noCostBuildPlacement);
        Assert.False(infiniteDraft);
        Assert.False(revealMap);
        Assert.Equal(0, playCount);
    }
}
