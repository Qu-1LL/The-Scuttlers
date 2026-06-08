using TriloGame.Game.Core.Progression;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Progression;

public sealed class ResearchBranchTests
{
    [Fact]
    public void ResearchBranch_StoresVisibleRootAndGridShapedChildren()
    {
        var rootTemplate = new SkillNode("Root Node", "Branch origin.");
        var leftTemplate = new SkillNode("Left Child", "Branch left child.");
        var rightTemplate = new SkillNode("Right Child", "Branch right child.");

        var branch = new ResearchBranch();
        var root = branch.SetRoot(new BinarySkillNode(rootTemplate, new GridPoint(1, 0), "Mobility"), new GridPoint(1, 0));
        var left = branch.AddLeftChild(root, new BinarySkillNode(leftTemplate, new GridPoint(2, 0), "Mobility"));
        var right = branch.AddRightChild(root, new BinarySkillNode(rightTemplate, new GridPoint(1, 1), "Mobility"));

        Assert.Equal(3, branch.Count);
        Assert.Same(root, branch.Root);
        Assert.Equal(new GridPoint(1, 0), root.Delta);
        Assert.Equal(new GridPoint(2, 0), left.Delta);
        Assert.Equal(new GridPoint(1, 1), right.Delta);
        Assert.True(branch.ContainsDelta(new GridPoint(1, 0)));
        Assert.True(branch.ContainsDelta(new GridPoint(2, 0)));
        Assert.True(branch.ContainsDelta(new GridPoint(1, 1)));
        Assert.Same(left, root.Left);
        Assert.Same(right, root.Right);
        Assert.Same(root, left.Parent);
        Assert.Same(root, right.Parent);
        Assert.True(branch.ContainsSourceSkill("Mobility", "Root Node"));
        Assert.True(branch.ContainsSourceSkill("Mobility", "Left Child"));
    }

    [Fact]
    public void EmptyResearchBranch_StartsWithTheTwoInvisibleRootEntrySlots()
    {
        var branch = new ResearchBranch();

        var slots = branch.GetAvailableSlots();

        Assert.Equal(2, slots.Count);
        Assert.Contains(slots, slot => slot.Parent is null && slot.Delta == new GridPoint(1, 0));
        Assert.Contains(slots, slot => slot.Parent is null && slot.Delta == new GridPoint(0, 1));
    }
}
