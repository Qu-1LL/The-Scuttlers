namespace TriloGame.Game.Shared.Diagnostics;

public readonly record struct GameAppDebugMetricsSnapshot(
    bool Paused,
    bool Danger,
    int TickCount,
    double TickSpeedMs,
    string ActiveBfsDebugField,
    bool ShowRoleLabels,
    bool NoCostBuildPlacement,
    TickTimingSnapshot LastTick,
    TickTimingSnapshot AverageTick,
    double AverageMinerMsPerTrilobite,
    double AverageBuilderMsPerTrilobite,
    double AverageFarmerMsPerTrilobite,
    double AverageFighterMsPerTrilobite);

public static class GameAppDebugMetricsBuilder
{
    public static IReadOnlyList<string> BuildLines(GameAppDebugMetricsSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "RUN STATE",
            $"Paused: {(snapshot.Paused ? "Yes" : "No")}    Danger: {(snapshot.Danger ? "Yes" : "No")}    Tick: {snapshot.TickCount}",
            $"Tick Speed: {(int)snapshot.TickSpeedMs} ms",
            $"BFS View: {snapshot.ActiveBfsDebugField} (visible while paused)",
            $"Role Labels: {(snapshot.ShowRoleLabels ? "On" : "Off")}    No Cost Build: {(snapshot.NoCostBuildPlacement ? "On" : "Off")}",
            string.Empty,
            "PERFORMANCE",
            snapshot.LastTick.DescribeDominantWorkShort(),
            $"Miner role: {FormatRoleTimingMetric(snapshot.AverageMinerMsPerTrilobite, snapshot.LastTick.MinerTiming)}",
            $"Builder role: {FormatRoleTimingMetric(snapshot.AverageBuilderMsPerTrilobite, snapshot.LastTick.BuilderTiming)}",
            $"Farmer role: {FormatRoleTimingMetric(snapshot.AverageFarmerMsPerTrilobite, snapshot.LastTick.FarmerTiming)}",
            $"Fighter role: {FormatRoleTimingMetric(snapshot.AverageFighterMsPerTrilobite, snapshot.LastTick.FighterTiming)}",
            $"Avg ene: {snapshot.AverageTick.EnemyMoveMs:0.00} ms",
            $"Avg bld: {snapshot.AverageTick.BuildingTickMs:0.00} ms",
            $"Avg total: {snapshot.AverageTick.TotalMs:0.00} ms",
            $"Stats: Alloc {FormatByteCount(snapshot.LastTick.AllocatedBytes)}   GC {snapshot.LastTick.Gen0Collections}/{snapshot.LastTick.Gen1Collections}/{snapshot.LastTick.Gen2Collections}",
            $"Counts: {snapshot.LastTick.TrilobiteCount} tri  {snapshot.LastTick.EnemyCount} ene  {snapshot.LastTick.BuildingCount} bld"
        };

        return lines;
    }

    private static string FormatRoleTimingMetric(double averageMsPerTrilobite, RoleTimingSnapshot lastTiming)
    {
        return $"avg {averageMsPerTrilobite:0.00} ms/tri   last X{lastTiming.Count} = {lastTiming.TotalMs:0.00} ms";
    }

    private static string FormatByteCount(long byteCount)
    {
        if (byteCount >= 1024 * 1024)
        {
            return $"{byteCount / (1024d * 1024d):0.00} MB";
        }

        if (byteCount >= 1024)
        {
            return $"{byteCount / 1024d:0.0} KB";
        }

        return $"{byteCount} B";
    }
}
