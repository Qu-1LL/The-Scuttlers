using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Progression;

public sealed class TriloDexTests
{
    [Fact]
    public void GlobalDex_StartsEmptyUntilHardCodedTreesAreAdded()
    {
        Assert.Same(TriloDex.Global, TriloDex.Global);
        Assert.Empty(TriloDex.GlobalFeatureTrees);
        Assert.Empty(TriloDex.Global.FeatureTrees);
        Assert.Equal(0, TriloDex.Global.Count);
        Assert.True(TriloDex.Global.IsEmpty);
    }

    [Fact]
    public void GameSession_ExposesTheSharedGlobalDex()
    {
        var session = new GameSession();

        Assert.Same(TriloDex.Global, session.ProgressionDex);
        Assert.Same(session.ProgressionDex.FeatureTrees, session.FeatureTrees);
        Assert.Null(session.GetFeatureTree("missing"));
    }
}
