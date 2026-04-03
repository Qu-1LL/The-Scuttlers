using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class SurfaceFeatureTests
{
    [Fact]
    public void TrySpawnQueenOpal_PlacesOpalNextToQueenAndBlocksBuilding()
    {
        var (session, cave, queen) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();

        var spawned = cave.TrySpawnQueenOpal();

        Assert.True(spawned);
        var opal = cave.GetOpalNode();
        Assert.NotNull(opal);
        var tile = cave.GetTile(opal!.TileKey);
        Assert.NotNull(tile);
        Assert.True(cave.IsTileRevealed(tile!));
        Assert.Contains(queen.TileArray.SelectMany(queenTile => queenTile.Neighbors).Distinct(), neighbor => neighbor.Key == tile!.Key);
        Assert.True(cave.HasOpal(tile!));
        Assert.False(cave.CanBuild(new Building("Probe", new GridPoint(1, 1), [[0]], session, false), tile!.Coordinates));
    }

    [Fact]
    public void MineTile_OpalConsumesYieldWithoutGivingInventory()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());

        var opalTile = cave.GetTile(cave.GetOpalNode()!.TileKey)!;
        var miner = new Trilobite("Miner", opalTile.Coordinates, session)
        {
            Assignment = "miner"
        };

        Assert.True(cave.Spawn(miner, opalTile));

        var result = miner.MineTile(opalTile.Key);

        Assert.True(result.HitApplied);
        Assert.False(result.YieldedResource);
        Assert.Equal(0, result.ResourceAmount);
        Assert.Equal(1999, result.RemainingYield);
        Assert.False(miner.HasInventory());
        Assert.Equal(1999, cave.GetOpalNode()!.RemainingYield);
    }

    [Fact]
    public void TickSurfaceFeatures_OpalWarningPhaseIncreasesAntHoleSpawnChance()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());

        for (var index = 0; index < GameConstants.OpalInitialGraceTicks + GameConstants.OpalDormantTicks + 1; index++)
        {
            cave.TickSurfaceFeatures();
        }

        var opal = cave.GetOpalNode();
        Assert.NotNull(opal);
        Assert.Equal(499, cave.GetAntHoleSpawnChanceDenominator());
        Assert.True(opal!.GetRedness() > 0f);
    }

    [Fact]
    public void TickSurfaceFeatures_OpalWarningPhaseMapsSpawnRateAcrossCountdown()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());

        for (var index = 0; index < GameConstants.OpalInitialGraceTicks + GameConstants.OpalDormantTicks + (GameConstants.OpalWarningTicks / 2); index++)
        {
            cave.TickSurfaceFeatures();
        }

        var opal = cave.GetOpalNode();
        Assert.NotNull(opal);
        Assert.InRange(cave.GetAntHoleSpawnChanceDenominator(), 249, 251);
        Assert.InRange(opal!.GetRedness(), 0.14f, 0.16f);
    }

    [Fact]
    public void TickSurfaceFeatures_OpalGracePeriodBlocksEarlyProgression()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());

        for (var index = 0; index < GameConstants.OpalInitialGraceTicks - 1; index++)
        {
            cave.TickSurfaceFeatures();
        }

        var opal = cave.GetOpalNode();
        Assert.NotNull(opal);
        Assert.Equal(GameConstants.AntHoleBaseSpawnChanceDenominator, cave.GetAntHoleSpawnChanceDenominator());
        Assert.False(cave.AllowsNaturalEnemySpawns());
        Assert.Equal(0, opal!.TicksSinceLastMine);
        Assert.Equal(0f, opal.GetWarningProgress());
    }

    [Fact]
    public void TickSurfaceFeatures_AfterGracePeriod_AllowsNaturalEnemySpawnsAgain()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());

        for (var index = 0; index < GameConstants.OpalInitialGraceTicks; index++)
        {
            cave.TickSurfaceFeatures();
        }

        Assert.True(cave.AllowsNaturalEnemySpawns());
        Assert.Equal(GameConstants.AntHoleBaseSpawnChanceDenominator, cave.GetAntHoleSpawnChanceDenominator());
    }

    [Fact]
    public void DisableEnemySpawns_BlocksNaturalEnemySpawns()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());
        session.Runtime.DisableEnemySpawns = true;

        for (var index = 0; index < GameConstants.OpalInitialGraceTicks; index++)
        {
            cave.TickSurfaceFeatures();
        }

        Assert.False(cave.AllowsNaturalEnemySpawns());
    }

    [Fact]
    public void RunTick_WhenOpalFrozen_DoesNotAdvanceOpalProgression()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        Assert.True(cave.TrySpawnQueenOpal());
        session.Runtime.FreezeOpalProgression = true;

        TickRunner.RunTick(session);
        TickRunner.RunTick(session);

        Assert.Equal(0, cave.GetOpalNode()!.TicksSinceLastMine);
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
