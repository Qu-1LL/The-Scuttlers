using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class WorkerFleeBehaviorTests
{
    [Theory]
    [InlineData("miner")]
    [InlineData("builder")]
    [InlineData("farmer")]
    public void WorkerRoles_FleeTowardQueenWhenEnemyBfsDistanceIsWithinThreshold(string assignment)
    {
        var (session, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();

        var queenFeedTiles = queen.GetFeedTiles().Where(tile => tile.CreatureFits()).ToArray();
        var reachableTiles = cave.GetReachableTiles().Where(tile => tile.CreatureFits()).ToArray();
        var workerTile = reachableTiles
            .Where(tile => MinDistanceToQueen(tile.Coordinates, queenFeedTiles) > 2)
            .FirstOrDefault(tile => reachableTiles.Any(candidate =>
                candidate.Key != tile.Key &&
                !cave.HasCreatureInCell(candidate.Coordinates) &&
                GridPoint.ManhattanDistance(tile.Coordinates, candidate.Coordinates) <= GameConstants.WorkerEnemyFleeRadius))
            ?? throw new InvalidOperationException("No worker tile with a nearby enemy candidate was available.");

        var worker = new Trilobite($"Test {assignment}", workerTile.Coordinates, session)
        {
            Assignment = assignment
        };
        Assert.True(cave.Spawn(worker, workerTile));

        var enemyTile = reachableTiles
            .Where(tile => tile.Key != workerTile.Key && !cave.HasCreatureInCell(tile.Coordinates))
            .OrderBy(tile => GridPoint.ManhattanDistance(workerTile.Coordinates, tile.Coordinates))
            .First(tile => GridPoint.ManhattanDistance(workerTile.Coordinates, tile.Coordinates) <= GameConstants.WorkerEnemyFleeRadius);
        var enemy = new Enemy("Nearby Ant", enemyTile.Coordinates, session);
        Assert.True(cave.Spawn(enemy, enemyTile));

        Assert.True(cave.RefreshDangerState());
        cave.RefreshBfsField("enemy");
        var enemyDistance = cave.GetBfsFieldValue("enemy", worker.Location);
        var initialDistance = MinDistanceToQueen(worker.Location, queenFeedTiles);
        var initialWorldDistance = MinWorldDistanceToQueen(worker, queen);

        for (var tick = 0; tick < 12 && MinWorldDistanceToQueen(worker, queen) >= initialWorldDistance; tick++)
        {
            worker.Move();
            cave.AdvanceCreatureMovement();
        }

        var nextDistance = MinDistanceToQueen(worker.Location, queenFeedTiles);
        var nextWorldDistance = MinWorldDistanceToQueen(worker, queen);
        Assert.True(session.Danger);
        Assert.InRange(enemyDistance, 0, GameConstants.WorkerEnemyFleeRadius - 1);
        Assert.True(
            nextWorldDistance < initialWorldDistance,
            $"Expected {assignment} to move closer to the queen. Cell distance before: {initialDistance}, after: {nextDistance}.");
    }

    [Fact]
    public void HiddenAnts_DoNotTriggerDangerUntilRevealed()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();

        var spawned = cave.SpawnUndiscoveredAntCluster(3);

        Assert.InRange(spawned, 1, 3);
        Assert.False(session.Danger);
        Assert.Equal(spawned, cave.Enemies.Count);
        Assert.All(cave.Enemies, enemy =>
        {
            var tile = cave.GetTile(enemy.Location);
            Assert.NotNull(tile);
            Assert.False(cave.IsTileRevealed(tile!));
        });
    }

    [Fact]
    public void HiddenAnts_DoNotSpawnInsideQueenEnemySpawnExclusionRadius()
    {
        var (_, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(18, 18));
        var blockedTiles = queen.ProjectedTiles
            .Where(tile => tile.CreatureFits() && tile.Built is null)
            .ToArray();

        Assert.NotEmpty(blockedTiles);
        foreach (var tile in blockedTiles)
        {
            cave.RevealedTiles.Remove(tile);
        }

        var spawned = cave.SpawnUndiscoveredAntCluster(3);

        Assert.Equal(0, spawned);
        Assert.Empty(cave.Enemies);
    }

    private static int MinDistanceToQueen(GridPoint location, IEnumerable<TriloGame.Game.Core.World.Tile> queenFeedTiles)
    {
        return queenFeedTiles.Min(tile => GridPoint.ManhattanDistance(location, tile.Coordinates));
    }

    private static long MinWorldDistanceToQueen(Creature creature, Queen queen)
    {
        return queen.GetFeedTiles().Min(tile => (WorldPoint.FromGridPoint(tile.Coordinates) - creature.Position).LengthSquared);
    }
}
