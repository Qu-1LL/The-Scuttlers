using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Movement;

public sealed class MovementReplayTests
{
    [Fact]
    public void IdenticalCommandStreams_ProduceIdenticalPerTickMovementState()
    {
        var first = CreateScenario();
        var second = CreateScenario();

        for (var tick = 0; tick < 40; tick++)
        {
            if (tick == 8)
            {
                first.Creatures[0].ApplyImpulse(new WorldVector(WorldUnits.UnitsPerTile * 2, 0), 77);
                second.Creatures[0].ApplyImpulse(new WorldVector(WorldUnits.UnitsPerTile * 2, 0), 77);
            }

            TickRunner.RunTick(first.Session);
            TickRunner.RunTick(second.Session);
            Assert.Equal(Capture(first.Session, first.Creatures), Capture(second.Session, second.Creatures));
        }
    }

    private static (GameSession Session, IReadOnlyList<Trilobite> Creatures) CreateScenario()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 12, GridPoint.Zero);
        var creatures = new List<Trilobite>();
        for (var index = 0; index < 8; index++)
        {
            var start = new GridPoint(4 + index, 4 + (index % 2) * 2);
            var creature = TestWorldFactory.SpawnTrilobite(cave, session, start, $"Replay {index}");
            var destination = new GridPoint(18 - index, 7 - (index % 2) * 2);
            Assert.True(creature.NavigateTo(destination));
            creatures.Add(creature);
        }

        return (session, creatures);
    }

    private static string Capture(GameSession session, IReadOnlyList<Trilobite> creatures)
    {
        return string.Join(
            ';',
            creatures.Select(creature =>
            {
                var hitbox = session.Combat.GetActiveFor(creature);
                return $"{creature.Id}:{creature.Position.X},{creature.Position.Y}:" +
                       $"{creature.Velocity.X},{creature.Velocity.Y}:" +
                       $"{creature.FacingDirection.X},{creature.FacingDirection.Y}:" +
                       $"{creature.Role}:{creature.Activity}:{creature.MovementCohort}:" +
                       $"{creature.IdleDestination}:{creature.IdleRestTicks}:" +
                       $"{creature.ReservedZone?.Id}:{creature.ActiveMiningClaim}:" +
                       $"{hitbox?.Id},{hitbox?.Shape.Kind},{hitbox?.PreferredTarget?.Id}:" +
                       $"{creature.Health}:{creature.DamageFlashSequence}:" +
                       $"{creature.Inventory.Type},{creature.Inventory.Amount}";
            }));
    }
}
