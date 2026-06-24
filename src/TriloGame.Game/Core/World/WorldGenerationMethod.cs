namespace TriloGame.Game.Core.World;

public enum WorldGenerationMethod
{
    Version0,
    PerlinNoise,
    FractalBrownianMotion
}

public static class WorldGenerationMethods
{
    public static WorldGenerationMethod[] All { get; } =
    [
        WorldGenerationMethod.Version0,
        WorldGenerationMethod.PerlinNoise,
        WorldGenerationMethod.FractalBrownianMotion
    ];

    public static string GetDisplayName(WorldGenerationMethod method)
    {
        return method switch
        {
            WorldGenerationMethod.Version0 => "Version 0",
            WorldGenerationMethod.PerlinNoise => "Perlin Noise",
            WorldGenerationMethod.FractalBrownianMotion => "Fractal Brownian Motion",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown world generation method.")
        };
    }
}
