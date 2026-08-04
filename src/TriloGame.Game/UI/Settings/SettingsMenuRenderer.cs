using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Rendering;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Settings;

public sealed class SettingsMenuRenderer
{
    private static readonly GumUiFrameStyle PanelFrameStyle = new(UiPalette.SurfaceOverlay, UiPalette.BorderPanel, 3, 16);
    private static readonly GumUiFrameStyle TopHudOpenFrameStyle = new(UiPalette.SurfaceSelected, UiPalette.BorderFocus, 2, 14);
    private static readonly GumUiButtonStyle TopHudButtonStyle = new(
        new GumUiFrameStyle(UiPalette.SurfaceRaised, UiPalette.BorderControl, 2, 14),
        new GumUiFrameStyle(UiPalette.SurfaceRaisedHover, UiPalette.BorderHover, 2, 14),
        UiPalette.TextPrimary,
        GumTextStyle.Small);
    private static readonly GumUiButtonStyle ChromeButtonStyle = new(
        new GumUiFrameStyle(UiPalette.SurfaceControl, UiPalette.BorderControlStrong, 2, 10),
        new GumUiFrameStyle(UiPalette.SurfaceControlHover, UiPalette.BorderHoverStrong, 2, 10),
        UiPalette.TextPrimary);
    private static readonly GumUiButtonStyle TrilodexButtonStyle = new(
        new GumUiFrameStyle(UiPalette.AccentGold, UiPalette.AccentGoldBorder, 2, 12),
        new GumUiFrameStyle(UiPalette.AccentGoldHover, UiPalette.AccentGoldBorderHover, 2, 12),
        UiPalette.TextOnAccent,
        GumTextStyle.Small);
    // Selected display mode reads as an active toggle rather than a pressable button.
    private static readonly GumUiButtonStyle DisplayModeSelectedStyle = new(
        new GumUiFrameStyle(UiPalette.SurfaceSelected, UiPalette.BorderFocus, 2, 10),
        new GumUiFrameStyle(UiPalette.SurfaceSelectedHover, UiPalette.BorderHoverStrong, 2, 10),
        UiPalette.TextPrimary,
        GumTextStyle.Small);
    private static readonly GumUiButtonStyle ReturnToMenuButtonStyle = new(
        new GumUiFrameStyle(UiPalette.AccentGreen, UiPalette.AccentGreenBorder, 2, 12),
        new GumUiFrameStyle(UiPalette.AccentGreenHover, UiPalette.AccentGreenBorderHover, 2, 12),
        UiPalette.TextPrimary,
        GumTextStyle.Small);

    public void Draw(
        GumUiRenderer gumUiRenderer,
        RenderingContext rendering,
        Point viewport,
        Point pointer,
        bool isOpen,
        bool isMainMenuOpen,
        int volumePercent,
        bool musicEnabled,
        GameDisplayMode displayMode,
        GameResolution resolution,
        bool canStepResolutionDown = true,
        bool canStepResolutionUp = true)
    {
        if (!isMainMenuOpen)
        {
            DrawTopHudButton(gumUiRenderer, viewport, pointer, isOpen);
        }

        if (!isOpen)
        {
            return;
        }

        DrawPanel(
            gumUiRenderer,
            rendering,
            viewport,
            pointer,
            isMainMenuOpen,
            volumePercent,
            musicEnabled,
            displayMode,
            resolution,
            canStepResolutionDown,
            canStepResolutionUp);
    }

    private static void DrawTopHudButton(GumUiRenderer gumUiRenderer, Point viewport, Point pointer, bool isOpen)
    {
        var buttonBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var buttonHovered = buttonBounds.Contains(pointer);
        GumUiChrome.DrawFrame(gumUiRenderer, buttonBounds, isOpen ? TopHudOpenFrameStyle : (buttonHovered ? TopHudButtonStyle.HoverFrame : TopHudButtonStyle.NormalFrame));
        DrawGearIcon(gumUiRenderer, new Rectangle(buttonBounds.X + 12, buttonBounds.Y + 10, 24, 24), UiPalette.TextPrimary);
        GumUiText.AddFittedCentered(
            gumUiRenderer,
            new Rectangle(buttonBounds.X + 40, buttonBounds.Y, buttonBounds.Width - 46, buttonBounds.Height),
            "Settings",
            UiPalette.TextPrimary,
            GumTextStyle.Small);
    }

