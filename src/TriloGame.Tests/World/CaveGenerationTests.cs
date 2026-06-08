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
}
