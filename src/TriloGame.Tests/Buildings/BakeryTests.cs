using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class BakeryTests
{
    [Fact]
    public void Construction_ProvidesStationableTwoInputProcessingWithTheSpecifiedFootprint()
    {
        var session = new GameSession();
        var bakery = new Bakery(session);
        var miningPost = new MiningPost(session);

        Assert.Equal("Bakery", bakery.Name);
        Assert.Equal("Bakery", bakery.TextureKey);
        Assert.Equal(new GridPoint(2, 3), bakery.Size);
        Assert.True(bakery.HasStation);
        Assert.Equal(new[] { 1, 0 }, bakery.OpenMap[0]);
        Assert.Equal(new[] { 1, 0 }, bakery.OpenMap[1]);
        Assert.Equal(new[] { 0, 0 }, bakery.OpenMap[2]);
        Assert.IsAssignableFrom<IProcessingBuilding>(bakery);
        Assert.IsNotAssignableFrom<IResourceStorage>(bakery);
        Assert.Equal(5, bakery.ProcessingIntervalTicks);
        Assert.Collection(
            bakery.InputDefinitions,
            input => Assert.Equal(new ProcessingResourceDefinition(ResourceName.Algae, 1, 250), input),
            input => Assert.Equal(new ProcessingResourceDefinition(ResourceName.AlgaeMeal, 1, 250), input));
        Assert.Collection(
            bakery.OutputDefinitions,
            output => Assert.Equal(new ProcessingResourceDefinition(ResourceName.AlgaePie, 1, 250), output));
        Assert.Equal(miningPost.Health, bakery.Health);
        Assert.Equal(miningPost.MaxHealth, bakery.MaxHealth);
        Assert.Equal(miningPost.Recipe, bakery.Recipe);
    }

    [Fact]
    public void Tick_ConsumesAlgaeAndMealToProduceOnePieEveryFiveTicks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var bakery = new Bakery(session);
        Assert.True(cave.Build(bakery, new GridPoint(6, 6)));
        Assert.Equal(3, bakery.DepositInput(ResourceName.Algae, 3));
        Assert.Equal(3, bakery.DepositInput(ResourceName.AlgaeMeal, 3));

        for (var tick = 0; tick < 4; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Equal(3, bakery.GetInputAmount(ResourceName.Algae));
        Assert.Equal(3, bakery.GetInputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(0, bakery.GetOutputAmount(ResourceName.AlgaePie));

        TickRunner.RunTick(session);

        Assert.Equal(2, bakery.GetInputAmount(ResourceName.Algae));
        Assert.Equal(2, bakery.GetInputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(1, bakery.GetOutputAmount(ResourceName.AlgaePie));
    }

    [Fact]
    public void Interaction_UsesOnlyTheTwoPassableOwnedTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var bakery = new Bakery(session);
        Assert.True(cave.Build(bakery, new GridPoint(6, 6)));
        var interactionTiles = bakery.TileArray.Where(bakery.IsInteractionTile).ToArray();
        var worker = TestWorldFactory.SpawnTrilobite(cave, session, interactionTiles[0].Coordinates, "Worker");

        Assert.Equal(2, interactionTiles.Length);
        Assert.All(interactionTiles, tile => Assert.True(tile.CreatureFits()));
        Assert.Equal(2, bakery.GetNavigationSeedTiles(cave).Count);
        Assert.True(worker.IsAtBuildingInteractionTile(bakery));
    }

    [Fact]
    public void Tick_StopsWhenPieOutputReachesCapacity()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var bakery = new Bakery(session);
        Assert.True(cave.Build(bakery, new GridPoint(6, 6)));
        Assert.Equal(250, bakery.DepositInput(ResourceName.Algae, 250));
        Assert.Equal(250, bakery.DepositInput(ResourceName.AlgaeMeal, 250));

        for (var batch = 1; batch <= 250; batch++)
        {
            session.TickCount = batch * bakery.ProcessingIntervalTicks;
            Assert.Equal(1, bakery.Tick(cave));
        }

        Assert.Equal(250, bakery.GetOutputAmount(ResourceName.AlgaePie));
        Assert.Equal(0, bakery.GetInputAmount(ResourceName.Algae));
        Assert.Equal(0, bakery.GetInputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(1, bakery.DepositInput(ResourceName.Algae, 1));
        Assert.Equal(1, bakery.DepositInput(ResourceName.AlgaeMeal, 1));
        session.TickCount += bakery.ProcessingIntervalTicks;

        Assert.Equal(0, bakery.Tick(cave));
        Assert.Equal(1, bakery.GetInputAmount(ResourceName.Algae));
        Assert.Equal(1, bakery.GetInputAmount(ResourceName.AlgaeMeal));
    }

    [Fact]
    public void AlgaePie_IsOrganicAndProvidesFourNutrition()
    {
        var session = new GameSession();
        var queen = new Queen(session);

        var result = queen.FeedResource(ResourceName.AlgaePie, 4);

        Assert.Equal(ResourceCategory.Organic, ItemCatalog.GetCategory(ResourceName.AlgaePie));
        Assert.Equal(4, ItemCatalog.GetNutritionValue(ResourceName.AlgaePie));
        Assert.Equal(4, result.Accepted);
        Assert.Equal(16, queen.NutritionCount);
    }
}