    private static void DrawPanel(
        GumUiRenderer gumUiRenderer,
        RenderingContext rendering,
        Point viewport,
        Point pointer,
        bool isMainMenuOpen,
        int volumePercent,
        bool musicEnabled,
        GameDisplayMode displayMode,
        GameResolution resolution,
        bool canStepResolutionDown,
        bool canStepResolutionUp)
    {
        var includeQuitToMainMenu = !isMainMenuOpen;
        var layout = SettingsMenuLayout.BuildPanel(viewport, includeQuitToMainMenu);

        gumUiRenderer.AddFilledRectangle(
            new Rectangle(0, 0, viewport.X, viewport.Y),
            isMainMenuOpen ? UiPalette.ScrimForMainMenu : UiPalette.ScrimForGameplay);
        GumUiChrome.DrawFrame(gumUiRenderer, layout.Panel, PanelFrameStyle);

        DrawChromeButton(gumUiRenderer, layout.Close, layout.Close.Contains(pointer), "X", GumTextStyle.Small);
        GumUiText.AddFittedCentered(gumUiRenderer, layout.Title, "Settings", UiPalette.TextPrimary, GumTextStyle.Ui);
        GumUiText.AddFittedCentered(
            gumUiRenderer,
            layout.VolumeValue,
            $"Volume: {Math.Clamp(volumePercent, 0, 100)}%",
            UiPalette.TextSecondary,
            GumTextStyle.Small);

        DrawChromeButton(gumUiRenderer, layout.VolumeDown, layout.VolumeDown.Contains(pointer), "-", GumTextStyle.Ui);
        DrawChromeButton(gumUiRenderer, layout.VolumeUp, layout.VolumeUp.Contains(pointer), "+", GumTextStyle.Ui);
        DrawVolumeBar(
            gumUiRenderer,
            layout.VolumeBar,
            SettingsMenuLayout.GetVolumeFillBounds(layout.VolumeBar, volumePercent),
            layout.VolumeBar.Contains(pointer));
        DrawMusicToggle(gumUiRenderer, layout.MusicToggle, layout.MusicCheckbox, layout.MusicToggle.Contains(pointer), musicEnabled);
        DrawDisplayModeRow(gumUiRenderer, layout, pointer, displayMode);
        DrawResolutionRow(gumUiRenderer, layout, pointer, displayMode, resolution, canStepResolutionDown, canStepResolutionUp);
        GumUiChrome.DrawButton(gumUiRenderer, layout.Trilodex, "Trilodex", layout.Trilodex.Contains(pointer), TrilodexButtonStyle);

        if (includeQuitToMainMenu)
        {
            GumUiChrome.DrawButton(
                gumUiRenderer,
                layout.ReturnToMainMenu,
                "Return To Main Menu",
                layout.ReturnToMainMenu.Contains(pointer),
                ReturnToMenuButtonStyle);
        }

        // DismissHint is laid out but deliberately not drawn - it was not drawn before this refactor
        // either, and a refactor is the wrong place to add visible text. The row is kept so the
        // footer reserves its space and Back has somewhere to sit.
        DrawBackButton(gumUiRenderer, rendering, layout.Back, layout.Back.Contains(pointer));
    }

    private static void DrawChromeButton(GumUiRenderer gumUiRenderer, Rectangle bounds, bool hovered, string label, GumTextStyle textStyle)
    {
        GumUiChrome.DrawButton(gumUiRenderer, bounds, label, hovered, ChromeButtonStyle with { TextStyle = textStyle });
    }

    private static void DrawVolumeBar(GumUiRenderer gumUiRenderer, Rectangle bounds, Rectangle fillBounds, bool hovered)
    {
        gumUiRenderer.AddRoundedFrame(
            bounds,
            hovered ? UiPalette.SurfaceBase : UiPalette.SurfaceSunken,
            hovered ? UiPalette.BorderHover : UiPalette.BorderPanel,
            2,
            12);
        gumUiRenderer.AddRoundedRectangle(fillBounds, UiPalette.BorderFocus, 10);
    }

    private static void DrawMusicToggle(GumUiRenderer gumUiRenderer, Rectangle bounds, Rectangle checkboxBounds, bool hovered, bool musicEnabled)
    {
        gumUiRenderer.AddRoundedFrame(
            checkboxBounds,
            musicEnabled ? UiPalette.AccentGreen : UiPalette.SurfaceSunken,
            hovered ? UiPalette.BorderHoverStrong : UiPalette.BorderControlStrong,
            2,
            6);

        if (musicEnabled)
        {
            gumUiRenderer.AddLine(
                new Vector2(checkboxBounds.X + 7, checkboxBounds.Y + 15),
                new Vector2(checkboxBounds.X + 12, checkboxBounds.Bottom - 8),
                UiPalette.TextPrimary,
                3);
            gumUiRenderer.AddLine(
                new Vector2(checkboxBounds.X + 12, checkboxBounds.Bottom - 8),
                new Vector2(checkboxBounds.Right - 6, checkboxBounds.Y + 7),
                UiPalette.TextPrimary,
                3);
        }

        GumUiText.AddFittedLeft(
            gumUiRenderer,
            new Rectangle(checkboxBounds.Right + 12, bounds.Y, Math.Max(0, bounds.Right - checkboxBounds.Right - 12), bounds.Height),
            "Music",
            UiPalette.TextPrimary,
            GumTextStyle.Small);
    }

