using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Research;

namespace TriloGame.Game.UI.Resources;

public static class StoredResourceTotalsLayout
{
    private const int PanelPadding = 10;
    private const int IconSize = 20;
    private const int IconTextGap = 6;
    private const int RowGap = 6;
    private const int PanelRadius = 14;
    private const int PanelBorderThickness = 2;
    private const int PanelTopGap = 10;

    public static StoredResourceTotalsLayoutInfo Build(Point viewport, IReadOnlyDictionary<string, int> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var entries = BuildVisibleEntries(resources);
        if (entries.Count == 0)
        {
            return StoredResourceTotalsLayoutInfo.Empty;
        }

        var textStyle = GumTextStyle.Small;
        var textMetrics = GumTextLayout.GetMetrics(textStyle);
        var rowHeight = Math.Max(IconSize, textMetrics.LineHeight);
        var maxMeasuredTextWidth = 0;
        foreach (var entry in entries)
        {
            var resourceNameWidth = GumTextLayout.Measure(entry.ResourceType, textStyle).X;
            var countWidth = GumTextLayout.Measure(entry.CountText, textStyle).X;
            maxMeasuredTextWidth = Math.Max(maxMeasuredTextWidth, Math.Max(resourceNameWidth, countWidth));
        }

        var buttonBounds = ResearchDraftLayout.GetSkillTreeButtonBounds(viewport);
        var panelWidth = (PanelPadding * 2) + IconSize + IconTextGap + maxMeasuredTextWidth;
        var panelHeight = (PanelPadding * 2) + (entries.Count * rowHeight) + (Math.Max(0, entries.Count - 1) * RowGap);
        var panelBounds = new Rectangle(
            buttonBounds.X,
            buttonBounds.Bottom + PanelTopGap,
            panelWidth,
            panelHeight);

        var rows = new List<StoredResourceTotalsRowLayout>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var rowTop = panelBounds.Y + PanelPadding + (index * (rowHeight + RowGap));
            var iconBounds = new Rectangle(
                panelBounds.X + PanelPadding,
                rowTop + Math.Max(0, (rowHeight - IconSize) / 2),
                IconSize,
                IconSize);
            var textBounds = new Rectangle(
                iconBounds.Right + IconTextGap,
                rowTop,
                Math.Max(1, panelBounds.Right - PanelPadding - (iconBounds.Right + IconTextGap)),
                rowHeight);
            rows.Add(new StoredResourceTotalsRowLayout(entries[index].ResourceType, entries[index].CountText, iconBounds, textBounds));
        }

        return new StoredResourceTotalsLayoutInfo(
            panelBounds,
            rows,
            PanelRadius,
            PanelBorderThickness);
    }

    private static List<StoredResourceEntry> BuildVisibleEntries(IReadOnlyDictionary<string, int> resources)
    {
        var entries = new List<StoredResourceEntry>();
        var includedResourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ore in OreType.GetOres())
        {
            var count = resources.GetValueOrDefault(ore.Name, 0);
            if (count <= 0)
            {
                continue;
            }

            entries.Add(new StoredResourceEntry(ore.Name, count.ToString()));
            includedResourceTypes.Add(ore.Name);
        }

        foreach (var pair in resources.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value <= 0 || !includedResourceTypes.Add(pair.Key))
            {
                continue;
            }

            entries.Add(new StoredResourceEntry(pair.Key, pair.Value.ToString()));
        }

        return entries;
    }

    private readonly record struct StoredResourceEntry(string ResourceType, string CountText);
}

public readonly record struct StoredResourceTotalsLayoutInfo(
    Rectangle PanelBounds,
    IReadOnlyList<StoredResourceTotalsRowLayout> Rows,
    int PanelRadius,
    int PanelBorderThickness)
{
    public static StoredResourceTotalsLayoutInfo Empty => new(Rectangle.Empty, [], 0, 0);
}

public readonly record struct StoredResourceTotalsRowLayout(
    string ResourceType,
    string CountText,
    Rectangle IconBounds,
    Rectangle TextBounds);
