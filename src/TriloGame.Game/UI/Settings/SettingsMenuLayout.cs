using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Settings;

// Every control in the settings panel, in one place.
//
// Built once by BuildPanel and read from the resulting record, rather than each control deriving its
// own absolute offset from the panel. The offsets were the problem: fifteen `panelBounds.Y + N`
// constants, where inserting a row meant editing every one below it plus the panel's total height,
// with overlap the only symptom of a miss. The rows now flow, so a row cannot be positioned
// independently of the ones above it, and the panel's height is measured from its contents instead
// of being a magic number kept in step by hand.
public readonly record struct SettingsPanelLayout(
    Rectangle Panel,
    Rectangle Close,
    Rectangle Title,
    Rectangle VolumeValue,
    Rectangle VolumeDown,
    Rectangle VolumeBar,
    Rectangle VolumeUp,
    Rectangle MusicToggle,
    Rectangle MusicCheckbox,
    Rectangle DisplayModeLabel,
    Rectangle Fullscreen,
    Rectangle Windowed,
    Rectangle ResolutionLabel,
    Rectangle ResolutionDown,
    Rectangle ResolutionValue,
    Rectangle ResolutionUp,
    Rectangle Trilodex,
    Rectangle ReturnToMainMenu,
    Rectangle Back,
    Rectangle DismissHint);

public static class SettingsMenuLayout
{
    public const int VolumeStep = 5;
    public const int TopHudButtonWidth = 132;
    public const int TopHudButtonHeight = 44;
    public const int TopHudButtonGap = 12;

    // Row heights and gaps, named once. These are the only vertical numbers in the panel now;
    // everything else is a consequence of stacking them.
    private const int PanelInset = 24;
    private const int TitleHeight = 26;
    private const int LabelHeight = 20;
    private const int ButtonHeight = 34;
    private const int ActionHeight = 38;
    private const int StepperEndWidth = 40;
    private const int RowGap = 10;
    private const int SectionGap = 16;
    private const int HintHeight = 18;
    private const int BackWidth = 42;
    private const int BackHeight = 32;

    public static Rectangle GetSettingsButtonBounds(Point viewport)
    {
        return GetTopHudButtonBounds(viewport, 0);
    }

    public static Rectangle GetTopHudButtonBounds(Point viewport, int index)
    {
        var safeIndex = Math.Max(0, index);
        return new Rectangle(
            18 + ((TopHudButtonWidth + TopHudButtonGap) * safeIndex),
            18,
            TopHudButtonWidth,
            TopHudButtonHeight);
    }

    public static SettingsPanelLayout BuildPanel(Point viewport)
    {
        return BuildPanel(viewport, includeQuitToMainMenu: true);
    }

    // The panel is measured, then centred.
    //
    // Two passes over the same row sequence: the first stacks from the origin purely to learn the
    // height, the second stacks again inside the centred rectangle. It has to be that way round -
    // centring needs the height, and the height is only known once the rows have been laid out - and
    // it is why the height cannot drift from the contents the way a hand-maintained total did.
    public static SettingsPanelLayout BuildPanel(Point viewport, bool includeQuitToMainMenu)
    {
        var width = Math.Min(420, Math.Max(320, viewport.X - 56));
        var measured = StackRows(new Rectangle(0, 0, width, int.MaxValue / 2), includeQuitToMainMenu);
        var height = measured.ConsumedHeight + PanelInset;
        var panel = new Rectangle((viewport.X - width) / 2, (viewport.Y - height) / 2, width, height);
        return StackRows(panel, includeQuitToMainMenu).ToLayout(panel);
    }

    public static Rectangle GetPanelBounds(Point viewport)
    {
        return BuildPanel(viewport).Panel;
    }

    public static Rectangle GetPanelBounds(Point viewport, bool includeQuitToMainMenu)
    {
        return BuildPanel(viewport, includeQuitToMainMenu).Panel;
    }

