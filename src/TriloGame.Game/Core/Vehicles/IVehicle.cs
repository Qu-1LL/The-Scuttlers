using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Vehicles;

public interface IVehicle
{
    string Name { get; }

    string Description { get; }

    string TextureKey { get; }

    string AssignmentClassification { get; }

    GameSession Session { get; }

    Cave? Cave { get; }

    GridPoint Size { get; }

    GridPoint? Location { get; }

    int Health { get; }

    int MaxHealth { get; }

    int MaxStationedCreatures { get; }

    IReadOnlyCollection<Creature> StationedCreatures { get; }

    IReadOnlyList<VehicleStationSlot> StationSlots { get; }

    IReadOnlyList<Tile> TileArray { get; }

    IReadOnlyList<GridPoint> PathPreview { get; }

    int GetDisplayRotationTurns();

    bool CanStation(Creature creature);

    bool StationCreature(Creature creature);

    bool DestationCreature(Creature creature);

    bool IsCreatureStationed(Creature creature);

    void EnqueueMove(GridPoint destination);

    void ClearMoveQueue();

    object? Move();

    int TakeDamage(int amount, object? source = null);

    bool RemoveFromGame(object? source = null);
}
