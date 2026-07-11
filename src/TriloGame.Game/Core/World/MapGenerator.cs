using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

public sealed partial class MapGenerator
{
    private const double DiagonalPerlinGradient = 0.70710678d;
    public const int WorldSize = 800;
    private const double PerlinWorldFrequency = 80d;
    private const double PerlinWorldThreshold = 0.55d;
    private const int PerlinWorldMinimumRegionSize = 5;
    private const int PerlinStarterClearingRadius = 4;
    private const double FractalBrownianMotionWorldLacunarity = 2d;
    private const double FractalBrownianMotionWorldPersistence = 0.5d;
    private const int FractalBrownianMotionWorldIterations = 4;
    private const double PerlinRandomFrequency = 30d;
    private const double PerlinRandomDensity = 0.55d;
    private const int PerlinRandomAutomataIterations = 3;
    private const int PerlinRandomGrowthIterations = 3;
    private const int PerlinRandomGrowthThreshold = 3;
    private const int PerlinRandomMinimumRegionSize = 21;
    private const double PatternlessRandomDensity = 0.55d;
    private const int PatternlessRandomAutomataIterations = 5;
    private const int PatternlessRandomGrowthIterations = 3;
    private const int PatternlessRandomGrowthThreshold = 3;
    private const int PatternlessRandomMinimumRegionSize = 6;
    private const int PatternlessRandomPostFloodFillGrowthIterations = 2;
    private const int VoronoiBordersRegionCount = 1600;
    private const int VoronoiBordersRandomAutomataIterations = 5;
    private const int VoronoiBordersFinalGrowthIterations = 3;
    private const int VoronoiBordersFinalGrowthThreshold = 3;
    private const int VoronoiBordersMinimumRegionSize = 11;
    private const double NormalizedConcentrationUpperBound = 0.9999999d;
    private const int SizeMult = 30;
    private const int HoleLimit = 10;
    private const double DegradeLimit = 2.75;
    private const double DegradeDeviation = 0.7;
    private const int CavernCount = 25;
    private const int Radius = 20;
    private const int OreMult = 300;
    private const int OreDist = 8;
    private readonly XorShift64 _random;

    public MapGenerator(ulong? seed = null)
    {
        _random = seed.HasValue
            ? new XorShift64(seed.Value)
            : new XorShift64();
    }

    public void Generate(Cave cave, WorldGenerationMethod method)
    {
        ArgumentNullException.ThrowIfNull(cave);

        var pattern = GetGenerationPattern(method);
        pattern.Generate(this, cave);
    }

    // Convert the reusable Perlin noise map into a live cave while preserving the cleaned map topology.
    public void PerlinNoiseGeneration(Cave cave)
    {
        ArgumentNullException.ThrowIfNull(cave);

        var perlinSeed = NextSeed32();
        var map = PerlinNoiseMap(
            size: WorldSize,
            seed: unchecked((int)perlinSeed),
            frequency: PerlinWorldFrequency,
            threshold: PerlinWorldThreshold,
            minimumRegionSize: PerlinWorldMinimumRegionSize);
        PopulateGeneratedNoiseMap(cave, map);
    }

    public NoiseMap PerlinNoiseMap(int size = WorldSize, int seed = 0, double frequency = PerlinWorldFrequency, double threshold = PerlinWorldThreshold, int minimumRegionSize = PerlinWorldMinimumRegionSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRegionSize);
        if (double.IsNaN(frequency) || double.IsInfinity(frequency) || frequency < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be a finite non-negative number.");
        }

