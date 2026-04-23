using TriloGame.Game.Core.Progression;

namespace TriloGame.Tests.Progression;

public sealed class FeatureTreeTests
{
    [Fact]
    public void FeatureTree_StoresTierAndPrerequisiteTreeNames()
    {
        var tree = new FeatureTree(
            "Mobility",
            "Movement upgrades.",
            ["movement", "movement", "speed"],
            2,
            ["  Survey  ", "Economy", "Survey"]);

        Assert.Equal("Mobility", tree.Name);
        Assert.Equal("Movement upgrades.", tree.Description);
        Assert.Equal(2, tree.Tier);
        Assert.Equal(["movement", "speed"], tree.FeaturesAffected);
        Assert.Equal(["Survey", "Economy"], tree.PrerequisiteTrees);
        Assert.True(tree.HasPrerequisites);
        Assert.False(tree.HasRoot);
        Assert.Null(tree.Root);
    }

    [Fact]
    public void TierOneFeatureTrees_CanStartWithoutPrerequisites()
    {
        var tree = new FeatureTree(
            "Survey",
            "Reveal upgrades.",
            ["reveal"],
            1,
            []);

        Assert.Equal(1, tree.Tier);
        Assert.Empty(tree.PrerequisiteTrees);
        Assert.False(tree.HasPrerequisites);
    }

    [Fact]
    public void FeatureTree_RejectsTierBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FeatureTree("Invalid", "Bad tier.", [], 0));
    }
}
