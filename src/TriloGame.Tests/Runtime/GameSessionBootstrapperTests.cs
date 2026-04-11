using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Runtime.Bootstrap;

namespace TriloGame.Tests.Runtime;

public sealed class GameSessionBootstrapperTests
{
    [Fact]
    public void CreateNewGame_BuildsStarterColonyAndAssignsStarterRoles()
    {
        var result = new GameSessionBootstrapper().CreateNewGame();
        var cave = Assert.IsType<TriloGame.Game.Core.World.Cave>(result.Session.Cave);
        var queen = Assert.IsType<Queen>(cave.GetQueenBuilding());

        Assert.Contains(cave.GetBuildingList(), building => building is MiningPost);
        Assert.Null(cave.GetOpalNode());

        var assignments = cave.GetTrilobiteList().ToDictionary(trilobite => trilobite.Name, trilobite => trilobite.Assignment, StringComparer.Ordinal);
        Assert.Equal("miner", assignments["Jeffery"]);
        Assert.Equal("builder", assignments["Quinton"]);
        Assert.Equal("farmer", assignments["Yeetmuncher"]);
        Assert.Equal("fighter", assignments["Sigma"]);
        Assert.Contains(result.Session.UnlockedBuildings, factory => factory.Name == "Turret");
        Assert.NotNull(queen);
    }

    [Fact]
    public void CreateNewGame_DisablesEnemySpawnsByDefault()
    {
        var result = new GameSessionBootstrapper().CreateNewGame();

        Assert.True(result.Session.Runtime.DisableEnemySpawns);
    }
}
