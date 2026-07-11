using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Rendering;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Game.UI.Hud;

public sealed class ResourceHudRenderer
{
    private static readonly GumUiFrameStyle PanelFrame = new(new Color(7, 15, 22, 205), new Color(92, 143, 164, 220), 2, 12);
    private static readonly GumUiFrameStyle TooltipFrame = new(new Color(7, 15, 22, 238), new Color(143, 205, 226), 2, 10);

    public void Draw(
        GumUiRenderer gumUi,
        SpriteFactory sprites,
        Point viewport,
        Point pointer,
        ResourceStockpileSnapshot stockpile)
    {
        var entries = ResourceHudModelBuilder.BuildEntries(stockpile);
        var layout = ResourceHudLayout.Build(viewport, entries, pointer);
        if (layout.PanelBounds.IsEmpty)
        {
            return;
        }

        GumUiChrome.DrawFrame(gumUi, layout.PanelBounds, PanelFrame);
        foreach (var item in layout.Items)
        {
            DrawItem(gumUi, sprites, item);
        }

        if (layout.Tooltip is ResourceHudTooltipLayout tooltip)
        {
            GumUiChrome.DrawFrame(gumUi, tooltip.Bounds, TooltipFrame);
            GumUiText.AddFittedCentered(gumUi, tooltip.TextBounds, tooltip.ResourceType, Color.White, GumTextStyle.Small);
        }
    }

    private static void DrawItem(GumUiRenderer gumUi, SpriteFactory sprites, ResourceHudItemLayout item)
    {
        if (sprites.TryGet(item.Entry.TextureKey, out var texture))
        {
            gumUi.AddSprite(FitTexture(texture.Width, texture.Height, item.IconBounds), texture);
        }
        else
        {
            GumUiText.AddFittedCentered(gumUi, item.IconBounds, item.Entry.ResourceType[..1], Color.White, GumTextStyle.Small);
        }

        GumUiText.Add(
            gumUi,
            item.AmountBounds,
            item.Entry.Amount.ToString(),
            Color.White,
            GumTextStyle.Small,
            HorizontalAlignment.Right,
            VerticalAlignment.Center,
            maxLines: 1);
    }

    private static Rectangle FitTexture(int textureWidth, int textureHeight, Rectangle bounds)
    {
        var scale = MathF.Min(bounds.Width / (float)textureWidth, bounds.Height / (float)textureHeight);
        var width = Math.Max(1, (int)MathF.Round(textureWidth * scale));
        var height = Math.Max(1, (int)MathF.Round(textureHeight * scale));
        return new Rectangle(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }
}

internal static class ResourceHudModelBuilder
{
    public static IReadOnlyList<ResourceHudEntryModel> BuildEntries(ResourceStockpileSnapshot stockpile)
    {
        if (stockpile.Entries.Count == 0)
        {
            return [];
        }

        var entries = new List<ResourceHudEntryModel>(stockpile.Entries.Count);
        foreach (var entry in stockpile.Entries)
        {
            if (entry.Amount <= 0)
            {
                continue;
            }

            entries.Add(new ResourceHudEntryModel(
                ItemCatalog.GetName(entry.ResourceType),
                entry.Amount,
                ItemCatalog.GetTextureKey(entry.ResourceType)));
        }

        return entries;
    }
}

internal static class ResourceHudLayout
{
    private const int PanelPadding = 8;
    private const int ItemWidth = SettingsMenuLayout.TopHudButtonWidth - (PanelPadding * 2);
    private const int ItemHeight = 34;
    private const int ItemGap = 8;

    public static ResourceHudLayoutInfo Build(
        Point viewport,
        IReadOnlyList<ResourceHudEntryModel> entries,
        Point pointer)
    {
        if (entries.Count == 0 || viewport.X <= 0 || viewport.Y <= 0)
        {
            return ResourceHudLayoutInfo.Empty;
        }

        var settingsButtonBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var contentHeight = (entries.Count * ItemHeight) + (Math.Max(0, entries.Count - 1) * ItemGap);
        var panelBounds = new Rectangle(
            settingsButtonBounds.X,
            settingsButtonBounds.Bottom + SettingsMenuLayout.TopHudButtonGap,
            SettingsMenuLayout.TopHudButtonWidth,
            contentHeight + (PanelPadding * 2));
        var items = new List<ResourceHudItemLayout>(entries.Count);
        ResourceHudTooltipLayout? tooltip = null;
        var x = panelBounds.X + PanelPadding;
        var y = panelBounds.Y + PanelPadding;
        for (var index = 0; index < entries.Count; index++)
        {
            var itemBounds = new Rectangle(x, y, ItemWidth, ItemHeight);
            var iconBounds = new Rectangle(itemBounds.X, itemBounds.Y + 3, 28, 28);
            var amountBounds = new Rectangle(iconBounds.Right + 8, itemBounds.Y, itemBounds.Right - iconBounds.Right - 8, ItemHeight);
            var hovered = itemBounds.Contains(pointer);
            var item = new ResourceHudItemLayout(entries[index], itemBounds, iconBounds, amountBounds, hovered);
            items.Add(item);

            if (hovered)
            {
                tooltip = BuildTooltip(item.Entry.ResourceType, itemBounds, viewport);
            }

            y += ItemHeight + ItemGap;
        }

        return new ResourceHudLayoutInfo(panelBounds, items, tooltip);
    }

    private static ResourceHudTooltipLayout BuildTooltip(string resourceType, Rectangle anchorBounds, Point viewport)
    {
        var width = Math.Clamp((resourceType.Length * 9) + 24, 96, 180);
        var height = 34;
        var margin = SettingsMenuLayout.TopHudButtonGap;
        var x = anchorBounds.Right + margin;
        if (x + width > viewport.X - margin)
        {
            x = anchorBounds.X - width - margin;
        }

        x = Math.Clamp(x, margin, Math.Max(margin, viewport.X - width - margin));
        var y = Math.Clamp(anchorBounds.Center.Y - (height / 2), margin, Math.Max(margin, viewport.Y - height - margin));
        if (y + height > viewport.Y - margin)
        {
            y = anchorBounds.Y - height - 6;
        }

        var bounds = new Rectangle(x, y, width, height);
        return new ResourceHudTooltipLayout(
            resourceType,
            bounds,
            new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 16, bounds.Height - 8));
    }
}

internal readonly record struct ResourceHudEntryModel(string ResourceType, int Amount, string TextureKey);

internal readonly record struct ResourceHudLayoutInfo(
    Rectangle PanelBounds,
    IReadOnlyList<ResourceHudItemLayout> Items,
    ResourceHudTooltipLayout? Tooltip)
{
    public static ResourceHudLayoutInfo Empty { get; } = new(Rectangle.Empty, [], null);
}

internal readonly record struct ResourceHudItemLayout(
    ResourceHudEntryModel Entry,
    Rectangle Bounds,
    Rectangle IconBounds,
    Rectangle AmountBounds,
    bool IsHovered);

internal readonly record struct ResourceHudTooltipLayout(
    string ResourceType,
    Rectangle Bounds,
    Rectangle TextBounds);
