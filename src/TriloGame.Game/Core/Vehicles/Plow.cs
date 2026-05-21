using System.Numerics;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Vehicles;

public sealed class Plow : Vehicle
{
    public static readonly GridPoint DefaultSize = new(2, 2);

    public Plow(GameSession session)
        : base(
            "Plow",
            "Plow",
            "farmer",
            DefaultSize,
            40,
            1,
            [new VehicleStationSlot(new Vector2(40f, 0f), MathF.PI * 0.5f)],
            session)
    {
    }

    protected override void OnStationCreature(Creature creature)
    {
    }

    protected override void OnDestationCreature(Creature creature)
    {
    }

    protected override void OnVehicleDestroyed(object? source)
    {
    }
}
