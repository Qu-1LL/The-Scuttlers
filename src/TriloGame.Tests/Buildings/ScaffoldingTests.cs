using TriloGame.Game.Core.Buildings;
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
    public void Scaffolding_BlocksTargetBuildingFootprintUntilConstructionCompletes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var scaffolding = new Scaffolding(session, new Smith(session));
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));

        var passableTile = cave.GetTile(new GridPoint(buildLocation.X + 1, buildLocation.Y + 1));
        var blockedTile = cave.GetTile(buildLocation);

        Assert.NotNull(passableTile);
        Assert.NotNull(blockedTile);
        Assert.False(passableTile!.CreatureFits());
        Assert.False(blockedTile!.CreatureFits());
        Assert.Same(scaffolding, passableTile.Built);
        Assert.Same(scaffolding, blockedTile.Built);
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
    public void Scaffolding_OpenMap_PreservesNonFootprintCells()
    {
        var (session, _, _) = TestWorldFactory.CreateSessionWithQueen();
        var targetBuilding = new Turret(session);
        var scaffolding = new Scaffolding(session, targetBuilding);

        Assert.Equal(2, scaffolding.OpenMap[0][2]);
        Assert.Equal(2, scaffolding.OpenMap[2][0]);
        Assert.Equal(0, scaffolding.OpenMap[1][1]);
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
