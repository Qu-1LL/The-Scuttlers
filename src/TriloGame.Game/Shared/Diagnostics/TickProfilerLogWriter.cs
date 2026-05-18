using System.Text.Json;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Shared.Diagnostics;

public static class TickProfilerLogWriter
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static StreamWriter? _writer;

    public static string ReportDirectoryPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "ProfilerReports");

    public static string ReportFilePath => Path.Combine(ReportDirectoryPath, "tick-profiler.jsonl");

    // Append one profiler snapshot as a JSON line for later analysis.
    public static void WriteTick(GameSession session, TickTimingSnapshot snapshot)
    {
        lock (Sync)
        {
            var writer = EnsureWriter();
            var entry = new TickProfilerLogEntry(
                DateTime.UtcNow,
                session.TickCount,
                session.Danger,
                snapshot.TotalMs,
                snapshot.EnemyBfsMs,
                snapshot.TrilobiteMoveMs,
                snapshot.ColonyBfsMs,
                snapshot.EnemyMoveMs,
                snapshot.BuildingTickMs,
                snapshot.AllocatedBytes,
                snapshot.Gen0Collections,
                snapshot.Gen1Collections,
                snapshot.Gen2Collections,
                snapshot.TrilobiteCount,
                snapshot.EnemyCount,
                snapshot.BuildingCount,
                snapshot.DescribeDominantWork(),
                snapshot.DescribeDominantWorkShort(),
                new NavigationTickLogEntry(
                    snapshot.Navigation.PointPathRequestCount,
                    snapshot.Navigation.BuildingPathRequestCount,
                    snapshot.Navigation.PointPathRequestAllocatedBytes,
                    snapshot.Navigation.BuildingPathRequestAllocatedBytes,
                    snapshot.Navigation.BuildPathFromFieldCallCount,
                    snapshot.Navigation.BuildPathFromFieldMs,
                    snapshot.Navigation.BuildPathFromFieldAllocatedBytes,
                    snapshot.Navigation.BuildPointBfsFieldCallCount,
                    snapshot.Navigation.BuildPointBfsFieldMs,
                    snapshot.Navigation.BuildPointBfsFieldAllocatedBytes,
                    snapshot.Navigation.DroppedResourceScanCount,
                    snapshot.Navigation.DroppedResourceScanMs,
                    snapshot.Navigation.DroppedResourceScanAllocatedBytes,
                    snapshot.Navigation.DroppedResourceTilesScanned,
                    snapshot.Navigation.SuccessfulPathCount,
                    snapshot.Navigation.TotalPathLength,
                    snapshot.Navigation.MaxPathLength,
                    snapshot.Navigation.AveragePathLength,
                    snapshot.Navigation.RerouteCount,
                    snapshot.Navigation.QueuedNavigationSteps,
                    snapshot.Navigation.PathPreviewSampleCount,
                    snapshot.Navigation.TotalPathPreviewLength,
                    snapshot.Navigation.MaxPathPreviewLength,
                    snapshot.Navigation.AveragePathPreviewLength,
                    snapshot.Navigation.PathPreviewFrontRemovalCount,
                    snapshot.Navigation.PathPreviewFrontRemovalLengthTotal,
                    snapshot.Navigation.AverageFrontRemovalLength,
                    snapshot.Navigation.AverageDroppedResourceTilesScanned));

            writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));
            writer.Flush();
        }
    }

    // Flush and release the current log writer when the session shuts down.
    public static void Shutdown()
    {
        lock (Sync)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    // Reset the writer so tests can control the output directory and file lifetime.
    public static void ResetForTests(string? reportDirectoryPath = null)
    {
        lock (Sync)
        {
            _writer?.Dispose();
            _writer = null;
            if (!string.IsNullOrWhiteSpace(reportDirectoryPath))
            {
                ReportDirectoryPath = reportDirectoryPath;
            }
        }
    }

    // Lazily create the JSONL writer the first time a snapshot is logged.
    private static StreamWriter EnsureWriter()
    {
        if (_writer is not null)
        {
            return _writer;
        }

        Directory.CreateDirectory(ReportDirectoryPath);
        _writer = new StreamWriter(
            new FileStream(ReportFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = false
        };
        return _writer;
    }

    private sealed record TickProfilerLogEntry(
        DateTime TimestampUtc,
        int TickCount,
        bool Danger,
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
        string DominantWork,
        string DominantWorkShort,
        NavigationTickLogEntry Navigation);

    private sealed record NavigationTickLogEntry(
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
        double AveragePathLength,
        int RerouteCount,
        int QueuedNavigationSteps,
        int PathPreviewSampleCount,
        int TotalPathPreviewLength,
        int MaxPathPreviewLength,
        double AveragePathPreviewLength,
        int PathPreviewFrontRemovalCount,
        int PathPreviewFrontRemovalLengthTotal,
        double AverageFrontRemovalLength,
        double AverageDroppedResourceTilesScanned);
}
