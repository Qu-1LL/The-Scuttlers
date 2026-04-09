using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Gum;

public enum GumTextStyle
{
    UiLarge,
    Ui,
    Small,
    Debug,
    Compact
}

public readonly record struct GumTextMetrics(int FontSize, float CharacterWidth, int LineHeight);

public static class GumTextLayout
{
    public static GumTextMetrics GetMetrics(GumTextStyle style)
    {
        return style switch
        {
            GumTextStyle.UiLarge => new GumTextMetrics(24, 13.2f, 30),
            GumTextStyle.Ui => new GumTextMetrics(21, 11.4f, 26),
            GumTextStyle.Debug => new GumTextMetrics(19, 10.2f, 24),
            GumTextStyle.Compact => new GumTextMetrics(14, 7.6f, 16),
            _ => new GumTextMetrics(18, 9.4f, 22)
        };
    }

    public static Point Measure(string text, GumTextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Point.Zero;
        }

        var metrics = GetMetrics(style);
        var lines = text.Replace("\r", string.Empty).Split('\n');
        var maxWidth = 0f;
        foreach (var line in lines)
        {
            maxWidth = MathF.Max(maxWidth, EstimateWidth(line, metrics));
        }

        return new Point(
            Math.Max(1, (int)MathF.Ceiling(maxWidth)),
            Math.Max(metrics.LineHeight, lines.Length * metrics.LineHeight));
    }

    public static string FitToWidth(string text, int maxWidth, GumTextStyle style)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
        {
            return string.Empty;
        }

        var metrics = GetMetrics(style);
        if (EstimateWidth(text, metrics) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        if (EstimateWidth(ellipsis, metrics) > maxWidth)
        {
            return string.Empty;
        }

        var endIndex = text.Length;
        while (endIndex > 0)
        {
            var candidate = $"{text[..endIndex].TrimEnd()}{ellipsis}";
            if (EstimateWidth(candidate, metrics) <= maxWidth)
            {
                return candidate;
            }

            endIndex--;
        }

        return ellipsis;
    }

    public static IReadOnlyList<string> Wrap(IEnumerable<string> paragraphs, int maxWidth, int maxLines, GumTextStyle style)
    {
        var metrics = GetMetrics(style);
        if (maxWidth <= 0 || maxLines <= 0)
        {
            return [];
        }

        var lines = new List<string>(Math.Min(maxLines, 8));
        var truncated = false;

        foreach (var paragraph in paragraphs)
        {
            var rawParagraph = paragraph ?? string.Empty;
            if (rawParagraph.Length == 0)
            {
                if (lines.Count < maxLines)
                {
                    lines.Add(string.Empty);
                }
                else
                {
                    truncated = true;
                }

                continue;
            }

            var words = rawParagraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                if (lines.Count < maxLines)
                {
                    lines.Add(string.Empty);
                }
                else
                {
                    truncated = true;
                }

                continue;
            }

            var current = words[0];
            for (var index = 1; index < words.Length; index++)
            {
                var candidate = $"{current} {words[index]}";
                if (EstimateWidth(candidate, metrics) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (lines.Count >= maxLines)
                {
                    truncated = true;
                    break;
                }

                lines.Add(FitToWidth(current, maxWidth, style));
                current = words[index];
            }

            if (truncated)
            {
                break;
            }

            if (lines.Count >= maxLines)
            {
                truncated = true;
                break;
            }

            lines.Add(FitToWidth(current, maxWidth, style));
        }

        if (truncated && lines.Count > 0)
        {
            lines[^1] = FitToWidth($"{lines[^1].TrimEnd()}...", maxWidth, style);
        }

        return lines;
    }

    private static float EstimateWidth(string text, GumTextMetrics metrics)
    {
        var width = 0f;
        foreach (var character in text)
        {
            width += EstimateCharacterWidth(character, metrics.CharacterWidth);
        }

        return width;
    }

    private static float EstimateCharacterWidth(char character, float baseWidth)
    {
        if (character == ' ')
        {
            return baseWidth * 0.42f;
        }

        if (char.IsDigit(character))
        {
            return baseWidth * 0.88f;
        }

        if (",.;:'!|".Contains(character))
        {
            return baseWidth * 0.38f;
        }

        if ("()[]{}".Contains(character))
        {
            return baseWidth * 0.5f;
        }

        if ("MW@#%&".Contains(character))
        {
            return baseWidth * 1.18f;
        }

        if (char.IsUpper(character))
        {
            return baseWidth * 1.02f;
        }

        return baseWidth * 0.82f;
    }
}
