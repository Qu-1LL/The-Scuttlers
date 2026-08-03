using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Simulation;

public sealed class MineOrderPlannerTests
{
    [Fact]
    public void BuildPlans_SingleMinerOrdersTargetsFromClosestToFurthest()
    {
        var (_, cave, _, post, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(1);
        cave.RevealCave();
        var miner = miners[0];
        var targets = cave.GetTiles()
            .Where(tile => cave.IsTileRevealed(tile) && Building.IsMineableType(tile.Base) && MineOrderPlanner.GetNavigationTarget(cave, tile) is not null)
            .OrderByDescending(tile => GridPoint.SquaredDistance(miner.Location, tile.Coordinates))
            .Take(3)
            .Select(tile => tile.Key)
            .ToArray();

        var plans = MineOrderPlanner.BuildPlans(cave, [miner], targets);

        Assert.True(plans.ContainsKey(miner));
        var assigned = plans[miner];
        Assert.Equal(3, assigned.Count);

        var assignedDistances = assigned
            .Select(key => GridPoint.SquaredDistance(miner.Location, cave.GetTile(key)!.Coordinates))
            .ToArray();
        Assert.True(assignedDistances.SequenceEqual(assignedDistances.OrderBy(distance => distance)));
    }

    [Fact]
    public void BuildPlans_MultipleMinersDistributesTargetsAcrossMiners()
    {
        var (_, cave, _, _, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(3);
        cave.RevealCave();
        var targets = cave.GetTiles()
            .Where(tile => cave.IsTileRevealed(tile) && Building.IsMineableType(tile.Base) && MineOrderPlanner.GetNavigationTarget(cave, tile) is not null)
            .Take(6)
            .Select(tile => tile.Key)
            .ToArray();

        var plans = MineOrderPlanner.BuildPlans(cave, miners, targets);

        Assert.True(plans.Count >= 2);
        var assignedKeys = plans.Values.SelectMany(keys => keys).ToArray();
        Assert.Equal(assignedKeys.Length, assignedKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(targets.Length, assignedKeys.Length);
    }

    [Fact]
    public void ResolveTargets_UnmineableSelectionFallsBackToNearestMineableTile()
    {
        var (_, cave, _, _, _) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(1);
        cave.RevealCave();
        var emptyTile = cave.GetReachableTiles()
            .First(tile => string.Equals(tile.Base, "empty", StringComparison.Ordinal));

        var resolved = MineOrderPlanner.ResolveTargets(cave, [emptyTile.Key]);

        Assert.Single(resolved);
        Assert.True(Building.IsMineableType(resolved[0].Base));
    }

    [Fact]
    public void BuildPlans_UndiscoveredSelectionsKeepRequestedTileKeysWhileResolvingToVisibleFrontier()
    {
        var (_, cave, _, _, miners) = TestWorldFactory.CreateSessionWithMiningPostAndMiners(1);
        cave.RevealCave();
        var hiddenTile = cave.GetTiles()
            .First(tile => !cave.IsTileRevealed(tile) && MineOrderPlanner.ResolveTarget(cave, tile) is not null);

        var resolvedTarget = MineOrderPlanner.ResolveTarget(cave, hiddenTile);
        Assert.NotNull(resolvedTarget);
        Assert.True(cave.IsTileRevealed(resolvedTarget!));
        Assert.True(Building.IsMineableType(resolvedTarget.Base));

        var plans = MineOrderPlanner.BuildPlans(cave, miners, [hiddenTile.Key]);

        Assert.True(plans.ContainsKey(miners[0]));
        Assert.Single(plans[miners[0]]);
        Assert.Equal(hiddenTile.Key, plans[miners[0]][0]);
    }

    [Fact]
    public void GetNavigationTarget_UsesNeighborForWalkableOreTiles()
    {
        var (_, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(6, 0));
        var oreLocation = new GridPoint(5, 5);
        var ore = cave.GetTile(oreLocation)!;
        ore.SetBase(OreType.LUMENITE.Name);
        ore.CreatureCanFit = true;
        ore.ConfigureOre(1, 1);

        var target = MineOrderPlanner.GetNavigationTarget(cave, ore);

        Assert.NotNull(target);
        Assert.NotEqual(oreLocation, target.Value);
        Assert.Contains(ore.Neighbors, neighbor => neighbor.Coordinates == target.Value);
    }
}
