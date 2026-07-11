using TriloGame.Game.Core.Progression;

namespace TriloGame.Tests.Progression;

public sealed class ResearchBranchTests
{
    [Fact]
    public void ResearchBranch_StoresVisibleRootAndCollectionChildren()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root Node", "Branch origin."), "Mobility"));
        var firstChild = branch.AddChild(root, new TreeInstanceNode(new SkillNode("First Child", "Branch child."), "Mobility"), childIndex: 0);
        var secondChild = branch.AddChild(root, new TreeInstanceNode(new SkillNode("Second Child", "Branch child."), "Mobility"));

        Assert.Equal(3, branch.Count);
        Assert.Equal("Unnamed Branch", branch.Name);
        Assert.Same(root, branch.Root);
        Assert.Equal(2, root.ChildCount);
        Assert.Same(firstChild, root.Children[0]);
        Assert.Same(secondChild, root.Children[1]);
        Assert.Same(root, firstChild.Parent);
        Assert.Same(root, secondChild.Parent);
        Assert.True(branch.ContainsSourceSkill("Mobility", "Root Node"));
        Assert.True(branch.ContainsSourceSkill("Mobility", "First Child"));
    }

    [Fact]
    public void ResearchBranch_StoresEditableDisplayName()
    {
        var branch = new ResearchBranch("Amber Fork");

        branch.Rename("  Moonlit Sprig  ");

        Assert.Equal("Moonlit Sprig", branch.Name);
        Assert.Throws<ArgumentException>(() => branch.Rename(" "));
    }

    [Fact]
    public void AddChild_RequiresTheParentToAlreadyBelongToTheBranch()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root"), "Mobility"));
        var outsideParent = new TreeInstanceNode(new SkillNode("Outside", "Outside"), "Mobility");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            branch.AddChild(outsideParent, new TreeInstanceNode(new SkillNode("Child", "Child"), "Mobility")));

        Assert.Equal("The parent tree instance node must belong to this research branch.", exception.Message);
        Assert.Same(root, branch.Root);
    }
}
