using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Core.Combat;

public sealed class CombatDirectorTests
{
    [Fact]
    public void ThreatSectors_AssignFightersToTheHighestPressureAdvances()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 24, new GridPoint(4, 4));
        var first = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 10), "First", "fighter");
        var second = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(28, 10), "Second", "fighter");
        var firstAdvance = new Enemy("First Ant", new GridPoint(11, 10), session);
        var secondAdvance = new Enemy("Second Ant", new GridPoint(29, 10), session);
        Assert.True(cave.Spawn(firstAdvance, cave.GetTile(firstAdvance.Location)!));
        Assert.True(cave.Spawn(secondAdvance, cave.GetTile(secondAdvance.Location)!));

        session.Combat.BeginTick(cave);

        Assert.Equal(2, session.Combat.LastDirectivePlan.AssignedFighterCount);
        Assert.True(session.Combat.TryGetDirective(first.Id, out var firstDirective));
        Assert.True(session.Combat.TryGetDirective(second.Id, out var secondDirective));
        Assert.NotEqual(firstDirective.SectorId, secondDirective.SectorId);
        Assert.Equal(firstAdvance.Id, firstDirective.TargetId);
        Assert.Equal(secondAdvance.Id, secondDirective.TargetId);
    }

    [Fact]
    public void ThreatAssignments_AreStableWhenFighterPresentationOrderChanges()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(32, 20, new GridPoint(4, 4));
        var lowId = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 8), "Low", "fighter");
        var highId = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(24, 8), "High", "fighter");
        var left = new Enemy("Left", new GridPoint(9, 8), session);
        var right = new Enemy("Right", new GridPoint(25, 8), session);
        Assert.True(cave.Spawn(left, cave.GetTile(left.Location)!));
        Assert.True(cave.Spawn(right, cave.GetTile(right.Location)!));

        session.Combat.BeginTick(cave);
        var lowDirective = session.Combat.Directives[lowId.Id];
        var highDirective = session.Combat.Directives[highId.Id];
        session.TickCount++;
        session.Combat.MarkSpatialDirty();
        session.Combat.BeginTick(cave);

        var lowAgain = session.Combat.Directives[lowId.Id];
        var highAgain = session.Combat.Directives[highId.Id];
        Assert.Equal(lowDirective with { AssignmentVersion = lowAgain.AssignmentVersion }, lowAgain);
        Assert.Equal(highDirective with { AssignmentVersion = highAgain.AssignmentVersion }, highAgain);
    }

    [Fact]
    public void ThreatAssignments_BalanceFightersAcrossLiveAnts()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 24, new GridPoint(4, 4));
        var fighters = new[]
        {
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 10), "First", "fighter"),
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(11, 10), "Second", "fighter"),
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(12, 10), "Third", "fighter"),
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(13, 10), "Fourth", "fighter")
        };
        var firstAnt = new Enemy("First Ant", new GridPoint(18, 10), session);
        var secondAnt = new Enemy("Second Ant", new GridPoint(24, 10), session);
        Assert.True(cave.Spawn(firstAnt, cave.GetTile(firstAnt.Location)!));
        Assert.True(cave.Spawn(secondAnt, cave.GetTile(secondAnt.Location)!));

        session.Combat.BeginTick(cave);

        var firstAssignments = 0;
        var secondAssignments = 0;
        for (var index = 0; index < fighters.Length; index++)
        {
            var directive = session.Combat.Directives[fighters[index].Id];
            if (directive.TargetId == firstAnt.Id) firstAssignments++;
            if (directive.TargetId == secondAnt.Id) secondAssignments++;
        }

        Assert.Equal(2, firstAssignments);
        Assert.Equal(2, secondAssignments);
    }
}
