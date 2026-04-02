using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class DebugMenuLayoutTests
{
    [Fact]
    public void Build_StacksTextCardsAndButtonRowsWithoutOverlap()
    {
        var layout = DebugMenuLayout.Build(new Point(1440, 900));

        Assert.True(layout.HeaderBounds.Bottom <= layout.SummaryBounds.Top);
        Assert.True(layout.SummaryBounds.Bottom <= layout.PerformanceBounds.Top);
        Assert.True(layout.PerformanceBounds.Bottom <= layout.QuickControlsLabelBounds.Top);
        Assert.True(layout.QuickControlsLabelBounds.Bottom <= layout.QuickControlsRowBounds.Top);
        Assert.True(layout.QuickControlsRowBounds.Bottom <= layout.SpeedLabelBounds.Top);
        Assert.True(layout.SpeedLabelBounds.Bottom <= layout.SpeedRowBounds.Top);
        Assert.True(layout.SpeedRowBounds.Bottom <= layout.BfsLabelBounds.Top);
        Assert.True(layout.BfsLabelBounds.Bottom <= layout.BfsTopRowBounds.Top);
        Assert.True(layout.BfsTopRowBounds.Bottom <= layout.BfsBottomRowBounds.Top);
        Assert.True(layout.BfsBottomRowBounds.Bottom <= layout.VisualLabelBounds.Top);
        Assert.True(layout.VisualLabelBounds.Bottom <= layout.VisualRowBounds.Top);
        Assert.True(layout.VisualRowBounds.Bottom <= layout.ActionsLabelBounds.Top);
        Assert.True(layout.ActionsLabelBounds.Bottom <= layout.ActionsRowBounds.Top);
        Assert.True(layout.ActionsRowBounds.Bottom <= layout.FooterBounds.Top);
    }

    [Fact]
    public void SplitRow_FillsAvailableWidthWithoutLeavingPanel()
    {
        var row = new Rectangle(30, 40, 500, 40);
        var buttons = DebugMenuLayout.SplitRow(row, 4, 10);

        Assert.Equal(4, buttons.Count);
        Assert.Equal(row.Left, buttons[0].Left);
        Assert.Equal(row.Right, buttons[^1].Right);
        Assert.All(buttons, button =>
        {
            Assert.Equal(row.Top, button.Top);
            Assert.Equal(row.Bottom, button.Bottom);
            Assert.True(button.Left >= row.Left);
            Assert.True(button.Right <= row.Right);
        });
    }
}
