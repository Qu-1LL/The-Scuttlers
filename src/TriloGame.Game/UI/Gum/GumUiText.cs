using Gum.Converters;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;

namespace TriloGame.Game.UI.Gum;

internal static class GumUiText
{
    public static void Add(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment verticalAlignment = VerticalAlignment.Center,
        int maxLines = 0)
    {
        Add(gumUi, parent: null, bounds, text, color, style, horizontalAlignment, verticalAlignment, maxLines);
    }

    public static void Add(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment verticalAlignment = VerticalAlignment.Center,
        int maxLines = 0)
    {
        if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var textStyle = GumTextStyleCatalog.Get(style);
        if (parent is null)
        {
            gumUi.AddText(
                bounds,
                text,
                color,
                horizontalAlignment,
                verticalAlignment,
                textStyle.FontSize,
                maxLines,
                textStyle.FontFamily,
                textStyle.CustomFontFile,
                textStyle.FontScale);
            return;
        }

        gumUi.AddText(
            parent,
            bounds,
            text,
            color,
            horizontalAlignment,
            verticalAlignment,
            textStyle.FontSize,
            maxLines,
            textStyle.FontFamily,
            textStyle.CustomFontFile,
            textStyle.FontScale);
    }

    public static void AddCentered(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 0)
    {
        AddCentered(gumUi, parent: null, bounds, text, color, style, maxLines);
    }

    public static void AddCentered(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 0)
    {
        Add(gumUi, parent, bounds, text, color, style, HorizontalAlignment.Center, VerticalAlignment.Center, maxLines);
    }

    public static void AddFittedCentered(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 1)
    {
        AddFittedCentered(gumUi, parent: null, bounds, text, color, style, maxLines);
    }

    public static void AddFittedCentered(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 1)
    {
        AddFitted(gumUi, parent, bounds, text, color, style, HorizontalAlignment.Center, VerticalAlignment.Center, maxLines);
    }

    public static void AddFittedLeft(
        GumUiRenderer gumUi,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        int maxLines = 1)
    {
        AddFitted(gumUi, parent: null, bounds, text, color, style, HorizontalAlignment.Left, VerticalAlignment.Center, maxLines);
    }

    private static void AddFitted(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string text,
        Color color,
        GumTextStyle style,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment,
        int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var textStyle = GumTextStyleCatalog.Get(style);
        var fittedWidth = Math.Max(1, (int)MathF.Floor(bounds.Width / MathF.Max(0.01f, textStyle.FontScale)));
        Add(
            gumUi,
            parent,
            bounds,
            GumTextLayout.FitToWidth(text, fittedWidth, style),
            color,
            style,
            horizontalAlignment,
            verticalAlignment,
            maxLines);
    }

}
