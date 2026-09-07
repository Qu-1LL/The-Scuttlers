using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class GrindingMillTests
{
    [Fact]
    public void Construction_MatchesMiningPostCostAndHealth_AndProvidesSharedClassificationBuffers()
    {
        var session = new GameSession();
        var mill = new GrindingMill(session);
        var miningPost = new MiningPost(session);

        Assert.Equal("Grinding Mill", mill.Name);
        Assert.Equal("GrindingMill", mill.TextureKey);
        Assert.Equal(new GridPoint(2, 3), mill.Size);
        Assert.Equal(new[] { 0, 0 }, mill.OpenMap[0]);
        Assert.Equal(new[] { 0, 0 }, mill.OpenMap[1]);
        Assert.Equal(new[] { 0, 0 }, mill.OpenMap[2]);
        Assert.IsAssignableFrom<IProcessor>(mill);
        Assert.IsNotAssignableFrom<IResourceStorage>(mill);
        Assert.Equal(5, mill.ProcessingIntervalTicks);
        Assert.Collection(
            mill.InputDefinitions,
            input =>
            {
                Assert.Equal("RAW PLANTS", input.Label);
                Assert.Equal(ResourceClassificationKeys.FoodType, input.ClassificationKey);
                Assert.Equal(ResourceClassificationValues.Raw, input.ClassificationValue);
                Assert.Equal(1, input.AmountPerProcess);
                Assert.Equal(500, input.Capacity);
            });
        Assert.Collection(
            mill.OutputDefinitions,
            output =>
            {
                Assert.Equal("MEALS", output.Label);
                Assert.Equal(ResourceClassificationKeys.FoodType, output.ClassificationKey);
                Assert.Equal(ResourceClassificationValues.Meal, output.ClassificationValue);
                Assert.Equal(1, output.AmountPerProcess);
                Assert.Equal(500, output.Capacity);
            });
        Assert.Equal(500, mill.GetInputCapacity(ResourceName.Algae));
        Assert.Equal(500, mill.GetOutputCapacity(ResourceName.AlgaeMeal));
        Assert.Equal(500, mill.GetInputCapacity(ResourceName.Gloop));
        Assert.Equal(500, mill.GetOutputCapacity(ResourceName.GloopMeal));
        Assert.Equal(miningPost.Health, mill.Health);
        Assert.Equal(miningPost.MaxHealth, mill.MaxHealth);
        Assert.Equal(miningPost.Recipe, mill.Recipe);
    }

    [Fact]
    public void SharedPlantAndMealCapacity_IsEnforcedAcrossPlantTypes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(6, 6)));

        Assert.Equal(300, mill.DepositInput(ResourceName.Algae, 300));
        Assert.Equal(200, mill.DepositInput(ResourceName.Gloop, 300));
        Assert.Equal(0, mill.GetInputSpace(ResourceName.Algae));
        Assert.Equal(0, mill.GetInputSpace(ResourceName.Gloop));

        for (var batch = 1; batch <= 500; batch++)
        {
            session.TickCount = batch * mill.ProcessingIntervalTicks;
            Assert.Equal(1, mill.Tick(cave));
        }

        Assert.Equal(300, mill.GetOutputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(200, mill.GetOutputAmount(ResourceName.GloopMeal));
        Assert.Equal(0, mill.GetOutputSpace(ResourceName.AlgaeMeal));
        Assert.Equal(0, mill.GetOutputSpace(ResourceName.GloopMeal));
    }

    [Fact]
    public void Tick_ConvertsOneAlgaeToMealEveryFiveTicks()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(6, 6)));
        Assert.Equal(3, mill.DepositInput(ResourceName.Algae, 3));

        for (var tick = 0; tick < 4; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Equal(3, mill.GetInputAmount(ResourceName.Algae));
        Assert.Equal(0, mill.GetOutputAmount(ResourceName.AlgaeMeal));

        TickRunner.RunTick(session);

        Assert.Equal(2, mill.GetInputAmount(ResourceName.Algae));
        Assert.Equal(1, mill.GetOutputAmount(ResourceName.AlgaeMeal));
        Assert.Equal(498, mill.GetInputSpace(ResourceName.Algae));
        Assert.Equal(499, mill.GetOutputSpace(ResourceName.AlgaeMeal));
        Assert.Equal(2, session.Resources[ResourceName.Algae]);
        Assert.Equal(1, session.Resources[ResourceName.AlgaeMeal]);

        var stockpile = new ResourceStockpileSystem().Refresh(session);
        Assert.Equal(2, stockpile.GetAmount(ResourceName.Algae));
        Assert.Equal(1, stockpile.GetAmount(ResourceName.AlgaeMeal));
    }

    [Fact]
    public void Tick_ConvertsGloopIntoMatchingGloopMeal()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(6, 6)));
        Assert.Equal(2, mill.DepositInput(ResourceName.Gloop, 2));

        session.TickCount = mill.ProcessingIntervalTicks;

        Assert.Equal(1, mill.Tick(cave));
        Assert.Equal(1, mill.GetInputAmount(ResourceName.Gloop));
        Assert.Equal(1, mill.GetOutputAmount(ResourceName.GloopMeal));
        Assert.Equal(0, mill.GetOutputAmount(ResourceName.AlgaeMeal));
    }

    [Fact]
    public void OutputCollectors_AreAssignedOnlyWhenTheMillCanFillEveryReservedLoad()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(10, 0));
        var mill = new GrindingMill(session);
        Assert.True(cave.Build(mill, new GridPoint(6, 6)));
        Assert.Equal(10, mill.DepositInput(ResourceName.Algae, 10));
        for (var batch = 1; batch <= 5; batch++)
        {
            session.TickCount = batch * mill.ProcessingIntervalTicks;
            Assert.Equal(1, mill.Tick(cave));
        }

        var firstCollector = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(2, 6), "First", "farmer");
        var secondCollector = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 6), "Second", "farmer");

        Assert.True(mill.TryAssignOutputCollector(firstCollector, ResourceName.AlgaeMeal));
        Assert.Equal(firstCollector.InventoryCapacity, mill.GetAssignedOutputCarryingCapacity(ResourceName.AlgaeMeal));
        Assert.False(mill.TryAssignOutputCollector(secondCollector, ResourceName.AlgaeMeal));

        for (var batch = 6; batch <= 10; batch++)
        {
            session.TickCount = batch * mill.ProcessingIntervalTicks;
            Assert.Equal(1, mill.Tick(cave));
        }

        Assert.False(mill.TryAssignOutputCollector(secondCollector, ResourceName.AlgaeMeal));

        Assert.True(mill.ReleaseOutputCollector(firstCollector));
        Assert.True(mill.TryAssignOutputCollector(secondCollector, ResourceName.AlgaeMeal));
        Assert.Equal(1, mill.GetOutputCollectorCount(ResourceName.AlgaeMeal));
        Assert.Equal(secondCollector.InventoryCapacity, mill.GetAssignedOutputCarryingCapacity(ResourceName.AlgaeMeal));
    }

    [Fact]
    public void AlgaeMeal_IsOrganicAndProvidesTwoNutrition()
    {
        var session = new GameSession();
        var queen = new Queen(session);

        var result = queen.FeedAlgaeMeal(5);

        Assert.Equal(ResourceCategory.Organic, ItemCatalog.GetCategory(ResourceName.AlgaeMeal));
        Assert.Equal(2, ItemCatalog.GetNutritionValue(ResourceName.AlgaeMeal));
        Assert.Equal(5, result.Accepted);
        Assert.Equal(10, queen.NutritionCount);
        Assert.Equal(20, queen.NutritionQuota);
    }
}
