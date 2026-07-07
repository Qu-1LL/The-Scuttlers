using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Vehicles;

// Driveable vehicles advance when their driver takes a turn instead of from the autonomous vehicle tick.
public interface IDriveable : IVehicle
{
    Creature? Driver { get; }

    bool IsCreatureDriving(Creature creature);
}
