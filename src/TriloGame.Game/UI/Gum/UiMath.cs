namespace TriloGame.Game.UI.Gum;

public static class UiMath
{
    // Clamp where the MINIMUM yields to the maximum instead of throwing.
    //
    // UI layout is full of "at least this big, but never bigger than the space available", and the
    // space available can legitimately fall below the preferred minimum on a small window. Math.Clamp
    // rejects min > max with an ArgumentException rather than picking a side, so writing that idiom
    // directly turns a merely cramped panel into a crash - which is exactly what happened to the
    // settings menu's scrollbar thumb when a viewport got shorter than the minimum thumb height.
    //
    // Every existing site that got this right did so by hand-wrapping the minimum in a Math.Min or
    // the maximum in a Math.Max (see DebugMenuLayout, ResourceHud, MenuController.Layout). This is
    // that same guard, named, so the next site does not have to rediscover it.
    public static int ClampAtMost(int value, int preferredMinimum, int maximum)
    {
        return Math.Clamp(value, Math.Min(preferredMinimum, maximum), maximum);
    }

    public static float ClampAtMost(float value, float preferredMinimum, float maximum)
    {
        return Math.Clamp(value, MathF.Min(preferredMinimum, maximum), maximum);
    }
}
