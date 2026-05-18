using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Tests.Progression;

public sealed class TriloDexTests
{
    [Fact]
    public void GlobalDex_ContainsTheAuthoredFeatureTreeCatalog()
    {
        Assert.Same(TriloDex.Global, TriloDex.Global);
        Assert.Equal(28, TriloDex.GlobalFeatureTrees.Count);
        Assert.Equal(28, TriloDex.Global.FeatureTrees.Count);
        Assert.Equal(28, TriloDex.Global.Count);
        Assert.False(TriloDex.Global.IsEmpty);

        Assert.Equal(12, TriloDex.GlobalFeatureTrees.Count(tree => tree.Tier == 1));
        Assert.Equal(12, TriloDex.GlobalFeatureTrees.Count(tree => tree.Tier == 2));
        Assert.Equal(4, TriloDex.GlobalFeatureTrees.Count(tree => tree.Tier == 3));
    }

    [Fact]
    public void GameSession_ExposesTheSharedGlobalDex()
    {
        var session = new GameSession();

        Assert.Same(TriloDex.Global, session.ProgressionDex);
        Assert.Same(session.ProgressionDex.FeatureTrees, session.FeatureTrees);
        Assert.Null(session.GetFeatureTree("missing"));
    }

    [Fact]
    public void GlobalDex_ReplicatesTheRequestedTreeShapes()
    {
        var b1 = TriloDex.Global.FindFeatureTree("B1");
        var b2 = TriloDex.Global.FindFeatureTree("B2");
        var b3 = TriloDex.Global.FindFeatureTree("B3");

        Assert.NotNull(b1);
        Assert.NotNull(b2);
        Assert.NotNull(b3);

        Assert.Equal(["building"], b1!.FeaturesAffected);
        Assert.Equal(["building"], b2!.FeaturesAffected);
        Assert.Equal(["building"], b3!.FeaturesAffected);

        Assert.Equal("B1-a", b1.Root!.Name);
        Assert.Equal(["B1-b"], b1.Root.Children.Select(child => child.Name));
        Assert.Equal(["B1-c"], b1.Root.Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B1-d"], b1.Root.Children[0].Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B1-e", "B1-f", "B1-g", "B1-h"], b1.Root.Children[0].Children[0].Children[0].Children.Select(child => child.Name));

        Assert.Equal("B2-a", b2.Root!.Name);
        Assert.Equal(["B2-b", "B2-v"], b2.Root.Children.Select(child => child.Name));
        Assert.Equal(["B2-c"], b2.Root.Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B2-d"], b2.Root.Children[0].Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B2-e", "B2-f"], b2.Root.Children[0].Children[0].Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B2-w"], b2.Root.Children[1].Children.Select(child => child.Name));
        Assert.Equal(["B2-x"], b2.Root.Children[1].Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B2-y", "B2-z"], b2.Root.Children[1].Children[0].Children[0].Children.Select(child => child.Name));

        Assert.Equal("B3-a", b3.Root!.Name);
        Assert.Equal(["B3-b"], b3.Root.Children.Select(child => child.Name));
        Assert.Equal(["B3-c", "B3-f"], b3.Root.Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B3-d"], b3.Root.Children[0].Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B3-e"], b3.Root.Children[0].Children[0].Children[0].Children.Select(child => child.Name));
        Assert.Equal(["B3-g"], b3.Root.Children[0].Children[1].Children.Select(child => child.Name));
        Assert.Equal(["B3-h"], b3.Root.Children[0].Children[1].Children[0].Children.Select(child => child.Name));
    }

    [Fact]
    public void GlobalDex_UsesReadablePrerequisiteTreeNaming()
    {
        var bc1 = TriloDex.Global.FindFeatureTree("BC1");
        var bcf1 = TriloDex.Global.FindFeatureTree("BCF1");

        Assert.NotNull(bc1);
        Assert.NotNull(bcf1);

        Assert.Equal(["B1", "C1"], bc1!.PrerequisiteTrees);
        Assert.Equal(["BC1", "BF1", "CF1"], bcf1!.PrerequisiteTrees);
    }
}
