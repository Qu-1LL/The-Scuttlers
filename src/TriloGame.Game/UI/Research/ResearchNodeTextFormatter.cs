using System.Text;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.UI.Research;

internal static class ResearchNodeTextFormatter
{
    public static ResearchNodeInfo BuildNodeInfo(GameSession session, TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return BuildNodeInfo(session, node.Name, node.Description, node.SourceFeatureTreeName, node.EffectDescriptors);
    }

    public static ResearchNodeInfo BuildNodeInfo(GameSession session, ResearchTreeViewNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return BuildNodeInfo(session, node.Name, node.Description, node.SourceFeatureTreeName, node.EffectDescriptors);
    }

    public static string BuildNodeAffectText(GameSession session, TreeInstanceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return BuildNodeAffectText(session, node.Description, node.SourceFeatureTreeName, node.EffectDescriptors);
    }

    public static string BuildNodeAffectText(GameSession session, ResearchTreeViewNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return BuildNodeAffectText(session, node.Description, node.SourceFeatureTreeName, node.EffectDescriptors);
    }

    private static ResearchNodeInfo BuildNodeInfo(
        GameSession session,
        string title,
        string description,
        string? sourceFeatureTreeName,
        IReadOnlyList<ResearchEffectDescriptor> effectDescriptors)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new ResearchNodeInfo(
            title,
            string.IsNullOrWhiteSpace(sourceFeatureTreeName) ? "Core" : sourceFeatureTreeName,
            BuildNodeAffectText(session, description, sourceFeatureTreeName, effectDescriptors));
    }

    private static string BuildNodeAffectText(
        GameSession session,
        string description,
        string? sourceFeatureTreeName,
        IReadOnlyList<ResearchEffectDescriptor> effectDescriptors)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (effectDescriptors.Count > 0)
        {
            var parts = new List<string>(effectDescriptors.Count);
            foreach (var descriptor in effectDescriptors)
            {
                parts.Add(FormatEffectDescriptor(descriptor));
            }

            return string.Join(", ", parts);
        }

        if (!string.IsNullOrWhiteSpace(sourceFeatureTreeName))
        {
            var featureTree = session.GetFeatureTree(sourceFeatureTreeName);
            if (featureTree is not null && featureTree.FeaturesAffected.Count > 0)
            {
                return BuildFeatureAffectLabel(featureTree);
            }
        }

        return description;
    }

    private static string BuildFeatureAffectLabel(FeatureTree featureTree)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < featureTree.FeaturesAffected.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(FormatFeatureName(featureTree.FeaturesAffected[index]));
        }

        return builder.ToString();
    }

    private static string FormatFeatureName(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return "Unknown";
        }

        var trimmed = featureName.Trim();
        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string FormatEffectDescriptor(ResearchEffectDescriptor descriptor)
    {
        var builder = new StringBuilder();
        builder.Append(descriptor.Operation switch
        {
            ResearchOperation.AddFlat => $"+{descriptor.Value:0.##} ",
            ResearchOperation.AddPercent => $"+{descriptor.Value * 100d:0.##}% ",
            ResearchOperation.Multiply => $"x{descriptor.Value:0.##} ",
            ResearchOperation.Set => $"Set to {descriptor.Value:0.##} ",
            _ => string.Empty
        });
        builder.Append(descriptor.StatKey);

        if (descriptor.TargetKind != ResearchTargetKind.Global)
        {
            builder.Append(" (");
            builder.Append(descriptor.TargetKind);
            if (!string.IsNullOrWhiteSpace(descriptor.TargetKey))
            {
                builder.Append(": ");
                builder.Append(descriptor.TargetKey);
            }

            builder.Append(')');
        }

        return builder.ToString();
    }
}
