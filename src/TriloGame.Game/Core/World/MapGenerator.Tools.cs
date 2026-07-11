namespace TriloGame.Game.Core.World;

public sealed partial class MapGenerator
{
    internal delegate void GenerationPatternRunner(MapGenerator generator, Cave cave);

    public sealed class GenerationPattern
    {
        private readonly GenerationPatternRunner _runner;

        private GenerationPattern(WorldGenerationMethod method, string displayName, GenerationPatternRunner runner)
        {
            Method = method;
            DisplayName = displayName;
            _runner = runner;
        }

        public WorldGenerationMethod Method { get; }

        public string DisplayName { get; }

        internal void Generate(MapGenerator generator, Cave cave)
        {
            _runner(generator, cave);
        }

        internal static GenerationPattern Create(WorldGenerationMethod method, string displayName, GenerationPatternRunner runner)
        {
            return new GenerationPattern(method, displayName, runner);
        }
    }

    private enum GeneratorCapability
    {
        ConcentrationFieldGenerator,
        ConcentrationFieldInterpolater,
        MapGenerator,
        MapInterpolater
    }

    // Register top-level world-generation patterns here. Tool generators below are building blocks, not menu options.
    private static readonly GenerationPattern[] s_generationPatterns =
    [
        GenerationPattern.Create(WorldGenerationMethod.Version0, "Version 0", (generator, cave) => generator.VersionZeroGeneration(cave)),
        GenerationPattern.Create(WorldGenerationMethod.PerlinNoise, "Perlin Noise", (generator, cave) => generator.PerlinNoiseGeneration(cave)),
        GenerationPattern.Create(WorldGenerationMethod.PerlinRandom, "Perlin Random", (generator, cave) => generator.PerlinRandomGeneration(cave)),
        GenerationPattern.Create(WorldGenerationMethod.FractalBrownianMotion, "Fractal Brownian Motion", (generator, cave) => generator.FractalBrownianMotionGeneration(cave)),
        GenerationPattern.Create(WorldGenerationMethod.PatternlessRandom, "Patternless Random", (generator, cave) => generator.PatternlessRandomGeneration(cave)),
        GenerationPattern.Create(WorldGenerationMethod.VoronoiBorders, "Voronoi Borders", (generator, cave) => generator.VoronoiBordersGeneration(cave))
    ];

    private static readonly IReadOnlyList<GenerationPattern> s_generationPatternView = Array.AsReadOnly(s_generationPatterns);

    public static IReadOnlyList<GenerationPattern> GenerationPatterns => s_generationPatternView;

    public static GenerationPattern GetGenerationPattern(WorldGenerationMethod method)
    {
        foreach (var pattern in s_generationPatterns)
        {
            if (pattern.Method == method)
            {
                return pattern;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown world generation method.");
    }

    private NoiseMap FinalizeGeneratedMap(GenerationGraph graph, int minimumRegionSize)
    {
        var map = graph.ToNoiseMap();
        CarveStarterClearing(map, graph.Size / 2, graph.Size / 2, PerlinStarterClearingRadius);
        FloodFill(minimumRegionSize, map);
        FillWalls(map);
        return map;
    }

    // Future world patterns can compose tool graphs and then reuse the shared cleanup/materialization path in one call.
    private void PopulateGeneratedGraph(Cave cave, GenerationGraph graph, int minimumRegionSize = PerlinWorldMinimumRegionSize)
    {
        ArgumentNullException.ThrowIfNull(cave);
        ArgumentNullException.ThrowIfNull(graph);

        PopulateGeneratedNoiseMap(cave, FinalizeGeneratedMap(graph, minimumRegionSize));
    }

    private uint NextSeed32()
    {
        return unchecked((uint)(NextInt(int.MaxValue - 1) + 1));
    }

    private abstract class SelectableGenerator
    {
        protected SelectableGenerator(string key, string label, params GeneratorCapability[] implementedCapabilities)
        {
            Key = key;
            Label = label;
            ImplementedCapabilities = implementedCapabilities;
        }

        public string Key { get; }

        public string Label { get; }

        public IReadOnlyList<GeneratorCapability> ImplementedCapabilities { get; }
    }

    private sealed class RandomMapGenerator : SelectableGenerator
    {
        public RandomMapGenerator()
            : base("random", "Random", GeneratorCapability.MapGenerator, GeneratorCapability.ConcentrationFieldInterpolater)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, double density = 0.5d)
        {
            var graph = new GenerationGraph(size);
            var random = new XorShift32(seed);

            for (var index = 0; index < graph.CellCount; index++)
            {
                graph[index] = random.NextFloat() < density ? 1d : 0d;
            }

            return graph;
        }

        public GenerationGraph InterpolateConcentrationField(GenerationGraph concentrationField, uint seed, double density = 0.5d)
        {
            var random = new XorShift32(seed);

            for (var index = 0; index < concentrationField.CellCount; index++)
            {
                var floor = random.NextFloat() > concentrationField[index] && random.NextFloat() < density;
                concentrationField[index] = floor ? 1d : 0d;
            }

            return concentrationField;
        }
    }

    private sealed class CellularAutomata : SelectableGenerator
    {
        public CellularAutomata()
            : base("cellular-automata", "Cellular Automata", GeneratorCapability.MapGenerator, GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, double density = 0.7d, int threshold = 3, int iterations = 5)
        {
            var graph = new RandomMapGenerator().GenerateMap(size, seed, density);
            return InterpolateMap(graph, seed, iterations, threshold);
        }

        public GenerationGraph InterpolateMap(GenerationGraph graph, uint seed, int iterations = 5, int threshold = 3)
        {
            _ = seed;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var nextStep = new GenerationGraph(graph.Size);
                for (var index = 0; index < graph.CellCount; index++)
                {
                    nextStep[index] = CountNeighborsWithValue(graph, index, 1d) >= threshold ? 1d : 0d;
                }

                graph = nextStep;
            }

            return graph;
        }
    }

