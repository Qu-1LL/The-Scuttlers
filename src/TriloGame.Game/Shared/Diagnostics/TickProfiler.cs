namespace TriloGame.Game.Shared.Diagnostics;

public readonly record struct RoleTimingSnapshot(
    double TotalMs,
    int Count)
{
    public static RoleTimingSnapshot Empty => new(0d, 0);

    public double AverageMsPerTrilobite => Count > 0 ? TotalMs / Count : 0d;
}

public readonly record struct NavigationTickMetrics(
    int PointPathRequestCount,
    int BuildingPathRequestCount,
    long PointPathRequestAllocatedBytes,
    long BuildingPathRequestAllocatedBytes,
    int BuildPathFromFieldCallCount,
    double BuildPathFromFieldMs,
    long BuildPathFromFieldAllocatedBytes,
    int BuildPointBfsFieldCallCount,
    double BuildPointBfsFieldMs,
    long BuildPointBfsFieldAllocatedBytes,
    int DroppedResourceScanCount,
    double DroppedResourceScanMs,
    long DroppedResourceScanAllocatedBytes,
    int DroppedResourceTilesScanned,
    int SuccessfulPathCount,
    int TotalPathLength,
    int MaxPathLength,
    int RerouteCount,
    int QueuedNavigationSteps,
    int PathPreviewSampleCount,
    int TotalPathPreviewLength,
    int MaxPathPreviewLength,
    int PathPreviewFrontRemovalCount,
    int PathPreviewFrontRemovalLengthTotal)
{
    public static NavigationTickMetrics Empty => default;

    public double AveragePathLength => SuccessfulPathCount <= 0
        ? 0d
        : (double)TotalPathLength / SuccessfulPathCount;

    public double AverageDroppedResourceTilesScanned => DroppedResourceScanCount <= 0
        ? 0d
        : (double)DroppedResourceTilesScanned / DroppedResourceScanCount;

    public double AveragePathPreviewLength => PathPreviewSampleCount <= 0
        ? 0d
        : (double)TotalPathPreviewLength / PathPreviewSampleCount;

    public double AverageFrontRemovalLength => PathPreviewFrontRemovalCount <= 0
        ? 0d
        : (double)PathPreviewFrontRemovalLengthTotal / PathPreviewFrontRemovalCount;

    public bool HasData =>
        PointPathRequestCount > 0 ||
        BuildingPathRequestCount > 0 ||
        BuildPathFromFieldCallCount > 0 ||
        BuildPointBfsFieldCallCount > 0 ||
        DroppedResourceScanCount > 0 ||
        QueuedNavigationSteps > 0 ||
        RerouteCount > 0;

    public NavigationTickMetrics Add(in NavigationTickMetrics other)
    {
        return new NavigationTickMetrics(
            PointPathRequestCount + other.PointPathRequestCount,
            BuildingPathRequestCount + other.BuildingPathRequestCount,
            PointPathRequestAllocatedBytes + other.PointPathRequestAllocatedBytes,
            BuildingPathRequestAllocatedBytes + other.BuildingPathRequestAllocatedBytes,
            BuildPathFromFieldCallCount + other.BuildPathFromFieldCallCount,
            BuildPathFromFieldMs + other.BuildPathFromFieldMs,
            BuildPathFromFieldAllocatedBytes + other.BuildPathFromFieldAllocatedBytes,
            BuildPointBfsFieldCallCount + other.BuildPointBfsFieldCallCount,
            BuildPointBfsFieldMs + other.BuildPointBfsFieldMs,
            BuildPointBfsFieldAllocatedBytes + other.BuildPointBfsFieldAllocatedBytes,
            DroppedResourceScanCount + other.DroppedResourceScanCount,
            DroppedResourceScanMs + other.DroppedResourceScanMs,
            DroppedResourceScanAllocatedBytes + other.DroppedResourceScanAllocatedBytes,
            DroppedResourceTilesScanned + other.DroppedResourceTilesScanned,
            SuccessfulPathCount + other.SuccessfulPathCount,
            TotalPathLength + other.TotalPathLength,
            MaxPathLength + other.MaxPathLength,
            RerouteCount + other.RerouteCount,
            QueuedNavigationSteps + other.QueuedNavigationSteps,
            PathPreviewSampleCount + other.PathPreviewSampleCount,
            TotalPathPreviewLength + other.TotalPathPreviewLength,
            MaxPathPreviewLength + other.MaxPathPreviewLength,
            PathPreviewFrontRemovalCount + other.PathPreviewFrontRemovalCount,
            PathPreviewFrontRemovalLengthTotal + other.PathPreviewFrontRemovalLengthTotal);
    }

    public NavigationTickMetrics Subtract(in NavigationTickMetrics other)
    {
        return new NavigationTickMetrics(
            PointPathRequestCount - other.PointPathRequestCount,
            BuildingPathRequestCount - other.BuildingPathRequestCount,
            PointPathRequestAllocatedBytes - other.PointPathRequestAllocatedBytes,
            BuildingPathRequestAllocatedBytes - other.BuildingPathRequestAllocatedBytes,
            BuildPathFromFieldCallCount - other.BuildPathFromFieldCallCount,
            BuildPathFromFieldMs - other.BuildPathFromFieldMs,
            BuildPathFromFieldAllocatedBytes - other.BuildPathFromFieldAllocatedBytes,
            BuildPointBfsFieldCallCount - other.BuildPointBfsFieldCallCount,
            BuildPointBfsFieldMs - other.BuildPointBfsFieldMs,
            BuildPointBfsFieldAllocatedBytes - other.BuildPointBfsFieldAllocatedBytes,
            DroppedResourceScanCount - other.DroppedResourceScanCount,
            DroppedResourceScanMs - other.DroppedResourceScanMs,
            DroppedResourceScanAllocatedBytes - other.DroppedResourceScanAllocatedBytes,
            DroppedResourceTilesScanned - other.DroppedResourceTilesScanned,
            SuccessfulPathCount - other.SuccessfulPathCount,
            TotalPathLength - other.TotalPathLength,
            MaxPathLength - other.MaxPathLength,
            RerouteCount - other.RerouteCount,
            QueuedNavigationSteps - other.QueuedNavigationSteps,
            PathPreviewSampleCount - other.PathPreviewSampleCount,
            TotalPathPreviewLength - other.TotalPathPreviewLength,
            MaxPathPreviewLength - other.MaxPathPreviewLength,
            PathPreviewFrontRemovalCount - other.PathPreviewFrontRemovalCount,
            PathPreviewFrontRemovalLengthTotal - other.PathPreviewFrontRemovalLengthTotal);
    }

    public NavigationTickMetrics Divide(int divisor)
    {
        if (divisor <= 0)
        {
            return this;
        }

        return new NavigationTickMetrics(
            PointPathRequestCount / divisor,
            BuildingPathRequestCount / divisor,
            PointPathRequestAllocatedBytes / divisor,
            BuildingPathRequestAllocatedBytes / divisor,
            BuildPathFromFieldCallCount / divisor,
            BuildPathFromFieldMs / divisor,
            BuildPathFromFieldAllocatedBytes / divisor,
            BuildPointBfsFieldCallCount / divisor,
            BuildPointBfsFieldMs / divisor,
            BuildPointBfsFieldAllocatedBytes / divisor,
            DroppedResourceScanCount / divisor,
            DroppedResourceScanMs / divisor,
            DroppedResourceScanAllocatedBytes / divisor,
            DroppedResourceTilesScanned / divisor,
            SuccessfulPathCount / divisor,
            TotalPathLength / divisor,
            MaxPathLength / divisor,
            RerouteCount / divisor,
            QueuedNavigationSteps / divisor,
            PathPreviewSampleCount / divisor,
            TotalPathPreviewLength / divisor,
            MaxPathPreviewLength / divisor,
            PathPreviewFrontRemovalCount / divisor,
            PathPreviewFrontRemovalLengthTotal / divisor);
    }
}

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
    int BuildingCount,
    NavigationTickMetrics Navigation = default,
    RoleTimingSnapshot MinerTiming = default,
    RoleTimingSnapshot BuilderTiming = default,
    RoleTimingSnapshot FarmerTiming = default,
    RoleTimingSnapshot FighterTiming = default,
    bool RoleTimingsCaptured = false)
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
        0,
        NavigationTickMetrics.Empty,
        RoleTimingSnapshot.Empty,
        RoleTimingSnapshot.Empty,
        RoleTimingSnapshot.Empty,
        RoleTimingSnapshot.Empty,
        false);

    public double TotalBfsMs => EnemyBfsMs + ColonyBfsMs;

    public double MeasuredPhaseMs => EnemyBfsMs + TrilobiteMoveMs + ColonyBfsMs + EnemyMoveMs + BuildingTickMs;

    public double OtherMs => System.Math.Max(0d, TotalMs - MeasuredPhaseMs);

    public string DescribeDominantWork()
    {
        var (dominantMs, detail, _) = GetDominantWorkInfo();
        if (dominantMs <= 0.05d)
        {
            return "No measurable tick work yet";
        }

        var share = TotalMs <= 0d ? 0d : System.Math.Clamp((dominantMs / TotalMs) * 100d, 0d, 100d);
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

        var share = TotalMs <= 0d ? 0d : System.Math.Clamp((dominantMs / TotalMs) * 100d, 0d, 100d);
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
    private NavigationTickMetrics _sumNavigation = NavigationTickMetrics.Empty;
    private double _sumTotalMs;
    private double _sumEnemyBfsMs;
    private double _sumTrilobiteMoveMs;
    private double _sumColonyBfsMs;
    private double _sumEnemyMoveMs;
    private double _sumBuildingTickMs;
    private double _sumMinerRoleTotalMs;
    private double _sumBuilderRoleTotalMs;
    private double _sumFarmerRoleTotalMs;
    private double _sumFighterRoleTotalMs;
    private long _sumAllocatedBytes;
    private int _sumMinerRoleCount;
    private int _sumBuilderRoleCount;
    private int _sumFarmerRoleCount;
    private int _sumFighterRoleCount;
    private int _roleTimingSampleCount;

    public TickTimingSnapshot Last { get; private set; } = TickTimingSnapshot.Empty;

    public TickTimingSnapshot Average { get; private set; } = TickTimingSnapshot.Empty;

    public int SampleCount => _history.Count;

    public double AverageMinerMsPerTrilobite => ComputeAverageRoleMsPerTrilobite(_sumMinerRoleTotalMs, _sumMinerRoleCount);

    public double AverageBuilderMsPerTrilobite => ComputeAverageRoleMsPerTrilobite(_sumBuilderRoleTotalMs, _sumBuilderRoleCount);

    public double AverageFarmerMsPerTrilobite => ComputeAverageRoleMsPerTrilobite(_sumFarmerRoleTotalMs, _sumFarmerRoleCount);

    public double AverageFighterMsPerTrilobite => ComputeAverageRoleMsPerTrilobite(_sumFighterRoleTotalMs, _sumFighterRoleCount);

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
        _sumNavigation = _sumNavigation.Add(snapshot.Navigation);

        if (!snapshot.RoleTimingsCaptured)
        {
            return;
        }

        _roleTimingSampleCount++;
        _sumMinerRoleTotalMs += snapshot.MinerTiming.TotalMs;
        _sumBuilderRoleTotalMs += snapshot.BuilderTiming.TotalMs;
        _sumFarmerRoleTotalMs += snapshot.FarmerTiming.TotalMs;
        _sumFighterRoleTotalMs += snapshot.FighterTiming.TotalMs;
        _sumMinerRoleCount += snapshot.MinerTiming.Count;
        _sumBuilderRoleCount += snapshot.BuilderTiming.Count;
        _sumFarmerRoleCount += snapshot.FarmerTiming.Count;
        _sumFighterRoleCount += snapshot.FighterTiming.Count;
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
        _sumNavigation = _sumNavigation.Subtract(snapshot.Navigation);

        if (!snapshot.RoleTimingsCaptured)
        {
            return;
        }

        _roleTimingSampleCount--;
        _sumMinerRoleTotalMs -= snapshot.MinerTiming.TotalMs;
        _sumBuilderRoleTotalMs -= snapshot.BuilderTiming.TotalMs;
        _sumFarmerRoleTotalMs -= snapshot.FarmerTiming.TotalMs;
        _sumFighterRoleTotalMs -= snapshot.FighterTiming.TotalMs;
        _sumMinerRoleCount -= snapshot.MinerTiming.Count;
        _sumBuilderRoleCount -= snapshot.BuilderTiming.Count;
        _sumFarmerRoleCount -= snapshot.FarmerTiming.Count;
        _sumFighterRoleCount -= snapshot.FighterTiming.Count;
    }

    private TickTimingSnapshot BuildAverageSnapshot()
    {
        if (_history.Count == 0)
        {
            return TickTimingSnapshot.Empty;
        }

        var sampleCount = _history.Count;
        var roleSampleCount = _roleTimingSampleCount;
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
            Last.BuildingCount,
            _sumNavigation.Divide(sampleCount),
            BuildAverageRoleTimingSnapshot(_sumMinerRoleTotalMs, _sumMinerRoleCount, roleSampleCount),
            BuildAverageRoleTimingSnapshot(_sumBuilderRoleTotalMs, _sumBuilderRoleCount, roleSampleCount),
            BuildAverageRoleTimingSnapshot(_sumFarmerRoleTotalMs, _sumFarmerRoleCount, roleSampleCount),
            BuildAverageRoleTimingSnapshot(_sumFighterRoleTotalMs, _sumFighterRoleCount, roleSampleCount),
            roleSampleCount > 0);
    }

    private static double ComputeAverageRoleMsPerTrilobite(double totalMs, int totalCount)
    {
        return totalCount > 0 ? totalMs / totalCount : 0d;
    }

    private static RoleTimingSnapshot BuildAverageRoleTimingSnapshot(double totalMs, int totalCount, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return RoleTimingSnapshot.Empty;
        }

        return new RoleTimingSnapshot(
            totalMs / sampleCount,
            (int)System.Math.Round(totalCount / (double)sampleCount, MidpointRounding.AwayFromZero));
    }
}

