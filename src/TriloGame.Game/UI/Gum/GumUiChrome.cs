using Microsoft.Xna.Framework;
using Gum.GueDeriving;

namespace TriloGame.Game.UI.Gum;

internal readonly record struct GumUiFrameStyle(
    Color Fill,
    Color Border,
    int Thickness = 2,
    int Radius = 12);

internal readonly record struct GumUiButtonStyle(
    GumUiFrameStyle NormalFrame,
    GumUiFrameStyle HoverFrame,
    Color TextColor,
    GumTextStyle TextStyle = GumTextStyle.Ui,
    Color? HoverTextColor = null,
    int MaxLines = 1,
    // A control that is present but cannot be used. Optional so existing styles need no change:
    // anything that never disables simply leaves these null and DrawButton's `enabled` defaults true.
    //
    // Falls back to the shared disabled palette rather than to a dimmed NormalFrame, because dimming
    // the normal frame reads as "not hovered" rather than "not available" - the two have to be
    // distinguishable at a glance or the player keeps clicking.
    GumUiFrameStyle? DisabledFrame = null,
    Color? DisabledTextColor = null)
{
    public GumUiFrameStyle ResolveDisabledFrame()
    {
        return DisabledFrame ?? new GumUiFrameStyle(
            UiPalette.DisabledSurface,
            UiPalette.DisabledBorder,
            NormalFrame.Thickness,
            NormalFrame.Radius);
    }

    public Color ResolveDisabledTextColor()
    {
        return DisabledTextColor ?? UiPalette.DisabledText;
    }

    public GumUiFrameStyle ResolveFrame(bool hovered, bool enabled)
    {
        if (!enabled)
        {
            return ResolveDisabledFrame();
        }

        return hovered ? HoverFrame : NormalFrame;
    }

    public Color ResolveTextColor(bool hovered, bool enabled)
    {
        if (!enabled)
        {
            return ResolveDisabledTextColor();
        }

        return hovered ? HoverTextColor ?? TextColor : TextColor;
    }
}

internal static class GumUiChrome
{
    public static void DrawFrame(GumUiRenderer gumUi, Rectangle bounds, GumUiFrameStyle style)
    {
        DrawFrame(gumUi, parent: null, bounds, style);
    }

    public static void DrawFrame(GumUiRenderer gumUi, ContainerRuntime? parent, Rectangle bounds, GumUiFrameStyle style)
    {
        if (parent is null)
        {
            gumUi.AddRoundedFrame(bounds, style.Fill, style.Border, style.Thickness, style.Radius);
            return;
        }

        gumUi.AddRoundedFrame(parent, bounds, style.Fill, style.Border, style.Thickness, style.Radius);
    }

    public static void DrawButton(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        bool hovered,
        GumUiButtonStyle style,
        bool enabled = true)
    {
        DrawButton(gumUi, parent: null, bounds, text, hovered, style, enabled);
    }

    public static void DrawButton(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        bool hovered,
        GumUiButtonStyle style,
        bool enabled = true)
    {
        // A disabled control never shows its hover treatment, whatever the pointer is doing - a
        // button that lights up and then refuses to act is worse than one that stays quiet.
        DrawFrame(gumUi, parent, bounds, style.ResolveFrame(hovered, enabled));
        GumUiText.AddFittedCentered(
            gumUi,
            parent,
            bounds,
            text,
            style.ResolveTextColor(hovered, enabled),
            style.TextStyle,
            style.MaxLines);
    }
}
