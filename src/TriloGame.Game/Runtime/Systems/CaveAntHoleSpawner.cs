using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Systems;

public sealed class CaveAntHoleSpawner : IAntHoleSpawner
{
    // Find a legal ant-hole tile and spawn point pair that respects queen-distance constraints.
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

        // Gradually relax the minimum distance so late-game maps still have legal spawn options.
        for (var minimumDistance = constraints.MinDistanceFromQueen; minimumDistance >= 0; minimumDistance--)
        {
            // Scan candidate hole tiles from nearest to farthest for stable, explainable selection.
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
        // Re-run the actual spawn call only after the hole/spawn pair has passed every preview check.
        if (!cave.SpawnAntHole(selectedHoleTile, 1))
        {
            return new AntSpawnAttemptResult(
                false,
                $"SpawnAntHole failed after validation for hole {selectedHoleTile.Key}.",
                null,
                selectedHoleTile.Key,
                selectedSpawnTile.Key);
        }

        var spawnedEnemy = cave.GetAntHoles()
            .FirstOrDefault(hole => string.Equals(hole.TileKey, selectedHoleTile.Key, StringComparison.Ordinal))
            ?.Ants
            .FirstOrDefault();

        var rangeMessage = relaxedMinDistance == constraints.MinDistanceFromQueen
            ? $"Spawned ant through hole {selectedHoleTile.Key}."
            : $"Spawned ant through hole {selectedHoleTile.Key} after relaxing minimum distance from {constraints.MinDistanceFromQueen} to {relaxedMinDistance}.";
        return new AntSpawnAttemptResult(
            true,
            rangeMessage,
            spawnedEnemy,
            selectedHoleTile.Key,
            selectedSpawnTile.Key);
    }

    private readonly record struct AntHoleCandidate(Tile HoleTile, Tile SpawnTile);
}
