using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Runtime;

public sealed class AntHandlerTests
{
    [Fact]
    public void Advance_AttemptsScheduledSpawnsAcrossTheConfiguredWindow()
    {
        var session = new GameSession();
        session.Runtime.DisableEnemySpawns = false;
        var spawner = new FakeAntHoleSpawner();
        var handler = new AntHandler(spawner);
        var round = new RoundInfo(
            0,
            0d,
            GameConstants.RoundDurationMs,
            GameConstants.RoundZeroGraceDurationMs,
            GameConstants.RoundSpawnWindowDurationMs,
            GameConstants.RoundBaseAntCount,
            true);

        handler.HandleRoundStarted(round);

        var firstSpawnOffsetMs = GameConstants.RoundSpawnWindowDurationMs / 4d;
        handler.Advance(session, round with { ElapsedGameTimeMs = GameConstants.RoundZeroGraceDurationMs + firstSpawnOffsetMs - 1d });
        Assert.Equal(0, spawner.AttemptCount);

        handler.Advance(session, round with { ElapsedGameTimeMs = GameConstants.RoundZeroGraceDurationMs + firstSpawnOffsetMs });
        Assert.Equal(3, spawner.AttemptCount);

        handler.Advance(session, round with { ElapsedGameTimeMs = GameConstants.RoundZeroGraceDurationMs + GameConstants.RoundSpawnWindowDurationMs });
        Assert.Equal(GameConstants.RoundBaseAntCount, spawner.AttemptCount);
    }

    [Fact]
    public void GetRemainingKillsForRound_GracePeriodStartsAtZero()
    {
        var session = new GameSession();
        var handler = new AntHandler(new FakeAntHoleSpawner());
        var round = new RoundInfo(
            0,
            0d,
            GameConstants.RoundDurationMs,
            GameConstants.RoundZeroGraceDurationMs,
            GameConstants.RoundSpawnWindowDurationMs,
            GameConstants.RoundBaseAntCount,
            true);

        handler.HandleRoundStarted(round);

        Assert.Equal(0, handler.GetRemainingKillsForRound(session, round));
        Assert.True(handler.CanSkipCurrentRound(session, round));
    }

    [Fact]
    public void HandleRoundEnded_ClearsOutstandingSpawns()
    {
        var session = new GameSession();
        session.Runtime.DisableEnemySpawns = false;
        var spawner = new FakeAntHoleSpawner();
        var handler = new AntHandler(spawner);
        var round = new RoundInfo(
            1,
            0d,
            GameConstants.RoundDurationMs,
            0d,
            GameConstants.RoundSpawnWindowDurationMs,
            GameConstants.RoundBaseAntCount + GameConstants.RoundAntGrowthPerRound,
            false);

        handler.HandleRoundStarted(round);
        handler.HandleRoundEnded(round);
        handler.Advance(session, round with { ElapsedGameTimeMs = GameConstants.RoundSpawnWindowDurationMs });

        Assert.Equal(0, spawner.AttemptCount);
    }

    [Fact]
    public void Advance_RoundsOneThroughFive_SpawnOneAntHoleAtATime()
    {
        var session = new GameSession();
        session.Runtime.DisableEnemySpawns = false;
        var spawner = new FakeAntHoleSpawner();
        var handler = new AntHandler(spawner);
        var round = new RoundInfo(
            2,
            0d,
            GameConstants.RoundDurationMs,
            0d,
            GameConstants.RoundSpawnWindowDurationMs,
            GameConstants.RoundBaseAntCount + (GameConstants.RoundAntGrowthPerRound * 2),
            false);

        handler.HandleRoundStarted(round);

        var expectedSpawnOffsets = new[]
        {
            GameConstants.RoundSpawnWindowDurationMs / 16d,
            (GameConstants.RoundSpawnWindowDurationMs * 3d) / 16d,
            (GameConstants.RoundSpawnWindowDurationMs * 5d) / 16d,
            (GameConstants.RoundSpawnWindowDurationMs * 7d) / 16d
        };

        foreach (var spawnOffsetMs in expectedSpawnOffsets)
        {
            var priorAttemptCount = spawner.AttemptCount;
            handler.Advance(session, round with { ElapsedGameTimeMs = spawnOffsetMs });
            var attemptsThisMoment = spawner.AttemptCount - priorAttemptCount;
            Assert.Equal(1, attemptsThisMoment);
        }

        handler.Advance(session, round with { ElapsedGameTimeMs = GameConstants.RoundSpawnWindowDurationMs });
        Assert.Equal(round.AntsToSpawn, spawner.AttemptCount);
    }

