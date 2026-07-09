using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Runtime;

public sealed class ResourceStockpileSystemTests
{
    [Fact]
    public void Refresh_AggregatesStoredResourcesFromAllResourceStorageBuildings()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(14, 8);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 0));
        var storage = new Storage(session);
        var silo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(8, 0));
        Assert.True(cave.Build(storage, new GridPoint(5, 0)));
        post.Deposit(ResourceName.Sandstone, 12);
        post.Deposit(ResourceName.Lumenite, 3);
        storage.Deposit(ResourceName.Lumenite, 7);
        silo.Deposit(ResourceName.Algae, 9);
        var system = new ResourceStockpileSystem();

        var stockpile = system.Refresh(session);

        Assert.Equal(9, stockpile.GetAmount(ResourceName.Algae));
        Assert.Equal(12, stockpile.GetAmount(ResourceName.Sandstone));
        Assert.Equal(10, stockpile.GetAmount(ResourceName.Lumenite));
        Assert.Equal(
            [ResourceName.Algae, ResourceName.Sandstone, ResourceName.Lumenite],
            stockpile.Entries.Select(entry => entry.ResourceType).ToArray());
    }

    [Fact]
    public void Refresh_ReflectsWithdrawalsAndRemovedStorageImmediately()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(14, 8);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 0));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(5, 0)));
        post.Deposit(ResourceName.Sandstone, 12);
        storage.Deposit(ResourceName.Mycocore, 5);
        var system = new ResourceStockpileSystem();

        post.Withdraw(ResourceName.Sandstone, 7);
        cave.RemoveBuilding(storage);
        var stockpile = system.Refresh(session);

        Assert.Equal(5, stockpile.GetAmount(ResourceName.Sandstone));
        Assert.Equal(0, stockpile.GetAmount(ResourceName.Mycocore));
        Assert.Single(stockpile.Entries);
    }
}
