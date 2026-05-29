using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;

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
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.SANDSTONE.Name);
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.ALGAE.Name);
        Assert.Contains(cave.GetTiles(), tile => tile.Base == OreType.MAGNETITE.Name);
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