    [Fact]
    public void Advance_RoundsAfterFive_BatchSpawnEventsIntoOneToThreeAntHolesPerSpawnMoment()
    {
        var session = new GameSession();
        session.Runtime.DisableEnemySpawns = false;
        var spawner = new FakeAntHoleSpawner();
        var handler = new AntHandler(spawner);
        var round = new RoundInfo(
            6,
            0d,
            GameConstants.RoundDurationMs,
            0d,
            GameConstants.RoundSpawnWindowDurationMs,
            GameConstants.RoundBaseAntCount + (GameConstants.RoundAntGrowthPerRound * 6),
            false);

        handler.HandleRoundStarted(round);

        var expectedSpawnOffsets = new[]
        {
            GameConstants.RoundSpawnWindowDurationMs / 16d,
            (GameConstants.RoundSpawnWindowDurationMs * 3d) / 16d,
            (GameConstants.RoundSpawnWindowDurationMs * 5d) / 16d,
            (GameConstants.RoundSpawnWindowDurationMs * 7d) / 16d
        };

        foreach (var spawnOffsetMs in expectedSpawnOffsets)
        {
            var priorAttemptCount = spawner.AttemptCount;
            handler.Advance(session, round with { ElapsedGameTimeMs = spawnOffsetMs });
            var attemptsThisMoment = spawner.AttemptCount - priorAttemptCount;
            Assert.InRange(attemptsThisMoment, 1, 3);
        }

        handler.Advance(session, round with { ElapsedGameTimeMs = GameConstants.RoundSpawnWindowDurationMs });
        Assert.Equal(round.AntsToSpawn, spawner.AttemptCount);
    }

    [Fact]
    public void CanSkipCurrentRound_RequiresAllScheduledSpawnsToFinishAndTrackedAntHolesToClear()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(90, 90, new GridPoint(20, 20));
        session.Runtime.DisableEnemySpawns = false;
        cave.RevealCave();
        var handler = new AntHandler(new FakeAntHoleSpawner(spawnIntoSession: true));
        var round = new RoundInfo(
            1,
            0d,
            GameConstants.RoundDurationMs,
            0d,
            0d,
            1,
            false);

        handler.HandleRoundStarted(round);
        handler.Advance(session, round);

        Assert.False(handler.CanSkipCurrentRound(session, round));
        Assert.Equal(1, handler.GetRemainingKillsForRound(session, round));
        Assert.Single(cave.GetAntHoles());
        Assert.Empty(cave.GetEnemyList());

        for (var tick = 0; tick < GameConstants.AntHoleSpawnDelayTicks; tick++)
        {
            cave.TickSurfaceFeatures();
        }

        var ant = cave.GetEnemyList().Single();
        Assert.Empty(cave.GetAntHoles());
        Assert.False(handler.CanSkipCurrentRound(session, round));
        Assert.Equal(1, handler.GetRemainingKillsForRound(session, round));

        Assert.True(ant.RemoveFromGame("test"));
        Assert.False(session.Danger);
        Assert.Empty(cave.GetAntHoles());

        Assert.Equal(0, handler.GetRemainingKillsForRound(session, round));
        Assert.True(handler.CanSkipCurrentRound(session, round));
    }

    private sealed class FakeAntHoleSpawner : IAntHoleSpawner
    {
        private readonly bool _spawnIntoSession;

        public FakeAntHoleSpawner(bool spawnIntoSession = false)
        {
            _spawnIntoSession = spawnIntoSession;
        }

        public int AttemptCount { get; private set; }

        public AntSpawnAttemptResult TrySpawnAnt(GameSession session, AntSpawnConstraints constraints)
        {
            AttemptCount++;
            if (_spawnIntoSession)
            {
                var cave = session.Cave!;
                var queenCenter = cave.GetQueenBuilding()!.GetCenter();
                var holeTile = cave.GetTiles()
                    .First(tile =>
                        cave.IsTileRevealed(tile) &&
                        cave.CanPlaceAntHole(tile) &&
                        GridPoint.ManhattanDistance(tile.Coordinates, queenCenter) >= constraints.MinDistanceFromQueen &&
                        GridPoint.ManhattanDistance(tile.Coordinates, queenCenter) <= constraints.MaxDistanceFromQueen &&
                        cave.PreviewAntHoleSpawnTiles(tile, 1).Count > 0);
                var spawned = cave.SpawnAntHole(holeTile, 1, constraints.SpawnSourceId);
                Assert.True(spawned);
                var spawnTileKey = cave.PreviewAntHoleSpawnTiles(holeTile, 1).Single().Key;
                return new AntSpawnAttemptResult(true, "ok", null, holeTile.Key, spawnTileKey);
            }

            return new AntSpawnAttemptResult(true, "ok", null, $"hole-{AttemptCount}", $"spawn-{AttemptCount}");
        }
    }
}
