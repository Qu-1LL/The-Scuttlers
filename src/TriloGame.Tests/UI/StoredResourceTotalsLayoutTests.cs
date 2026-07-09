using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.UI.Resources;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class StoredResourceTotalsLayoutTests
{
    [Fact]
    public void Build_PlacesPanelBelowSkillTreeButton_AndShowsOnlyPositiveResourcesInRarityOrder()
    {
        var viewport = new Point(1440, 900);
        var resources = new Dictionary<ResourceName, int>
        {
            [ResourceName.Sandstone] = 12,
            [ResourceName.Algae] = 4,
            [ResourceName.Magnetite] = 0,
            [ResourceName.Cochinium] = 1
        };

        var layout = StoredResourceTotalsLayout.Build(viewport, resources);
        var skillTreeButtonBounds = ResearchDraftLayout.GetSkillTreeButtonBounds(viewport);

        Assert.Equal(skillTreeButtonBounds.X, layout.PanelBounds.X);
        Assert.True(layout.PanelBounds.Top > skillTreeButtonBounds.Bottom);
        Assert.Equal(3, layout.Rows.Count);
        Assert.Equal(ResourceName.Algae, layout.Rows[0].ResourceType);
        Assert.Equal(ResourceName.Sandstone, layout.Rows[1].ResourceType);
        Assert.Equal(ResourceName.Cochinium, layout.Rows[2].ResourceType);
        Assert.All(layout.Rows, row => Assert.True(row.IconBounds.Left >= layout.PanelBounds.Left));
        Assert.All(layout.Rows, row => Assert.True(row.TextBounds.Left > row.IconBounds.Right));
    }

    [Fact]
    public void Build_ScalesWidthByLongestResourceName_AndHeightByUniqueResourceCount()
    {
        var viewport = new Point(1280, 800);
        var singleResource = new Dictionary<ResourceName, int>
        {
            [ResourceName.Algae] = 3
        };
        var mixedResources = new Dictionary<ResourceName, int>
        {
            [ResourceName.Algae] = 3,
            [ResourceName.Sandstone] = 1,
            [ResourceName.Magnetite] = 9
        };

        var singleLayout = StoredResourceTotalsLayout.Build(viewport, singleResource);
        var mixedLayout = StoredResourceTotalsLayout.Build(viewport, mixedResources);

        Assert.True(mixedLayout.PanelBounds.Width > singleLayout.PanelBounds.Width);
        Assert.True(mixedLayout.PanelBounds.Height > singleLayout.PanelBounds.Height);
        Assert.Single(singleLayout.Rows);
        Assert.Equal(3, mixedLayout.Rows.Count);
    }
}
