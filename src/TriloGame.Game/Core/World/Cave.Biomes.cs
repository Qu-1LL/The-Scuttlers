using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private readonly List<BiomeRegion> _biomeRegions = [];

    public IReadOnlyList<BiomeRegion> GetBiomeRegions() => _biomeRegions;

    // Expand the cave with the same randomized cavern-placement rules used by the base generator.
    private GridPoint AddGeneratedCavern(List<GridPoint> origins, BiomeRegion? biome = null)
    {
        while (true)
        {
            var parent = origins[RandomUtil.NextInt(origins.Count)];
            var t = RandomUtil.NextDouble();
            var xOffset = (Radius * 2d * t) + (Radius * RandomUtil.NextDouble());
            var yOffset = (Radius * 2d * (1d - t)) + (Radius * RandomUtil.NextDouble());

            var candidateX = (int)System.Math.Floor(parent.X + xOffset);
            var candidateY = (int)System.Math.Floor(parent.Y + yOffset);

            if (RandomUtil.NextDouble() > 0.5d)
            {
                candidateX = -candidateX;
            }

            if (RandomUtil.NextDouble() > 0.5d)
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
            var newRadius = (int)System.Math.Floor((0.5d + RandomUtil.NextDouble()) * Radius);
            FillCircle(nextOrigin.X, nextOrigin.Y, newRadius, biome);
            return nextOrigin;
        }
    }

    // Create a dedicated biome cavern and assign every covered tile to that biome region.
    private BiomeRegion AddBiomeCavern(List<GridPoint> origins, string biomeName)
    {
        var biome = new BiomeRegion(biomeName);
        _biomeRegions.Add(biome);
        AddGeneratedCavern(origins, biome);
        return biome;
    }

    // Keep tile ownership in sync when later biome passes overwrite earlier ones.
    private static void SetTileBiome(Tile tile, BiomeRegion? biome)
    {
        if (ReferenceEquals(tile.Biome, biome))
        {
            biome?.AddTile(tile);
            return;
        }

        tile.Biome?.RemoveTile(tile);
        tile.SetBiome(biome);
        biome?.AddTile(tile);
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

    public override Tile? RemoveTile(string key)
    {
        var deleted = base.RemoveTile(key);
        if (deleted is not null)
        {
            SetTileBiome(deleted, null);
        }

        return deleted;
    }
}
