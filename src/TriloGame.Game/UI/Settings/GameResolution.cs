namespace TriloGame.Game.UI.Settings;

public readonly record struct GameResolution(int Width, int Height)
{
    public string Label => $"{Width} x {Height}";
}

// The window sizes the settings menu offers, and the stepping between them.
//
// These are WINDOWED sizes. Fullscreen is borderless at the desktop resolution and deliberately does
// not switch display modes (see GameApp.ConfigureDisplayMode), so there is no resolution to choose
// there - the desktop already picked one.
public static class GameResolutions
{
    // Ordered smallest first, which is what makes stepping monotonic. 16:9 and 16:10 both appear
    // because the game letterboxes nothing: the camera simply shows more or less world, so an
    // unusual aspect ratio costs the player nothing.
    private static readonly GameResolution[] PresetList =
    [
        new(1280, 720),
        new(1366, 768),
        new(1440, 900),
        new(1600, 900),
        new(1680, 1050),
        new(1920, 1080),
        new(2560, 1440)
    ];

    public static IReadOnlyList<GameResolution> Presets => PresetList;

    // The default, and the size the game falls back to whenever nothing larger fits.
    public static GameResolution Default => new(1440, 900);

    // Presets that fit on the given desktop, largest-fitting always included.
    //
    // Filtered rather than clamped because a window larger than the desktop cannot be moved back
    // into view on Windows - its title bar ends up off-screen, and with the title bar now present in
    // windowed mode (which is the whole point of having it) that is the one thing the player uses to
    // recover. Never returns empty: the smallest preset is kept even on a tiny desktop, since a
    // window that is slightly too large still beats no way to pick a size at all.
    public static IReadOnlyList<GameResolution> GetSelectable(int desktopWidth, int desktopHeight)
    {
        var fitting = new List<GameResolution>(PresetList.Length);
        foreach (var preset in PresetList)
        {
            if (preset.Width <= desktopWidth && preset.Height <= desktopHeight)
            {
                fitting.Add(preset);
            }
        }

        return fitting.Count > 0 ? fitting : [PresetList[0]];
    }

    // Where a resolution sits in a list, or the closest entry when it is not a preset at all - which
    // is the normal case once the window has been dragged to an arbitrary size. Compared on total
    // pixels so a custom 1500x950 lands between its neighbours rather than snapping to whichever
    // happens to share a width.
    public static int GetNearestIndex(IReadOnlyList<GameResolution> resolutions, GameResolution resolution)
    {
        if (resolutions.Count == 0)
        {
            return -1;
        }

        var bestIndex = 0;
        var bestDistance = long.MaxValue;
        for (var index = 0; index < resolutions.Count; index++)
        {
            var candidate = resolutions[index];
            if (candidate == resolution)
            {
                return index;
            }

            var distance = Math.Abs(((long)candidate.Width * candidate.Height) - ((long)resolution.Width * resolution.Height));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    // One step up or down the list, clamped at both ends rather than wrapping.
    //
    // Wrapping would send a player who taps past the largest size straight to the smallest, which on
    // a big monitor is a jarring change to undo. Clamping makes the ends of the list feel like ends.
    //
    // Stepping from a CUSTOM size (a dragged window) moves to the neighbour of the nearest preset,
    // not to the nearest preset itself, so a step always visibly changes the window.
    public static GameResolution Step(
        IReadOnlyList<GameResolution> resolutions,
        GameResolution current,
        int direction)
    {
        if (resolutions.Count == 0)
        {
            return current;
        }

        var index = GetNearestIndex(resolutions, current);
        var isExactMatch = resolutions[index] == current;
        // From a custom size, stepping toward the nearest preset should land ON it rather than skip
        // past: the preset is already a change of size in that direction.
        if (!isExactMatch)
        {
            var nearest = resolutions[index];
            var nearestIsLarger = (long)nearest.Width * nearest.Height > (long)current.Width * current.Height;
            if ((direction > 0 && nearestIsLarger) || (direction < 0 && !nearestIsLarger))
            {
                return nearest;
            }
        }

        return resolutions[Math.Clamp(index + Math.Sign(direction), 0, resolutions.Count - 1)];
    }

    public static bool CanStep(
        IReadOnlyList<GameResolution> resolutions,
        GameResolution current,
        int direction)
    {
        return resolutions.Count > 0 && Step(resolutions, current, direction) != current;
    }
}
