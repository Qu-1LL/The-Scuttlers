using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Hud;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class ResourceHudTests
{
    [Fact]
    public void BuildEntries_UsesCatalogTextureKeysIncludingAlgaeIcon()
    {
        var stockpile = new ResourceStockpileSnapshot(
        [
            new ResourceStockpileEntry(ResourceName.Algae, 5),
            new ResourceStockpileEntry(ResourceName.Sandstone, 8),
            new ResourceStockpileEntry(ResourceName.Lumenite, 3)
        ]);

        var entries = ResourceHudModelBuilder.BuildEntries(stockpile);

        Assert.Equal("SoilTile_Algae_3", entries[0].TextureKey);
        Assert.Equal(OreType.SANDSTONE.Name, entries[1].TextureKey);
        Assert.Equal(OreType.LUMENITE.Name, entries[2].TextureKey);
    }

    [Fact]
    public void Build_PlacesResourceStackBelowSettingsButtonAndBuildsHoverTooltip()
    {
        ResourceHudEntryModel[] entries =
        [
            new(OreType.SANDSTONE.Name, 8, OreType.SANDSTONE.Name),
            new(OreType.LUMENITE.Name, 3, OreType.LUMENITE.Name)
        ];
        var viewport = new Point(1280, 800);
        var settingsBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var firstLayout = ResourceHudLayout.Build(viewport, entries, Point.Zero).Items[0];
        var pointer = firstLayout.Bounds.Center;

        var layout = ResourceHudLayout.Build(viewport, entries, pointer);

        Assert.Equal(settingsBounds.X, layout.PanelBounds.X);
        Assert.Equal(settingsBounds.Bottom + SettingsMenuLayout.TopHudButtonGap, layout.PanelBounds.Y);
        Assert.Equal(settingsBounds.Width, layout.PanelBounds.Width);
        Assert.Equal(2, layout.Items.Count);
        Assert.True(layout.PanelBounds.Contains(layout.Items[0].Bounds));
        Assert.True(layout.PanelBounds.Contains(layout.Items[1].Bounds));
        Assert.Equal(layout.Items[0].Bounds.X, layout.Items[1].Bounds.X);
        Assert.True(layout.Items[1].Bounds.Y > layout.Items[0].Bounds.Bottom);
        Assert.True(layout.Items[0].Bounds.Contains(layout.Items[0].IconBounds));
        Assert.True(layout.Items[0].Bounds.Contains(layout.Items[0].AmountBounds));
        Assert.True(layout.Items[0].IsHovered);
        Assert.NotNull(layout.Tooltip);
        Assert.Equal(OreType.SANDSTONE.Name, layout.Tooltip!.Value.ResourceType);
    }
}
