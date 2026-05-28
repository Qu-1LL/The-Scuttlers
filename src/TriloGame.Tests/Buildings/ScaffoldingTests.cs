using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class ScaffoldingTests
{
    [Fact]
    public void CompletedScaffolding_ReplacesItselfWithTargetBuilding()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var targetBuilding = new Storage(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, scaffolding);

        Assert.True(cave.Build(scaffolding, buildLocation));

        var requiredSandstone = scaffolding.GetRemainingRequirement("Sandstone");
        Assert.Equal(requiredSandstone, scaffolding.Deposit("Sandstone", requiredSandstone));
        Assert.Equal(scaffolding.ConstructionRequired, scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(targetBuilding, cave.Buildings);
        Assert.Equal(buildLocation, targetBuilding.Location);
    }

    [Fact]
    public void RegularScaffoldingPlacement_IsRejectedWhenItCutsOffExistingBuildingAccess()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var existingStorage = new Storage(session);
        Assert.True(cave.Build(existingStorage, new GridPoint(12, 4)));

        foreach (var location in new[]
                 {
                     new GridPoint(11, 3), new GridPoint(12, 3), new GridPoint(13, 3), new GridPoint(14, 3),
                     new GridPoint(11, 6), new GridPoint(12, 6), new GridPoint(13, 6), new GridPoint(14, 6),
                     new GridPoint(14, 4), new GridPoint(14, 5)
                 })
        {
            SetWallTile(cave, location);
        }

        cave.RefreshReachableTiles();

        var scaffolding = new Scaffolding(session, new Storage(session));
        var locationToBlock = new GridPoint(10, 4);

        Assert.True(cave.SimulatedBuildPreservesReachability(scaffolding, locationToBlock));
        Assert.False(cave.SimulatedBuildPreservesBuildingAccess(scaffolding, locationToBlock));
        Assert.False(cave.CanBuild(scaffolding, locationToBlock, preserveReachability: true));
    }

    [Fact]
    public void Scaffolding_AllowsTraversalAcrossTargetBuildingFootprintUntilConstructionCompletes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var scaffolding = new Scaffolding(session, new Smith(session));
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));

        var passableTile = cave.GetTile(new GridPoint(buildLocation.X + 1, buildLocation.Y + 1));
        var blockedTile = cave.GetTile(buildLocation);

        Assert.NotNull(passableTile);
        Assert.NotNull(blockedTile);
        Assert.True(passableTile!.CreatureFits());
        Assert.True(blockedTile!.CreatureFits());
        Assert.Same(scaffolding, passableTile.Built);
        Assert.Same(scaffolding, blockedTile.Built);
    }

    [Fact]
    public void ScaffoldingPlacement_AllowsTrilobiteAlreadyOnFootprint()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var scaffolding = new Scaffolding(session, new Storage(session));
        var buildLocation = new GridPoint(6, 4);
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, buildLocation);

        Assert.True(cave.Build(scaffolding, buildLocation));
        Assert.Equal(buildLocation, trilobite.Location);
        Assert.Same(scaffolding, cave.GetTile(buildLocation)!.Built);
        Assert.Contains(trilobite, cave.GetTile(buildLocation)!.Trilobites);
    }

    [Fact]
    public void CompletedScaffolding_WaitsForTrilobitesToLeaveBeforeReplacingItself()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var targetBuilding = new Storage(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, buildLocation);

        CompleteScaffolding(scaffolding);

        Assert.True(scaffolding.ResourceComplete);
        Assert.Contains(scaffolding, cave.Buildings);
        Assert.DoesNotContain(targetBuilding, cave.Buildings);

        TickRunner.RunTick(session);

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(targetBuilding, cave.Buildings);
        Assert.Equal(buildLocation, targetBuilding.Location);
        Assert.False(cave.IsResourceCompleteScaffoldingLocation(trilobite.Location));
    }

    [Fact]
    public void TrilobiteMovement_DoesNotEnterResourceCompleteScaffolding()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var scaffolding = new Scaffolding(session, new Storage(session));
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));
        TestWorldFactory.SpawnTrilobite(cave, session, buildLocation, "Blocker");
        var walker = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "Walker");

        CompleteScaffolding(scaffolding);

        Assert.True(scaffolding.ResourceComplete);
        Assert.False(cave.MoveCreature(walker, new GridPoint(6, 5)));
        Assert.Equal(new GridPoint(5, 5), walker.Location);
    }

    [Fact]
    public void TrilobiteMovement_EscapesAcrossResourceCompleteScaffoldingWhenSurrounded()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(1, 1));
        var targetBuilding = new Radar(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        var buildLocation = new GridPoint(6, 4);
        var interiorLocation = new GridPoint(7, 5);

        Assert.True(cave.Build(scaffolding, buildLocation));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, interiorLocation);

        CompleteScaffolding(scaffolding);

        Assert.True(scaffolding.ResourceComplete);

        TickRunner.RunTick(session);

        Assert.NotEqual(interiorLocation, trilobite.Location);
        Assert.True(cave.IsResourceCompleteScaffoldingLocation(trilobite.Location));
        Assert.Contains(scaffolding, cave.Buildings);

        for (var tick = 0; tick < 8 && cave.Buildings.Contains(scaffolding); tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(targetBuilding, cave.Buildings);
        Assert.False(cave.IsResourceCompleteScaffoldingLocation(trilobite.Location));
    }

    [Fact]
    public void CompletedScaffolding_WithPassableTargetTiles_ReplacesItselfWithTargetBuilding()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var targetBuilding = new Smith(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));

        var requiredSandstone = scaffolding.GetRemainingRequirement("Sandstone");
        Assert.Equal(requiredSandstone, scaffolding.Deposit("Sandstone", requiredSandstone));
        Assert.Equal(scaffolding.ConstructionRequired, scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(targetBuilding, cave.Buildings);
        Assert.Equal(buildLocation, targetBuilding.Location);

        var formerlyPassableScaffoldTile = cave.GetTile(new GridPoint(buildLocation.X + 1, buildLocation.Y + 1));
        Assert.NotNull(formerlyPassableScaffoldTile);
        Assert.True(formerlyPassableScaffoldTile!.CreatureFits());
        Assert.Same(targetBuilding, formerlyPassableScaffoldTile.Built);
    }

    [Fact]
    public void CompletedGarageScaffolding_FacingExistingRanch_ReplacesItselfWithGarage()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(0, 0));
        TestWorldFactory.BuildGarage(cave, session, new GridPoint(4, 6));
        TestWorldFactory.BuildSoilPatch(cave, session, new GridPoint(6, 6));
        var targetGarage = new Garage(session);
        targetGarage.SetDisplayRotationTurns(2);
        var scaffolding = new Scaffolding(session, targetGarage);
        scaffolding.SetDisplayRotationTurns(2);
        var buildLocation = new GridPoint(8, 6);

        Assert.True(cave.Build(scaffolding, buildLocation));

        CompleteScaffolding(scaffolding);

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(targetGarage, cave.Buildings);
        Assert.Equal(buildLocation, targetGarage.Location);
        Assert.Equal(2, targetGarage.GetDisplayRotationTurns());
        Assert.NotNull(targetGarage.Ranch);
    }

    [Fact]
    public void CompletedSoilAreaScaffolding_BuildsAllMemberPatchesAtOnce()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var soilArea = new SoilArea(session);
        var firstPatch = new SoilPatch(session);
        var secondPatch = new SoilPatch(session);
        soilArea.AddSoilPatch(firstPatch, GridPoint.Zero);
        soilArea.AddSoilPatch(secondPatch, new GridPoint(2, 0));
        var scaffolding = new Scaffolding(session, soilArea);
        var buildLocation = new GridPoint(6, 4);

        Assert.Equal(new GridPoint(4, 2), scaffolding.Size);
        Assert.Equal(10, scaffolding.GetRemainingRequirement("Algae"));
        Assert.True(cave.Build(scaffolding, buildLocation));

        Assert.Equal(10, scaffolding.Deposit("Algae", 10));
        Assert.Equal(scaffolding.ConstructionRequired, scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(firstPatch, cave.Buildings);
        Assert.Contains(secondPatch, cave.Buildings);
        Assert.DoesNotContain(soilArea, cave.Buildings);
        Assert.Same(soilArea, firstPatch.SoilArea);
        Assert.Same(soilArea, secondPatch.SoilArea);
        Assert.Equal(buildLocation, firstPatch.Location);
        Assert.Equal(new GridPoint(buildLocation.X + 2, buildLocation.Y), secondPatch.Location);
        Assert.Equal(2, cave.GetSoilPatches().Count);
    }

    [Fact]
    public void Scaffolding_OpenMap_PreservesNonFootprintCellsAndMakesFootprintTraversable()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var targetBuilding = new Turret(session);
        var scaffolding = new Scaffolding(session, targetBuilding);

        Assert.Equal(2, scaffolding.OpenMap[0][2]);
        Assert.Equal(2, scaffolding.OpenMap[2][0]);
        Assert.Equal(1, scaffolding.OpenMap[1][1]);
    }

    private static void SetWallTile(TriloGame.Game.Core.World.Cave cave, GridPoint location)
    {
        var tile = cave.GetTile(location)
            ?? throw new InvalidOperationException($"Expected tile at {location}.");
        tile.SetBase("wall");
        tile.CreatureCanFit = false;
        tile.ConfigureWall(1);
    }

    private static void CompleteScaffolding(Scaffolding scaffolding)
    {
        foreach (var pair in scaffolding.RecipeRequired.ToArray())
        {
            var required = scaffolding.GetRemainingRequirement(pair.Key);
            Assert.Equal(required, scaffolding.Deposit(pair.Key, required));
        }

        Assert.Equal(
            scaffolding.ConstructionRequired,
            scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));
    }
}
