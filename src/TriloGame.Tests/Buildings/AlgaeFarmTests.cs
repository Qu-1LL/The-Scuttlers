using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class AlgaeFarmTests
{
    [Fact]
    public void FarmPathLoopsBackToStartingFarmTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var start = Assert.IsType<GridPoint>(farm.GetApproachTile(buildLocation));
        var path = farm.GetPath(start);

        Assert.NotEmpty(path);
        Assert.Equal(start, path[0]);
        Assert.Equal(start, path[^1]);
    }

    [Fact]
    public void NextHarvestStepStaysWithinFarmAndAdvancesCycle()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var start = Assert.IsType<GridPoint>(farm.GetApproachTile(buildLocation));
        var path = farm.GetPath(start);

        Assert.True(farm.TryGetNextHarvestStep(start, out var next));
        Assert.Contains(next, path);
        Assert.NotEqual(start, next);
        Assert.True(farm.IsLocationOnFarm(next));
    }

    [Fact]
    public void Assign_DefaultCapacityAllowsOnlyOneTrilobite()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var first = new TriloGame.Game.Core.Entities.Trilobite("Farmer 1", buildLocation, session);
        var second = new TriloGame.Game.Core.Entities.Trilobite("Farmer 2", buildLocation, session);

        Assert.True(farm.Assign(first));
        Assert.False(farm.Assign(second));
        Assert.Equal(1, farm.GetVolume());
        Assert.Equal(0, farm.GetAvailableAssignmentSlots());
    }

    [Fact]
    public void IncreaseAssignmentCapacity_AllowsAdditionalTrilobites()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, farm);
        Assert.True(cave.Build(farm, buildLocation));

        var first = new TriloGame.Game.Core.Entities.Trilobite("Farmer 1", buildLocation, session);
        var second = new TriloGame.Game.Core.Entities.Trilobite("Farmer 2", buildLocation, session);

        Assert.True(farm.Assign(first));
        farm.IncreaseAssignmentCapacity();

        Assert.True(farm.Assign(second));
        Assert.Equal(2, farm.GetVolume());
        Assert.Equal(0, farm.GetAvailableAssignmentSlots());
    }
}
