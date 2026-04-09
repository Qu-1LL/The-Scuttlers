using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class SurfaceFeatureTests
{
    [Fact]
    public void TrySpawnQueenOpal_WhenOpalIsDisabled_ReturnsFalse()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();

        var spawned = cave.TrySpawnQueenOpal();

        Assert.False(spawned);
        Assert.Null(cave.GetOpalNode());
    }

    [Fact]
    public void TickSurfaceFeatures_WhenOpalIsDisabled_UsesBaseAntHoleSpawnRules()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
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
    public void RunTick_WhenOpalIsDisabled_DoesNotCreateOrAdvanceOpalState()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        session.Runtime.FreezeOpalProgression = true;

        TickRunner.RunTick(session);
        TickRunner.RunTick(session);

        Assert.Null(cave.GetOpalNode());
    }

    [Fact]
    public void SpawnAntHole_RemovesHoleWhenLastAntIsDefeated()
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
        var ant = hole.Ants.Single();

        Assert.True(ant.RemoveFromGame("test"));

        Assert.Empty(cave.GetAntHoles());
    }

    [Fact]
    public void SpawnAntHole_ClampsToSingleAnt()
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

        Assert.Single(hole.Ants);
    }

    [Fact]
    public void RefreshDangerState_WhenDangerClears_RemovesAntHoles()
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
        var hole = cave.GetAntHoles().Single();
        var ant = hole.Ants.Single();
        var antTile = cave.GetTile(ant.Location)!;

        cave.RevealedTiles.Remove(antTile);
        Assert.False(cave.RefreshDangerState());
        Assert.False(session.Danger);
        Assert.Empty(cave.GetAntHoles());
    }
}
