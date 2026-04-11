using System.Diagnostics;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Game.Runtime.Systems;

internal sealed class TickProfilingObserver : ITickPhaseObserver
{
    private long _tickStart;
    private long _phaseStart;
    private long _trilobiteMoveStart;
    private long _allocatedStart;
    private int _gen0Start;
    private int _gen1Start;
    private int _gen2Start;
    private double _enemyBfsMs;
    private double _trilobiteMoveMs;
    private double _colonyBfsMs;
    private double _enemyMoveMs;
    private double _buildingTickMs;
    private double _minerRoleTotalMs;
    private double _builderRoleTotalMs;
    private double _farmerRoleTotalMs;
    private double _fighterRoleTotalMs;
    private int _minerRoleCount;
    private int _builderRoleCount;
    private int _farmerRoleCount;
    private int _fighterRoleCount;

    public void OnTickStarted(GameSession session)
    {
        _tickStart = Stopwatch.GetTimestamp();
        _phaseStart = _tickStart;
        _allocatedStart = GC.GetTotalAllocatedBytes(false);
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
        _enemyBfsMs = 0d;
        _trilobiteMoveMs = 0d;
        _colonyBfsMs = 0d;
        _enemyMoveMs = 0d;
        _buildingTickMs = 0d;
        _minerRoleTotalMs = 0d;
        _builderRoleTotalMs = 0d;
        _farmerRoleTotalMs = 0d;
        _fighterRoleTotalMs = 0d;
        _minerRoleCount = 0;
        _builderRoleCount = 0;
        _farmerRoleCount = 0;
        _fighterRoleCount = 0;
        NavigationInstrumentation.BeginTick();
    }

    public void OnPhaseStarted(TickPhase phase)
    {
    }

    public void OnPhaseCompleted(TickPhase phase)
    {
        var elapsedMs = ConsumeElapsedMs();
        switch (phase)
        {
            case TickPhase.TraitTick:
            case TickPhase.SurfaceFeatureTick:
            case TickPhase.NaturalEnemySpawn:
            case TickPhase.DangerRefresh:
                break;
            case TickPhase.EnemyBfs:
                _enemyBfsMs = elapsedMs;
                break;
            case TickPhase.TrilobiteMove:
                _trilobiteMoveMs = elapsedMs;
                break;
            case TickPhase.ColonyBfs:
                _colonyBfsMs = elapsedMs;
                break;
            case TickPhase.EnemyMove:
                _enemyMoveMs = elapsedMs;
                break;
            case TickPhase.BuildingTick:
                _buildingTickMs = elapsedMs;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown tick phase.");
        }
    }

    public void OnTrilobiteMoveStarted(string assignment)
    {
        _trilobiteMoveStart = Stopwatch.GetTimestamp();
    }

    public void OnTrilobiteMoveCompleted(string assignment)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(_trilobiteMoveStart).TotalMilliseconds;
        switch (assignment)
        {
            case "miner":
                _minerRoleTotalMs += elapsedMs;
                _minerRoleCount++;
                break;
            case "builder":
                _builderRoleTotalMs += elapsedMs;
                _builderRoleCount++;
                break;
            case "farmer":
                _farmerRoleTotalMs += elapsedMs;
                _farmerRoleCount++;
                break;
            case "fighter":
                _fighterRoleTotalMs += elapsedMs;
                _fighterRoleCount++;
                break;
        }
    }

    public void OnTickCompleted(GameSession session)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        var navigation = NavigationInstrumentation.CompleteTick();
        var snapshot = new TickTimingSnapshot(
            Stopwatch.GetElapsedTime(_tickStart).TotalMilliseconds,
            _enemyBfsMs,
            _trilobiteMoveMs,
            _colonyBfsMs,
            _enemyMoveMs,
            _buildingTickMs,
            GC.GetTotalAllocatedBytes(false) - _allocatedStart,
            GC.CollectionCount(0) - _gen0Start,
            GC.CollectionCount(1) - _gen1Start,
            GC.CollectionCount(2) - _gen2Start,
            cave.GetTrilobiteList().Count,
            cave.GetEnemyList().Count,
            cave.GetBuildingList().Count,
            navigation,
            new RoleTimingSnapshot(_minerRoleTotalMs, _minerRoleCount),
            new RoleTimingSnapshot(_builderRoleTotalMs, _builderRoleCount),
            new RoleTimingSnapshot(_farmerRoleTotalMs, _farmerRoleCount),
            new RoleTimingSnapshot(_fighterRoleTotalMs, _fighterRoleCount),
            true);
        session.Runtime.TickProfiler.Record(snapshot);
        TickProfilerLogWriter.WriteTick(session, snapshot);
    }

    private double ConsumeElapsedMs()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(_phaseStart, now).TotalMilliseconds;
        _phaseStart = now;
        return elapsedMs;
    }
}
