using TriloGame.Game.Core.Economy;

namespace TriloGame.Tests.Core;

public sealed class ResourceCostComparerTests
{
    [Fact]
    public void HasRequiredResources_ReturnsTrue_WhenEveryCostIsCovered()
    {
        var available = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["sandstone"] = 120,
            ["magnetite"] = 8
        };
        var costs = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Sandstone"] = 100,
            ["Magnetite"] = 8
        };

        var result = ResourceCostComparer.HasRequiredResources(available, costs);

        Assert.True(result);
        Assert.False(ResourceCostComparer.TryFindFirstShortfall(available, costs, out _));
    }

    [Fact]
    public void TryFindFirstShortfall_ReturnsMissingResourceAndAmount()
    {
        var available = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sandstone"] = 35
        };
        var costs = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Sandstone"] = 50
        };

        var hasShortfall = ResourceCostComparer.TryFindFirstShortfall(available, costs, out var shortfall);

        Assert.True(hasShortfall);
        Assert.Equal("Sandstone", shortfall.ResourceType);
        Assert.Equal(15, shortfall.MissingAmount);
    }
}