    private sealed class CellularGrowth : SelectableGenerator
    {
        public CellularGrowth()
            : base("cellular-growth", "Cellular Growth", GeneratorCapability.MapGenerator, GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, double density = 0.7d, int threshold = 3, int iterations = 5)
        {
            var graph = new RandomMapGenerator().GenerateMap(size, seed, density);
            return InterpolateMap(graph, seed, iterations, threshold);
        }

        public GenerationGraph InterpolateMap(GenerationGraph map, uint seed, int iterations = 5, int threshold = 3)
        {
            _ = seed;

            var graph = map;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                graph = RunCellularGrowthPass(graph, threshold);
            }

            return graph;
        }
    }

    private sealed class CellularShrink : SelectableGenerator
    {
        public CellularShrink()
            : base("cellular-shrink", "Cellular Shrink", GeneratorCapability.MapGenerator, GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, double density = 0.7d, int threshold = 3, int iterations = 5)
        {
            var graph = new RandomMapGenerator().GenerateMap(size, seed, density);
            return InterpolateMap(graph, seed, iterations, threshold);
        }

        public GenerationGraph InterpolateMap(GenerationGraph map, uint seed, int iterations = 5, int threshold = 3)
        {
            _ = seed;

            var graph = map;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                graph = RunCellularShrinkPass(graph, threshold);
            }

            return graph;
        }
    }

    private sealed class RandomAutomata : SelectableGenerator
    {
        public RandomAutomata()
            : base("random-automata", "Random Automata", GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph InterpolateMap(GenerationGraph map, uint seed, int iterations = 5)
        {
            var graph = map;
            var random = new XorShift32(seed);

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                graph = RunRandomAutomataPass(graph, random);
            }

            return graph;
        }
    }

    private sealed class RandomGrowth : SelectableGenerator
    {
        public RandomGrowth()
            : base("random-growth", "Random Growth", GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph InterpolateMap(GenerationGraph map, uint seed, int iterations = 5)
        {
            var graph = map;
            var random = new XorShift32(seed);

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                graph = RunRandomGrowthPass(graph, random);
            }

            return graph;
        }
    }

    private sealed class RandomShrink : SelectableGenerator
    {
        public RandomShrink()
            : base("random-shrink", "Random Shrink", GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph InterpolateMap(GenerationGraph map, uint seed, int iterations = 5)
        {
            var graph = map;
            var random = new XorShift32(seed);

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                graph = RunRandomShrinkPass(graph, random);
            }

            return graph;
        }
    }

    private sealed class PerlinNoise : SelectableGenerator
    {
        public PerlinNoise()
            : base("perlin-noise", "Perlin Noise", GeneratorCapability.MapGenerator, GeneratorCapability.ConcentrationFieldGenerator)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, double frequency = 1d, double threshold = 0.5d)
        {
            var graph = GenerateConcentrationField(size, seed, frequency);
            new RawThreshold().InterpolateConcentrationField(graph, seed, threshold);
            return graph;
        }

        public GenerationGraph GenerateConcentrationField(int size, uint seed, double frequency = 1d)
        {
            var graph = MakeWeirdField(size, seed, frequency);
            return NormalizeGraph(graph);
        }

        public GenerationGraph MakeWeirdField(int size, uint seed, double frequency)
        {
            var graph = new GenerationGraph(size);
            var sampleScale = frequency / System.Math.Max(size, 1);
            var perlinSeed = unchecked((int)seed);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var sampleX = (x + 0.5d) * sampleScale;
                    var sampleY = (y + 0.5d) * sampleScale;
                    graph[x, y] = Perlin2D(sampleX, sampleY, perlinSeed);
                }
            }

            return graph;
        }
    }

    private sealed class FractalBrownianMotion : SelectableGenerator
    {
        public FractalBrownianMotion()
            : base("fractal-brownian-motion", "Fractal Brownian Motion", GeneratorCapability.MapGenerator, GeneratorCapability.ConcentrationFieldGenerator)
        {
        }

        public GenerationGraph GenerateMap(
            int size,
            uint seed,
            double threshold = 0.5d,
            double frequency = 1d,
            double lacunarity = 2d,
            double persistence = 0.5d,
            int iterations = 4)
        {
            var graph = GenerateConcentrationField(size, seed, frequency, lacunarity, persistence, iterations);
            new RawThreshold().InterpolateConcentrationField(graph, seed, threshold);
            return graph;
        }

        public GenerationGraph GenerateConcentrationField(
            int size,
            uint seed,
            double frequency = 1d,
            double lacunarity = 2d,
            double persistence = 0.5d,
            int iterations = 4)
        {
            var noise = new PerlinNoise();
            var fields = new List<GenerationGraph>(iterations);
            var octaveFrequency = frequency;
            var amplitude = 1d;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var field = noise.MakeWeirdField(size, seed, octaveFrequency);
                for (var index = 0; index < field.CellCount; index++)
                {
                    field[index] *= amplitude;
                }

                fields.Add(field);
                octaveFrequency *= lacunarity;
                amplitude *= persistence;
            }

            var graph = new GenerationGraph(size);
            for (var index = 0; index < graph.CellCount; index++)
            {
                var value = 0d;
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    value += fields[fieldIndex][index];
                }

                graph[index] = value;
            }

            return NormalizeGraph(graph);
        }
    }

    private sealed class DrunkardsWalk : SelectableGenerator
    {
        public DrunkardsWalk()
            : base("drunkards-walk", "Drunkard's Walk", GeneratorCapability.MapGenerator, GeneratorCapability.ConcentrationFieldInterpolater)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, int drunkardCount = 1, int steps = 10)
        {
            var graph = new GenerationGraph(size);
            var random = new XorShift32(seed);
            var drunkards = new int[drunkardCount];

            for (var index = 0; index < drunkardCount; index++)
            {
                var tileIndex = graph.GetIndex(random.Next(size), random.Next(size));
                graph[tileIndex] = 1d;
                drunkards[index] = tileIndex;
            }

            Span<int> neighbors = stackalloc int[4];
            for (var step = 0; step < steps; step++)
            {
                for (var drunkardIndex = 0; drunkardIndex < drunkards.Length; drunkardIndex++)
                {
                    var count = graph.GetNeighborIndices(drunkards[drunkardIndex], neighbors);
                    var selected = random.Next(count);
                    var nextTile = neighbors[selected];
                    graph[nextTile] = 1d;
                    drunkards[drunkardIndex] = nextTile;
                }
            }

            return graph;
        }

        public GenerationGraph InterpolateConcentrationField(GenerationGraph concentrationField, uint seed, int drunkardCount = 1, int steps = 10)
        {
            var graph = concentrationField;
            var random = new XorShift32(seed);
            var drunkards = new int[drunkardCount];

            for (var index = 0; index < drunkardCount; index++)
            {
                var tileIndex = graph.GetIndex(random.Next(graph.Size), random.Next(graph.Size));
                graph[tileIndex] = 1d;
                drunkards[index] = tileIndex;
            }

            Span<int> neighbors = stackalloc int[4];
            for (var step = 0; step < steps; step++)
            {
                for (var drunkardIndex = 0; drunkardIndex < drunkards.Length; drunkardIndex++)
                {
                    var count = graph.GetNeighborIndices(drunkards[drunkardIndex], neighbors);
                    var selected = WeightedMove(graph, neighbors, count, random) ?? random.Next(count);
                    var nextTile = neighbors[selected];
                    graph[nextTile] = 1d;
                    drunkards[drunkardIndex] = nextTile;
                }
            }

            return ZeroOutFloatingTileValues(graph);
        }
    }

    private sealed class VoronoiRegions : SelectableGenerator
    {
        public VoronoiRegions()
            : base("voronoi-regions", "Voronoi Regions", GeneratorCapability.MapGenerator, GeneratorCapability.ConcentrationFieldGenerator)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, int regionCount = 5)
        {
            var graph = new GenerationGraph(size);
            var random = new XorShift32(seed);
            var regions = new HashSet<VoronoiRegion>();
            var chosen = new HashSet<int>();

            for (var index = 0; index < regionCount; index++)
            {
                var tileIndex = graph.GetIndex(random.Next(size), random.Next(size));
                if (chosen.Add(tileIndex))
                {
                    regions.Add(new VoronoiRegion(graph, tileIndex));
                }
            }

            var walls = new HashSet<int>();
            while (regions.Count > 0)
            {
                foreach (var region in regions.ToArray())
                {
                    foreach (var wall in region.Advance())
                    {
                        walls.Add(wall);
                    }

                    if (region.OpenEdgeCount == 0)
                    {
                        regions.Remove(region);
                    }
                }
            }

            var finalGraph = new GenerationGraph(size, 1d);
            foreach (var wall in walls)
            {
                finalGraph[wall] = 0d;
            }

            return finalGraph;
        }

        public GenerationGraph GenerateConcentrationField(int size, uint seed, int regionCount = 5)
        {
            var graph = new GenerationGraph(size);
            var random = new XorShift32(seed);
            var regions = new HashSet<VoronoiRegion>();
            var chosen = new HashSet<int>();

            for (var index = 0; index < regionCount; index++)
            {
                var tileIndex = graph.GetIndex(random.Next(size), random.Next(size));
                if (chosen.Add(tileIndex))
                {
                    regions.Add(new VoronoiRegion(graph, tileIndex));
                }
            }

            while (regions.Count > 0)
            {
                foreach (var region in regions.ToArray())
                {
                    region.Advance();
                    if (region.OpenEdgeCount == 0)
                    {
                        regions.Remove(region);
                    }
                }
            }

            return NormalizeGraph(graph);
        }
    }

    private sealed class DiffusionLimitedAggregation : SelectableGenerator
    {
        public DiffusionLimitedAggregation()
            : base("diffusion-limited-aggregation", "Diffusion Limited Aggregation", GeneratorCapability.MapGenerator, GeneratorCapability.ConcentrationFieldInterpolater)
        {
        }

        public GenerationGraph GenerateMap(int size, uint seed, int catalysts = 1, double density = 0.5d)
        {
            var graph = new GenerationGraph(size);
            var random = new XorShift32(seed);
            return RunAggregation(
                graph,
                random,
                catalysts,
                GetTargetOccupiedTileCount(size, density),
                pickCatalystTile: (aggregateGraph, rng, isCatalystSafeTile, pickRandomEmptyTile) => pickRandomEmptyTile(isCatalystSafeTile),
                pickWalkerStep: (aggregateGraph, rng, openNeighbors, openNeighborCount) => openNeighbors[rng.Next(openNeighborCount)]);
        }

        public GenerationGraph InterpolateConcentrationField(GenerationGraph concentrationField, uint seed, int catalysts = 1, double density = 0.5d)
        {
            var graph = new GenerationGraph(concentrationField.Size);
            var random = new XorShift32(seed);
            return RunAggregation(
                graph,
                random,
                catalysts,
                GetTargetOccupiedTileCount(concentrationField.Size, density),
                pickCatalystTile: (aggregateGraph, rng, isCatalystSafeTile, pickRandomEmptyTile) =>
                {
                    var weightedTile = PickWeightedTile(concentrationField, rng, weightTileIndex => isCatalystSafeTile(weightTileIndex));
                    return weightedTile ?? pickRandomEmptyTile(isCatalystSafeTile);
                },
                pickWalkerStep: (aggregateGraph, rng, openNeighbors, openNeighborCount) =>
                {
                    var selectedIndex = WeightedMove(concentrationField, openNeighbors, openNeighborCount, rng);
                    return selectedIndex.HasValue ? openNeighbors[selectedIndex.Value] : null;
                });
        }

        private static int GetTargetOccupiedTileCount(int size, double density)
        {
            var totalTiles = size * size;
            return System.Math.Max(0, System.Math.Min(totalTiles, (int)System.Math.Round(totalTiles * density)));
        }

        private static GenerationGraph RunAggregation(
            GenerationGraph graph,
            XorShift32 random,
            int catalysts,
            int targetOccupiedTiles,
            CatalystPicker pickCatalystTile,
            WalkerStepPicker pickWalkerStep)
        {
            if (targetOccupiedTiles == 0)
            {
                return graph;
            }

            var size = graph.Size;
            var occupiedCount = 0;
            var maxActiveWalkers = System.Math.Max(1, size / 10);
            var maxWalkerSteps = System.Math.Max(1, size * 10);
            var catalystEdgePadding = size / 10;
            var catalystSafeZoneSize = System.Math.Max(0, size - (catalystEdgePadding * 2));
            var hasCatalystSafeZone = catalystSafeZoneSize > 0;
            var maxCatalystSafeZoneTiles = hasCatalystSafeZone
                ? catalystSafeZoneSize * catalystSafeZoneSize
                : graph.CellCount;

            bool OccupyTile(int? tileIndex)
            {
                if (!tileIndex.HasValue || graph[tileIndex.Value] == 1d)
                {
                    return false;
                }

                graph[tileIndex.Value] = 1d;
                occupiedCount++;
                return true;
            }

            bool HasOccupiedNeighbor(int tileIndex)
            {
                Span<int> neighbors = stackalloc int[4];
                var count = graph.GetNeighborIndices(tileIndex, neighbors);
                for (var index = 0; index < count; index++)
                {
                    if (graph[neighbors[index]] == 1d)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanWalkerStick(int tileIndex)
            {
                return graph[tileIndex] == 0d && HasOccupiedNeighbor(tileIndex);
            }

            bool IsCatalystSafeTile(int tileIndex)
            {
                if (graph[tileIndex] != 0d)
                {
                    return false;
                }

                if (!hasCatalystSafeZone)
                {
                    return true;
                }

                var x = graph.GetX(tileIndex);
                var y = graph.GetY(tileIndex);
                return x >= catalystEdgePadding &&
                    x < size - catalystEdgePadding &&
                    y >= catalystEdgePadding &&
                    y < size - catalystEdgePadding;
            }

            int? PickRandomEmptyTile(Func<int, bool>? predicate = null)
            {
                var maxAttempts = System.Math.Max(size * 4, 16);
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var tileIndex = graph.GetIndex(random.Next(size), random.Next(size));
                    if (graph[tileIndex] == 0d && (predicate is null || predicate(tileIndex)))
                    {
                        return tileIndex;
                    }
                }

                for (var tileIndex = 0; tileIndex < graph.CellCount; tileIndex++)
                {
                    if (graph[tileIndex] == 0d && (predicate is null || predicate(tileIndex)))
                    {
                        return tileIndex;
                    }
                }

                return null;
            }

            Walker? CreateWalker()
            {
                var tileIndex = PickRandomEmptyTile();
                return tileIndex.HasValue ? new Walker(tileIndex.Value) : null;
            }

            var catalystCount = System.Math.Min(System.Math.Min(targetOccupiedTiles, catalysts), maxCatalystSafeZoneTiles);
            for (var index = 0; index < catalystCount; index++)
            {
                var catalystTile = pickCatalystTile(graph, random, IsCatalystSafeTile, PickRandomEmptyTile) ??
                    PickRandomEmptyTile(IsCatalystSafeTile) ??
                    PickRandomEmptyTile();

                if (!OccupyTile(catalystTile))
                {
                    break;
                }
            }

            var activeWalkers = new List<Walker>(maxActiveWalkers);

            void RefillWalkers()
            {
                while (activeWalkers.Count < maxActiveWalkers && occupiedCount < targetOccupiedTiles)
                {
                    var walker = CreateWalker();
                    if (walker is null)
                    {
                        break;
                    }

                    activeWalkers.Add(walker);
                }
            }

            RefillWalkers();

            Span<int> neighbors = stackalloc int[4];
            Span<int> openNeighbors = stackalloc int[4];
            while (occupiedCount < targetOccupiedTiles && activeWalkers.Count > 0)
            {
                for (var walkerIndex = activeWalkers.Count - 1; walkerIndex >= 0 && occupiedCount < targetOccupiedTiles; walkerIndex--)
                {
                    var walker = activeWalkers[walkerIndex];
                    if (graph[walker.TileIndex] == 1d)
                    {
                        activeWalkers.RemoveAt(walkerIndex);
                        continue;
                    }

                    if (CanWalkerStick(walker.TileIndex))
                    {
                        OccupyTile(walker.TileIndex);
                        activeWalkers.RemoveAt(walkerIndex);
                        continue;
                    }

                    var neighborCount = graph.GetNeighborIndices(walker.TileIndex, neighbors);
                    var openNeighborCount = 0;
                    for (var index = 0; index < neighborCount; index++)
                    {
                        var neighbor = neighbors[index];
                        if (graph[neighbor] == 0d)
                        {
                            openNeighbors[openNeighborCount++] = neighbor;
                        }
                    }

                    if (openNeighborCount == 0)
                    {
                        activeWalkers.RemoveAt(walkerIndex);
                        continue;
                    }

                    var nextTile = pickWalkerStep(graph, random, openNeighbors, openNeighborCount);
                    if (!nextTile.HasValue || graph[nextTile.Value] != 0d)
                    {
                        activeWalkers.RemoveAt(walkerIndex);
                        continue;
                    }

                    walker.TileIndex = nextTile.Value;
                    walker.StepsTaken++;

                    if (CanWalkerStick(walker.TileIndex))
                    {
                        OccupyTile(walker.TileIndex);
                        activeWalkers.RemoveAt(walkerIndex);
                        continue;
                    }

                    if (walker.StepsTaken >= maxWalkerSteps)
                    {
                        activeWalkers.RemoveAt(walkerIndex);
                    }
                }

                RefillWalkers();
            }

            return graph;
        }

        private delegate int? CatalystPicker(
            GenerationGraph graph,
            XorShift32 random,
            Func<int, bool> isCatalystSafeTile,
            Func<Func<int, bool>?, int?> pickRandomEmptyTile);

        private delegate int? WalkerStepPicker(
            GenerationGraph graph,
            XorShift32 random,
            Span<int> openNeighbors,
            int openNeighborCount);

        private sealed class Walker
        {
            public Walker(int tileIndex)
            {
                TileIndex = tileIndex;
            }

            public int TileIndex { get; set; }

            public int StepsTaken { get; set; }
        }
    }

    private sealed class FloodFillTool : SelectableGenerator
    {
        public FloodFillTool()
            : base("flood-fill", "FloodFill", GeneratorCapability.MapInterpolater)
        {
        }

        public GenerationGraph InterpolateMap(GenerationGraph map, uint seed, int minimumRegionSize = 2)
        {
            _ = seed;

            var graph = map;
            for (var index = 0; index < graph.CellCount; index++)
            {
                if (graph[index] == 1d && CountNeighborsWithValue(graph, index, 1d) == 0)
                {
                    graph[index] = 0d;
                }
            }

            var visited = new bool[graph.CellCount];
            var queue = new Queue<int>();
            var region = new List<int>();
            Span<int> neighbors = stackalloc int[4];

            for (var startIndex = 0; startIndex < graph.CellCount; startIndex++)
            {
                if (visited[startIndex] || graph[startIndex] != 1d)
                {
                    continue;
                }

                visited[startIndex] = true;
                queue.Enqueue(startIndex);
                region.Clear();

                while (queue.Count > 0)
                {
                    var tileIndex = queue.Dequeue();
                    region.Add(tileIndex);

                    var neighborCount = graph.GetNeighborIndices(tileIndex, neighbors);
                    for (var index = 0; index < neighborCount; index++)
                    {
                        var neighbor = neighbors[index];
                        if (visited[neighbor] || graph[neighbor] != 1d)
                        {
                            continue;
                        }

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                if (region.Count >= minimumRegionSize)
                {
                    continue;
                }

                for (var index = 0; index < region.Count; index++)
                {
                    graph[region[index]] = 0d;
                }
            }

            return graph;
        }
    }

    private sealed class RawThreshold : SelectableGenerator
    {
        public RawThreshold()
            : base("raw-threshold", "Raw Threshold", GeneratorCapability.ConcentrationFieldInterpolater)
        {
        }

        public GenerationGraph InterpolateConcentrationField(GenerationGraph concentrationField, uint seed, double threshold = 0.5d)
        {
            _ = seed;

            for (var index = 0; index < concentrationField.CellCount; index++)
            {
                concentrationField[index] = concentrationField[index] > threshold ? 1d : 0d;
            }

            return concentrationField;
        }
    }

    private sealed class VoronoiRegion
    {
        private readonly GenerationGraph _graph;
        private readonly HashSet<int> _tiles = [];
        private HashSet<int> _openEdge = [];
        private int _steps = 1;

        public VoronoiRegion(GenerationGraph graph, int catalyst)
        {
            _graph = graph;
            _tiles.Add(catalyst);
            _openEdge.Add(catalyst);
            graph[catalyst] = 1d;
        }

        public int OpenEdgeCount => _openEdge.Count;

        public IEnumerable<int> Advance()
        {
            _steps++;

            var deadEdge = new HashSet<int>();
            var newEdge = new HashSet<int>();
            Span<int> neighbors = stackalloc int[4];

            foreach (var tile in _openEdge)
            {
                if (_graph[tile] == _steps)
                {
                    _tiles.Remove(tile);
                    deadEdge.Add(tile);
                    continue;
                }

                var neighborCount = _graph.GetNeighborIndices(tile, neighbors);
                for (var index = 0; index < neighborCount; index++)
                {
                    var neighbor = neighbors[index];
                    if (_graph[neighbor] == 0d)
                    {
                        _graph[neighbor] = _steps;
                        _tiles.Add(neighbor);
                        newEdge.Add(neighbor);
                    }
                    else if (!_tiles.Contains(neighbor))
                    {
                        _graph[neighbor] = _graph[neighbor] == _steps ? _steps + 1d : _steps;
                        deadEdge.Add(tile);
                    }
                }
            }

            _openEdge = newEdge;
            return deadEdge;
        }
    }

    private sealed class GenerationGraph
    {
        private readonly double[] _values;

        public GenerationGraph(int size, double value = 0d)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);

            Size = size;
            _values = new double[size * size];
            if (value != 0d)
            {
                Array.Fill(_values, value);
            }
        }

        public int Size { get; }

        public int CellCount => _values.Length;

        public double this[int index]
        {
            get => _values[index];
            set => _values[index] = value;
        }

        public double this[int x, int y]
        {
            get => _values[GetIndex(x, y)];
            set => _values[GetIndex(x, y)] = value;
        }

        public int GetIndex(int x, int y)
        {
            if ((uint)x >= (uint)Size)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }

            if ((uint)y >= (uint)Size)
            {
                throw new ArgumentOutOfRangeException(nameof(y));
            }

            return (y * Size) + x;
        }

        public int GetX(int index)
        {
            return index % Size;
        }

        public int GetY(int index)
        {
            return index / Size;
        }

        public int GetNeighborIndices(int index, Span<int> neighbors)
        {
            var x = GetX(index);
            var y = GetY(index);
            var count = 0;

            if (y > 0)
            {
                neighbors[count++] = index - Size;
            }

            if (x < Size - 1)
            {
                neighbors[count++] = index + 1;
            }

            if (y < Size - 1)
            {
                neighbors[count++] = index + Size;
            }

            if (x > 0)
            {
                neighbors[count++] = index - 1;
            }

            return count;
        }

        public GenerationGraph Clone()
        {
            var clone = new GenerationGraph(Size);
            Array.Copy(_values, clone._values, _values.Length);
            return clone;
        }

        public ConcentrationField ToConcentrationField()
        {
            var field = new ConcentrationField(Size, Size);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    field[x, y] = this[x, y];
                }
            }

            return field;
        }

        public NoiseMap ToNoiseMap()
        {
            var map = new NoiseMap(Size, Size);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    map[x, y] = this[x, y] > 0.5d ? NoiseMapTileType.Floor : NoiseMapTileType.Empty;
                }
            }

            return map;
        }

        public static GenerationGraph FromConcentrationField(ConcentrationField field)
        {
            ArgumentNullException.ThrowIfNull(field);
            if (field.Width != field.Height)
            {
                throw new ArgumentException("Generation graph tools require square concentration fields.", nameof(field));
            }

            var graph = new GenerationGraph(field.Width);
            for (var y = 0; y < field.Height; y++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    graph[x, y] = field[x, y];
                }
            }

            return graph;
        }
    }

    private sealed class XorShift32
    {
        private const double OneOverTwoPow32 = 1d / 4294967296d;
        private uint _state;

        public XorShift32(uint seed)
        {
            _state = seed == 0u ? 0x6d2b79f5u : seed;
        }

        public uint Next()
        {
            unchecked
            {
                var value = _state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                _state = value;
                return value;
            }
        }

        public int Next(int modulo)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(modulo);
            return (int)(Next() % (uint)modulo);
        }

        public double NextFloat()
        {
            return Next() * OneOverTwoPow32;
        }

        public double NextFloatTo(double maxExclusive)
        {
            if (!double.IsFinite(maxExclusive) || maxExclusive < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Max must be a finite non-negative number.");
            }

            return NextFloat() * maxExclusive;
        }
    }

    private static GenerationGraph NormalizeGraph(GenerationGraph graph)
    {
        var normalizedGraph = new GenerationGraph(graph.Size);
        var minValue = double.PositiveInfinity;
        var maxValue = double.NegativeInfinity;

        for (var index = 0; index < graph.CellCount; index++)
        {
            var value = double.IsFinite(graph[index]) ? graph[index] : 0d;
            minValue = System.Math.Min(minValue, value);
            maxValue = System.Math.Max(maxValue, value);
        }

        if (!double.IsFinite(minValue) || !double.IsFinite(maxValue))
        {
            return normalizedGraph;
        }

        var range = maxValue - minValue;
        if (range <= 0d)
        {
            return normalizedGraph;
        }

        for (var index = 0; index < graph.CellCount; index++)
        {
            var value = double.IsFinite(graph[index]) ? graph[index] : 0d;
            normalizedGraph[index] = ((value - minValue) / range) * NormalizedConcentrationUpperBound;
        }

        return normalizedGraph;
    }

    private static GenerationGraph ZeroOutFloatingTileValues(GenerationGraph graph)
    {
        for (var index = 0; index < graph.CellCount; index++)
        {
            if (!double.IsFinite(graph[index]) || graph[index] != System.Math.Truncate(graph[index]))
            {
                graph[index] = 0d;
            }
        }

        return graph;
    }

    private static GenerationGraph InvertBinaryGraph(GenerationGraph graph)
    {
        for (var index = 0; index < graph.CellCount; index++)
        {
            graph[index] = graph[index] > 0.5d ? 0d : 1d;
        }

        return graph;
    }

    private static GenerationGraph RunCellularGrowthPass(GenerationGraph graph, int threshold)
    {
        var nextStep = new GenerationGraph(graph.Size);
        for (var index = 0; index < graph.CellCount; index++)
        {
            nextStep[index] = graph[index];
            if (graph[index] == 0d && CountNeighborsWithValue(graph, index, 1d) >= threshold)
            {
                nextStep[index] = 1d;
            }
        }

        return nextStep;
    }

    private static GenerationGraph RunCellularShrinkPass(GenerationGraph graph, int threshold)
    {
        var nextStep = new GenerationGraph(graph.Size);
        for (var index = 0; index < graph.CellCount; index++)
        {
            nextStep[index] = graph[index];
            if (graph[index] == 1d && CountNeighborsWithValue(graph, index, 0d) >= threshold)
            {
                nextStep[index] = 0d;
            }
        }

        return nextStep;
    }

    private static GenerationGraph RunRandomAutomataPass(GenerationGraph graph, XorShift32 random)
    {
        var nextStep = graph.Clone();
        for (var index = 0; index < graph.CellCount; index++)
        {
            var swapChance = CountDifferentNeighbors(graph, index) / 4d;
            if (random.NextFloat() < swapChance)
            {
                nextStep[index] = graph[index] == 1d ? 0d : 1d;
            }
        }

        return nextStep;
    }

    private static GenerationGraph RunRandomGrowthPass(GenerationGraph graph, XorShift32 random)
    {
        var nextStep = graph.Clone();
        for (var index = 0; index < graph.CellCount; index++)
        {
            if (graph[index] != 0d)
            {
                continue;
            }

            var swapChance = CountDifferentNeighbors(graph, index) / 4d;
            if (random.NextFloat() < swapChance)
            {
                nextStep[index] = 1d;
            }
        }

        return nextStep;
    }

    private static GenerationGraph RunRandomShrinkPass(GenerationGraph graph, XorShift32 random)
    {
        var nextStep = graph.Clone();
        for (var index = 0; index < graph.CellCount; index++)
        {
            if (graph[index] != 1d)
            {
                continue;
            }

            var swapChance = CountDifferentNeighbors(graph, index) / 4d;
            if (random.NextFloat() < swapChance)
            {
                nextStep[index] = 0d;
            }
        }

        return nextStep;
    }

    private static int CountNeighborsWithValue(GenerationGraph graph, int tileIndex, double value)
    {
        Span<int> neighbors = stackalloc int[4];
        var count = graph.GetNeighborIndices(tileIndex, neighbors);
        var matches = 0;
        for (var index = 0; index < count; index++)
        {
            if (graph[neighbors[index]] == value)
            {
                matches++;
            }
        }

        return matches;
    }

    private static int CountDifferentNeighbors(GenerationGraph graph, int tileIndex)
    {
        Span<int> neighbors = stackalloc int[4];
        var count = graph.GetNeighborIndices(tileIndex, neighbors);
        var different = 0;
        for (var index = 0; index < count; index++)
        {
            if (graph[neighbors[index]] != graph[tileIndex])
            {
                different++;
            }
        }

        return different;
    }

    private static double GetWeightValue(double value)
    {
        return double.IsFinite(value) && value > 0d ? value : 0d;
    }

    private static int? WeightedMove(GenerationGraph graph, Span<int> tileIndices, int count, XorShift32 random)
    {
        if (count <= 0)
        {
            return null;
        }

        var totalWeight = 0d;
        for (var index = 0; index < count; index++)
        {
            totalWeight += GetWeightValue(graph[tileIndices[index]]);
        }

        if (totalWeight <= 0d)
        {
            return random.Next(count);
        }

        var remainingWeight = random.NextFloatTo(totalWeight);
        int? fallbackIndex = null;

        for (var index = 0; index < count; index++)
        {
            var weight = GetWeightValue(graph[tileIndices[index]]);
            if (weight <= 0d)
            {
                continue;
            }

            fallbackIndex = index;
            remainingWeight -= weight;
            if (remainingWeight <= 0d)
            {
                return index;
            }
        }

        return fallbackIndex ?? random.Next(count);
    }

    private static int? PickWeightedTile(GenerationGraph graph, XorShift32 random, Func<int, bool>? predicate = null)
    {
        var candidateCount = 0;
        var totalWeight = 0d;

        for (var tileIndex = 0; tileIndex < graph.CellCount; tileIndex++)
        {
            if (predicate is not null && !predicate(tileIndex))
            {
                continue;
            }

            candidateCount++;
            totalWeight += GetWeightValue(graph[tileIndex]);
        }

        if (candidateCount == 0)
        {
            return null;
        }

        if (totalWeight <= 0d)
        {
            var remainingCandidates = random.Next(candidateCount);
            for (var tileIndex = 0; tileIndex < graph.CellCount; tileIndex++)
            {
                if (predicate is not null && !predicate(tileIndex))
                {
                    continue;
                }

                if (remainingCandidates == 0)
                {
                    return tileIndex;
                }

                remainingCandidates--;
            }

            return null;
        }

        var remainingWeight = random.NextFloatTo(totalWeight);
        int? fallbackTile = null;
        for (var tileIndex = 0; tileIndex < graph.CellCount; tileIndex++)
        {
            if (predicate is not null && !predicate(tileIndex))
            {
                continue;
            }

            var weight = GetWeightValue(graph[tileIndex]);
            if (weight <= 0d)
            {
                continue;
            }

            fallbackTile = tileIndex;
            remainingWeight -= weight;
            if (remainingWeight <= 0d)
            {
                return tileIndex;
            }
        }

        return fallbackTile;
    }
}
