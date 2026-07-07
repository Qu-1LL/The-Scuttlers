using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Simulation;

public static class TickRunner
{
    [ThreadStatic]
    private static List<Trilobite>? _trilobiteBuffer;

    [ThreadStatic]
    private static List<Enemy>? _enemyBuffer;

    [ThreadStatic]
    private static List<Building>? _buildingBuffer;

    public static void RunTick(GameSession session, ITickPhaseObserver? phaseObserver = null)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        session.TickCount++;
        phaseObserver?.OnTickStarted(session);

        phaseObserver?.OnPhaseStarted(TickPhase.TraitTick);
        session.TraitHandler.Tick();
        phaseObserver?.OnPhaseCompleted(TickPhase.TraitTick);

        phaseObserver?.OnPhaseStarted(TickPhase.SurfaceFeatureTick);
        cave.TickSurfaceFeatures();
        phaseObserver?.OnPhaseCompleted(TickPhase.SurfaceFeatureTick);

        phaseObserver?.OnPhaseStarted(TickPhase.DangerRefresh);
        cave.RefreshDangerState();
        phaseObserver?.OnPhaseCompleted(TickPhase.DangerRefresh);

        if (session.Danger)
        {
            phaseObserver?.OnPhaseStarted(TickPhase.EnemyBfs);
            cave.RefreshBfsField("enemy");
            phaseObserver?.OnPhaseCompleted(TickPhase.EnemyBfs);
        }

        phaseObserver?.OnPhaseStarted(TickPhase.TrilobiteMove);
        var trilobiteBuffer = GetTrilobiteBuffer();
        CopySnapshot(trilobiteBuffer, cave.GetTrilobiteList());
        foreach (var creature in trilobiteBuffer)
        {
            var assignment = creature.Assignment;
            phaseObserver?.OnTrilobiteMoveStarted(assignment);
            creature.Move();
            phaseObserver?.OnTrilobiteMoveCompleted(assignment);
        }
        phaseObserver?.OnPhaseCompleted(TickPhase.TrilobiteMove);

        if (session.Danger)
        {
            phaseObserver?.OnPhaseStarted(TickPhase.ColonyBfs);
            cave.RefreshBfsField("colony");
            phaseObserver?.OnPhaseCompleted(TickPhase.ColonyBfs);

            phaseObserver?.OnPhaseStarted(TickPhase.EnemyMove);
            var enemyBuffer = GetEnemyBuffer();
            CopySnapshot(enemyBuffer, cave.GetEnemyList());
            foreach (var creature in enemyBuffer)
            {
                creature.Move();
            }
            phaseObserver?.OnPhaseCompleted(TickPhase.EnemyMove);
        }

        phaseObserver?.OnPhaseStarted(TickPhase.BuildingTick);
        var buildingBuffer = GetBuildingBuffer();
        CopySnapshot(buildingBuffer, cave.GetBuildingList());
        foreach (var building in buildingBuffer)
        {
            building.Tick(cave);
        }
        cave.TickRanches();
        cave.TickVehicles();
        phaseObserver?.OnPhaseCompleted(TickPhase.BuildingTick);
        phaseObserver?.OnTickCompleted(session);
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

    private static List<Trilobite> GetTrilobiteBuffer()
    {
        return _trilobiteBuffer ??= [];
    }

    private static List<Enemy> GetEnemyBuffer()
    {
        return _enemyBuffer ??= [];
    }

    private static List<Building> GetBuildingBuffer()
    {
        return _buildingBuffer ??= [];
    }
}
