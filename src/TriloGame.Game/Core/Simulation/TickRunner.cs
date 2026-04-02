using System.Diagnostics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Simulation;

public static class TickRunner
{
    private static readonly List<Trilobite> TrilobiteBuffer = [];
    private static readonly List<Enemy> EnemyBuffer = [];
    private static readonly List<Building> BuildingBuffer = [];

    public static void RunTick(GameSession session)
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

        if (session.Danger)
        {
            cave.RefreshBfsField("enemy");
            enemyBfsMs = ConsumeElapsedMs(ref phaseStart);
        }

        CopySnapshot(TrilobiteBuffer, cave.GetTrilobiteList());
        foreach (var creature in TrilobiteBuffer)
        {
            creature.Move();
        }
        trilobiteMoveMs = ConsumeElapsedMs(ref phaseStart);

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

        session.TickProfiler.Record(new TickTimingSnapshot(
            Stopwatch.GetElapsedTime(tickStart).TotalMilliseconds,
            enemyBfsMs,
            trilobiteMoveMs,
            colonyBfsMs,
            enemyMoveMs,
            buildingTickMs,
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
}
