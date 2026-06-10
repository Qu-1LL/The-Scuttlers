using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public readonly record struct AntSpawnConstraints(
    int MinDistanceFromQueen,
    int MaxDistanceFromQueen,
    int? SpawnSourceId = null);

public readonly record struct AntSpawnAttemptResult(
    bool Success,
    string Message,
    Enemy? SpawnedEnemy = null,
    string? HoleTileKey = null,
    string? SpawnTileKey = null);

public interface IAntHoleSpawner
{
    AntSpawnAttemptResult TrySpawnAnt(GameSession session, AntSpawnConstraints constraints);
}
