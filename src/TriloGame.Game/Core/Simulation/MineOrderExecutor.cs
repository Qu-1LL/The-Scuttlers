using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Core.Simulation;

public static class MineOrderExecutor
{
    public static MineOrderExecutionResult Dispatch(
        Cave cave,
        IReadOnlyList<Trilobite> selectedMiners,
        IReadOnlyList<string> selectedTileKeys)
    {
        var activeMiners = GetActiveMiners(cave, selectedMiners);
        if (activeMiners.Count == 0)
        {
            return new MineOrderExecutionResult(0, 0, 0);
        }

        var plans = MineOrderPlanner.BuildPlans(cave, activeMiners, selectedTileKeys);
        var assigned = 0;
        var cleared = 0;
        foreach (var miner in activeMiners)
        {
            if (plans.TryGetValue(miner, out var tileKeys))
            {
                miner.SetManualMineOrders(tileKeys);
                assigned++;
            }
            else
            {
                miner.ClearManualMineOrders(restartBehavior: true);
                cleared++;
            }
        }

        return new MineOrderExecutionResult(activeMiners.Count, assigned, cleared);
    }

    private static List<Trilobite> GetActiveMiners(Cave cave, IReadOnlyList<Trilobite> selectedMiners)
    {
        var activeMiners = new List<Trilobite>(selectedMiners.Count);
        for (var index = 0; index < selectedMiners.Count; index++)
        {
            var miner = selectedMiners[index];
            if (miner.Cave != cave || !string.Equals(miner.Assignment, "miner", StringComparison.Ordinal))
            {
                continue;
            }

            var alreadyAdded = false;
            for (var existingIndex = 0; existingIndex < activeMiners.Count; existingIndex++)
            {
                if (ReferenceEquals(activeMiners[existingIndex], miner))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                activeMiners.Add(miner);
            }
        }

        return activeMiners;
    }
}

public readonly record struct MineOrderExecutionResult(int ActiveMinerCount, int AssignedMinerCount, int ClearedMinerCount);
