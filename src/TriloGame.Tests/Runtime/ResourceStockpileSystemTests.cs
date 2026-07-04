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
        post.Deposit(OreType.SANDSTONE.Name, 12);
        post.Deposit(OreType.LUMENITE.Name, 3);
        storage.Deposit(OreType.LUMENITE.Name, 7);
        silo.Deposit(OreType.ALGAE.Name, 9);
        var system = new ResourceStockpileSystem();

        var stockpile = system.Refresh(session);

        Assert.Equal(9, stockpile.GetAmount(OreType.ALGAE.Name));
        Assert.Equal(12, stockpile.GetAmount(OreType.SANDSTONE.Name));
        Assert.Equal(10, stockpile.GetAmount(OreType.LUMENITE.Name));
        Assert.Equal(
            [OreType.ALGAE.Name, OreType.SANDSTONE.Name, OreType.LUMENITE.Name],
            stockpile.Entries.Select(entry => entry.ResourceType).ToArray());
    }

    [Fact]
    public void Refresh_ReflectsWithdrawalsAndRemovedStorageImmediately()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(14, 8);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(0, 0));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(5, 0)));
        post.Deposit(OreType.SANDSTONE.Name, 12);
        storage.Deposit(OreType.MYCOCORE.Name, 5);
        var system = new ResourceStockpileSystem();

        post.Withdraw(OreType.SANDSTONE.Name, 7);
        cave.RemoveBuilding(storage);
        var stockpile = system.Refresh(session);

        Assert.Equal(5, stockpile.GetAmount(OreType.SANDSTONE.Name));
        Assert.Equal(0, stockpile.GetAmount(OreType.MYCOCORE.Name));
        Assert.Single(stockpile.Entries);
    }
}
