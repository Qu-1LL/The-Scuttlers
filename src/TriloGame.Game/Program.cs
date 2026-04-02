using TriloGame.Game;
using TriloGame.Game.Shared.Diagnostics;

CrashReporter.InstallProcessHandlers();

using var game = new GameApp();
CrashReporter.RegisterSnapshotProvider(game.BuildCrashDiagnostics);

try
{
    game.Run();
}
catch (Exception exception)
{
    CrashReporter.Report(exception, "Program.Main");
    throw;
}