    private static void DrawDisplayModeRow(
        GumUiRenderer gumUiRenderer,
        SettingsPanelLayout layout,
        Point pointer,
        GameDisplayMode displayMode)
    {
        GumUiText.AddFittedLeft(
            gumUiRenderer,
            layout.DisplayModeLabel,
            "Display",
            UiPalette.TextSecondary,
            GumTextStyle.Small);

        var isFullscreen = displayMode == GameDisplayMode.Fullscreen;
        var unselected = ChromeButtonStyle with { TextStyle = GumTextStyle.Small };

        GumUiChrome.DrawButton(
            gumUiRenderer,
            layout.Fullscreen,
            "Fullscreen",
            layout.Fullscreen.Contains(pointer),
            isFullscreen ? DisplayModeSelectedStyle : unselected);
        GumUiChrome.DrawButton(
            gumUiRenderer,
            layout.Windowed,
            "Windowed",
            layout.Windowed.Contains(pointer),
            isFullscreen ? unselected : DisplayModeSelectedStyle);
    }

    // Resolution is a windowed-only setting: fullscreen is borderless at the desktop resolution, so
    // there is nothing to pick. Rather than hide the row on that mode - which would move everything
    // below it every time the player toggles - the row stays put and goes inert, and the label says
    // why. A control that disappears reads as a bug; one that greys out reads as a rule.
    private static void DrawResolutionRow(
        GumUiRenderer gumUiRenderer,
        SettingsPanelLayout layout,
        Point pointer,
        GameDisplayMode displayMode,
        GameResolution resolution,
        bool canStepDown,
        bool canStepUp)
    {
        var isWindowed = displayMode == GameDisplayMode.Windowed;
        GumUiText.AddFittedLeft(
            gumUiRenderer,
            layout.ResolutionLabel,
            isWindowed ? "Resolution" : "Resolution (windowed only)",
            isWindowed ? UiPalette.TextSecondary : UiPalette.DisabledTextStrong,
            GumTextStyle.Small);

        var arrowStyle = ChromeButtonStyle with { TextStyle = GumTextStyle.Ui };
        // Greyed rather than omitted at the ends of the list, so the player can see there is no
        // larger or smaller size instead of wondering why a tap did nothing. The disabled treatment
        // now comes from the shared chrome rather than a bespoke draw path.
        GumUiChrome.DrawButton(
            gumUiRenderer,
            layout.ResolutionDown,
            "<",
            layout.ResolutionDown.Contains(pointer),
            arrowStyle,
            enabled: isWindowed && canStepDown);
        GumUiChrome.DrawButton(
            gumUiRenderer,
            layout.ResolutionUp,
            ">",
            layout.ResolutionUp.Contains(pointer),
            arrowStyle,
            enabled: isWindowed && canStepUp);

        gumUiRenderer.AddRoundedFrame(
            layout.ResolutionValue,
            UiPalette.SurfaceBase,
            isWindowed ? UiPalette.BorderValue : UiPalette.DisabledValueBorder,
            2,
            10);
        GumUiText.AddFittedCentered(
            gumUiRenderer,
            layout.ResolutionValue,
            resolution.Label,
            isWindowed ? UiPalette.TextPrimary : UiPalette.DisabledValueText,
            GumTextStyle.Small);
    }

    private static void DrawBackButton(GumUiRenderer gumUiRenderer, RenderingContext rendering, Rectangle bounds, bool hovered)
    {
        gumUiRenderer.AddRoundedFrame(
            bounds,
            hovered ? UiPalette.SurfaceControlHover : UiPalette.SurfaceControl,
            hovered ? UiPalette.BorderHoverStrong : UiPalette.BorderControlStrong,
            2,
            12);
        if (!rendering.Sprites.TryGet("BackArrow", out var backArrowTexture))
        {
            return;
        }

        gumUiRenderer.AddSprite(
            new Rectangle(bounds.X + 9, bounds.Y + 7, Math.Max(0, bounds.Width - 18), Math.Max(0, bounds.Height - 14)),
            backArrowTexture,
            UiPalette.TextPrimary);
    }

    private static void DrawGearIcon(GumUiRenderer gumUiRenderer, Rectangle bounds, Color color)
    {
        var center = new Vector2(bounds.Center.X, bounds.Center.Y);
        var outerRadius = Math.Min(bounds.Width, bounds.Height) * 0.45f;
        var innerRadius = outerRadius * 0.52f;
        const int toothCount = 8;

        for (var index = 0; index < toothCount; index++)
        {
            var angle = MathHelper.TwoPi * index / toothCount;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            gumUiRenderer.AddLine(
                center + (direction * innerRadius),
                center + (direction * outerRadius),
                color,
                3);
        }

        for (var index = 0; index < toothCount; index++)
        {
            var angle = MathHelper.TwoPi * index / toothCount;
            var nextAngle = MathHelper.TwoPi * (index + 1) / toothCount;
            var from = center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * innerRadius);
            var to = center + (new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * innerRadius);
            gumUiRenderer.AddLine(from, to, color, 2);
        }
    }
}
