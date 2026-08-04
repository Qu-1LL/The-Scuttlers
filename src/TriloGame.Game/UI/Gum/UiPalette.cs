using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Gum;

// The UI's colours, named by the job they do rather than by where they happen to be used.
//
// These were 331 inline `new Color(r, g, b)` literals across 195 distinct values, so the same panel
// blue was written out six times in six files. Nothing tied them together: a contrast pass or a
// retheme meant a cross-file find-and-replace with no way to tell whether you had caught them all.
//
// Named by ROLE, deliberately. A name like SurfaceRaised survives someone deciding the UI should be
// warmer; a name like DarkBlue does not, and the second time it drifts you are back where you
// started. Where a value genuinely is a one-off accent - the Trilodex gold, the return-to-menu green
// - it lives here too, under the name of the thing it belongs to.
public static class UiPalette
{
    // ---- Surfaces --------------------------------------------------------------------------------
    //
    // Three depths, darkest first. Panels sit on Overlay, content frames sit inside panels, and
    // controls sit inside those. Keeping them ordered by lightness is what makes a nested panel read
    // as nested rather than as a differently coloured box.
    public static readonly Color SurfaceOverlay = new(8, 19, 29, 247);
    public static readonly Color SurfaceSunken = new(10, 22, 32);
    public static readonly Color SurfaceBase = new(13, 30, 42);
    public static readonly Color SurfacePanel = new(13, 28, 40);
    public static readonly Color SurfaceRaised = new(16, 38, 54);
    public static readonly Color SurfaceRaisedHover = new(22, 50, 71);
    public static readonly Color SurfaceControl = new(22, 44, 60);
    public static readonly Color SurfaceControlHover = new(36, 64, 82);
    public static readonly Color SurfaceSelected = new(27, 65, 88);
    public static readonly Color SurfaceSelectedHover = new(34, 78, 104);

    // ---- Borders ---------------------------------------------------------------------------------
    public static readonly Color BorderPanel = new(77, 122, 140);
    public static readonly Color BorderSubtle = new(35, 56, 72);
    public static readonly Color BorderContent = new(53, 84, 102);
    public static readonly Color BorderControl = new(54, 88, 107);
    public static readonly Color BorderControlStrong = new(110, 149, 167);
    public static readonly Color BorderHover = new(125, 179, 196);
    public static readonly Color BorderHoverStrong = new(188, 221, 234);
    public static readonly Color BorderFocus = new(163, 217, 235);
    public static readonly Color BorderValue = new(96, 137, 155);

    // ---- Text ------------------------------------------------------------------------------------
    public static readonly Color TextPrimary = Color.White;
    public static readonly Color TextBody = new(226, 238, 244);
    public static readonly Color TextSecondary = new(216, 232, 239);
    public static readonly Color TextMuted = new(210, 228, 236);
    public static readonly Color TextLabel = new(159, 195, 210);
    public static readonly Color TextCaption = new(135, 173, 187);
    public static readonly Color TextOnAccent = new(18, 26, 34);

    // ---- Disabled --------------------------------------------------------------------------------
    //
    // A control that is present but inert. Distinct from muted TEXT: this is the whole control going
    // quiet, which is why it has its own surface, border and text rather than reusing dim variants of
    // the active ones - reusing them made a disabled button read as merely unhovered.
    public static readonly Color DisabledSurface = new(15, 30, 40);
    public static readonly Color DisabledBorder = new(52, 72, 84);
    public static readonly Color DisabledText = new(88, 108, 120);
    public static readonly Color DisabledTextStrong = new(126, 149, 161);
    public static readonly Color DisabledValueText = new(140, 162, 173);
    public static readonly Color DisabledValueBorder = new(56, 78, 90);

    // ---- Accents ---------------------------------------------------------------------------------
    public static readonly Color AccentGold = new(152, 125, 74);
    public static readonly Color AccentGoldHover = new(180, 147, 92);
    public static readonly Color AccentGoldBorder = new(233, 201, 143);
    public static readonly Color AccentGoldBorderHover = new(255, 229, 170);
    // The cost readout in the build preview: warm enough to read as a number worth weighing against
    // the stockpile rather than as another caption.
    public static readonly Color AccentCost = new(232, 205, 138);

    public static readonly Color AccentGreen = new(61, 92, 76);
    public static readonly Color AccentGreenHover = new(82, 113, 96);
    public static readonly Color AccentGreenBorder = new(129, 170, 149);
    public static readonly Color AccentGreenBorderHover = new(185, 230, 204);

    // ---- Scrims ----------------------------------------------------------------------------------
    //
    // How far the world behind a panel is dimmed. The main menu dims harder than an in-game panel
    // because there is no gameplay behind it worth keeping legible.
    public static Color ScrimForMainMenu => new(0, 0, 0, 180);
    public static Color ScrimForGameplay => new(0, 0, 0, 96);
}
