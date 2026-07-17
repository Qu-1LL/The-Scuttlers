using System.Diagnostics;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Performance;

public sealed class CombatWorldPerformanceTests
{
    [Fact]
    public void UniformGrid_HandlesTwoThousandHurtboxesAndThousandsOfQueries()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("TRILO_ENFORCE_PERF_BUDGETS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var grid = new CombatSpatialGrid();
        for (var index = 0; index < 2000; index++)
        {
            var point = new WorldPoint((index % 100) * WorldUnits.UnitsPerTile, (index / 100) * WorldUnits.UnitsPerTile);
            grid.Add(new CombatHurtbox
            {
                Id = index + 1,
                Target = new object(),
                Shape = CombatShape.Circle(point, WorldUnits.FromPixels(8)),
                Faction = CombatFactionMask.Hostile
            });
        }

        // Warm the dictionary and candidate sort path before measuring the hot loop.
        grid.Query(CombatShape.Circle(new WorldPoint(0, 0), WorldUnits.FromPixels(64)));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 4000; index++)
        {
            var point = new WorldPoint((index % 100) * WorldUnits.UnitsPerTile, (index / 100) * WorldUnits.UnitsPerTile);
            grid.Query(CombatShape.Circle(point, WorldUnits.FromPixels(64)));
        }
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Broadphase took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 8_000_000);
    }
}
