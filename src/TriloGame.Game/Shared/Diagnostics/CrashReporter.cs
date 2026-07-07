using System.Text;

namespace TriloGame.Game.Shared.Diagnostics;

public static class CrashReporter
{
    private static readonly object Sync = new();
    private static bool _handlersInstalled;
    private static Func<string>? _snapshotProvider;
    private static string? _lastReportPath;

    public static string ReportDirectoryPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "CrashReports");

    // Install one set of process-wide crash handlers for unhandled exceptions.
    public static void InstallProcessHandlers()
    {
        lock (Sync)
        {
            if (_handlersInstalled)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _handlersInstalled = true;
        }
    }

    // Register a callback that can provide a live game-state snapshot during crash reporting.
    public static void RegisterSnapshotProvider(Func<string> snapshotProvider)
    {
        lock (Sync)
        {
            _snapshotProvider = snapshotProvider;
        }
    }

    // Write one crash report and reuse the same file path if reporting is triggered repeatedly.
    public static string Report(Exception exception, string source, bool isTerminating = false)
    {
        try
        {
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath))
                {
                    return _lastReportPath;
                }

                Directory.CreateDirectory(ReportDirectoryPath);

                var reportPath = Path.Combine(
                    ReportDirectoryPath,
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-crash.txt");
                File.WriteAllText(reportPath, BuildReportText(exception, source, isTerminating));
                _lastReportPath = reportPath;
                TryWriteConsoleLine($"Crash report written to: {reportPath}");
                return reportPath;
            }
        }
        catch (Exception reportException)
        {
            TryWriteConsoleLine("Crash reporter failed to write a report.");
            TryWriteConsoleLine(reportException.ToString());
            TryWriteConsoleLine(exception.ToString());
            return string.Empty;
        }
    }

    // Reset static crash-reporter state so tests can run in isolation.
    public static void ResetForTests(string? reportDirectoryPath = null)
    {
        lock (Sync)
        {
            _snapshotProvider = null;
            _lastReportPath = null;
            if (!string.IsNullOrWhiteSpace(reportDirectoryPath))
            {
                ReportDirectoryPath = reportDirectoryPath;
            }
        }
    }

    // Capture and report AppDomain-level unhandled exceptions.
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        var exception = args.ExceptionObject as Exception
            ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown unhandled exception object.");
        Report(exception, "AppDomain.CurrentDomain.UnhandledException", args.IsTerminating);
    }

    // Capture task exceptions that would otherwise be dropped on finalization.
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Report(args.Exception, "TaskScheduler.UnobservedTaskException");
        args.SetObserved();
    }

    // Build the full crash report text, including environment details and a game snapshot.
    private static string BuildReportText(Exception exception, string source, bool isTerminating)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TriloGame Crash Report");
        builder.AppendLine("=====================");
        builder.AppendLine($"TimestampUtc: {DateTime.UtcNow:O}");
        builder.AppendLine($"Source: {source}");
        builder.AppendLine($"IsTerminating: {isTerminating}");
        builder.AppendLine($"ProcessId: {Environment.ProcessId}");
        builder.AppendLine($"ThreadId: {Environment.CurrentManagedThreadId}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($"Framework: {Environment.Version}");
        builder.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
        builder.AppendLine($"CurrentDirectory: {Environment.CurrentDirectory}");
        builder.AppendLine();
        builder.AppendLine("Exception");
        builder.AppendLine("---------");
        builder.AppendLine(exception.ToString());
        builder.AppendLine();
        builder.AppendLine("Game Snapshot");
        builder.AppendLine("-------------");
        builder.AppendLine(TryBuildSnapshot());
        return builder.ToString();
    }

    // Ask the registered provider for a snapshot without letting snapshot failures hide the crash.
    private static string TryBuildSnapshot()
    {
        try
        {
            var snapshot = _snapshotProvider?.Invoke();
            return string.IsNullOrWhiteSpace(snapshot)
                ? "No snapshot provider data available."
                : snapshot;
        }
        catch (Exception snapshotException)
        {
            return $"Snapshot generation failed:{Environment.NewLine}{snapshotException}";
        }
    }

    // Best-effort logging avoids secondary failures while the process is already unstable.
    private static void TryWriteConsoleLine(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
        }
    }
}
