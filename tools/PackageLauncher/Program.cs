using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TriloGame.PackageLauncher;

internal static class Program
{
    public static int Main()
    {
        var gamePath = Path.Combine(AppContext.BaseDirectory, "GameFiles", "TriloGame.Game.exe");
        if (!File.Exists(gamePath))
        {
            ShowMessage(
                "The game files could not be found. Keep the GameFiles folder next to this launcher.",
                "The Scuttlers");
            return 1;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = gamePath,
                WorkingDirectory = Path.GetDirectoryName(gamePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false
            });
        }
        catch (Exception exception)
        {
            ShowMessage($"The game could not be launched.\n\n{exception.Message}", "The Scuttlers");
            return 1;
        }

        return 0;
    }

    private static void ShowMessage(string text, string caption)
    {
        _ = MessageBoxW(IntPtr.Zero, text, caption, 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
