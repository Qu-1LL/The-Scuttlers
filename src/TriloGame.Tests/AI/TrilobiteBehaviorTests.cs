using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class TrilobiteBehaviorTests
{
    [Fact]
    public void RestartBehavior_ForUnassignedTrilobiteLeavesItIdle()
    {
        var (_, _, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var startingLocation = trilobite.Location;

        var restarted = trilobite.RestartBehavior();
        var moveResult = trilobite.Move();

        Assert.True(restarted);
        Assert.Null(moveResult);
        Assert.Equal(startingLocation, trilobite.Location);
    }
}
