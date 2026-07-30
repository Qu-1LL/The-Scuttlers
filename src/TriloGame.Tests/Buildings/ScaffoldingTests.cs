using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class ScaffoldingTests
{
    [Fact]
    public void BuildFirst_TogglesOnlyWhileScaffoldingIsInProgress()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var scaffolding = new Scaffolding(session, new Storage(session));

        Assert.False(scaffolding.BuildFirst);
        Assert.True(scaffolding.ToggleBuildFirst());
        Assert.True(scaffolding.BuildFirst);
        Assert.True(scaffolding.ToggleBuildFirst());
        Assert.False(scaffolding.BuildFirst);

        CompleteScaffolding(scaffolding);

        Assert.False(scaffolding.IsInProgress());
        Assert.False(scaffolding.ToggleBuildFirst());
        Assert.False(scaffolding.BuildFirst);
    }

    [Fact]
    public void CompletedScaffolding_ReplacesItselfWithTargetBuilding()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var targetBuilding = new Storage(session);
        var scaffolding = new Scaffolding(session, targetBuilding);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, scaffolding);

        Assert.True(cave.Build(scaffolding, buildLocation));

        var requiredSandstone = scaffolding.GetRemainingRequirement(ResourceName.Sandstone);
        Assert.Equal(requiredSandstone, scaffolding.Deposit(ResourceName.Sandstone, requiredSandstone));
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
    public void Scaffolding_OpenMap_PreservesExcludedCellsAndMakesOtherTilesTraversable()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var scaffolding = new Scaffolding(session, new Turret(session));

        Assert.Equal(2, scaffolding.OpenMap[0][2]);
        Assert.Equal(2, scaffolding.OpenMap[2][0]);
        Assert.Equal(1, scaffolding.OpenMap[0][0]);
        Assert.Equal(1, scaffolding.OpenMap[1][1]);
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
        Assert.Same(trilobite, cave.GetTrilobiteAtTileKey(buildLocation.ToString()));
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
        Assert.True(scaffolding.CompletionPending);
        Assert.Contains(scaffolding, cave.Buildings);
        Assert.DoesNotContain(targetBuilding, cave.Buildings);

        var guard = 20;
        while (cave.Buildings.Contains(scaffolding) && guard-- > 0)
        {
            TickRunner.RunTick(session);
        }

        Assert.DoesNotContain(scaffolding, cave.Buildings);
        Assert.Contains(targetBuilding, cave.Buildings);
        Assert.Equal(buildLocation, targetBuilding.Location);
        Assert.NotEqual(buildLocation, trilobite.Location);
    }

    [Fact]
    public void CategoryRecipes_AcceptMatchingResourceTypes()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var scaffolding = new Scaffolding(session, new Storage(session));

        Assert.True(scaffolding.NeedsResource(ResourceName.Malachite));
        Assert.Equal(12, scaffolding.Deposit(ResourceName.Malachite, 12));
        Assert.Equal(8, scaffolding.Deposit(ResourceName.Sandstone, 8));
        Assert.True(scaffolding.IsRecipeComplete());
    }

    [Fact]
    public void CategoryRecipes_TrackDepositedResourcesByActualType()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var scaffolding = new Scaffolding(session, new Storage(session));

        Assert.Equal(12, scaffolding.Deposit(ResourceName.Malachite, 12));
        Assert.Equal(8, scaffolding.Deposit(ResourceName.Sandstone, 8));

        Assert.Equal(20, scaffolding.GetTotalDepositedAmount());
        Assert.Equal(12, scaffolding.GetDepositedResources().GetValueOrDefault(ResourceName.Malachite));
        Assert.Equal(8, scaffolding.GetDepositedResources().GetValueOrDefault(ResourceName.Sandstone));
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
        foreach (var requirement in scaffolding.RecipeRequired)
        {
            var resourceType = ResolveResourceType(requirement);
            Assert.Equal(requirement.Amount, scaffolding.Deposit(resourceType, requirement.Amount));
        }

        Assert.Equal(
            scaffolding.ConstructionRequired,
            scaffolding.ApplyConstructionWork(scaffolding.ConstructionRequired));
    }

    private static ResourceName ResolveResourceType(ResourceRequirement requirement)
    {
        if (requirement.SpecificResource is { } specificResource)
        {
            return specificResource;
        }

        foreach (var resourceType in Enum.GetValues<ResourceName>())
        {
            if (requirement.Matches(resourceType))
            {
                return resourceType;
            }
        }

        throw new InvalidOperationException("Expected at least one resource that satisfies the requirement.");
    }
}
