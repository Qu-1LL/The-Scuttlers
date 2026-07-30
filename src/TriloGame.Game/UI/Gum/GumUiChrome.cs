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
    int MaxLines = 1);

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
        GumUiButtonStyle style)
    {
        DrawButton(gumUi, parent: null, bounds, text, hovered, style);
    }

    public static void DrawButton(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        bool hovered,
        GumUiButtonStyle style)
    {
        DrawFrame(gumUi, parent, bounds, hovered ? style.HoverFrame : style.NormalFrame);
        GumUiText.AddFittedCentered(
            gumUi,
            parent,
            bounds,
            text,
            hovered ? style.HoverTextColor ?? style.TextColor : style.TextColor,
            style.TextStyle,
            style.MaxLines);
    }
}
