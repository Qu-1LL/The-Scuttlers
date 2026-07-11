using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.World;

public sealed class CaveGenerationTests
{
    [Fact]
    public void NewCave_GeneratesTilesWallsAndGuaranteedOreTypes()
    {
        var session = new GameSession();
        var cave = new Cave(session);

        Assert.Same(cave, session.Cave);
        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.CHITINSTONE.Name);
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.LUMENITE.Name);
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.MYCOCORE.Name);
    }

    [Fact]
    public void NewCave_GeneratesSparseFloorHolesAsObstacleEmptyTiles()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var floorHoles = cave.GetTiles()
            .Where(tile => !tile.HasFloorCover)
            .ToArray();

        Assert.NotEmpty(floorHoles);
        Assert.All(floorHoles, tile =>
        {
            Assert.Equal("empty", tile.Base);
            Assert.False(tile.CreatureFits());
        });
    }

    [Fact]
    public void NewCave_GeneratesFloorHoleClustersAtLeastThreeByThree()
    {
        var (_, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var floorHoleComponents = CollectFloorHoleComponents(cave);

        Assert.NotEmpty(floorHoleComponents);
        foreach (var component in floorHoleComponents)
        {
            var minX = component.Min(tile => tile.Coordinates.X);
            var maxX = component.Max(tile => tile.Coordinates.X);
            var minY = component.Min(tile => tile.Coordinates.Y);
            var maxY = component.Max(tile => tile.Coordinates.Y);

            Assert.True(component.Count >= 9);
            Assert.True(maxX - minX + 1 >= 3);
            Assert.True(maxY - minY + 1 >= 3);
        }
    }

    [Fact]
    public void BuildCellularFloorHoleShape_AlwaysPreservesAMinimumThreeByThreeCore()
    {
        var shape = CaveGenerator.BuildCellularFloorHoleShape(6, 6);
        var centerX = shape.GetLength(0) / 2;
        var centerY = shape.GetLength(1) / 2;
        var filledCount = 0;

        for (var x = centerX - 1; x <= centerX + 1; x++)
        {
            for (var y = centerY - 1; y <= centerY + 1; y++)
            {
                Assert.True(shape[x, y]);
            }
        }

        for (var x = 0; x < shape.GetLength(0); x++)
        {
            for (var y = 0; y < shape.GetLength(1); y++)
            {
                if (shape[x, y])
                {
                    filledCount++;
                }
            }
        }

        Assert.True(filledCount >= 9);
    }

    [Fact]
    public void NewCave_AssignsQuarterTurnOreRotationsDuringGeneration()
    {
        var session = new GameSession();
        var cave = new Cave(session);
        var oreTiles = cave.GetTiles()
            .Where(tile => tile.IsOreTile())
            .ToArray();

        Assert.NotEmpty(oreTiles);
        Assert.All(oreTiles, tile => Assert.InRange(tile.OreRotationQuarterTurns, 0, 3));
    }

    private static IReadOnlyList<IReadOnlyList<Tile>> CollectFloorHoleComponents(Cave cave)
    {
        var holeTiles = cave.GetTiles()
            .Where(tile => !tile.HasFloorCover)
            .ToDictionary(tile => tile.Key, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<Tile>>();

        foreach (var tile in holeTiles.Values)
        {
            if (!visited.Add(tile.Key))
            {
                continue;
            }

            var component = new List<Tile>();
            var queue = new Queue<Tile>();
            queue.Enqueue(tile);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                EnqueueFloorHoleNeighbor(holeTiles, visited, queue, current.Coordinates.X - 1, current.Coordinates.Y);
                EnqueueFloorHoleNeighbor(holeTiles, visited, queue, current.Coordinates.X + 1, current.Coordinates.Y);
                EnqueueFloorHoleNeighbor(holeTiles, visited, queue, current.Coordinates.X, current.Coordinates.Y - 1);
                EnqueueFloorHoleNeighbor(holeTiles, visited, queue, current.Coordinates.X, current.Coordinates.Y + 1);
            }

            components.Add(component);
        }

        return components;
    }

    private static void EnqueueFloorHoleNeighbor(
        IReadOnlyDictionary<string, Tile> holeTiles,
        ISet<string> visited,
        Queue<Tile> queue,
        int x,
        int y)
    {
        var key = new GridPoint(x, y).ToString();
        if (!visited.Add(key) || !holeTiles.TryGetValue(key, out var tile))
        {
            return;
        }

        queue.Enqueue(tile);
    }

    [Fact]
    public void NewCave_WithExplicitWorldGenerationMethod_UsesThatGenerator()
    {
        var session = new GameSession();
        var cave = new Cave(session, WorldGenerationMethod.Version0);

        Assert.Same(cave, session.Cave);
        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
    }

    [Fact]
    public void NewCave_WithPerlinNoiseWorldGeneration_GeneratesPlayableCaveTiles()
    {
        var session = new GameSession();
        var cave = new Cave(session, WorldGenerationMethod.PerlinNoise);

        Assert.Same(cave, session.Cave);
        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "empty");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
        Assert.Empty(cave.GetBiomeRegions());
    }

    [Fact]
    public void NewCave_WithPerlinRandomWorldGeneration_GeneratesPlayableCaveTiles()
    {
        var session = new GameSession();
        var cave = new Cave(session, WorldGenerationMethod.PerlinRandom);

        Assert.Same(cave, session.Cave);
        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "empty");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
        Assert.Empty(cave.GetBiomeRegions());
    }

    [Fact]
    public void NewCave_WithFractalBrownianMotionWorldGeneration_GeneratesPlayableCaveTiles()
    {
        var session = new GameSession();
        var cave = new Cave(session, WorldGenerationMethod.FractalBrownianMotion);

        Assert.Same(cave, session.Cave);
        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "empty");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
        Assert.Empty(cave.GetBiomeRegions());
    }

    [Fact]
    public void NewCave_WithPatternlessRandomWorldGeneration_GeneratesPlayableCaveTiles()
    {
        var session = new GameSession();
        var cave = new Cave(session, WorldGenerationMethod.PatternlessRandom);

        Assert.Same(cave, session.Cave);
        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "empty");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
        Assert.Empty(cave.GetBiomeRegions());
    }

    [Fact]
    public void NewCave_TracksBiomeRegionsAndTileMembership()
    {
        var session = new GameSession();
        var cave = new Cave(session);

        Assert.Equal(
            BiomeNames.All.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            cave.GetBiomeRegions().Select(region => region.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var biomeTiles = cave.GetTiles().Where(tile => tile.Biome is not null).ToArray();
        Assert.NotEmpty(biomeTiles);

        foreach (var region in cave.GetBiomeRegions())
        {
            Assert.All(region.Tiles, tile =>
            {
                Assert.Same(region, tile.Biome);
                Assert.Equal(region.Name, tile.BiomeName);
                Assert.Same(tile, cave.GetTile(tile.Key));
            });
        }

        Assert.All(biomeTiles, tile =>
        {
            var matchingRegions = cave.GetBiomeRegions().Where(region => region.Tiles.Contains(tile)).ToArray();
            Assert.Single(matchingRegions);
            Assert.Same(matchingRegions[0], tile.Biome);
        });
    }

    [Fact]
    public void RemoveTile_DetachesBiomeMembership()
    {
        var session = new GameSession();
        var cave = new Cave(session);
        var biomeTile = cave.GetTiles().FirstOrDefault(tile => tile.Biome is not null);

        Assert.NotNull(biomeTile);
        var region = biomeTile!.Biome;
        Assert.NotNull(region);

        var removed = cave.RemoveTile(biomeTile.Key);

        Assert.Same(biomeTile, removed);
        Assert.Null(biomeTile.Biome);
        Assert.Null(biomeTile.BiomeName);
        Assert.DoesNotContain(biomeTile, region!.Tiles);
    }
}
