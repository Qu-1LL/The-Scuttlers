using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class WallTests
{
    [Fact]
    public void BuildAndRemoveWalls_UpdatesConnectionVisualsForAffectedWalls()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(1, 1));
        var center = TestWorldFactory.BuildWall(cave, session, new GridPoint(6, 6));

        Assert.Equal(WallType.Default.NoConnectionSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());

        var bottom = TestWorldFactory.BuildWall(cave, session, new GridPoint(6, 7));
        Assert.Equal(WallType.Default.OneConnectionSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());
        Assert.True(center.Connections["bottom"]);
        Assert.False(center.Connections["top"]);
        Assert.Equal(WallType.Default.OneConnectionSprite, bottom.TextureKey);
        Assert.Equal(2, bottom.GetDisplayRotationTurns());
        Assert.True(bottom.Connections["top"]);

        var top = TestWorldFactory.BuildWall(cave, session, new GridPoint(6, 5));
        Assert.Equal(WallType.Default.TwoConnectionsStraightSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());
        Assert.True(center.Connections["top"]);
        Assert.True(center.Connections["bottom"]);

        Assert.True(cave.RemoveBuilding(top));
        Assert.Equal(WallType.Default.OneConnectionSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());
        Assert.False(center.Connections["top"]);
        Assert.True(center.Connections["bottom"]);

        var left = TestWorldFactory.BuildWall(cave, session, new GridPoint(5, 6));
        Assert.Equal(WallType.Default.TwoConnectionsBendSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());
        Assert.True(center.Connections["left"]);

        var right = TestWorldFactory.BuildWall(cave, session, new GridPoint(7, 6));
        Assert.Equal(WallType.Default.ThreeConnectionsSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());
        Assert.True(center.Connections["right"]);

        top = TestWorldFactory.BuildWall(cave, session, new GridPoint(6, 5));
        Assert.Equal(WallType.Default.FourConnectionsSprite, center.TextureKey);
        Assert.Equal(0, center.GetDisplayRotationTurns());
        Assert.True(center.Connections["top"]);
        Assert.True(center.Connections["right"]);
        Assert.True(center.Connections["bottom"]);
        Assert.True(center.Connections["left"]);
    }

    [Fact]
    public void WallField_StaysUpdatedWhenEnemiesMove()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 6, new GridPoint(1, 1));
        TestWorldFactory.BuildWall(cave, session, new GridPoint(7, 3));

        var wallField = cave.GetBfsFieldObject("wall")
            ?? throw new InvalidOperationException("Expected the wall BFS field to exist after a wall is built.");

        Assert.True(wallField.IsUpdated());

        var enemyLocation = new GridPoint(9, 3);
        var enemyTile = cave.GetTile(enemyLocation)
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Wall Tester", enemyLocation, session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.True(wallField.IsUpdated());

        Assert.True(cave.MoveCreature(enemy, new GridPoint(8, 3)));
        Assert.True(wallField.IsUpdated());
    }

    [Fact]
    public void BuiltWalls_AreTraversableForTrilobites()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 8, new GridPoint(1, 1));
        var wallLocation = new GridPoint(5, 4);
        TestWorldFactory.BuildWall(cave, session, wallLocation);
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Builder", "builder");

        var path = cave.BuildDirectPathToPoint(trilobite.Location, new GridPoint(6, 4));

        Assert.NotNull(path);
        Assert.Equal([new GridPoint(4, 4), wallLocation, new GridPoint(6, 4)], path);
        Assert.True(cave.MoveCreature(trilobite, wallLocation));
        Assert.Equal(wallLocation, trilobite.Location);
    }

    [Fact]
    public void WallScaffoldingPlacement_SkipsExistingBuildingAccessCheck()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(16, 10, new GridPoint(1, 1));
        var existingStorage = new Storage(session);
        Assert.True(cave.Build(existingStorage, new GridPoint(11, 4)));

        foreach (var location in new[]
                 {
                     new GridPoint(10, 3), new GridPoint(11, 3), new GridPoint(12, 3), new GridPoint(13, 3),
                     new GridPoint(10, 5), new GridPoint(10, 6), new GridPoint(11, 6), new GridPoint(12, 6), new GridPoint(13, 6),
                     new GridPoint(13, 4), new GridPoint(13, 5)
                 })
        {
            SetWallTile(cave, location);
        }

        cave.RefreshReachableTiles();

        var scaffolding = new Scaffolding(session, new Wall(session));
        var entranceTile = new GridPoint(10, 4);

        Assert.True(cave.SimulatedBuildPreservesReachability(scaffolding, entranceTile));
        Assert.False(cave.SimulatedBuildPreservesBuildingAccess(scaffolding, entranceTile));
        Assert.True(cave.CanBuild(scaffolding, entranceTile, preserveReachability: true));
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
