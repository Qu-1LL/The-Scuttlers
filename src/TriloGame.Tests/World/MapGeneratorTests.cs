using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;

namespace TriloGame.Tests.World;

public sealed class MapGeneratorTests
{
    [Fact]
    public void PerlinNoiseMap_DefaultsToAn800SquareMap()
    {
        var map = new MapGenerator().PerlinNoiseMap();

        Assert.Equal(800, map.Width);
        Assert.Equal(800, map.Height);
        Assert.Equal(800 * 800, map.CellCount);
        Assert.True(map.IsInBounds(map.Width / 2, map.Height / 2));
        Assert.True(map.IsInBounds((map.Width / 2) - 1, (map.Height / 2) - 1));
        Assert.False(map.IsInBounds(map.Width, map.Height / 2));
        Assert.False(map.IsInBounds(map.Width / 2, map.Height));
    }

    [Fact]
    public void PerlinNoiseMap_IsDeterministicAndSeedSensitive()
    {
        var generator = new MapGenerator();
        var first = generator.PerlinNoiseMap(size: 64, seed: 17, frequency: 11d, threshold: 0.55d, minimumRegionSize: 0);
        var second = generator.PerlinNoiseMap(size: 64, seed: 17, frequency: 11d, threshold: 0.55d, minimumRegionSize: 0);
        var differentSeed = generator.PerlinNoiseMap(size: 64, seed: 18, frequency: 11d, threshold: 0.55d, minimumRegionSize: 0);

        var anyDifferent = false;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                Assert.Equal(first[x, y], second[x, y]);
                if (!anyDifferent && first[x, y] != differentSeed[x, y])
                {
                    anyDifferent = true;
                }
            }
        }

        Assert.True(anyDifferent);
    }

    [Fact]
    public void NormalizeConcentrationField_ScalesFiniteValuesIntoExpectedRange()
    {
        var field = new ConcentrationField(2, 2);
        field[0, 0] = -2d;
        field[1, 0] = 0d;
        field[0, 1] = 4d;
        field[1, 1] = double.NaN;

        var normalized = MapGenerator.NormalizeConcentrationField(field);

        Assert.Equal(0d, normalized[0, 0]);
        Assert.InRange(normalized[1, 0], 0.3333332d, 0.3333334d);
        Assert.InRange(normalized[0, 1], 0.9999998d, 0.9999999d);
        Assert.InRange(normalized[1, 1], 0.3333332d, 0.3333334d);
    }

    [Fact]
    public void FractalBrownianMotionMap_IsDeterministicAndSeedSensitive()
    {
        var generator = new MapGenerator();
        var first = generator.FractalBrownianMotionMap(size: 64, seed: 17, frequency: 11d, lacunarity: 2d, persistence: 0.5d, iterations: 4, threshold: 0.55d, minimumRegionSize: 0);
        var second = generator.FractalBrownianMotionMap(size: 64, seed: 17, frequency: 11d, lacunarity: 2d, persistence: 0.5d, iterations: 4, threshold: 0.55d, minimumRegionSize: 0);
        var differentSeed = generator.FractalBrownianMotionMap(size: 64, seed: 18, frequency: 11d, lacunarity: 2d, persistence: 0.5d, iterations: 4, threshold: 0.55d, minimumRegionSize: 0);

        var anyDifferent = false;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                Assert.Equal(first[x, y], second[x, y]);
                if (!anyDifferent && first[x, y] != differentSeed[x, y])
                {
                    anyDifferent = true;
                }
            }
        }

        Assert.True(anyDifferent);
    }

    [Fact]
    public void FloodFill_RemovesFloorRegionsAtOrBelowTheRequestedMinimum()
    {
        var generator = new MapGenerator();
        var map = new NoiseMap(8, 4);

        map[0, 0] = NoiseMapTileType.Floor;
        map[1, 0] = NoiseMapTileType.Floor;

        map[4, 1] = NoiseMapTileType.Floor;
        map[5, 1] = NoiseMapTileType.Floor;
        map[6, 1] = NoiseMapTileType.Floor;
        map[4, 2] = NoiseMapTileType.Floor;
        map[5, 2] = NoiseMapTileType.Floor;
        map[6, 2] = NoiseMapTileType.Floor;

        generator.FloodFill(5, map);

        Assert.Equal(NoiseMapTileType.Empty, map[0, 0]);
        Assert.Equal(NoiseMapTileType.Empty, map[1, 0]);
        Assert.Equal(NoiseMapTileType.Floor, map[4, 1]);
        Assert.Equal(NoiseMapTileType.Floor, map[5, 1]);
        Assert.Equal(NoiseMapTileType.Floor, map[6, 1]);
        Assert.Equal(NoiseMapTileType.Floor, map[4, 2]);
        Assert.Equal(NoiseMapTileType.Floor, map[5, 2]);
        Assert.Equal(NoiseMapTileType.Floor, map[6, 2]);
    }

    [Fact]
    public void FillWalls_BuildsABoundaryAndRemovesWallsWithoutFloorNeighbors()
    {
        var generator = new MapGenerator();
        var map = new NoiseMap(5, 5);

        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                map[x, y] = NoiseMapTileType.Floor;
            }
        }

        map[0, 0] = NoiseMapTileType.Wall;

        generator.FillWalls(map);

        Assert.Equal(NoiseMapTileType.Empty, map[0, 0]);
        Assert.Equal(NoiseMapTileType.Wall, map[1, 1]);
        Assert.Equal(NoiseMapTileType.Wall, map[2, 1]);
        Assert.Equal(NoiseMapTileType.Wall, map[3, 1]);
        Assert.Equal(NoiseMapTileType.Wall, map[1, 2]);
        Assert.Equal(NoiseMapTileType.Floor, map[2, 2]);
        Assert.Equal(NoiseMapTileType.Wall, map[3, 2]);
        Assert.Equal(NoiseMapTileType.Wall, map[1, 3]);
        Assert.Equal(NoiseMapTileType.Wall, map[2, 3]);
        Assert.Equal(NoiseMapTileType.Wall, map[3, 3]);
    }

    [Fact]
    public void Generate_WithPerlinNoiseMethod_PopulatesACave()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var generator = new MapGenerator(33333UL);

        generator.Generate(cave, WorldGenerationMethod.PerlinNoise);

        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
    }

    [Fact]
    public void Generate_WithPerlinNoiseMethod_UsesTheFullDefaultNoiseMapSpan()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var generator = new MapGenerator(33333UL);

        generator.Generate(cave, WorldGenerationMethod.PerlinNoise);

        Assert.Contains(cave.GetTiles(), tile => System.Math.Abs(tile.Coordinates.X) > 100 || System.Math.Abs(tile.Coordinates.Y) > 100);
    }

    [Fact]
    public void Generate_WithFractalBrownianMotionMethod_PopulatesACave()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var generator = new MapGenerator(33333UL);

        generator.Generate(cave, WorldGenerationMethod.FractalBrownianMotion);

        Assert.NotEmpty(cave.GetTiles());
        Assert.Contains(cave.GetTiles(), tile => tile.Base == "wall");
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
    }

    [Fact]
    public void PerlinNoiseMap_CarvesAStarterCircleAtTheCenteredSpawn()
    {
        var map = new MapGenerator().PerlinNoiseMap(seed: 17);
        var centerX = map.Width / 2;
        var centerY = map.Height / 2;

        Assert.Equal(NoiseMapTileType.Floor, map[centerX, centerY]);
        Assert.Equal(NoiseMapTileType.Floor, map[centerX - 2, centerY]);
        Assert.Equal(NoiseMapTileType.Floor, map[centerX + 2, centerY]);
        Assert.Equal(NoiseMapTileType.Floor, map[centerX, centerY - 2]);
        Assert.Equal(NoiseMapTileType.Floor, map[centerX, centerY + 2]);
    }

    [Fact]
    public void PerlinNoiseMap_DefaultThresholdKeepsTerrainBeyondTheStarterCircle()
    {
        var map = new MapGenerator().PerlinNoiseMap(size: 128, seed: 17);
        var nonEmptyCount = 0;
        var hasDistantTerrain = false;
        var centerX = map.Width / 2;
        var centerY = map.Height / 2;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[x, y] == NoiseMapTileType.Empty)
                {
                    continue;
                }

                nonEmptyCount++;
                if (!hasDistantTerrain && (System.Math.Abs(x - centerX) > 16 || System.Math.Abs(y - centerY) > 16))
                {
                    hasDistantTerrain = true;
                }
            }
        }

        Assert.True(nonEmptyCount > 220, $"Expected terrain beyond the starter clearing, but only found {nonEmptyCount} non-empty tiles.");
        Assert.True(hasDistantTerrain);
    }

    [Fact]
    public void FractalBrownianMotionMap_DefaultThresholdKeepsTerrainBeyondTheStarterCircle()
    {
        var map = new MapGenerator().FractalBrownianMotionMap(size: 128, seed: 17);
        var nonEmptyCount = 0;
        var hasDistantTerrain = false;
        var centerX = map.Width / 2;
        var centerY = map.Height / 2;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[x, y] == NoiseMapTileType.Empty)
                {
                    continue;
                }

                nonEmptyCount++;
                if (!hasDistantTerrain && (System.Math.Abs(x - centerX) > 16 || System.Math.Abs(y - centerY) > 16))
                {
                    hasDistantTerrain = true;
                }
            }
        }

        Assert.True(nonEmptyCount > 220, $"Expected terrain beyond the starter clearing, but only found {nonEmptyCount} non-empty tiles.");
        Assert.True(hasDistantTerrain);
    }

    [Fact]
    public void WorldGenerationMethods_IncludeFractalBrownianMotion()
    {
        Assert.Contains(WorldGenerationMethod.FractalBrownianMotion, WorldGenerationMethods.All);
        Assert.Equal("Fractal Brownian Motion", WorldGenerationMethods.GetDisplayName(WorldGenerationMethod.FractalBrownianMotion));
    }

    [Fact]
    public void VersionZeroGeneration_WithTheSameSeed_ReproducesTheSameTileLayout()
    {
        var firstSession = new GameSession();
        var secondSession = new GameSession();
        var firstCave = new Cave(firstSession, generateDefaultMap: false);
        var secondCave = new Cave(secondSession, generateDefaultMap: false);

        new MapGenerator(33333UL).VersionZeroGeneration(firstCave);
        new MapGenerator(33333UL).VersionZeroGeneration(secondCave);

        var firstTiles = firstCave.GetTiles()
            .OrderBy(tile => tile.Key, StringComparer.Ordinal)
            .ToArray();
        var secondTiles = secondCave.GetTiles()
            .OrderBy(tile => tile.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(firstTiles.Length, secondTiles.Length);
        for (var index = 0; index < firstTiles.Length; index++)
        {
            Assert.Equal(firstTiles[index].Key, secondTiles[index].Key);
            Assert.Equal(firstTiles[index].Base, secondTiles[index].Base);
            Assert.Equal(firstTiles[index].BiomeName, secondTiles[index].BiomeName);
            Assert.Equal(firstTiles[index].ResourceYield, secondTiles[index].ResourceYield);
            Assert.Equal(firstTiles[index].HitsPerYield, secondTiles[index].HitsPerYield);
        }
    }
}