    // The row sequence. Reading top to bottom is reading the panel top to bottom, which is the whole
    // point of expressing it this way.
    private static StackedRows StackRows(Rectangle panel, bool includeQuitToMainMenu)
    {
        var stack = new StackLayout(panel, PanelInset, topInset: 16);
        var title = stack.Row(TitleHeight);
        var volumeValue = stack.Row(LabelHeight + 10, RowGap);

        var volumeRow = stack.Row(ButtonHeight + 6, RowGap);
        var (volumeDown, volumeBarCell, volumeUp) = StackLayout.Stepper(volumeRow, StepperEndWidth, gap: 12);
        // The bar is thinner than its row and vertically centred in it, so the two end buttons stay
        // full height while the track reads as a track.
        var volumeBar = new Rectangle(
            volumeBarCell.X,
            volumeBarCell.Y + ((volumeBarCell.Height - 18) / 2),
            volumeBarCell.Width,
            18);

        var musicToggle = stack.Row(ButtonHeight, SectionGap);
        var musicCheckbox = new Rectangle(musicToggle.X, musicToggle.Y + 3, 28, 28);

        var displayModeLabel = stack.Row(LabelHeight, SectionGap);
        var displayModeRow = stack.Row(ButtonHeight, 4);
        var displayColumns = StackLayout.Columns(displayModeRow, 2, gap: 8);

        var resolutionLabel = stack.Row(LabelHeight, SectionGap);
        var resolutionRow = stack.Row(ButtonHeight, 4);
        var (resolutionDown, resolutionValue, resolutionUp) = StackLayout.Stepper(resolutionRow, StepperEndWidth);

        var trilodex = stack.Row(ActionHeight, SectionGap);
        var returnToMainMenu = includeQuitToMainMenu
            ? stack.Row(ActionHeight, RowGap)
            : Rectangle.Empty;

        // Footer: the dismiss hint with the back button beside it.
        //
        // Back used to be pinned to the panel's bottom-right corner, which worked only because the
        // hand-maintained panel height left slack below the last row. Measuring the panel from its
        // rows removed that slack and the corner landed on top of Return To Main Menu - caught
        // immediately by UiRegionMap.FindOverlaps, which is the reason for having it. Giving Back a
        // row means the panel reserves space for it like anything else.
        var footer = stack.Row(BackHeight, SectionGap);
        var back = new Rectangle(footer.Right - BackWidth, footer.Y, BackWidth, BackHeight);
        var dismissHint = new Rectangle(
            footer.X,
            footer.Y + ((BackHeight - HintHeight) / 2),
            Math.Max(0, back.Left - 8 - footer.X),
            HintHeight);

        return new StackedRows(
            stack.ConsumedHeight,
            title,
            volumeValue,
            volumeDown,
            volumeBar,
            volumeUp,
            musicToggle,
            musicCheckbox,
            displayModeLabel,
            displayColumns[0],
            displayColumns[1],
            resolutionLabel,
            resolutionDown,
            resolutionValue,
            resolutionUp,
            trilodex,
            returnToMainMenu,
            back,
            dismissHint);
    }

    private readonly record struct StackedRows(
        int ConsumedHeight,
        Rectangle Title,
        Rectangle VolumeValue,
        Rectangle VolumeDown,
        Rectangle VolumeBar,
        Rectangle VolumeUp,
        Rectangle MusicToggle,
        Rectangle MusicCheckbox,
        Rectangle DisplayModeLabel,
        Rectangle Fullscreen,
        Rectangle Windowed,
        Rectangle ResolutionLabel,
        Rectangle ResolutionDown,
        Rectangle ResolutionValue,
        Rectangle ResolutionUp,
        Rectangle Trilodex,
        Rectangle ReturnToMainMenu,
        Rectangle Back,
        Rectangle DismissHint)
    {
        // Close is the one control still pinned to a panel corner rather than flowing: it sits in the
        // title bar's own space, which the title row already reserves.
        public SettingsPanelLayout ToLayout(Rectangle panel)
        {
            return new SettingsPanelLayout(
                panel,
                new Rectangle(panel.Right - 50, panel.Y + 14, 34, 34),
                Title,
                VolumeValue,
                VolumeDown,
                VolumeBar,
                VolumeUp,
                MusicToggle,
                MusicCheckbox,
                DisplayModeLabel,
                Fullscreen,
                Windowed,
                ResolutionLabel,
                ResolutionDown,
                ResolutionValue,
                ResolutionUp,
                Trilodex,
                ReturnToMainMenu,
                Back,
                DismissHint);
        }
    }

    public static int GetSnappedVolumeFromBar(Rectangle barBounds, int pointerX)
    {
        if (barBounds.Width <= 1)
        {
            return 0;
        }

        var ratio = Math.Clamp((pointerX - barBounds.Left) / (float)barBounds.Width, 0f, 1f);
        var raw = (int)MathF.Round(ratio * 100f);
        return Math.Clamp((int)MathF.Round(raw / (float)VolumeStep) * VolumeStep, 0, 100);
    }

    public static Rectangle GetVolumeFillBounds(Rectangle barBounds, int volumePercent)
    {
        var width = Math.Max(0, (int)MathF.Round(barBounds.Width * (Math.Clamp(volumePercent, 0, 100) / 100f)));
        return new Rectangle(barBounds.X, barBounds.Y, width, barBounds.Height);
    }
}
