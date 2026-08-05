using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Input;

// SDL's window-relative position preserves platform window insets that MonoGame's global-to-client
// conversion can lose in borderless fullscreen modes.
internal static class SdlWindowPointer
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetMouseStateDelegate(out int x, out int y);

    private static readonly GetMouseStateDelegate? GetMouseState = LoadGetMouseState();

    public static Point GetPosition(Point fallback)
    {
        if (GetMouseState is null)
        {
            return fallback;
        }

        GetMouseState(out var x, out var y);
        return new Point(x, y);
    }

    // MonoGame dynamically loads SDL, so resolve this function from MonoGame's existing handle to
    // avoid opening a second SDL instance with a separate input state.
    private static GetMouseStateDelegate? LoadGetMouseState()
    {
        try
        {
            var sdlType = typeof(Microsoft.Xna.Framework.Game).Assembly.GetType("Sdl", throwOnError: false);
            var libraryField = sdlType?.GetField(
                "NativeLibrary",
                BindingFlags.Public | BindingFlags.Static);
            if (libraryField?.GetValue(null) is not IntPtr libraryHandle || libraryHandle == IntPtr.Zero)
            {
                return null;
            }

            var function = NativeLibrary.GetExport(libraryHandle, "SDL_GetMouseState");
            return Marshal.GetDelegateForFunctionPointer<GetMouseStateDelegate>(function);
        }
        catch (Exception exception) when (
            exception is TypeInitializationException or
            MissingFieldException or
            DllNotFoundException or
            EntryPointNotFoundException or
            BadImageFormatException)
        {
            return null;
        }
    }
}
