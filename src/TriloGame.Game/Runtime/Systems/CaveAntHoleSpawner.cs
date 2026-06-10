using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Systems;

public sealed class CaveAntHoleSpawner : IAntHoleSpawner
{
    public AntSpawnAttemptResult TrySpawnAnt(GameSession session, AntSpawnConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(session);

        var cave = session.Cave;
        var queen = cave?.GetQueenBuilding();
        if (cave is null || queen is null)
        {
            return new AntSpawnAttemptResult(false, "Cannot spawn ants without an active cave and queen.");
        }

        var queenCenter = queen.GetCenter();
        var orderedCandidates = cave.GetTiles()
            .OrderBy(tile => GridPoint.ManhattanDistance(tile.Coordinates, queenCenter))
            .ThenBy(tile => tile.Key, StringComparer.Ordinal)
            .ToArray();
        var selectedCandidate = default(AntHoleCandidate?);
        var relaxedMinDistance = constraints.MinDistanceFromQueen;
        var candidateEvaluations = 0;
        var previewFailureCount = 0;
        var outOfRangeSpawnCount = 0;
        var unreachableSpawnCount = 0;

        for (var minimumDistance = constraints.MinDistanceFromQueen; minimumDistance >= 0; minimumDistance--)
        {
            foreach (var tile in orderedCandidates)
            {
                if (!cave.IsTileRevealed(tile) || !cave.CanPlaceAntHole(tile))
                {
                    continue;
                }

                var distance = GridPoint.ManhattanDistance(tile.Coordinates, queenCenter);
                if (distance < minimumDistance || distance > constraints.MaxDistanceFromQueen)
                {
                    continue;
                }

                candidateEvaluations++;
                var previewSpawnTiles = cave.PreviewAntHoleSpawnTiles(tile, 1);
                if (previewSpawnTiles.Count == 0)
                {
                    previewFailureCount++;
                    continue;
                }

                var spawnTile = previewSpawnTiles[0];
                var spawnDistance = GridPoint.ManhattanDistance(spawnTile.Coordinates, queenCenter);
                if (spawnDistance < minimumDistance || spawnDistance > constraints.MaxDistanceFromQueen)
                {
                    outOfRangeSpawnCount++;
                    continue;
                }

                if (!cave.IsTileReachable(spawnTile))
                {
                    unreachableSpawnCount++;
                    continue;
                }

                selectedCandidate = new AntHoleCandidate(tile, spawnTile);
                relaxedMinDistance = minimumDistance;
                break;
            }

            if (selectedCandidate is not null)
            {
                break;
            }
        }

        if (selectedCandidate is null)
        {
            return new AntSpawnAttemptResult(
                false,
                $"No valid ant-hole candidate was found in range 0-{constraints.MaxDistanceFromQueen} with a queen path after relaxing the minimum distance from {constraints.MinDistanceFromQueen}. Candidate evaluations: {candidateEvaluations}, preview failures: {previewFailureCount}, out-of-range spawn tiles: {outOfRangeSpawnCount}, unreachable spawn tiles: {unreachableSpawnCount}.");
        }

        var selectedHoleTile = selectedCandidate.Value.HoleTile;
        var selectedSpawnTile = selectedCandidate.Value.SpawnTile;
        if (!cave.SpawnAntHole(selectedHoleTile, 1, constraints.SpawnSourceId))
        {
            return new AntSpawnAttemptResult(
                false,
                $"SpawnAntHole failed after validation for hole {selectedHoleTile.Key}.",
                null,
                selectedHoleTile.Key,
                selectedSpawnTile.Key);
        }

        var rangeMessage = relaxedMinDistance == constraints.MinDistanceFromQueen
            ? $"Scheduled ant through hole {selectedHoleTile.Key}."
            : $"Scheduled ant through hole {selectedHoleTile.Key} after relaxing minimum distance from {constraints.MinDistanceFromQueen} to {relaxedMinDistance}.";
        return new AntSpawnAttemptResult(
            true,
            rangeMessage,
            null,
            selectedHoleTile.Key,
            selectedSpawnTile.Key);
    }

    private readonly record struct AntHoleCandidate(Tile HoleTile, Tile SpawnTile);
}
