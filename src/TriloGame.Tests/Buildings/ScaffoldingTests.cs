using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
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
    public void Scaffolding_PreservesTargetBuildingPassability()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var scaffolding = new Scaffolding(session, new AlgaeFarm(session));

        Assert.All(scaffolding.OpenMap.SelectMany(row => row), cell => Assert.Equal(1, cell));
    }

    [Fact]
    public void CompletedScaffolding_WaitsForTrilobitesToLeaveBeforeReplacingItself()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var targetBuilding = new AlgaeFarm(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, buildLocation);

        CompleteScaffolding(scaffolding);

        Assert.True(scaffolding.ResourceComplete);
        Assert.True(scaffolding.CompletionPending);
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
        var scaffolding = new Scaffolding(session, new AlgaeFarm(session));
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));
        TestWorldFactory.SpawnTrilobite(cave, session, buildLocation, "Blocker");
        var walker = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 4), "Walker");

        CompleteScaffolding(scaffolding);

        Assert.True(scaffolding.ResourceComplete);
        Assert.False(cave.MoveCreature(walker, new GridPoint(6, 4)));
        Assert.Equal(new GridPoint(5, 4), walker.Location);
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
        var placement = cave.EvaluateBuildPlacement(scaffolding, locationToBlock, preserveReachability: true);

        Assert.False(placement.CanBuild);
        Assert.Equal(BuildPlacementFailureReason.BlocksExistingBuildingAccess, placement.FailureReasons);
        Assert.All(placement.Cells.Where(cell => cell.Required), cell => Assert.True(cell.CanBuild));
        Assert.False(cave.CanBuild(scaffolding, locationToBlock, preserveReachability: true));
    }

    private static void CompleteScaffolding(Scaffolding scaffolding)
    {
        var requiredSandstone = scaffolding.GetRemainingRequirement("Sandstone");
        Assert.Equal(requiredSandstone, scaffolding.Deposit("Sandstone", requiredSandstone));
        Assert.Equal(scaffolding.ConstructionRequired, scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));
    }

    private static void SetWallTile(TriloGame.Game.Core.World.Cave cave, GridPoint location)
    {
        var tile = cave.GetTile(location)
            ?? throw new InvalidOperationException($"Expected tile at {location}.");
        tile.SetBase("wall");
        tile.CreatureCanFit = false;
        tile.ConfigureWall(1);
    }
}
