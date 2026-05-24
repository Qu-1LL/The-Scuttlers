using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Combat;

namespace TriloGame.Tests.Core.Combat;

public sealed class ProjectileCatalogTests
{
    [Fact]
    public void Rock_UsesHalfTileSpriteScale()
    {
        Assert.Equal(0.5f, ProjectileCatalog.Rock.SpriteScale);
    }

    [Fact]
    public void Rock_UsesConfiguredTravelPixelsPerTick()
    {
        Assert.Equal(TileConstants.TileSize * 2f, ProjectileCatalog.Rock.TravelPixelsPerTick);
    }
}