internal static class NavigationInstrumentation
{
    [ThreadStatic]
    private static NavigationTickAccumulator? _current;

    public static void BeginTick()
    {
        (_current ??= new NavigationTickAccumulator()).Reset();
    }

    public static NavigationTickMetrics CompleteTick()
    {
        if (_current is null)
        {
            return NavigationTickMetrics.Empty;
        }

        var snapshot = _current.BuildSnapshot();
        _current.Reset();
        return snapshot;
    }

    public static void RecordPointPathRequest(int pathLength, long allocatedBytes)
    {
        _current?.RecordPointPathRequest(pathLength, allocatedBytes);
    }

    public static void RecordBuildingPathRequest(int pathLength, long allocatedBytes)
    {
        _current?.RecordBuildingPathRequest(pathLength, allocatedBytes);
    }

    public static void RecordBuildPathFromField(double elapsedMs, long allocatedBytes)
    {
        _current?.RecordBuildPathFromField(elapsedMs, allocatedBytes);
    }

    public static void RecordBuildPointBfsField(double elapsedMs, long allocatedBytes)
    {
        _current?.RecordBuildPointBfsField(elapsedMs, allocatedBytes);
    }

    public static void RecordDroppedResourceScan(int scannedTiles, double elapsedMs, long allocatedBytes)
    {
        _current?.RecordDroppedResourceScan(scannedTiles, elapsedMs, allocatedBytes);
    }

