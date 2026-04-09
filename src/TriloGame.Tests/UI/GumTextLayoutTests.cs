using TriloGame.Game.UI.Gum;

namespace TriloGame.Tests.UI;

public sealed class GumTextLayoutTests
{
    [Fact]
    public void Wrap_CompactStyle_KeepsDisableEnemySpawnsVisibleWithinTwoLines()
    {
        var lines = GumTextLayout.Wrap(["Disable Enemy Spawns"], 120, 2, GumTextStyle.Compact);

        Assert.NotEmpty(lines);
        Assert.True(lines.Count <= 2);
        Assert.Equal("Disable Enemy Spawns", string.Join(' ', lines).Replace("  ", " ").Trim());
    }
}
