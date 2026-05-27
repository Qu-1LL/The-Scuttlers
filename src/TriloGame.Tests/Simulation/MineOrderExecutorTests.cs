using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Simulation;

public sealed class MineOrderExecutorTests
{
    [Fact]
    public void Dispatch_AppliesPlannedManualOrdersToSelectedMiners()
    {
        var (_, cave, _, _, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(2);
        cave.RevealCave();
        var targets = cave.GetTiles()
            .Where(tile => cave.IsTileRevealed(tile) && Building.IsMineableType(tile.Base) && MineOrderPlanner.GetNavigationTarget(cave, tile) is not null)
            .Take(4)
            .Select(tile => tile.Key)
            .ToArray();

        var result = MineOrderExecutor.Dispatch(cave, miners, targets);

        Assert.Equal(2, result.ActiveMinerCount);
        Assert.True(result.AssignedMinerCount > 0);
        Assert.Equal(0, result.ClearedMinerCount);
        Assert.Equal(targets.Length, miners.Sum(miner => miner.GetManualMineOrders().Count));
    }

    [Fact]
    public void Dispatch_ClearsExistingOrders_WhenNoTargetsCanBePlanned()
    {
        var (_, cave, _, _, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(1);
        var miner = miners[0];
        miner.SetManualMineOrders(["stale"]);

        var result = MineOrderExecutor.Dispatch(cave, [miner], []);

        Assert.Equal(1, result.ActiveMinerCount);
        Assert.Equal(0, result.AssignedMinerCount);
        Assert.Equal(1, result.ClearedMinerCount);
        Assert.Empty(miner.GetManualMineOrders());
    }

    [Fact]
    public void Dispatch_IgnoresDuplicateAndNonMinerSelections()
    {
        var (_, cave, _, _, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(2);
        cave.RevealCave();
        var activeMiner = miners[0];
        var builder = miners[1];
        Assert.True(builder.ChangeAssignment("builder"));
        var target = cave.GetTiles()
            .First(tile => cave.IsTileRevealed(tile) && Building.IsMineableType(tile.Base) && MineOrderPlanner.GetNavigationTarget(cave, tile) is not null)
            .Key;

        var result = MineOrderExecutor.Dispatch(cave, [activeMiner, activeMiner, builder], [target]);

        Assert.Equal(1, result.ActiveMinerCount);
        Assert.Equal(1, result.AssignedMinerCount);
        Assert.Single(activeMiner.GetManualMineOrders());
        Assert.Empty(builder.GetManualMineOrders());
    }
}
