using Microsoft.Xna.Framework;
using TriloGame.Game.Shared.Diagnostics;

namespace TriloGame.Tests.Diagnostics;

public sealed class GameAppCrashDiagnosticsBuilderTests
{
    [Fact]
    public void Build_IncludesSectionHeadersAndNullCaveMarker()
    {
        var snapshot = new GameAppCrashDiagnosticsSnapshot(
            AppScreen: "Gameplay",
            Paused: true,
            GameOver: false,
            MainMenuOpen: false,
            DebugMenuOpen: false,
            SettingsMenuOpen: true,
            ResearchDraftOpen: false,
            TrilodexOpen: false,
            BuildMode: false,
            DebugAntHolePlacementMode: false,
            TickSpeedMs: 100d,
            TickAccumulatorMs: 12.5d,
            ActiveBfsDebugField: "enemy",
            DisableEnemySpawns: false,
            TickTiming: "last: total 1.00 ms",
            TickTimingAverage: "avg: total 1.25 ms",
            Viewport: new Point(1440, 900),
            CameraOrigin: "10, 20",
            CameraScale: 1.5d,
            CameraViewCenter: "30, 40",
            MousePoint: new Point(5, 6),
            MouseDelta: new Point(1, 2),
            Dragging: true,
            DragStart: new Point(7, 8),
            CameraPanDragActive: false,
            SelectionDragActive: true,
            KeysHeld: "A, D",
            MenuPanelOpen: true,
            MenuActiveTab: "selected",
            MenuAssignmentFilter: "miner",
            PendingResearchBranches: 2,
            SelectedObject: "Trilobite:Jeffery",
            SelectedTrilobites: "Jeffery:miner@(1,1)",
            FloatingBuilding: "none",
            RoleRadialMenu: "closed",
            SelectionBox: "none",
            SelectedMiningTiles: ["1,1", "1,2"],
            TickCount: 42,
            Danger: true,
            DebugEnemyCount: 3,
            Resources: "algae=5, sandstone=12",
            HasCave: false,
            RevealedTiles: 0,
            ReachableTiles: 0,
            TrilobiteCount: 0,
            EnemyCount: 0,
            BuildingCount: 0,
            QueenSummary: "missing",
            BuildingSummary: "none",
            TrilobiteSummary: "none",
            EnemySummary: "none");

        var output = GameAppCrashDiagnosticsBuilder.Build(snapshot);

        Assert.Contains("[Game]", output);
        Assert.Contains("[Input]", output);
        Assert.Contains("[UI]", output);
        Assert.Contains("[Session]", output);
        Assert.Contains("SelectedMiningTiles: 1,1, 1,2", output);
        Assert.Contains("Cave: null", output);
    }
}
