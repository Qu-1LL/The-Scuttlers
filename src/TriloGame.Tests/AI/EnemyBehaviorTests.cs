using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class EnemyBehaviorTests
{
    [Fact]
    public void SpawningAndRemovingLastEnemy_TogglesDangerState()
    {
        var (session, cave, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var enemyTile = cave.GetReachableTiles()
            .FirstOrDefault(tile => tile.CreatureFits() && tile.Key != trilobite.Location.ToString() && tile.Trilobites.Count == 0)
            ?? throw new InvalidOperationException("No reachable enemy spawn tile was available for the danger-state test.");
        var enemy = new Enemy("Test Enemy", GridPoint.Parse(enemyTile.Key), session);

        Assert.True(cave.Spawn(enemy, enemyTile));
        Assert.True(session.Danger);
        Assert.Single(cave.Enemies);

        enemy.TakeDamage(enemy.Health);

        Assert.False(session.Danger);
        Assert.Empty(cave.Enemies);
    }
}
