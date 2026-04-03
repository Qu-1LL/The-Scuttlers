using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class SelectionRetentionTests
{
    [Fact]
    public void ShouldPreserveCurrentSelection_ReturnsTrue_WhenClickedTrilobiteIsAlreadySelected()
    {
        var session = new GameSession();
        var first = new Trilobite("First", GridPoint.Zero, session);
        var second = new Trilobite("Second", new GridPoint(1, 0), session);

        var result = SelectionRetention.ShouldPreserveCurrentSelection([first, second], second);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPreserveCurrentSelection_ReturnsFalse_WhenClickedTrilobiteIsNotSelected()
    {
        var session = new GameSession();
        var first = new Trilobite("First", GridPoint.Zero, session);
        var second = new Trilobite("Second", new GridPoint(1, 0), session);
        var third = new Trilobite("Third", new GridPoint(2, 0), session);

        var result = SelectionRetention.ShouldPreserveCurrentSelection([first, second], third);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPreserveCurrentSelection_UsesProvidedComparer_ForTileKeys()
    {
        var result = SelectionRetention.ShouldPreserveCurrentSelection(
            ["1,1", "2,2", "3,3"],
            "2,2",
            StringComparer.Ordinal);

        Assert.True(result);
    }
}
