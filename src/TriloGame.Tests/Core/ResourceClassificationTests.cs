using TriloGame.Game.Core.Economy;

namespace TriloGame.Tests.Core;

public sealed class ResourceClassificationTests
{
    [Fact]
    public void GloopProducts_HaveOrganicPlantAndFoodClassifications()
    {
        var meal = ItemCatalog.Get(ResourceName.GloopMeal);
        var pie = ItemCatalog.Get(ResourceName.GloopPie);

        Assert.Equal(ResourceClassificationValues.Organic, meal.GetClassification(ResourceClassificationKeys.ResourceType));
        Assert.Equal(ResourceClassificationValues.Gloop, meal.GetClassification(ResourceClassificationKeys.PlantType));
        Assert.Equal(ResourceClassificationValues.Meal, meal.GetClassification(ResourceClassificationKeys.FoodType));
        Assert.Equal(ResourceClassificationValues.Pie, pie.GetClassification(ResourceClassificationKeys.FoodType));
        Assert.Equal(ResourceCategory.Organic, ItemCatalog.GetCategory(ResourceName.GloopPie));
    }

    [Fact]
    public void MissingClassification_IsASafeNonMatch()
    {
        Assert.Null(ItemCatalog.GetClassification(ResourceName.Chitinstone, ResourceClassificationKeys.FoodType));
        Assert.False(ItemCatalog.HasClassification(
            ResourceName.Chitinstone,
            ResourceClassificationKeys.FoodType,
            ResourceClassificationValues.Meal));
    }

    [Fact]
    public void RelatedPlantResources_KeepThePlantTypeWhenChangingFoodForm()
    {
        Assert.True(ItemCatalog.TryGetRelatedPlantResource(
            ResourceName.Gloop,
            ResourceClassificationValues.Meal,
            out var meal));
        Assert.Equal(ResourceName.GloopMeal, meal);

        Assert.True(ItemCatalog.TryGetRelatedPlantResource(
            ResourceName.GloopMeal,
            ResourceClassificationValues.Pie,
            out var pie));
        Assert.Equal(ResourceName.GloopPie, pie);
    }
}
