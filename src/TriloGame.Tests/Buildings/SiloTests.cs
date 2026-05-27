using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class SiloTests
{
    [Fact]
    public void SiloStoresOnlyAlgaeAndUpdatesSessionTotals()
    {
        var session = new GameSession();
        var silo = new Silo(session);

        Assert.IsAssignableFrom<IStorage>(silo);
        Assert.Equal(5000, silo.Capacity);
        Assert.Equal(2500, silo.Deposit(OreType.ALGAE.Name, 2500));
        Assert.Equal(0, silo.Deposit(OreType.SANDSTONE.Name, 25));
        Assert.Equal(2500, silo.GetInventoryTotal());
        Assert.Equal(2500, session.GetStoredResourceTotal(OreType.ALGAE.Name));

        Assert.Equal(400, silo.Withdraw(OreType.ALGAE.Name, 400));
        Assert.Equal(2100, silo.GetInventoryTotal());
        Assert.Equal(2100, session.GetStoredResourceTotal(OreType.ALGAE.Name));
    }

    [Fact]
    public void NewSiloPullsAlgaeFromAdjacentSiloNetworkWhenBuilt()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        var leftSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(4, 6));

        Assert.Equal(60, leftSilo.Deposit(OreType.ALGAE.Name, 60));

        var rightSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(6, 6));

        Assert.Equal(30, leftSilo.GetInventoryTotal());
        Assert.Equal(30, rightSilo.GetInventoryTotal());
    }

    [Fact]
    public void GarageSplitsIncomingAlgaeBetweenAdjacentSilos()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 16, new GridPoint(0, 0));
        var leftSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(4, 6));
        var garage = TestWorldFactory.BuildGarage(cave, session, new GridPoint(6, 6));
        var rightSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(8, 6));

        Assert.Equal(40, garage.Deposit(OreType.ALGAE.Name, 40));
        Assert.Equal(5, garage.Deposit(OreType.SANDSTONE.Name, 5));

        Assert.Equal(0, garage.GetInventory().GetValueOrDefault(OreType.ALGAE.Name, 0));
        Assert.Equal(5, garage.GetInventory().GetValueOrDefault(OreType.SANDSTONE.Name, 0));
        Assert.Equal(20, leftSilo.GetInventoryTotal());
        Assert.Equal(20, rightSilo.GetInventoryTotal());
    }

    [Fact]
    public void ConnectedSilosRebalanceAfterWithdrawal()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(28, 16, new GridPoint(0, 0));
        var leftSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(4, 6));
        var middleSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(6, 6));
        var rightSilo = TestWorldFactory.BuildSilo(cave, session, new GridPoint(8, 6));

        Assert.Equal(90, leftSilo.Deposit(OreType.ALGAE.Name, 90));
        Assert.Equal(15, leftSilo.Withdraw(OreType.ALGAE.Name, 15));

        Assert.Equal(75, leftSilo.GetInventoryTotal() + middleSilo.GetInventoryTotal() + rightSilo.GetInventoryTotal());
        Assert.InRange(System.Math.Abs(leftSilo.GetInventoryTotal() - middleSilo.GetInventoryTotal()), 0, 1);
        Assert.InRange(System.Math.Abs(middleSilo.GetInventoryTotal() - rightSilo.GetInventoryTotal()), 0, 1);
    }
}
