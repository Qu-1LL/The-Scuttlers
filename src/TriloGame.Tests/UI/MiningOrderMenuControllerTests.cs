using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class MiningOrderMenuControllerTests
{
    [Fact]
    public void Open_SelectsAllMiners()
    {
        var session = new GameSession();
        var first = new Trilobite("Miner A", GridPoint.Zero, session);
        var second = new Trilobite("Miner B", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();

        controller.Open(Vector2.One, [first, second]);

        Assert.True(controller.IsOpen);
        Assert.Equal(2, controller.Miners.Count);
        Assert.Contains(first, controller.SelectedMiners);
        Assert.Contains(second, controller.SelectedMiners);
    }

    [Fact]
    public void HandleClick_RowSelectsSingleMinerWithoutAppend()
    {
        var session = new GameSession();
        var first = new Trilobite("Miner A", GridPoint.Zero, session);
        var second = new Trilobite("Miner B", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();
        controller.Open(Vector2.Zero, [first, second]);

        var result = controller.HandleClick(
            new Point(10, 10),
            [new MiningOrderMenuRow(second, new Rectangle(0, 0, 40, 24))],
            new Rectangle(0, 0, 100, 100),
            new Rectangle(0, 60, 100, 30),
            appendSelection: false);

        Assert.Equal(MiningOrderMenuOutcome.SelectionChanged, result.Outcome);
        Assert.True(result.PlaySelectSound);
        Assert.DoesNotContain(first, controller.SelectedMiners);
        Assert.Contains(second, controller.SelectedMiners);
    }

    [Fact]
    public void HandleClick_RowTogglesMinerWithAppend()
    {
        var session = new GameSession();
        var first = new Trilobite("Miner A", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();
        controller.Open(Vector2.Zero, [first]);

        var result = controller.HandleClick(
            new Point(10, 10),
            [new MiningOrderMenuRow(first, new Rectangle(0, 0, 40, 24))],
            new Rectangle(0, 0, 100, 100),
            new Rectangle(0, 60, 100, 30),
            appendSelection: true);

        Assert.Equal(MiningOrderMenuOutcome.SelectionChanged, result.Outcome);
        Assert.Empty(controller.SelectedMiners);
    }

    [Fact]
    public void HandleClick_SendButtonRequestsSend()
    {
        var session = new GameSession();
        var miner = new Trilobite("Miner A", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();
        controller.Open(Vector2.Zero, [miner]);

        var result = controller.HandleClick(
            new Point(10, 70),
            [],
            new Rectangle(0, 0, 100, 100),
            new Rectangle(0, 60, 100, 30),
            appendSelection: false);

        Assert.Equal(MiningOrderMenuOutcome.SendRequested, result.Outcome);
        Assert.True(result.PlaySelectSound);
    }

    [Fact]
    public void SyncMiners_PreservesSelectionByName()
    {
        var session = new GameSession();
        var original = new Trilobite("Miner A", GridPoint.Zero, session);
        var replacement = new Trilobite("Miner A", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();
        controller.Open(Vector2.Zero, [original]);

        var changed = controller.SyncMiners([replacement]);

        Assert.True(changed);
        Assert.DoesNotContain(original, controller.SelectedMiners);
        Assert.Contains(replacement, controller.SelectedMiners);
    }

    [Fact]
    public void HandleWheel_ConsumesPanelButOnlyScrollsListViewport()
    {
        var session = new GameSession();
        var miner = new Trilobite("Miner A", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();
        controller.Open(Vector2.Zero, [miner]);

        var panelResult = controller.HandleWheel(
            new Point(90, 90),
            new Rectangle(0, 0, 100, 100),
            new Rectangle(0, 0, 50, 50),
            maxScroll: 200f,
            wheelDelta: 90);
        var listResult = controller.HandleWheel(
            new Point(10, 10),
            new Rectangle(0, 0, 100, 100),
            new Rectangle(0, 0, 50, 50),
            maxScroll: 200f,
            wheelDelta: 90);

        Assert.Equal(MiningOrderMenuOutcome.Consumed, panelResult.Outcome);
        Assert.Equal(MiningOrderMenuOutcome.Consumed, listResult.Outcome);
        Assert.Equal(90f, controller.Scroll);
    }

    [Fact]
    public void Layout_BuildsVisibleRowsAndClampsToGameplayBounds()
    {
        var session = new GameSession();
        var first = new Trilobite("Miner A", GridPoint.Zero, session);
        var second = new Trilobite("Miner B", GridPoint.Zero, session);
        var controller = new MiningOrderMenuController();
        controller.Open(new Vector2(1400f, 880f), [first, second]);

        var layout = MiningOrderMenuLayout.Build(controller, new Point(1440, 900), openPanelWidth: 520f);

        Assert.True(layout.PanelBounds.Right <= SelectionFocusLayout.GetGameplayBounds(new Point(1440, 900), 520f).Right);
        Assert.True(layout.PanelBounds.Bottom <= SelectionFocusLayout.GetGameplayBounds(new Point(1440, 900), 520f).Bottom);
        Assert.Equal(2, layout.Rows.Count);
        Assert.Equal(first, layout.Rows[0].Miner);
        Assert.Equal(second, layout.Rows[1].Miner);
    }

    [Fact]
    public void Layout_OnlyIncludesRowsInsideViewport()
    {
        var session = new GameSession();
        var miners = new List<Trilobite>();
        for (var index = 0; index < 20; index++)
        {
            miners.Add(new Trilobite($"Miner {index:00}", GridPoint.Zero, session));
        }

        var controller = new MiningOrderMenuController();
        controller.Open(Vector2.Zero, miners);
        controller.HandleWheel(
            new Point(20, 90),
            new Rectangle(0, 0, 300, 380),
            new Rectangle(16, 78, 268, 240),
            maxScroll: 600f,
            wheelDelta: 360);

        var layout = MiningOrderMenuLayout.Build(controller, new Point(1440, 900), openPanelWidth: 0f);

        Assert.True(layout.Rows.Count < miners.Count);
        Assert.All(layout.Rows, row => Assert.True(row.Bounds.Bottom >= layout.ListViewportBounds.Top && row.Bounds.Top <= layout.ListViewportBounds.Bottom));
        Assert.NotNull(layout.ScrollbarTrackBounds);
        Assert.NotNull(layout.ScrollbarThumbBounds);
    }
}
