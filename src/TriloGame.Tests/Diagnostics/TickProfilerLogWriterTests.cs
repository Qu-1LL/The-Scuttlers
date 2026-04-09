using System.Text.Json;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Tests.Diagnostics;

public sealed class TickProfilerLogWriterTests
{
    [Fact]
    public void WriteTick_WritesJsonLineWithNavigationMetrics()
    {
        var reportDirectory = Path.Combine(Path.GetTempPath(), "TriloGameTickProfilerLogTests", Guid.NewGuid().ToString("N"));

        try
        {
            TickProfilerLogWriter.ResetForTests(reportDirectory);
            var session = new GameSession
            {
                TickCount = 42,
                Danger = true
            };

            var snapshot = new TickTimingSnapshot(
                12.5d,
                1d,
                6d,
                2d,
                1.5d,
                0.5d,
                2048L,
                1,
                0,
                0,
                12,
                3,
                4,
                new NavigationTickMetrics(
                    5,
                    4,
                    100L,
                    200L,
                    9,
                    2.25d,
                    300L,
                    7,
                    3.5d,
                    400L,
                    2,
                    1.25d,
                    500L,
                    120,
                    6,
                    48,
                    14,
                    3,
                    20,
                    5,
                    45,
                    12,
                    4,
                    18));

            TickProfilerLogWriter.WriteTick(session, snapshot);
            TickProfilerLogWriter.Shutdown();

            var reportPath = Path.Combine(reportDirectory, "tick-profiler.jsonl");
            Assert.True(File.Exists(reportPath));

            var lines = File.ReadAllLines(reportPath);
            Assert.Single(lines);

            using var document = JsonDocument.Parse(lines[0]);
            var root = document.RootElement;

            Assert.Equal(42, root.GetProperty("TickCount").GetInt32());
            Assert.True(root.GetProperty("Danger").GetBoolean());
            Assert.Equal(12.5d, root.GetProperty("TotalMs").GetDouble());

            var navigation = root.GetProperty("Navigation");
            Assert.Equal(5, navigation.GetProperty("PointPathRequestCount").GetInt32());
            Assert.Equal(120, navigation.GetProperty("DroppedResourceTilesScanned").GetInt32());
            Assert.Equal(14, navigation.GetProperty("MaxPathLength").GetInt32());
        }
        finally
        {
            TickProfilerLogWriter.ResetForTests();
            if (Directory.Exists(reportDirectory))
            {
                Directory.Delete(reportDirectory, recursive: true);
            }
        }
    }
}
