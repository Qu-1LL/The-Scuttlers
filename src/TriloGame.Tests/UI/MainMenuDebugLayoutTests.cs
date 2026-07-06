using Microsoft.Xna.Framework;
using TriloGame.Game.Core.World;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class MainMenuDebugLayoutTests
{
    [Fact]
    public void Build_StacksCardsDropdownAndFooterWithoutOverlap()
    {
        var layout = MainMenuDebugLayout.Build(new Point(1440, 900), optionCount: WorldGenerationMethods.SelectablePatterns.Count, dropdownExpanded: true);

        Assert.True(layout.HeaderBounds.Bottom <= layout.SummaryBounds.Top);
        Assert.True(layout.SummaryBounds.Bottom <= layout.WorldGenerationLabelBounds.Top);
        Assert.True(layout.WorldGenerationLabelBounds.Bottom <= layout.DropdownBounds.Top);
        Assert.True(layout.DropdownOptionsBounds.HasValue);
        Assert.True(layout.DropdownBounds.Bottom <= layout.DropdownOptionsBounds.Value.Top);
        Assert.True(layout.DropdownOptionsBounds.Value.Bottom <= layout.FooterBounds.Top);
    }

    [Fact]
    public void StackRows_FillsAvailableHeightWithoutLeavingBounds()
    {
        var rows = MainMenuDebugLayout.StackRows(new Rectangle(20, 30, 300, 90), rowCount: 3, gap: 6);

        Assert.Equal(3, rows.Count);
        Assert.Equal(30, rows[0].Top);
        Assert.Equal(120, rows[^1].Bottom);
        Assert.All(rows, row =>
        {
            Assert.Equal(20, row.Left);
            Assert.Equal(320, row.Right);
        });
    }
}
