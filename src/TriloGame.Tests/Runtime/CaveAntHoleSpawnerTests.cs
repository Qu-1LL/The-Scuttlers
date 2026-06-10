using TriloGame.Game.Core.Constants;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Runtime;

public sealed class CaveAntHoleSpawnerTests
{
    [Fact]
    public void TrySpawnAnt_UsesARevealedReachableHoleWithinConfiguredRange()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(90, 90, new GridPoint(20, 20));
        var spawner = new CaveAntHoleSpawner();

        var result = spawner.TrySpawnAnt(session, new AntSpawnConstraints(30, 50));

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.HoleTileKey);
        Assert.NotNull(result.SpawnTileKey);
        var holeTile = cave.GetTile(result.HoleTileKey!);
        var spawnTile = cave.GetTile(result.SpawnTileKey!);
        Assert.NotNull(holeTile);
        Assert.NotNull(spawnTile);
        Assert.InRange(GridPoint.ManhattanDistance(holeTile!.Coordinates, queen.GetCenter()), 30, 50);
        Assert.InRange(GridPoint.ManhattanDistance(spawnTile!.Coordinates, queen.GetCenter()), 30, 50);
        Assert.True(cave.IsTileReachable(spawnTile!));
        Assert.Single(cave.GetAntHoles());
        Assert.Empty(cave.GetEnemyList());

        for (var tick = 0; tick < GameConstants.AntHoleSpawnDelayTicks; tick++)
        {
            cave.TickSurfaceFeatures();
        }

        Assert.Empty(cave.GetAntHoles());
        Assert.Single(cave.GetEnemyList());
    }

    [Fact]
    public void TrySpawnAnt_ReturnsFailureWhenNoHoleCanMeetRangeAndPathRules()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 30, new GridPoint(10, 10));
        var spawner = new CaveAntHoleSpawner();
        foreach (var tile in cave.GetTiles())
        {
            if (queen.TileArray.Contains(tile))
            {
                continue;
            }

            tile.SetBase("wall");
        }

        var result = spawner.TrySpawnAnt(session, new AntSpawnConstraints(30, 50));

        Assert.False(result.Success);
        Assert.Contains("No valid ant-hole candidate", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySpawnAnt_RelaxesMinimumDistanceWhenIdealRingHasNoValidHole()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(35, 35, new GridPoint(17, 17));
        var spawner = new CaveAntHoleSpawner();

        foreach (var tile in cave.GetTiles())
        {
            if (queen.TileArray.Contains(tile))
            {
                continue;
            }

            var distance = GridPoint.ManhattanDistance(tile.Coordinates, queen.GetCenter());
            if (distance >= 30 && distance <= 50)
            {
                tile.SetBase("wall");
            }
        }

        var result = spawner.TrySpawnAnt(session, new AntSpawnConstraints(30, 50));

        Assert.True(result.Success, result.Message);
        Assert.Contains("relaxing minimum distance", result.Message, StringComparison.Ordinal);
        var spawnTile = cave.GetTile(result.SpawnTileKey!);
        Assert.NotNull(spawnTile);
        Assert.True(GridPoint.ManhattanDistance(spawnTile!.Coordinates, queen.GetCenter()) < 30);
        Assert.True(cave.IsTileReachable(spawnTile));
    }
}
