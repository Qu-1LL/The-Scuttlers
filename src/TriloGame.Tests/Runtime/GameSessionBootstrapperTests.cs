using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
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
        Assert.Contains(result.Session.UnlockedBuildings, factory => factory.Name == "Soil Patch");
        Assert.Contains(result.Session.UnlockedBuildings, factory => factory.Name == "Garage");
        Assert.Contains(result.Session.UnlockedBuildings, factory => factory.Name == "Silo");
        Assert.Contains(result.Session.UnlockedBuildings, factory => factory.Name == "Turret");
        Assert.NotNull(queen);
    }

    [Fact]
    public void CreateNewGame_DisablesEnemySpawnsByDefault()
    {
        var result = new GameSessionBootstrapper().CreateNewGame();

        Assert.True(result.Session.Runtime.DisableEnemySpawns);
        Assert.False(result.Session.Runtime.AllowManualMining);
    }

    [Fact]
    public void CreateNewGame_AllowsAnExplicitWorldGenerationMethod()
    {
        var result = new GameSessionBootstrapper().CreateNewGame(WorldGenerationMethod.Version0);
        var cave = Assert.IsType<Cave>(result.Session.Cave);

        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
    }

    [Fact]
    public void CreateNewGame_AllowsPerlinNoiseWorldGenerationMethod()
    {
        var result = new GameSessionBootstrapper().CreateNewGame(WorldGenerationMethod.PerlinNoise);
        var cave = Assert.IsType<Cave>(result.Session.Cave);

        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.NotNull(cave.GetQueenBuilding());
        Assert.Contains(cave.GetBuildingList(), building => building is MiningPost);
    }

    [Fact]
    public void CreateNewGame_AllowsFractalBrownianMotionWorldGenerationMethod()
    {
        var result = new GameSessionBootstrapper().CreateNewGame(WorldGenerationMethod.FractalBrownianMotion);
        var cave = Assert.IsType<Cave>(result.Session.Cave);

        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.NotNull(cave.GetQueenBuilding());
        Assert.Contains(cave.GetBuildingList(), building => building is MiningPost);
    }

    [Fact]
    public void CreateNewGame_SeedsTheSkillTreeWithAnUnlockedRootAnchor()
    {
        var result = new GameSessionBootstrapper().CreateNewGame();

        var root = Assert.IsType<TriloGame.Game.Core.Progression.BinarySkillNode>(result.Session.SkillTree.Root);
        Assert.Equal("Hive Core", root.Name);
        Assert.True(root.IsUnlocked);
        Assert.Equal(1, result.Session.SkillTree.Count);
    }
}
