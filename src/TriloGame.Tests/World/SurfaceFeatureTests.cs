using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class SurfaceFeatureTests
{
    [Fact]
    public void TickSurfaceFeatures_UsesBaseAntHoleSpawnRules()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        session.Runtime.DisableEnemySpawns = false;
        Assert.Equal(GameConstants.AntHoleBaseSpawnChanceDenominator, cave.GetAntHoleSpawnChanceDenominator());
        Assert.True(cave.AllowsNaturalEnemySpawns());
    }

    [Fact]
    public void DisableEnemySpawns_BlocksNaturalEnemySpawns()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        session.Runtime.DisableEnemySpawns = true;

        Assert.False(cave.AllowsNaturalEnemySpawns());
    }

    [Fact]
    public void SpawnAntHole_WaitsForDelayThenSpawnsAntAndRemovesHole()
    {
        var (_, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        var holeTile = cave.GetTiles()
            .First(tile =>
                cave.IsTileRevealed(tile) &&
                string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
                tile.CreatureFits() &&
                GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter()) >= 15 &&
                tile.Neighbors.Any(neighbor => string.Equals(neighbor.Base, "empty", StringComparison.Ordinal) && neighbor.CreatureFits()));

        Assert.True(cave.SpawnAntHole(holeTile, 1));
        var hole = cave.GetAntHoles().Single();
        Assert.Equal(GameConstants.AntHoleSpawnDelayTicks, hole.RemainingSpawnDelayTicks);
        Assert.Equal(0f, hole.SpawnProgress);
        Assert.Empty(cave.GetEnemyList());

        for (var tick = 0; tick < GameConstants.AntHoleSpawnDelayTicks - 1; tick++)
        {
            cave.TickSurfaceFeatures();
        }

        Assert.Single(cave.GetAntHoles());
        Assert.Equal(1f, hole.SpawnProgress);
        Assert.Empty(cave.GetEnemyList());

        cave.TickSurfaceFeatures();

        Assert.Empty(cave.GetAntHoles());
        Assert.Single(cave.GetEnemyList());
    }

    [Fact]
    public void SpawnAntHole_ClampsToSinglePendingAnt()
    {
        var (_, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        var holeTile = cave.GetTiles()
            .First(tile =>
                cave.IsTileRevealed(tile) &&
                string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
                tile.CreatureFits() &&
                GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter()) >= 15 &&
                tile.Neighbors.Any(neighbor => string.Equals(neighbor.Base, "empty", StringComparison.Ordinal) && neighbor.CreatureFits()));

        Assert.True(cave.SpawnAntHole(holeTile, 3));
        var hole = cave.GetAntHoles().Single();

        Assert.Equal(1, hole.PendingAntCount);
        Assert.Empty(cave.GetEnemyList());
    }

    [Fact]
    public void RefreshDangerState_DoesNotRemovePendingAntHolesBeforeTheyRelease()
    {
        var (session, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        var holeTile = cave.GetTiles()
            .First(tile =>
                cave.IsTileRevealed(tile) &&
                string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
                tile.CreatureFits() &&
                GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter()) >= 15 &&
                tile.Neighbors.Any(neighbor => string.Equals(neighbor.Base, "empty", StringComparison.Ordinal) && neighbor.CreatureFits()));

        Assert.True(cave.SpawnAntHole(holeTile, 1));

        Assert.False(cave.RefreshDangerState());
        Assert.False(session.Danger);
        Assert.Single(cave.GetAntHoles());
        Assert.Empty(cave.GetEnemyList());
    }

    [Fact]
    public void SpawnAntHole_DoesNotSpawnAntsInsideQueenEnemySpawnExclusionRadius()
    {
        var (_, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(40, 40, new GridPoint(10, 10));
        var queenCenter = queen.GetCenter();
        var blockedSpawnTile = cave.GetTile(new GridPoint(queenCenter.X + GameConstants.QueenEnemySpawnExclusionRadius, queenCenter.Y))
            ?? throw new InvalidOperationException("Expected a blocked ant spawn tile to exist.");
        var holeTile = cave.GetTile(new GridPoint(queenCenter.X + GameConstants.QueenEnemySpawnExclusionRadius + 1, queenCenter.Y))
            ?? throw new InvalidOperationException("Expected an ant-hole tile to exist.");

        Assert.Contains(blockedSpawnTile, queen.ProjectedTiles);

        for (var x = holeTile.Coordinates.X - GameConstants.AntHoleSpawnRadius; x <= holeTile.Coordinates.X + GameConstants.AntHoleSpawnRadius; x++)
        {
            for (var y = holeTile.Coordinates.Y - GameConstants.AntHoleSpawnRadius; y <= holeTile.Coordinates.Y + GameConstants.AntHoleSpawnRadius; y++)
            {
                var tile = cave.GetTile(new GridPoint(x, y));
                if (tile is null ||
                    ReferenceEquals(tile, holeTile) ||
                    ReferenceEquals(tile, blockedSpawnTile) ||
                    GridPoint.ManhattanDistance(tile.Coordinates, holeTile.Coordinates) > GameConstants.AntHoleSpawnRadius)
                {
                    continue;
                }

                tile.SetBase("wall");
                tile.CreatureCanFit = false;
                tile.ConfigureWall(1);
            }
        }

        Assert.False(cave.SpawnAntHole(holeTile, 1));
        Assert.Empty(cave.Enemies);
        Assert.Empty(cave.GetAntHoles());
    }

    [Fact]
    public void TickSurfaceFeatures_WhenReleaseTileBecomesBlocked_RemovesHoleWithoutSpawningAnt()
    {
        var (_, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        var holeTile = cave.GetTiles()
            .First(tile =>
                cave.IsTileRevealed(tile) &&
                string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
                tile.CreatureFits() &&
                GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter()) >= 15 &&
                cave.PreviewAntHoleSpawnTiles(tile, 1).Count > 0);

        Assert.True(cave.SpawnAntHole(holeTile, 1));

        for (var x = holeTile.Coordinates.X - GameConstants.AntHoleSpawnRadius; x <= holeTile.Coordinates.X + GameConstants.AntHoleSpawnRadius; x++)
        {
            for (var y = holeTile.Coordinates.Y - GameConstants.AntHoleSpawnRadius; y <= holeTile.Coordinates.Y + GameConstants.AntHoleSpawnRadius; y++)
            {
                var tile = cave.GetTile(new GridPoint(x, y));
                if (tile is null ||
                    ReferenceEquals(tile, holeTile) ||
                    GridPoint.ManhattanDistance(tile.Coordinates, holeTile.Coordinates) > GameConstants.AntHoleSpawnRadius)
                {
                    continue;
                }

                tile.SetBase("wall");
                tile.CreatureCanFit = false;
                tile.ConfigureWall(1);
            }
        }

        for (var tick = 0; tick < GameConstants.AntHoleSpawnDelayTicks; tick++)
        {
            cave.TickSurfaceFeatures();
        }

        Assert.Empty(cave.GetAntHoles());
        Assert.Empty(cave.GetEnemyList());
    }
}
