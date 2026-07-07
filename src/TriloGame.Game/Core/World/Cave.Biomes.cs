namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    private readonly List<BiomeRegion> _biomeRegions = [];

    public IReadOnlyList<BiomeRegion> GetBiomeRegions() => _biomeRegions;

    // Keep tile ownership in sync when later biome passes overwrite earlier ones.
    internal void SetTileBiome(Tile tile, BiomeRegion? biome)
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

    internal BiomeRegion CreateBiomeRegion(string biomeName)
    {
        var biome = new BiomeRegion(biomeName);
        _biomeRegions.Add(biome);
        return biome;
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
