using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Core.Combat;

public sealed class MiningStrikeSystemTests
{
    [Fact]
    public void MiningStrike_UsesSeparateLifetimeFromCombatHitboxes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, GridPoint.Zero);
        var miner = new Enemy("Mining Ant", new GridPoint(10, 8), session);
        Assert.True(cave.Spawn(miner, cave.GetTile(miner.Location)!));
        var target = cave.GetTile(new GridPoint(11, 8))!;
        target.SetBase("wall");
        target.ConfigureWall(3);
        cave.NotifyMineableTilesChanged([target.Key]);

        Assert.True(session.Mining.TryQueueMining(miner, target.Key));
        session.Mining.Advance(session);

        Assert.Single(session.Mining.Active);
        Assert.Empty(session.Combat.ActiveHitboxes);
    }
}