    public static void RecordNavigationReroute()
    {
        _current?.RecordNavigationReroute();
    }

    public static void RecordQueuedNavigationSteps(int stepCount, int pathPreviewLength)
    {
        _current?.RecordQueuedNavigationSteps(stepCount, pathPreviewLength);
    }

    public static void RecordPathPreviewFrontRemoval(int pathPreviewLength)
    {
        _current?.RecordPathPreviewFrontRemoval(pathPreviewLength);
    }

    private sealed class NavigationTickAccumulator
    {
        private int _pointPathRequestCount;
        private int _buildingPathRequestCount;
        private long _pointPathRequestAllocatedBytes;
        private long _buildingPathRequestAllocatedBytes;
        private int _buildPathFromFieldCallCount;
        private double _buildPathFromFieldMs;
        private long _buildPathFromFieldAllocatedBytes;
        private int _buildPointBfsFieldCallCount;
        private double _buildPointBfsFieldMs;
        private long _buildPointBfsFieldAllocatedBytes;
        private int _droppedResourceScanCount;
        private double _droppedResourceScanMs;
        private long _droppedResourceScanAllocatedBytes;
        private int _droppedResourceTilesScanned;
        private int _successfulPathCount;
        private int _totalPathLength;
        private int _maxPathLength;
        private int _rerouteCount;
        private int _queuedNavigationSteps;
        private int _pathPreviewSampleCount;
        private int _totalPathPreviewLength;
        private int _maxPathPreviewLength;
        private int _pathPreviewFrontRemovalCount;
        private int _pathPreviewFrontRemovalLengthTotal;

