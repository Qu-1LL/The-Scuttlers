using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class SoilPatchTests
{
    [Fact]
    public void SoilPatch_DefaultsToFourIndependentDormantTiles()
    {
        var soilPatch = new SoilPatch(new GameSession());

        Assert.Equal(new GridPoint(2, 2), soilPatch.Size);
        Assert.Equal("Soil Patch", soilPatch.Name);
        Assert.True(soilPatch.IgnoredByAnts);
        var recipe = Assert.Single(soilPatch.GetRecipe()!);
        Assert.Equal(5, recipe.Amount);
        Assert.Equal(ResourceCategory.Organic, recipe.Category);
        Assert.Null(recipe.SpecificResource);
        Assert.Equal(4, soilPatch.SoilTiles.Count);
        Assert.NotNull(soilPatch.SoilArea);
        Assert.Equal(4, soilPatch.SoilArea!.SoilTiles.Count);
        Assert.All(soilPatch.SoilTiles, soilTile =>
        {
            Assert.Equal(0, soilTile.GrowthLevel);
            Assert.Null(soilTile.PlantedResource);
            Assert.Equal(5, soilTile.ReturnedAlgaeAmount);
            Assert.Equal(0, soilTile.LastTickMod);
            Assert.Equal("SoilTile_0", soilTile.TextureKey);
        });
    }

    [Fact]
    public void SoilGrowthRollsEveryFiveTicks_AndAdvancesOnlyBelowTenPercent()
    {
        var session = new GameSession
        {
            TickCount = 1
        };
        var soilPatch = new SoilPatch(session);
        var soilTile = soilPatch.GetSoilTile(new GridPoint(0, 0))!;
        var advancingRandom = new FixedRandom(0.099d);

        Assert.True(soilTile.Plant(GrowableResourceType.ALGAE));
        Assert.Equal(1, soilTile.GrowthLevel);

        Assert.Equal(0, soilPatch.Tick(advancingRandom, 2));
        Assert.Equal(0, soilPatch.Tick(advancingRandom, 3));
        Assert.Equal(0, soilPatch.Tick(advancingRandom, 4));
        Assert.Equal(0, soilPatch.Tick(advancingRandom, 0));
        Assert.Equal(0, advancingRandom.CallCount);

        Assert.Equal(1, soilPatch.Tick(advancingRandom, 1));
        Assert.Equal(2, soilTile.GrowthLevel);
        Assert.Equal(1, advancingRandom.CallCount);

        var nonAdvancingRandom = new FixedRandom(0.10d);
        Assert.Equal(0, soilPatch.Tick(nonAdvancingRandom, 1));
        Assert.Equal(2, soilTile.GrowthLevel);
        Assert.Equal(1, nonAdvancingRandom.CallCount);
    }

    [Fact]
    public void HarvestAtWorldTile_ReturnsConfiguredAmountAndResetsOnlyTheHarvestedTile()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        soilPatch.SetPlantedResource(new GridPoint(0, 0), GrowableResourceType.ALGAE);
        soilPatch.SetPlantedResource(new GridPoint(1, 1), GrowableResourceType.ALGAE);
        soilPatch.SetReturnedAlgaeAmount(new GridPoint(0, 0), 8);
        soilPatch.SetGrowthLevel(new GridPoint(0, 0), 3);
        soilPatch.SetGrowthLevel(new GridPoint(1, 1), 3);

        Assert.Equal(8, soilPatch.HarvestAtWorldTile(new GridPoint(4, 4)));
        Assert.Equal(1, soilPatch.GetSoilTile(new GridPoint(0, 0))!.GrowthLevel);
        Assert.Equal(GrowableResourceType.ALGAE, soilPatch.GetSoilTile(new GridPoint(0, 0))!.PlantedResource);
        Assert.Equal(3, soilPatch.GetSoilTile(new GridPoint(1, 1))!.GrowthLevel);
    }

    [Fact]
    public void AntsIgnoreSoilPatchAsAHostileTarget()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(0, 0));
        var soilPatch = TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(4, 4));
        var enemy = new Enemy("Soil Ant", new GridPoint(3, 4), session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(new GridPoint(3, 4))!));

        cave.RefreshBfsField("colony");
        var colonyField = cave.GetBfsFieldObject("colony")
            ?? throw new InvalidOperationException("Expected colony field.");

        Assert.Null(enemy.GetHostileBuildingAtTileKey(new GridPoint(4, 4).ToString()));
        Assert.DoesNotContain(soilPatch, colonyField.TrackedBuildings);
    }

    // Supplies a deterministic growth roll while exposing whether an interval triggered it.
    private sealed class FixedRandom(double value) : Random
    {
        private readonly double _value = value;

        public int CallCount { get; private set; }

        public override double NextDouble()
        {
            CallCount++;
            return _value;
        }
    }
}
