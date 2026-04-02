using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
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
}
