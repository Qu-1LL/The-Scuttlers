using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeColorResolverTests
{
    [Fact]
    public void GetBaseFeatureColor_UsesAuthoredTrilodexTreeColor()
    {
        var session = new GameSession();

        var color = ResearchTreeColorResolver.GetBaseFeatureColor(session, "B2");

        Assert.Equal(new Color(0x2a, 0x9d, 0x8f), color);
    }

    [Fact]
    public void GetBaseFeatureColor_FallsBackToFeatureCategoryWhenTreeIsMissing()
    {
        var session = new GameSession();

        var color = ResearchTreeColorResolver.GetBaseFeatureColor(session, "B999");

        Assert.Equal(new Color(240, 88, 80), color);
    }
}