        if (double.IsNaN(threshold) || double.IsInfinity(threshold))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be a finite number.");
        }

        var concentrationField = PerlinConcentrationField(size, unchecked((uint)seed), frequency);
        return BuildNoiseMapFromConcentrationField(concentrationField, threshold, minimumRegionSize);
    }

    // Convert the reusable FBM noise map into a live cave while preserving the same cleanup rules as the Perlin generator.
    public void FractalBrownianMotionGeneration(Cave cave)
    {
        ArgumentNullException.ThrowIfNull(cave);

        var noiseSeed = NextSeed32();
        var map = FractalBrownianMotionMap(
            size: WorldSize,
            seed: unchecked((int)noiseSeed),
            frequency: PerlinWorldFrequency,
            lacunarity: FractalBrownianMotionWorldLacunarity,
            persistence: FractalBrownianMotionWorldPersistence,
            iterations: FractalBrownianMotionWorldIterations,
            threshold: PerlinWorldThreshold,
            minimumRegionSize: PerlinWorldMinimumRegionSize);
        PopulateGeneratedNoiseMap(cave, map);
    }

    // Compose the sandbox-style random fill and cleanup steps into a starter-safe world layout.
    public void PatternlessRandomGeneration(
        Cave cave,
        double density = PatternlessRandomDensity,
        int randomAutomataIterations = PatternlessRandomAutomataIterations,
        int cellularGrowthIterations = PatternlessRandomGrowthIterations,
        int cellularGrowthThreshold = PatternlessRandomGrowthThreshold,
        int minimumRegionSize = PatternlessRandomMinimumRegionSize,
        int postFloodFillGrowthIterations = PatternlessRandomPostFloodFillGrowthIterations)
    {
        ArgumentNullException.ThrowIfNull(cave);
        if (!double.IsFinite(density) || density < 0d || density > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(density), "Density must be a finite value between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(randomAutomataIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(cellularGrowthIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(cellularGrowthThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRegionSize);
        ArgumentOutOfRangeException.ThrowIfNegative(postFloodFillGrowthIterations);

        var patternSeed = NextSeed32();
        var graph = new RandomMapGenerator().GenerateMap(WorldSize, patternSeed, density);
        graph = new RandomAutomata().InterpolateMap(graph, patternSeed, randomAutomataIterations);
        graph = new FloodFillTool().InterpolateMap(graph, patternSeed, minimumRegionSize);
        graph = new RandomGrowth().InterpolateMap(graph, patternSeed, postFloodFillGrowthIterations);
        PopulateGeneratedNoiseMap(cave, FinalizeGeneratedMap(graph, minimumRegionSize: 0));
    }

    // Blend Perlin weighting with random fill and smoothing passes to produce a noisier hybrid cave layout.
    public void PerlinRandomGeneration(
        Cave cave,
        double frequency = PerlinRandomFrequency,
        double density = PerlinRandomDensity,
        int randomAutomataIterations = PerlinRandomAutomataIterations,
        int cellularGrowthIterations = PerlinRandomGrowthIterations,
        int cellularGrowthThreshold = PerlinRandomGrowthThreshold,
        int minimumRegionSize = PerlinRandomMinimumRegionSize)
    {
        ArgumentNullException.ThrowIfNull(cave);
        if (!double.IsFinite(frequency) || frequency < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be a finite non-negative number.");
        }

        if (!double.IsFinite(density) || density < 0d || density > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(density), "Density must be a finite value between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(randomAutomataIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(cellularGrowthIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(cellularGrowthThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRegionSize);

        var patternSeed = NextSeed32();
        var graph = new PerlinNoise().GenerateConcentrationField(WorldSize, patternSeed, frequency);
        graph = new RandomMapGenerator().InterpolateConcentrationField(graph, patternSeed, density);
        graph = new RandomAutomata().InterpolateMap(graph, patternSeed, randomAutomataIterations);
        graph = new CellularGrowth().InterpolateMap(graph, patternSeed, cellularGrowthIterations, cellularGrowthThreshold);
        PopulateGeneratedGraph(cave, graph, minimumRegionSize);
    }

    // Expand inverted Voronoi borders into cave tunnels, then smooth and clean the result into a starter-safe layout.
    public void VoronoiBordersGeneration(
        Cave cave,
        int regionCount = VoronoiBordersRegionCount,
        int randomAutomataIterations = VoronoiBordersRandomAutomataIterations,
        int finalCellularGrowthIterations = VoronoiBordersFinalGrowthIterations,
        int finalCellularGrowthThreshold = VoronoiBordersFinalGrowthThreshold,
        int minimumRegionSize = VoronoiBordersMinimumRegionSize)
    {
        ArgumentNullException.ThrowIfNull(cave);
        ArgumentOutOfRangeException.ThrowIfNegative(regionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(randomAutomataIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(finalCellularGrowthIterations);
        ArgumentOutOfRangeException.ThrowIfNegative(finalCellularGrowthThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRegionSize);

        var patternSeed = NextSeed32();
        var graph = new VoronoiRegions().GenerateMap(WorldSize, patternSeed, regionCount);
        graph = InvertBinaryGraph(graph);
        graph = new RandomAutomata().InterpolateMap(graph, patternSeed, randomAutomataIterations);
        graph = new CellularGrowth().InterpolateMap(graph, patternSeed, finalCellularGrowthIterations, finalCellularGrowthThreshold);
        PopulateGeneratedGraph(cave, graph, minimumRegionSize);
    }

    // Threshold the normalized FBM field into cave tiles, then apply the same starter clearing and cleanup passes as Perlin generation.
    public NoiseMap FractalBrownianMotionMap(
        int size = WorldSize,
        int seed = 0,
        double frequency = PerlinWorldFrequency,
        double lacunarity = FractalBrownianMotionWorldLacunarity,
        double persistence = FractalBrownianMotionWorldPersistence,
        int iterations = FractalBrownianMotionWorldIterations,
        double threshold = PerlinWorldThreshold,
        int minimumRegionSize = PerlinWorldMinimumRegionSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRegionSize);
        ArgumentOutOfRangeException.ThrowIfNegative(iterations);
        if (double.IsNaN(frequency) || double.IsInfinity(frequency) || frequency < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be a finite non-negative number.");
        }

        if (double.IsNaN(lacunarity) || double.IsInfinity(lacunarity) || lacunarity < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(lacunarity), "Lacunarity must be a finite non-negative number.");
        }

        if (double.IsNaN(persistence) || double.IsInfinity(persistence) || persistence < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(persistence), "Persistence must be a finite non-negative number.");
        }

        if (double.IsNaN(threshold) || double.IsInfinity(threshold))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be a finite number.");
        }

        var concentrationField = FractalBrownianMotionConcentrationField(
            size,
            unchecked((uint)seed),
            frequency,
            lacunarity,
            persistence,
            iterations);
        return BuildNoiseMapFromConcentrationField(concentrationField, threshold, minimumRegionSize);
    }

    // Normalize a concentration field into the JavaScript-compatible [0, 0.9999999] range.
    public static ConcentrationField NormalizeConcentrationField(ConcentrationField concentrationField)
    {
        ArgumentNullException.ThrowIfNull(concentrationField);

        var normalizedField = new ConcentrationField(concentrationField.Width, concentrationField.Height);
        var minValue = double.PositiveInfinity;
        var maxValue = double.NegativeInfinity;

        for (var y = 0; y < concentrationField.Height; y++)
        {
            for (var x = 0; x < concentrationField.Width; x++)
            {
                var value = concentrationField[x, y];
                if (!double.IsFinite(value))
                {
                    value = 0d;
                }

                minValue = System.Math.Min(minValue, value);
                maxValue = System.Math.Max(maxValue, value);
            }
        }

        if (!double.IsFinite(minValue) || !double.IsFinite(maxValue))
        {
            return normalizedField;
        }

        var range = maxValue - minValue;
        if (range <= 0d)
        {
            return normalizedField;
        }

        for (var y = 0; y < concentrationField.Height; y++)
        {
            for (var x = 0; x < concentrationField.Width; x++)
            {
                var value = concentrationField[x, y];
                if (!double.IsFinite(value))
                {
                    value = 0d;
                }

                normalizedField[x, y] = ((value - minValue) / range) * NormalizedConcentrationUpperBound;
            }
        }

        return normalizedField;
    }

    // Share the threshold, starter-clearing, flood-fill, and wall passes across noise-based generators.
    private NoiseMap BuildNoiseMapFromConcentrationField(ConcentrationField concentrationField, double threshold, int minimumRegionSize)
    {
        var graph = GenerationGraph.FromConcentrationField(concentrationField);
        new RawThreshold().InterpolateConcentrationField(graph, seed: 0u, threshold);
        return FinalizeGeneratedMap(graph, minimumRegionSize);
    }

    public void FloodFill(NoiseMap map)
    {
        FloodFill(5, map);
    }

    // Remove floor regions at or below the requested size threshold using four-direction connectivity.
    public void FloodFill(int minimumRegionSize, NoiseMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRegionSize);

        var visited = new bool[map.CellCount];
        var frontier = new Queue<int>();
        var region = new List<int>();

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var startIndex = GetMapIndex(map, x, y);
                if (visited[startIndex] || map[x, y] != NoiseMapTileType.Floor)
                {
                    continue;
                }

                visited[startIndex] = true;
                frontier.Enqueue(startIndex);
                region.Clear();

                while (frontier.Count > 0)
                {
                    var currentIndex = frontier.Dequeue();
                    region.Add(currentIndex);

                    var currentX = currentIndex % map.Width;
                    var currentY = currentIndex / map.Width;
                    EnqueueFloorNeighbor(map, currentX - 1, currentY, visited, frontier);
                    EnqueueFloorNeighbor(map, currentX + 1, currentY, visited, frontier);
                    EnqueueFloorNeighbor(map, currentX, currentY - 1, visited, frontier);
                    EnqueueFloorNeighbor(map, currentX, currentY + 1, visited, frontier);
                }

                if (region.Count <= minimumRegionSize)
                {
                    for (var index = 0; index < region.Count; index++)
                    {
                        var tileIndex = region[index];
                        map[tileIndex % map.Width, tileIndex / map.Width] = NoiseMapTileType.Empty;
                    }
                }
            }
        }
    }

    // Build a one-tile wall boundary around floor space.
    public void FillWalls(NoiseMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var emptyToWalls = new List<int>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (IsEmptyOrOutOfBounds(map, x, y) && BordersFloor(map, x, y))
                {
                    emptyToWalls.Add(GetMapIndex(map, x, y));
                }
            }
        }

        for (var index = 0; index < emptyToWalls.Count; index++)
        {
            var tileIndex = emptyToWalls[index];
            map[tileIndex % map.Width, tileIndex / map.Width] = NoiseMapTileType.Wall;
        }

    }

    // Sample a seeded square concentration field at tile centers so integer frequencies avoid lattice collapse.
    private ConcentrationField PerlinConcentrationField(int size = WorldSize, uint seed = 0, double frequency = PerlinWorldFrequency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        var graph = new PerlinNoise().GenerateConcentrationField(size, seed, frequency);
        return graph.ToConcentrationField();
    }

    // Mirror the JavaScript FBM concentration generator by summing weighted Perlin octaves, then normalizing the result once.
    private ConcentrationField FractalBrownianMotionConcentrationField(
        int size,
        uint seed,
        double frequency,
        double lacunarity,
        double persistence,
        int iterations)
    {
        var graph = new FractalBrownianMotion().GenerateConcentrationField(
            size,
            seed,
            frequency,
            lacunarity,
            persistence,
            iterations);
        return graph.ToConcentrationField();
    }

    // Build the legacy cave layout so the default world stays behavior-compatible.
    public void VersionZeroGeneration(Cave cave)
    {
        FillCircle(cave, 0, 0, Radius);

        var origins = new List<GridPoint> { GridPoint.Zero };
        for (var cavernIndex = 0; cavernIndex < CavernCount; cavernIndex++)
        {
            AddGeneratedCavern(cave, origins);
        }

        var sandBiome = AddBiomeCavern(cave, origins, BiomeNames.Sand);
        ApplySandBiomeGeneration(sandBiome);

        var lushBiome = AddBiomeCavern(cave, origins, BiomeNames.Lush);
        ApplyLushBiomeGeneration(lushBiome);

        var greenBiome = AddBiomeCavern(cave, origins, BiomeNames.Green);
        ApplyGreenBiomeGeneration(greenBiome);

        var lavaBiome = AddBiomeCavern(cave, origins, BiomeNames.Lava);
        ApplyLavaBiomeGeneration(lavaBiome);

        var protectedCenterRadius = Radius / 2;
        var holeBreakThreshold = (Radius * HoleLimit) + (CavernCount * HoleLimit);
        var tileKeys = Shuffle(cave.GetCoords());
        var removedHoleCount = 0;
        foreach (var tileKey in tileKeys)
        {
            var tile = cave.GetTile(tileKey)!;
            var coords = GridPoint.Parse(tileKey);
            if (tile.Neighbors.Count == 4 &&
                ((coords.X * coords.X) + (coords.Y * coords.Y) > protectedCenterRadius * protectedCenterRadius))
            {
                cave.RemoveTile(tileKey);
                removedHoleCount++;
            }

            if (removedHoleCount > holeBreakThreshold)
            {
                break;
            }
        }

        for (var index = 0; index < 2d + ((double)Radius / SizeMult) + ((double)Radius / CavernCount); index++)
        {
            DegradeCaveOnce(cave);
        }

        foreach (var tileKey in cave.GetCoords())
        {
            if (cave.GetTile(tileKey)?.Neighbors.Count == 0)
            {
                cave.RemoveTile(tileKey);
            }
        }

        foreach (var tileKey in cave.GetCoords())
        {
            var tile = cave.GetTile(tileKey)!;
            if (tile.Neighbors.Count < 4)
            {
                tile.SetBase("wall");
                tile.CreatureCanFit = false;
                tile.ConfigureWall(GameConstants.WallHitsRequired);
            }
        }

        foreach (var tileKey in cave.GetCoords())
        {
            var tile = cave.GetTile(tileKey)!;
            if (tile.Base != "wall")
            {
                continue;
            }

            var willDelete = tile.Neighbors.All(neighbor => neighbor.Base != "empty");
            if (willDelete)
            {
                cave.RemoveTile(tileKey);
            }
        }

        FillOres(cave);
    }

    // Expand the cave with the same randomized cavern-placement rules used by the original generator.
    private GridPoint AddGeneratedCavern(Cave cave, List<GridPoint> origins, BiomeRegion? biome = null)
    {
        while (true)
        {
            var parent = origins[NextInt(origins.Count)];
            var t = NextDouble();
            var xOffset = (Radius * 2d * t) + (Radius * NextDouble());
            var yOffset = (Radius * 2d * (1d - t)) + (Radius * NextDouble());

            var candidateX = (int)System.Math.Floor(parent.X + xOffset);
            var candidateY = (int)System.Math.Floor(parent.Y + yOffset);

            if (NextDouble() > 0.5d)
            {
                candidateX = -candidateX;
            }

            if (NextDouble() > 0.5d)
            {
                candidateY = -candidateY;
            }

            var tooClose = false;
            foreach (var origin in origins)
            {
                var dx = candidateX - origin.X;
                var dy = candidateY - origin.Y;
                if ((dx * dx) + (dy * dy) <= Radius * Radius)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
            {
                continue;
            }

            var nextOrigin = new GridPoint(candidateX, candidateY);
            origins.Add(nextOrigin);
            var newRadius = (int)System.Math.Floor((0.5d + NextDouble()) * Radius);
            FillCircle(cave, nextOrigin.X, nextOrigin.Y, newRadius, biome);
            return nextOrigin;
        }
    }

    // Create a dedicated biome cavern and assign every covered tile to that biome region.
    private BiomeRegion AddBiomeCavern(Cave cave, List<GridPoint> origins, string biomeName)
    {
        var biome = cave.CreateBiomeRegion(biomeName);
        AddGeneratedCavern(cave, origins, biome);
        return biome;
    }

    private void DegradeCaveOnce(Cave cave)
    {
        var tileKeys = Shuffle(cave.GetCoords());
        foreach (var tileKey in tileKeys)
        {
            var tile = cave.GetTile(tileKey)!;
            var neighborCount = tile.Neighbors.Count;
            var sample = NextNormal(neighborCount, DegradeDeviation);
            if (neighborCount < 4 && sample < DegradeLimit)
            {
                cave.RemoveTile(tileKey);
            }
        }
    }

    private void FillOres(Cave cave, int protectedCenterRadius = 0)
    {
        bool TryPlaceGuaranteedOre(int min, int maxExclusive, string oreName)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            {
                var x = NextInt(min, maxExclusive);
                var y = NextInt(min, maxExclusive);
                var tile = cave.GetTile(new GridPoint(x, y).ToString());
                if (tile is not null &&
                    tile.Base == "empty" &&
                    !IsInCircle(x, y, 0, 0, protectedCenterRadius))
                {
                    ConfigureGeneratedOreTile(tile, oreName);
                    return true;
                }
            }

            foreach (var fallbackTile in Shuffle(cave.GetTiles()))
            {
                var coords = fallbackTile.Coordinates;
                if (fallbackTile.Base != "empty" || IsInCircle(coords.X, coords.Y, 0, 0, protectedCenterRadius))
                {
                    continue;
                }

                ConfigureGeneratedOreTile(fallbackTile, oreName);
                return true;
            }

            return false;
        }

        var ores = OreType.GetOres();

        TryPlaceGuaranteedOre(-8, 9, OreType.SANDSTONE.Name);
        TryPlaceGuaranteedOre(-6, 7, OreType.MAGNETITE.Name);

        var oreCount = 0;
        foreach (var ore in ores)
        {
            var count = 0;
            foreach (var tile in Shuffle(cave.GetTiles()))
            {
                var lower = System.Math.Abs(NextNormal(3d * CavernCount * oreCount, CavernCount * (ores.Count - oreCount)) / OreDist);
                var upper = System.Math.Abs(NextNormal(3d * CavernCount * (oreCount + 3), 2d * CavernCount * (ores.Count - oreCount)) / OreDist);
                var coords = GridPoint.Parse(tile.Key);
                var vector = GetDistance(coords.X, coords.Y, 0, 0);
                if (vector > lower &&
                    vector < upper &&
                    tile.Base == "empty" &&
                    !IsInCircle(coords.X, coords.Y, 0, 0, protectedCenterRadius))
                {
                    ConfigureGeneratedOreTile(tile, ore.Name);
                    var veinCount = 0;
                    var roll = NextDouble();
                    while (roll < 0.85d && veinCount <= 2 + (ores.Count - oreCount))
                    {
                        var neighbor = GetRandomNeighbor(tile);
                        var brokenCount = 0;
                        while (neighbor is not null && neighbor.Base != "empty" && brokenCount < 4)
                        {
                            neighbor = GetRandomNeighbor(neighbor);
                            brokenCount++;
                        }

                        if (neighbor is not null && brokenCount < 4)
                        {
                            ConfigureGeneratedOreTile(neighbor, ore.Name);
                        }

                        roll = NextDouble();
                        veinCount++;
                    }

                    count++;
                }

                if (count >= (CavernCount / 5d) + (CavernCount * Radius * (ores.Count - oreCount)) / (double)OreMult)
                {
                    break;
                }
            }

            oreCount++;
        }
    }

    private void ConfigureGeneratedOreTile(Tile tile, string oreName)
    {
        tile.SetBase(oreName);
        tile.ConfigureOre(
            NextInt(GameConstants.MinOreYield, GameConstants.MaxOreYield + 1),
            NextInt(GameConstants.MinOreHitsPerYield, GameConstants.MaxOreHitsPerYield + 1));
    }

    private static void FillCircle(Cave cave, int originX, int originY, int radius, BiomeRegion? biome = null)
    {
        for (var x = originX - radius; x <= originX + radius; x++)
        {
            for (var y = originY - radius; y <= originY + radius; y++)
            {
                if (!IsInCircle(x, y, originX, originY, radius))
                {
                    continue;
                }

                var tileKey = new GridPoint(x, y).ToString();
                var tile = cave.AddTile(tileKey);
                if (biome is not null)
                {
                    cave.SetTileBiome(tile, biome);
                }

                var leftKey = new GridPoint(x - 1, y).ToString();
                if (cave.GetTile(leftKey) is not null)
                {
                    cave.AddEdge(tileKey, leftKey);
                }

                var upperKey = new GridPoint(x, y - 1).ToString();
                if (cave.GetTile(upperKey) is not null)
                {
                    cave.AddEdge(tileKey, upperKey);
                }
            }
        }
    }

    private static void CarveStarterClearing(NoiseMap map, int centerX, int centerY, int radius)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (!map.IsInBounds(x, y) || !IsInCircle(x, y, centerX, centerY, radius))
                {
                    continue;
                }

                map[x, y] = NoiseMapTileType.Floor;
            }
        }
    }

    // Materialize the centered noise-map cells into graph tiles so runtime systems can reuse normal cave traversal and placement rules.
    private static void PopulateCaveFromNoiseMap(Cave cave, NoiseMap map)
    {
        var originX = -(map.Width / 2);
        var originY = -(map.Height / 2);

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tileType = map[x, y];
                if (tileType == NoiseMapTileType.Empty)
                {
                    continue;
                }

                var location = new GridPoint(originX + x, originY + y);
                var key = location.ToString();
                var tile = cave.AddTile(key);
                if (tileType == NoiseMapTileType.Wall)
                {
                    tile.SetBase("wall");
                    tile.CreatureCanFit = false;
                    tile.ConfigureWall(GameConstants.WallHitsRequired);
                }

                if (x > 0 && map[x - 1, y] != NoiseMapTileType.Empty)
                {
                    cave.AddEdge(key, new GridPoint(location.X - 1, location.Y).ToString());
                }

                if (y > 0 && map[x, y - 1] != NoiseMapTileType.Empty)
                {
                    cave.AddEdge(key, new GridPoint(location.X, location.Y - 1).ToString());
                }
            }
        }
    }

    private void PopulateGeneratedNoiseMap(Cave cave, NoiseMap map)
    {
        PopulateCaveFromNoiseMap(cave, map);
        FillOres(cave, PerlinStarterClearingRadius);
    }

    // Reserve a seam for future Sand-specific biome generation rules.
    private static void ApplySandBiomeGeneration(BiomeRegion biome)
    {
    }

    // Reserve a seam for future Lush-specific biome generation rules.
    private static void ApplyLushBiomeGeneration(BiomeRegion biome)
    {
    }

    // Reserve a seam for future Green-specific biome generation rules.
    private static void ApplyGreenBiomeGeneration(BiomeRegion biome)
    {
    }

    // Reserve a seam for future Lava-specific biome generation rules.
    private static void ApplyLavaBiomeGeneration(BiomeRegion biome)
    {
    }

    private static bool IsInCircle(int x, int y, int cx, int cy, int radius)
    {
        var dx = x - cx;
        var dy = y - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static double GetDistance(int x, int y, int cx, int cy)
    {
        var dx = x - cx;
        var dy = y - cy;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static int GetMapIndex(NoiseMap map, int x, int y)
    {
        return (y * map.Width) + x;
    }

    private static void EnqueueFloorNeighbor(NoiseMap map, int x, int y, bool[] visited, Queue<int> frontier)
    {
        if (!map.IsInBounds(x, y))
        {
            return;
        }

        var index = GetMapIndex(map, x, y);
        if (visited[index] || map[x, y] != NoiseMapTileType.Floor)
        {
            return;
        }

        visited[index] = true;
        frontier.Enqueue(index);
    }

    private static bool BordersFloor(NoiseMap map, int x, int y)
    {
        return IsFloor(map, x - 1, y) ||
               IsFloor(map, x + 1, y) ||
               IsFloor(map, x, y - 1) ||
               IsFloor(map, x, y + 1);
    }

      private static bool IsEmptyOrOutOfBounds(NoiseMap map, int x, int y)
    {
        return !map.IsInBounds(x, y) || map[x, y] == NoiseMapTileType.Empty;
    }

    private static bool IsFloor(NoiseMap map, int x, int y)
    {
        return map.IsInBounds(x, y) && map[x, y] == NoiseMapTileType.Floor;
    }

    // Keep generator-local randomness deterministic and isolated from the shared utility RNG.
    private double NextDouble() => _random.NextFloat();

    private int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return (int)_random.Next((ulong)maxExclusive);
    }

    private int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Max must be greater than min.");
        }

        return minInclusive + NextInt(maxExclusive - minInclusive);
    }

    private double NextNormal(double mean, double standardDeviation)
    {
        var u = 1d - NextDouble();
        var v = 1d - NextDouble();
        var z = System.Math.Sqrt(-2d * System.Math.Log(u)) * System.Math.Cos(2d * System.Math.PI * v);
        return (z * standardDeviation) + mean;
    }

    private T[] Shuffle<T>(IEnumerable<T> source)
    {
        var values = source.ToArray();
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = NextInt(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }

        return values;
    }

    private Tile? GetRandomNeighbor(Tile tile)
    {
        if (tile.Neighbors.Count == 0)
        {
            return null;
        }

        var targetIndex = NextInt(tile.Neighbors.Count);
        var index = 0;
        foreach (var neighbor in tile.Neighbors)
        {
            if (index == targetIndex)
            {
                return neighbor;
            }

            index++;
        }

        return null;
    }

    private static double Perlin2D(double x, double y, int seed)
    {
        var x0 = (int)System.Math.Floor(x);
        var y0 = (int)System.Math.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;

        var sx = x - x0;
        var sy = y - y0;

        var g00 = GradientAt(x0, y0, seed);
        var g10 = GradientAt(x1, y0, seed);
        var g01 = GradientAt(x0, y1, seed);
        var g11 = GradientAt(x1, y1, seed);

        var n00 = Dot(g00, sx, sy);
        var n10 = Dot(g10, sx - 1d, sy);
        var n01 = Dot(g01, sx, sy - 1d);
        var n11 = Dot(g11, sx - 1d, sy - 1d);

        var u = Fade(sx);
        var v = Fade(sy);
        var bottom = Lerp(n00, n10, u);
        var top = Lerp(n01, n11, u);

        return Lerp(bottom, top, v);
    }

    private static double Dot((double X, double Y) vector, double x, double y)
    {
        return (vector.X * x) + (vector.Y * y);
    }

    private static double Fade(double t)
    {
        return (6d * System.Math.Pow(t, 5)) - (15d * System.Math.Pow(t, 4)) + (10d * System.Math.Pow(t, 3));
    }

    private static double Lerp(double a, double b, double delta)
    {
        return a + ((b - a) * delta);
    }

    private static double NormalizePerlinToConcentration(double value)
    {
        return System.Math.Clamp((value + 1d) * 0.5d, 0d, 1d);
    }

    // Mirror the JavaScript 32-bit lattice hash so identical seeds produce identical gradient picks.
    private static uint Hash2D(int x, int y, int seed)
    {
        var hash = unchecked((uint)seed);
        hash ^= unchecked((uint)(x * 374761393));
        hash ^= unchecked((uint)(y * 668265263));
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
        hash ^= hash >> 16;
        return hash;
    }

    // Pick one of the eight axis-aligned or diagonal gradients for the requested lattice point.
    private static (double X, double Y) GradientAt(int x, int y, int seed)
    {
        return (Hash2D(x, y, seed) & 7u) switch
        {
            0u => (1d, 0d),
            1u => (-1d, 0d),
            2u => (0d, 1d),
            3u => (0d, -1d),
            4u => (DiagonalPerlinGradient, DiagonalPerlinGradient),
            5u => (-DiagonalPerlinGradient, DiagonalPerlinGradient),
            6u => (DiagonalPerlinGradient, -DiagonalPerlinGradient),
            _ => (-DiagonalPerlinGradient, -DiagonalPerlinGradient)
        };
    }
}
