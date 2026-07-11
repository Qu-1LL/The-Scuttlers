using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class CaveOccupancyTests
{
    [Fact]
    public void SpawnAndMoveCreature_UpdatesCachedOccupancyLookups()
    {
        var (session, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var enemyTile = cave.GetReachableTiles()
            .FirstOrDefault(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString() && tile.Trilobites.Count == 0)
            ?? throw new InvalidOperationException("No reachable tile was available for the enemy occupancy test.");
        var enemy = new Enemy("Occupant", enemyTile.Coordinates, session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.Same(enemy, cave.GetEnemyAtTileKey(enemyTile.Key));
        Assert.Same(trilobite, cave.GetTrilobiteAtTileKey(trilobite.Location.ToString()));

        var nextTile = enemyTile.Neighbors.FirstOrDefault(tile => tile.CreatureFits() && tile.Trilobites.Count == 0 && tile.EnemyOccupant is null)
            ?? throw new InvalidOperationException("No adjacent tile was available for the enemy move occupancy test.");

        Assert.True(cave.MoveCreature(enemy, nextTile.Coordinates));
        Assert.Null(cave.GetEnemyAtTileKey(enemyTile.Key));
        Assert.Same(enemy, cave.GetEnemyAtTileKey(nextTile.Key));
    }

    [Fact]
    public void BuildAndRemoveBuilding_UpdatesTypedBuildingRegistries()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var miningPost = new MiningPost(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, miningPost);

        Assert.True(cave.Build(miningPost, buildLocation));
        Assert.Contains(miningPost, cave.GetMiningPosts());

        Assert.True(cave.RemoveBuilding(miningPost));
        Assert.DoesNotContain(miningPost, cave.GetMiningPosts());
    }

    [Fact]
    public void CanBuild_ReturnsFalse_WhenEnemyOccupiesCoveredTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(1, 1));
        var enemyLocation = new GridPoint(8, 8);
        var enemyTile = cave.GetTile(enemyLocation)
            ?? throw new InvalidOperationException("Expected an enemy tile to exist.");
        var enemy = new Enemy("Occupant", enemyLocation, session);
        var miningPost = new MiningPost(session);
        var buildLocation = new GridPoint(enemyLocation.X - 1, enemyLocation.Y - 1);

        Assert.True(cave.Spawn(enemy, enemyTile));
        var placement = cave.EvaluateBuildPlacement(miningPost, buildLocation);

        Assert.False(placement.CanBuild);
        Assert.Contains(placement.Cells, cell =>
            cell.Location == enemyLocation &&
            (cell.FailureReasons & BuildPlacementFailureReason.EnemyOccupant) != BuildPlacementFailureReason.None);
        Assert.False(cave.CanBuild(miningPost, buildLocation));
        Assert.False(cave.Build(miningPost, buildLocation));
        Assert.Same(enemy, cave.GetEnemyAtTileKey(enemyTile.Key));
    }

    [Fact]
    public void FloorHoles_BlockCreaturesAndBuildingPlacement()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(1, 1));
        var holeLocation = new GridPoint(8, 8);
        var holeTile = cave.GetTile(holeLocation)
            ?? throw new InvalidOperationException("Expected a floor-hole tile to exist.");
        var trilobite = new Trilobite("Tester", new GridPoint(2, 2), session);
        var wall = new Wall(session);

        holeTile.SetFloorCover(false);

        Assert.False(holeTile.CreatureFits());
        Assert.False(cave.PlaceCreatureOnTile(trilobite, holeLocation));
        var placement = cave.EvaluateBuildPlacement(wall, holeLocation);

        Assert.False(placement.CanBuild);
        Assert.Contains(placement.Cells, cell =>
            cell.Location == holeLocation &&
            (cell.FailureReasons & BuildPlacementFailureReason.ImpassableTile) != BuildPlacementFailureReason.None);
        Assert.False(cave.CanBuild(wall, holeLocation));
        Assert.False(cave.Build(wall, holeLocation));
    }

    [Fact]
    public void EvaluateBuildPlacement_ReportsMissingTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(1, 1));
        var wall = new Wall(session);
        var missingLocation = new GridPoint(100, 100);

        var placement = cave.EvaluateBuildPlacement(wall, missingLocation);

        Assert.False(placement.CanBuild);
        Assert.Contains(placement.Cells, cell =>
            cell.Location == missingLocation &&
            (cell.FailureReasons & BuildPlacementFailureReason.MissingTile) != BuildPlacementFailureReason.None);
    }

    [Fact]
    public void EvaluateBuildPlacement_ReportsMissingOptionalFootprintTiles()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(4, 4);
        var building = new OptionalFootprintBuilding(session);
        var location = new GridPoint(3, 1);

        var placement = cave.EvaluateBuildPlacement(building, location);

        Assert.False(placement.CanBuild);
        Assert.Contains(placement.Cells, cell =>
            cell.Location == new GridPoint(4, 1) &&
            !cell.Required &&
            (cell.FailureReasons & BuildPlacementFailureReason.MissingTile) != BuildPlacementFailureReason.None);
        Assert.False(cave.Build(building, location));
    }

    [Fact]
    public void RemoveBuilding_DoesNotRestorePassabilityForOptionalUnownedFootprintTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(16, 12, new GridPoint(1, 1));
        var optionalLocation = new GridPoint(10, 4);
        var optionalTile = SetWallTile(cave, optionalLocation);
        var turret = new Turret(session);
        var buildLocation = new GridPoint(8, 4);

        Assert.True(cave.Build(turret, buildLocation));
        Assert.Null(optionalTile.Built);

        Assert.True(cave.RemoveBuilding(turret));

        Assert.Equal("wall", optionalTile.Base);
        Assert.False(optionalTile.CreatureCanFit);
        Assert.False(optionalTile.CreatureFits());
        Assert.Null(optionalTile.Built);
    }

    [Fact]
    public void RemoveBuilding_ReleasesAssignedTrilobites()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, new GridPoint(10, 0));
        var miningPost = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 6));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(3, 9), "Miner", "miner");

        miner.SetAssignedBuilding(miningPost);
        miningPost.Assign(miner, null);

        Assert.Same(miningPost, miner.GetAssignedBuilding());
        Assert.Equal(1, miningPost.GetVolume());

        Assert.True(cave.RemoveBuilding(miningPost));

        Assert.Null(miner.GetAssignedBuilding());
        Assert.Equal(0, miningPost.GetVolume());
        Assert.DoesNotContain(miningPost, cave.GetMiningPosts());
    }

    [Fact]
    public void RemoveBuilding_RestoresCreaturesStationedInBarracksToTheirLastTrackedTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(10, 0));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(18, 6));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(18, 6), "Fighter", "fighter");

        fighter.SetAssignedBuilding(barracks);
        Assert.True(barracks.Assign(fighter));
        Assert.True(barracks.IsCreatureStationed(fighter));

        Assert.True(cave.RemoveBuilding(barracks));

        Assert.Equal(fighter.MaxHealth, fighter.Health);
        Assert.Same(cave, fighter.Cave);
        Assert.True(fighter.IsTrackedInTileSystem);
        Assert.Null(fighter.GetAssignedBuilding());
        Assert.Equal(new GridPoint(18, 6), fighter.Location);
        Assert.Same(fighter, cave.GetTrilobiteAtTileKey(fighter.Location.ToString()));
        Assert.Contains(fighter, cave.GetTrilobiteList());
        Assert.False(barracks.IsAssigned(fighter));
    }

    [Fact]
    public void RemoveBuilding_RestoresCreaturesStationedInTurretToTheirLastTrackedTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 16, new GridPoint(14, 0));
        var turret = TestWorldFactory.BuildTurret(cave, session, new GridPoint(18, 6));
        var accessTile = new GridPoint(18, 5);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, accessTile, "Fighter", "fighter");

        fighter.SetAssignedBuilding(turret);
        Assert.True(turret.Assign(fighter));
        Assert.False(fighter.FighterReturnToStation(true));
        Assert.True(turret.IsCreatureStationed(fighter));

        Assert.True(cave.RemoveBuilding(turret));

        Assert.Equal(fighter.MaxHealth, fighter.Health);
        Assert.Same(cave, fighter.Cave);
        Assert.True(fighter.IsTrackedInTileSystem);
        Assert.Null(fighter.HostedBuilding);
        Assert.Null(fighter.HostedWorldPosition);
        Assert.Null(fighter.GetAssignedBuilding());
        Assert.Equal(accessTile, fighter.Location);
        Assert.Same(fighter, cave.GetTrilobiteAtTileKey(accessTile.ToString()));
        Assert.Contains(fighter, cave.GetTile(accessTile.ToString())!.Trilobites);
        Assert.Contains(fighter, cave.GetTrilobiteList());
        Assert.False(turret.IsAssigned(fighter));
    }

    [Fact]
    public void EnemyField_ClearsWhenDangerEnds()
    {
        var (session, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        cave.RevealCave();
        var enemyTile = cave.GetReachableTiles()
            .FirstOrDefault(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString() && tile.Trilobites.Count == 0)
            ?? throw new InvalidOperationException("No reachable enemy spawn tile was available for the enemy BFS death test.");
        var enemy = new Enemy("Target", enemyTile.Coordinates, session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        var enemyField = cave.GetBfsFieldObject("enemy")
            ?? throw new InvalidOperationException("Expected the enemy BFS field to exist.");
        enemyField.Rebuild();
        Assert.Contains(enemy, enemyField.TrackedCreatures);

        enemy.TakeDamage(enemy.Health, "test");

        Assert.Null(enemy.Cave);
        Assert.Empty(cave.GetEnemyList());
        Assert.DoesNotContain(enemy, enemyField.TrackedCreatures);
        Assert.DoesNotContain(enemy, enemyField.UpdatedCreatures);
        Assert.Empty(enemyField.GetField(false));
        Assert.Equal(int.MaxValue, enemyField.GetFieldValue(trilobite.Location, refresh: false));
        Assert.Null(enemyField.GetNextStep(trilobite.Location, refresh: false));
    }

    [Fact]
    public void BuildAfterEnemyDeath_KeepsClearedEnemyFieldStable()
    {
        var (session, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        cave.RevealCave();
        var enemyTile = cave.GetReachableTiles()
            .FirstOrDefault(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString() && tile.Trilobites.Count == 0)
            ?? throw new InvalidOperationException("No reachable enemy spawn tile was available for the post-death build test.");
        var enemy = new Enemy("Builder Freeze Target", enemyTile.Coordinates, session);
        var miningPost = new MiningPost(session);
        var buildLocation = TestWorldFactory.FindBuildLocation(cave, miningPost);

        Assert.True(cave.Spawn(enemy, enemyTile));
        cave.RefreshBfsField("enemy");

        enemy.TakeDamage(enemy.Health, "test");

        Assert.True(cave.Build(miningPost, buildLocation));
        var enemyField = cave.GetBfsFieldObject("enemy")
            ?? throw new InvalidOperationException("Expected the enemy BFS field to exist after building.");
        Assert.DoesNotContain(enemy, enemyField.TrackedCreatures);
        Assert.DoesNotContain(enemy, enemyField.UpdatedCreatures);
        Assert.Empty(enemyField.GetField(false));
        Assert.Equal(int.MaxValue, enemyField.GetFieldValue(trilobite.Location, refresh: false));
    }

    private static Tile SetWallTile(Cave cave, GridPoint location)
    {
        var tile = cave.GetTile(location)
            ?? throw new InvalidOperationException($"Expected tile at {location}.");
        tile.SetBase("wall");
        tile.CreatureCanFit = false;
        tile.ConfigureWall(1);
        return tile;
    }

    private sealed class OptionalFootprintBuilding : Building
    {
        public OptionalFootprintBuilding(GameSession session)
            : base("Optional Footprint", new GridPoint(2, 1), [[1, 2]], session, false)
        {
        }
    }
}