        public void Reset()
        {
            _pointPathRequestCount = 0;
            _buildingPathRequestCount = 0;
            _pointPathRequestAllocatedBytes = 0L;
            _buildingPathRequestAllocatedBytes = 0L;
            _buildPathFromFieldCallCount = 0;
            _buildPathFromFieldMs = 0d;
            _buildPathFromFieldAllocatedBytes = 0L;
            _buildPointBfsFieldCallCount = 0;
            _buildPointBfsFieldMs = 0d;
            _buildPointBfsFieldAllocatedBytes = 0L;
            _droppedResourceScanCount = 0;
            _droppedResourceScanMs = 0d;
            _droppedResourceScanAllocatedBytes = 0L;
            _droppedResourceTilesScanned = 0;
            _successfulPathCount = 0;
            _totalPathLength = 0;
            _maxPathLength = 0;
            _rerouteCount = 0;
            _queuedNavigationSteps = 0;
            _pathPreviewSampleCount = 0;
            _totalPathPreviewLength = 0;
            _maxPathPreviewLength = 0;
            _pathPreviewFrontRemovalCount = 0;
            _pathPreviewFrontRemovalLengthTotal = 0;
        }

        public NavigationTickMetrics BuildSnapshot()
        {
            return new NavigationTickMetrics(
                _pointPathRequestCount,
                _buildingPathRequestCount,
                _pointPathRequestAllocatedBytes,
                _buildingPathRequestAllocatedBytes,
                _buildPathFromFieldCallCount,
                _buildPathFromFieldMs,
                _buildPathFromFieldAllocatedBytes,
                _buildPointBfsFieldCallCount,
                _buildPointBfsFieldMs,
                _buildPointBfsFieldAllocatedBytes,
                _droppedResourceScanCount,
                _droppedResourceScanMs,
                _droppedResourceScanAllocatedBytes,
                _droppedResourceTilesScanned,
                _successfulPathCount,
                _totalPathLength,
                _maxPathLength,
                _rerouteCount,
                _queuedNavigationSteps,
                _pathPreviewSampleCount,
                _totalPathPreviewLength,
                _maxPathPreviewLength,
                _pathPreviewFrontRemovalCount,
                _pathPreviewFrontRemovalLengthTotal);
        }

