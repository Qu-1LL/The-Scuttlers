using System.Diagnostics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Simulation;

public static class TickRunner
{
    private static readonly List<Trilobite> TrilobiteBuffer = [];
    private static readonly List<Enemy> EnemyBuffer = [];
    private static readonly List<Building> BuildingBuffer = [];

    public static void RunTick(GameSession session, bool captureRoleTimings = false)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        session.TickCount++;

        var tickStart = Stopwatch.GetTimestamp();
        var phaseStart = tickStart;
        var allocatedStart = GC.GetTotalAllocatedBytes(false);
        var gen0Start = GC.CollectionCount(0);
        var gen1Start = GC.CollectionCount(1);
        var gen2Start = GC.CollectionCount(2);
        var enemyBfsMs = 0d;
        var trilobiteMoveMs = 0d;
        var colonyBfsMs = 0d;
        var enemyMoveMs = 0d;
        var buildingTickMs = 0d;
        var minerRoleTotalMs = 0d;
        var builderRoleTotalMs = 0d;
        var farmerRoleTotalMs = 0d;
        var fighterRoleTotalMs = 0d;
        var minerRoleCount = 0;
        var builderRoleCount = 0;
        var farmerRoleCount = 0;
        var fighterRoleCount = 0;

        if (session.Danger)
        {
            cave.RefreshBfsField("enemy");
            enemyBfsMs = ConsumeElapsedMs(ref phaseStart);
        }

        CopySnapshot(TrilobiteBuffer, cave.GetTrilobiteList());
        if (captureRoleTimings)
        {
            foreach (var creature in TrilobiteBuffer)
            {
                var assignment = creature.Assignment;
                var creatureStart = Stopwatch.GetTimestamp();
                creature.Move();
                var creatureElapsedMs = Stopwatch.GetElapsedTime(creatureStart).TotalMilliseconds;
                trilobiteMoveMs += creatureElapsedMs;
                TrackRoleTiming(
                    assignment,
                    creatureElapsedMs,
                    ref minerRoleTotalMs,
                    ref minerRoleCount,
                    ref builderRoleTotalMs,
                    ref builderRoleCount,
                    ref farmerRoleTotalMs,
                    ref farmerRoleCount,
                    ref fighterRoleTotalMs,
                    ref fighterRoleCount);
            }

            phaseStart = Stopwatch.GetTimestamp();
        }
        else
        {
            foreach (var creature in TrilobiteBuffer)
            {
                creature.Move();
            }

            trilobiteMoveMs = ConsumeElapsedMs(ref phaseStart);
        }

        if (session.Danger)
        {
            cave.RefreshBfsField("colony");
            colonyBfsMs = ConsumeElapsedMs(ref phaseStart);

            CopySnapshot(EnemyBuffer, cave.GetEnemyList());
            foreach (var creature in EnemyBuffer)
            {
                creature.Move();
            }
            enemyMoveMs = ConsumeElapsedMs(ref phaseStart);
        }

        CopySnapshot(BuildingBuffer, cave.GetBuildingList());
        foreach (var building in BuildingBuffer)
        {
            building.Tick(cave);
        }
        buildingTickMs = ConsumeElapsedMs(ref phaseStart);

        var displayedTotalMs = captureRoleTimings
            ? enemyBfsMs + trilobiteMoveMs + colonyBfsMs + enemyMoveMs + buildingTickMs
            : Stopwatch.GetElapsedTime(tickStart).TotalMilliseconds;

        session.TickProfiler.Record(new TickTimingSnapshot(
            displayedTotalMs,
            enemyBfsMs,
            trilobiteMoveMs,
            colonyBfsMs,
            enemyMoveMs,
            buildingTickMs,
            new RoleTimingSnapshot(minerRoleTotalMs, minerRoleCount),
            new RoleTimingSnapshot(builderRoleTotalMs, builderRoleCount),
            new RoleTimingSnapshot(farmerRoleTotalMs, farmerRoleCount),
            new RoleTimingSnapshot(fighterRoleTotalMs, fighterRoleCount),
            captureRoleTimings,
            GC.GetTotalAllocatedBytes(false) - allocatedStart,
            GC.CollectionCount(0) - gen0Start,
            GC.CollectionCount(1) - gen1Start,
            GC.CollectionCount(2) - gen2Start,
            cave.GetTrilobiteList().Count,
            cave.GetEnemyList().Count,
            cave.GetBuildingList().Count));
    }

    private static double ConsumeElapsedMs(ref long phaseStart)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(phaseStart, now).TotalMilliseconds;
        phaseStart = now;
        return elapsed;
    }

    private static void CopySnapshot<T>(List<T> buffer, IReadOnlyList<T> source)
    {
        buffer.Clear();
        if (buffer.Capacity < source.Count)
        {
            buffer.Capacity = source.Count;
        }

        for (var index = 0; index < source.Count; index++)
        {
            buffer.Add(source[index]);
        }
    }

    private static void TrackRoleTiming(
        string assignment,
        double elapsedMs,
        ref double minerRoleTotalMs,
        ref int minerRoleCount,
        ref double builderRoleTotalMs,
        ref int builderRoleCount,
        ref double farmerRoleTotalMs,
        ref int farmerRoleCount,
        ref double fighterRoleTotalMs,
        ref int fighterRoleCount)
    {
        switch (assignment)
        {
            case "miner":
                minerRoleTotalMs += elapsedMs;
                minerRoleCount++;
                break;
            case "builder":
                builderRoleTotalMs += elapsedMs;
                builderRoleCount++;
                break;
            case "farmer":
                farmerRoleTotalMs += elapsedMs;
                farmerRoleCount++;
                break;
            case "fighter":
                fighterRoleTotalMs += elapsedMs;
                fighterRoleCount++;
                break;
        }
    }
}
