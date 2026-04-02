using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class RoleSelectionStateTests
{
    [Fact]
    public void GetUniformAssignment_ReturnsNullForMixedAssignments()
    {
        var session = new GameSession();
        var miner = new Trilobite("Miner", new GridPoint(0, 0), session)
        {
            Assignment = "miner"
        };
        var builder = new Trilobite("Builder", new GridPoint(1, 0), session)
        {
            Assignment = "builder"
        };

        var uniformAssignment = RoleSelectionState.GetUniformAssignment([miner, builder]);

        Assert.Null(uniformAssignment);
    }

    [Fact]
    public void GetUniformAssignment_ReturnsSharedAssignmentWhenAllMatch()
    {
        var session = new GameSession();
        var first = new Trilobite("First", new GridPoint(0, 0), session)
        {
            Assignment = "fighter"
        };
        var second = new Trilobite("Second", new GridPoint(1, 0), session)
        {
            Assignment = "fighter"
        };

        var uniformAssignment = RoleSelectionState.GetUniformAssignment([first, second]);

        Assert.Equal("fighter", uniformAssignment);
    }
}
