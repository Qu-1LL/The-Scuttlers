using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Utilities;

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

        if (!session.Runtime.FreezeOpalProgression)
        {
            cave.TickSurfaceFeatures();
        }

        if (cave.AllowsNaturalEnemySpawns() &&
            cave.Enemies.Count < GameConstants.MaxAmbientAntCount &&
            RandomUtil.NextInt(cave.GetAntHoleSpawnChanceDenominator()) == 0)
        {
            cave.TrySpawnAntHole();
        }

        cave.RefreshDangerState();
        cave.RefreshVisibleEnemyThreatMap(GameConstants.WorkerEnemyFleeRadius);
        phaseObserver?.OnTickStarted(session);

        if (session.Danger)
        {
            cave.RefreshBfsField("enemy");
            phaseObserver?.OnPhaseCompleted(TickPhase.EnemyBfs);
        }

        var trilobiteBuffer = GetTrilobiteBuffer();
        CopySnapshot(trilobiteBuffer, cave.GetTrilobiteList());
        foreach (var creature in trilobiteBuffer)
        {
            creature.Move();
        }
        phaseObserver?.OnPhaseCompleted(TickPhase.TrilobiteMove);

        if (session.Danger)
        {
            cave.RefreshBfsField("colony");
            phaseObserver?.OnPhaseCompleted(TickPhase.ColonyBfs);

            var enemyBuffer = GetEnemyBuffer();
            CopySnapshot(enemyBuffer, cave.GetEnemyList());
            foreach (var creature in enemyBuffer)
            {
                creature.Move();
            }
            phaseObserver?.OnPhaseCompleted(TickPhase.EnemyMove);
        }

        var buildingBuffer = GetBuildingBuffer();
        CopySnapshot(buildingBuffer, cave.GetBuildingList());
        foreach (var building in buildingBuffer)
        {
            building.Tick(cave);
        }
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
