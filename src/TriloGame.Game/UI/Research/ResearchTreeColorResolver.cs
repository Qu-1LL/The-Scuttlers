using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeColorResolver
{
    private static readonly Color FallbackColor = new(180, 191, 199);

    public static Color GetBaseFeatureColor(GameSession session, string? sourceFeatureTreeName)
    {
        if (string.IsNullOrWhiteSpace(sourceFeatureTreeName))
        {
            return FallbackColor;
        }

        var featureTree = session.GetFeatureTree(sourceFeatureTreeName);
        if (featureTree?.DisplayColor is FeatureTreeColor displayColor)
        {
            return ToColor(displayColor);
        }

        if (featureTree is null || featureTree.FeaturesAffected.Count == 0)
        {
            return GetFeatureColorFromTreeName(sourceFeatureTreeName);
        }

        var red = 0f;
        var green = 0f;
        var blue = 0f;
        foreach (var featureName in featureTree.FeaturesAffected)
        {
            var featureColor = GetFeatureColor(featureName);
            red += featureColor.R;
            green += featureColor.G;
            blue += featureColor.B;
        }

        var divisor = featureTree.FeaturesAffected.Count;
        return new Color(
            (int)MathF.Round(red / divisor),
            (int)MathF.Round(green / divisor),
            (int)MathF.Round(blue / divisor));
    }

    private static Color ToColor(FeatureTreeColor color)
    {
        return new Color(color.R, color.G, color.B);
    }

    private static Color GetFeatureColorFromTreeName(string featureTreeName)
    {
        return featureTreeName switch
        {
            var name when name.StartsWith("B", StringComparison.Ordinal) => GetFeatureColor("building"),
            var name when name.StartsWith("C", StringComparison.Ordinal) => GetFeatureColor("combat"),
            var name when name.StartsWith("F", StringComparison.Ordinal) => GetFeatureColor("farming"),
            var name when name.StartsWith("M", StringComparison.Ordinal) => GetFeatureColor("mining"),
            _ => FallbackColor
        };
    }

    private static Color GetFeatureColor(string featureName)
    {
        return featureName switch
        {
            "building" => new Color(240, 88, 80),
            "combat" => new Color(78, 164, 233),
            "farming" => new Color(239, 214, 86),
            "mining" => new Color(189, 138, 94),
            _ => FallbackColor
        };
    }
}
