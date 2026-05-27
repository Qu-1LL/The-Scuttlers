using System.Text;
using Microsoft.Xna.Framework;

namespace TriloGame.Game.Shared.Diagnostics;

public readonly record struct GameAppCrashDiagnosticsSnapshot(
    string AppScreen,
    bool Paused,
    bool GameOver,
    bool MainMenuOpen,
    bool DebugMenuOpen,
    bool SettingsMenuOpen,
    bool ResearchDraftOpen,
    bool TrilodexOpen,
    bool BuildMode,
    bool DebugAntHolePlacementMode,
    double TickSpeedMs,
    double TickAccumulatorMs,
    string ActiveBfsDebugField,
    bool DisableEnemySpawns,
    string TickTiming,
    string TickTimingAverage,
    Point Viewport,
    string CameraOrigin,
    double CameraScale,
    string CameraViewCenter,
    Point MousePoint,
    Point MouseDelta,
    bool Dragging,
    Point DragStart,
    bool CameraPanDragActive,
    bool SelectionDragActive,
    string KeysHeld,
    bool MenuPanelOpen,
    string MenuActiveTab,
    string MenuAssignmentFilter,
    int PendingResearchBranches,
    string SelectedObject,
    string SelectedTrilobites,
    string FloatingBuilding,
    string RoleRadialMenu,
    string SelectionBox,
    IReadOnlyList<string> SelectedMiningTiles,
    int TickCount,
    bool Danger,
    int DebugEnemyCount,
    string Resources,
    bool HasCave,
    int RevealedTiles,
    int ReachableTiles,
    int TrilobiteCount,
    int EnemyCount,
    int BuildingCount,
    string QueenSummary,
    string BuildingSummary,
    string TrilobiteSummary,
    string EnemySummary);

public static class GameAppCrashDiagnosticsBuilder
{
    public static string Build(GameAppCrashDiagnosticsSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[Game]");
        builder.AppendLine($"AppScreen: {snapshot.AppScreen}");
        builder.AppendLine($"Paused: {snapshot.Paused}");
        builder.AppendLine($"GameOver: {snapshot.GameOver}");
        builder.AppendLine($"MainMenuOpen: {snapshot.MainMenuOpen}");
        builder.AppendLine($"DebugMenuOpen: {snapshot.DebugMenuOpen}");
        builder.AppendLine($"SettingsMenuOpen: {snapshot.SettingsMenuOpen}");
        builder.AppendLine($"ResearchDraftOpen: {snapshot.ResearchDraftOpen}");
        builder.AppendLine($"TrilodexOpen: {snapshot.TrilodexOpen}");
        builder.AppendLine($"BuildMode: {snapshot.BuildMode}");
        builder.AppendLine($"DebugAntHolePlacementMode: {snapshot.DebugAntHolePlacementMode}");
        builder.AppendLine($"TickSpeedMs: {snapshot.TickSpeedMs}");
        builder.AppendLine($"TickAccumulatorMs: {snapshot.TickAccumulatorMs:0.###}");
        builder.AppendLine($"ActiveBfsDebugField: {snapshot.ActiveBfsDebugField}");
        builder.AppendLine($"DisableEnemySpawns: {snapshot.DisableEnemySpawns}");
        builder.AppendLine($"TickTiming: {snapshot.TickTiming}");
        builder.AppendLine($"TickTimingAverage: {snapshot.TickTimingAverage}");
        builder.AppendLine($"Viewport: {snapshot.Viewport.X}x{snapshot.Viewport.Y}");
        builder.AppendLine($"CameraOrigin: {snapshot.CameraOrigin}");
        builder.AppendLine($"CameraScale: {snapshot.CameraScale:0.###}");
        builder.AppendLine($"CameraViewCenter: {snapshot.CameraViewCenter}");
        builder.AppendLine();

        builder.AppendLine("[Input]");
        builder.AppendLine($"MousePoint: {snapshot.MousePoint.X}, {snapshot.MousePoint.Y}");
        builder.AppendLine($"MouseDelta: {snapshot.MouseDelta.X}, {snapshot.MouseDelta.Y}");
        builder.AppendLine($"Dragging: {snapshot.Dragging}");
        builder.AppendLine($"DragStart: {snapshot.DragStart.X}, {snapshot.DragStart.Y}");
        builder.AppendLine($"CameraPanDragActive: {snapshot.CameraPanDragActive}");
        builder.AppendLine($"SelectionDragActive: {snapshot.SelectionDragActive}");
        builder.AppendLine($"KeysHeld: {snapshot.KeysHeld}");
        builder.AppendLine();

        builder.AppendLine("[UI]");
        builder.AppendLine($"MenuPanelOpen: {snapshot.MenuPanelOpen}");
        builder.AppendLine($"MenuActiveTab: {snapshot.MenuActiveTab}");
        builder.AppendLine($"MenuAssignmentFilter: {snapshot.MenuAssignmentFilter}");
        builder.AppendLine($"PendingResearchBranches: {snapshot.PendingResearchBranches}");
        builder.AppendLine($"SelectedObject: {snapshot.SelectedObject}");
        builder.AppendLine($"SelectedTrilobites: {snapshot.SelectedTrilobites}");
        builder.AppendLine($"FloatingBuilding: {snapshot.FloatingBuilding}");
        builder.AppendLine($"RoleRadialMenu: {snapshot.RoleRadialMenu}");
        builder.AppendLine($"SelectionBox: {snapshot.SelectionBox}");
        builder.AppendLine($"SelectedMiningTiles: {JoinOrNone(snapshot.SelectedMiningTiles)}");
        builder.AppendLine();

        builder.AppendLine("[Session]");
        builder.AppendLine($"TickCount: {snapshot.TickCount}");
        builder.AppendLine($"Danger: {snapshot.Danger}");
        builder.AppendLine($"DebugEnemyCount: {snapshot.DebugEnemyCount}");
        builder.AppendLine($"Resources: {snapshot.Resources}");

        if (!snapshot.HasCave)
        {
            builder.AppendLine("Cave: null");
            return builder.ToString();
        }

        builder.AppendLine($"RevealedTiles: {snapshot.RevealedTiles}");
        builder.AppendLine($"ReachableTiles: {snapshot.ReachableTiles}");
        builder.AppendLine($"Trilobites: {snapshot.TrilobiteCount}");
        builder.AppendLine($"Enemies: {snapshot.EnemyCount}");
        builder.AppendLine($"Buildings: {snapshot.BuildingCount}");
        builder.AppendLine($"TickProfilerLast: {snapshot.TickTiming}");
        builder.AppendLine($"TickProfilerAvg: {snapshot.TickTimingAverage}");
        builder.AppendLine($"Queen: {snapshot.QueenSummary}");
        builder.AppendLine($"BuildingSummary: {snapshot.BuildingSummary}");
        builder.AppendLine($"TrilobiteSummary: {snapshot.TrilobiteSummary}");
        builder.AppendLine($"EnemySummary: {snapshot.EnemySummary}");
        return builder.ToString();
    }

    private static string JoinOrNone(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "none"
            : string.Join(", ", values);
    }
}
