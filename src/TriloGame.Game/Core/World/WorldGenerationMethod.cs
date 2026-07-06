namespace TriloGame.Game.Core.World;

public enum WorldGenerationMethod
{
    Version0,
    PerlinNoise,
    PerlinRandom,
    FractalBrownianMotion,
    PatternlessRandom,
    VoronoiBorders
}

public static class WorldGenerationMethods
{
    public static IReadOnlyList<MapGenerator.GenerationPattern> SelectablePatterns => MapGenerator.GenerationPatterns;

    public static WorldGenerationMethod[] All => SelectablePatterns
        .Select(pattern => pattern.Method)
        .ToArray();

    public static string GetDisplayName(WorldGenerationMethod method)
    {
        return MapGenerator.GetGenerationPattern(method).DisplayName;
    }
}
