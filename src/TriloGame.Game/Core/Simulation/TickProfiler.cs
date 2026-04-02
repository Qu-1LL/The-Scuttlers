namespace TriloGame.Game.Core.Simulation;

public readonly record struct TickTimingSnapshot(
    double TotalMs,
    double EnemyBfsMs,
    double TrilobiteMoveMs,
    double ColonyBfsMs,
    double EnemyMoveMs,
    double BuildingTickMs,
    long AllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int TrilobiteCount,
    int EnemyCount,
    int BuildingCount)
{
    public static TickTimingSnapshot Empty => new(
        0d,
        0d,
        0d,
        0d,
        0d,
        0d,
        0L,
        0,
        0,
        0,
        0,
        0,
        0);

    public double TotalBfsMs => EnemyBfsMs + ColonyBfsMs;

    public double MeasuredPhaseMs => EnemyBfsMs + TrilobiteMoveMs + ColonyBfsMs + EnemyMoveMs + BuildingTickMs;

    public double OtherMs => Math.Max(0d, TotalMs - MeasuredPhaseMs);

    public string DescribeDominantWork()
    {
        var (dominantMs, detail, _) = GetDominantWorkInfo();
        if (dominantMs <= 0.05d)
        {
            return "No measurable tick work yet";
        }

        var share = TotalMs <= 0d ? 0d : Math.Clamp((dominantMs / TotalMs) * 100d, 0d, 100d);
        var prefix = TotalMs >= 100d ? "Slow tick cause" : "Dominant tick work";
        return $"{prefix}: {detail} ({dominantMs:0.00} ms, {share:0}% of tick)";
    }

    public string DescribeDominantWorkShort()
    {
        var (dominantMs, _, shortLabel) = GetDominantWorkInfo();
        if (dominantMs <= 0.05d)
        {
            return "Work: idle";
        }

        var share = TotalMs <= 0d ? 0d : Math.Clamp((dominantMs / TotalMs) * 100d, 0d, 100d);
        var prefix = TotalMs >= 100d ? "Slow" : "Work";
        return $"{prefix}: {shortLabel}  {dominantMs:0.00} ms  {share:0}%";
    }

    private (double DominantMs, string Detail, string ShortLabel) GetDominantWorkInfo()
    {
        if (TotalMs <= 0.05d)
        {
            return (0d, "No measurable tick work yet", "idle");
        }

        var dominantMs = TrilobiteMoveMs;
        var detail = TrilobiteCount > 0
            ? $"iterating trilobites ({TrilobiteCount}) and running AI/movement"
            : "running trilobite AI/movement";
        var shortLabel = "tri AI/move";

        if (EnemyBfsMs > dominantMs)
        {
            dominantMs = EnemyBfsMs;
            detail = "recalculating enemy BFS/path fields";
            shortLabel = "enemy BFS";
        }

        if (ColonyBfsMs > dominantMs)
        {
            dominantMs = ColonyBfsMs;
            detail = "recalculating colony BFS/path fields";
            shortLabel = "colony BFS";
        }

        if (EnemyMoveMs > dominantMs)
        {
            dominantMs = EnemyMoveMs;
            detail = EnemyCount > 0
                ? $"iterating enemies ({EnemyCount}) and running AI/movement"
                : "running enemy AI/movement";
            shortLabel = "enemy AI/move";
        }

        if (BuildingTickMs > dominantMs)
        {
            dominantMs = BuildingTickMs;
            detail = BuildingCount > 0
                ? $"ticking buildings ({BuildingCount})"
                : "running building ticks";
            shortLabel = "bld ticks";
        }

        if (OtherMs > dominantMs)
        {
            dominantMs = OtherMs;
            detail = "doing other simulation work outside the timed phases";
            shortLabel = "other sim";
        }

        return dominantMs <= 0.05d
            ? (0d, "No dominant tick action recorded", "idle")
            : (dominantMs, detail, shortLabel);
    }
}

public sealed class TickProfiler
{
    private const int HistoryLimit = 60;
    private readonly Queue<TickTimingSnapshot> _history = [];
    private double _sumTotalMs;
    private double _sumEnemyBfsMs;
    private double _sumTrilobiteMoveMs;
    private double _sumColonyBfsMs;
    private double _sumEnemyMoveMs;
    private double _sumBuildingTickMs;
    private long _sumAllocatedBytes;

    public TickTimingSnapshot Last { get; private set; } = TickTimingSnapshot.Empty;

    public TickTimingSnapshot Average { get; private set; } = TickTimingSnapshot.Empty;

    public int SampleCount => _history.Count;

    public void Record(TickTimingSnapshot snapshot)
    {
        Last = snapshot;
        _history.Enqueue(snapshot);
        AddToSums(snapshot);

        while (_history.Count > HistoryLimit)
        {
            RemoveFromSums(_history.Dequeue());
        }

        Average = BuildAverageSnapshot();
    }

    private void AddToSums(TickTimingSnapshot snapshot)
    {
        _sumTotalMs += snapshot.TotalMs;
        _sumEnemyBfsMs += snapshot.EnemyBfsMs;
        _sumTrilobiteMoveMs += snapshot.TrilobiteMoveMs;
        _sumColonyBfsMs += snapshot.ColonyBfsMs;
        _sumEnemyMoveMs += snapshot.EnemyMoveMs;
        _sumBuildingTickMs += snapshot.BuildingTickMs;
        _sumAllocatedBytes += snapshot.AllocatedBytes;
    }

    private void RemoveFromSums(TickTimingSnapshot snapshot)
    {
        _sumTotalMs -= snapshot.TotalMs;
        _sumEnemyBfsMs -= snapshot.EnemyBfsMs;
        _sumTrilobiteMoveMs -= snapshot.TrilobiteMoveMs;
        _sumColonyBfsMs -= snapshot.ColonyBfsMs;
        _sumEnemyMoveMs -= snapshot.EnemyMoveMs;
        _sumBuildingTickMs -= snapshot.BuildingTickMs;
        _sumAllocatedBytes -= snapshot.AllocatedBytes;
    }

    private TickTimingSnapshot BuildAverageSnapshot()
    {
        if (_history.Count == 0)
        {
            return TickTimingSnapshot.Empty;
        }

        var sampleCount = _history.Count;
        return new TickTimingSnapshot(
            _sumTotalMs / sampleCount,
            _sumEnemyBfsMs / sampleCount,
            _sumTrilobiteMoveMs / sampleCount,
            _sumColonyBfsMs / sampleCount,
            _sumEnemyMoveMs / sampleCount,
            _sumBuildingTickMs / sampleCount,
            _sumAllocatedBytes / sampleCount,
            Last.Gen0Collections,
            Last.Gen1Collections,
            Last.Gen2Collections,
            Last.TrilobiteCount,
            Last.EnemyCount,
            Last.BuildingCount);
    }
}
