using TriloGame.Game.Core.Movement;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Movement;

public sealed class CreatureFormationPlannerTests
{
    [Fact]
    public void Build_UsesExactCenterAndAssignsNonOverlappingHexSlotsDeterministically()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 20, GridPoint.Zero);
        var creatures = new[]
        {
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 5), "One"),
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 5), "Two"),
            TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(9, 5), "Three")
        };
        var center = WorldPoint.FromGridPoint(new GridPoint(10, 10)) + new WorldVector(123, 321);

        var first = CreatureFormationPlanner.Build(cave, creatures, center);
        var second = CreatureFormationPlanner.Build(cave, creatures, center);

        Assert.Equal(creatures.Length, first.Count);
        Assert.Contains(first, assignment => assignment.Destination == center);
        Assert.Equal(first, second);
        for (var left = 0; left < first.Count; left++)
        {
            for (var right = left + 1; right < first.Count; right++)
            {
                var required = first[left].Creature.CollisionRadius + first[left].Creature.SeparationPadding +
                               first[right].Creature.CollisionRadius + first[right].Creature.SeparationPadding;
                Assert.True((first[left].Destination - first[right].Destination).LengthSquared >= (long)required * required);
            }
        }
    }
}