        public void RecordPointPathRequest(int pathLength, long allocatedBytes)
        {
            _pointPathRequestCount++;
            _pointPathRequestAllocatedBytes += global::System.Math.Max(0L, allocatedBytes);
            RecordPathLength(pathLength);
        }

        public void RecordBuildingPathRequest(int pathLength, long allocatedBytes)
        {
            _buildingPathRequestCount++;
            _buildingPathRequestAllocatedBytes += global::System.Math.Max(0L, allocatedBytes);
            RecordPathLength(pathLength);
        }

        public void RecordBuildPathFromField(double elapsedMs, long allocatedBytes)
        {
            _buildPathFromFieldCallCount++;
            _buildPathFromFieldMs += global::System.Math.Max(0d, elapsedMs);
            _buildPathFromFieldAllocatedBytes += global::System.Math.Max(0L, allocatedBytes);
        }

        public void RecordBuildPointBfsField(double elapsedMs, long allocatedBytes)
        {
            _buildPointBfsFieldCallCount++;
            _buildPointBfsFieldMs += global::System.Math.Max(0d, elapsedMs);
            _buildPointBfsFieldAllocatedBytes += global::System.Math.Max(0L, allocatedBytes);
        }

        public void RecordDroppedResourceScan(int scannedTiles, double elapsedMs, long allocatedBytes)
        {
            _droppedResourceScanCount++;
            _droppedResourceScanMs += global::System.Math.Max(0d, elapsedMs);
            _droppedResourceScanAllocatedBytes += global::System.Math.Max(0L, allocatedBytes);
            _droppedResourceTilesScanned += global::System.Math.Max(0, scannedTiles);
        }

        public void RecordNavigationReroute()
        {
            _rerouteCount++;
        }

        public void RecordQueuedNavigationSteps(int stepCount, int pathPreviewLength)
        {
            _queuedNavigationSteps += global::System.Math.Max(0, stepCount);
            _pathPreviewSampleCount++;
            _totalPathPreviewLength += global::System.Math.Max(0, pathPreviewLength);
            _maxPathPreviewLength = global::System.Math.Max(_maxPathPreviewLength, pathPreviewLength);
        }

        public void RecordPathPreviewFrontRemoval(int pathPreviewLength)
        {
            _pathPreviewFrontRemovalCount++;
            _pathPreviewFrontRemovalLengthTotal += global::System.Math.Max(0, pathPreviewLength);
            _maxPathPreviewLength = global::System.Math.Max(_maxPathPreviewLength, pathPreviewLength);
        }

        private void RecordPathLength(int pathLength)
        {
            if (pathLength <= 0)
            {
                return;
            }

            _successfulPathCount++;
            _totalPathLength += pathLength;
            _maxPathLength = global::System.Math.Max(_maxPathLength, pathLength);
        }
    }
}
