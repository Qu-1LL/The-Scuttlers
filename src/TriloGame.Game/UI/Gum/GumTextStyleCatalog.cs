namespace TriloGame.Game.UI.Gum;

public readonly record struct GumTextStyleSpec(
    string FontFamily,
    int FontSize,
    float CharacterWidth,
    int LineHeight,
    string? CustomFontFile,
    float FontScale);

public static class GumTextStyleCatalog
{
    public const string DefaultFontFamily = "Trebuchet MS";
    public const string DisplayFontFile = "Fonts/Display/TrebuchetDisplay48.fnt";

    public static GumTextStyleSpec Get(GumTextStyle style)
    {
        var metrics = GumTextLayout.GetMetrics(style);
        var customFontFile = style == GumTextStyle.Display ? DisplayFontFile : null;
        return new GumTextStyleSpec(
            DefaultFontFamily,
            metrics.FontSize,
            metrics.CharacterWidth,
            metrics.LineHeight,
            CustomFontFile: customFontFile,
            FontScale: 1f);
    }
}
