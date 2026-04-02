using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Tests.Diagnostics;

public sealed class CrashReporterTests
{
    [Fact]
    public void Report_WritesCrashFileWithExceptionAndSnapshot()
    {
        var reportDirectory = Path.Combine(Path.GetTempPath(), "TriloGameCrashReporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(reportDirectory);

        try
        {
            CrashReporter.ResetForTests(reportDirectory);
            CrashReporter.RegisterSnapshotProvider(() => "TickCount: 42");

            var reportPath = CrashReporter.Report(new InvalidOperationException("boom"), "CrashReporterTests");

            Assert.False(string.IsNullOrWhiteSpace(reportPath));
            Assert.True(File.Exists(reportPath));

            var contents = File.ReadAllText(reportPath);
            Assert.Contains("CrashReporterTests", contents);
            Assert.Contains("boom", contents);
            Assert.Contains("TickCount: 42", contents);
        }
        finally
        {
            CrashReporter.ResetForTests();
            if (Directory.Exists(reportDirectory))
            {
                Directory.Delete(reportDirectory, true);
            }
        }
    }
}
