using TriloGame.Game.Core.Buildings;

namespace TriloGame.Tests.Buildings;

public sealed class BuildingRotationTests
{
    [Fact]
    public void CompletedScaffolding_PreservesRotatedTargetDisplayState()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var scaffolding = new Scaffolding(session, new AlgaeFarm(session));

        scaffolding.RotateMap();
        var nextRotation = (scaffolding.GetDisplayRotationTurns() + 1) % 4;
        scaffolding.SetDisplayRotationTurns(nextRotation);
        scaffolding.TargetBuilding.SetDisplayRotationTurns(nextRotation);

        var buildLocation = TestWorldFactory.FindBuildLocation(cave, scaffolding, preserveReachability: true);
        Assert.True(cave.Build(scaffolding, buildLocation));

        Assert.Equal(20, scaffolding.Deposit("Sandstone", 20));
        Assert.Equal(scaffolding.ConstructionRequired, scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));

        var finishedFarm = Assert.Single(cave.Buildings.OfType<AlgaeFarm>());
        Assert.Equal(buildLocation, finishedFarm.Location);
        Assert.Equal(1, finishedFarm.GetDisplayRotationTurns());
        Assert.Equal(3, finishedFarm.Size.X);
        Assert.Equal(2, finishedFarm.Size.Y);
        Assert.DoesNotContain(scaffolding, cave.Buildings);
    }
}
