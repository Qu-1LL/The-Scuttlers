using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Buildings;

public sealed class StorageTests
{
    [Fact]
    public void CategoryQueries_SelectTheLargestMatchingStoredResource()
    {
        var storage = new Storage(new GameSession());

        Assert.Equal(3, storage.Deposit(ResourceName.Sandstone, 3));
        Assert.Equal(7, storage.Deposit(ResourceName.Malachite, 7));

        Assert.Equal(10, storage.GetStoredAmount(ResourceCategory.Rock));

        var match = storage.FindStoredResource(ResourceRequirement.ForCategory(ResourceCategory.Rock, 5), 5);

        Assert.NotNull(match);
        Assert.Equal(ResourceName.Malachite, match.Value.ResourceType);
        Assert.Equal(5, match.Value.Amount);
    }
}
