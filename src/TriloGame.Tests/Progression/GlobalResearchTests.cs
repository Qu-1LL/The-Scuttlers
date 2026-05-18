using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;

namespace TriloGame.Tests.Progression;

public sealed class GlobalResearchTests
{
    [Fact]
    public void Intake_StoresDescriptorsBySkillNodeName()
    {
        var research = new GlobalResearch();

        research.Intake(
            "Dig Sprint",
            [
                new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddPercent, 0.15),
                new ResearchEffectDescriptor("Trilobite.WorkRate", ResearchOperation.AddFlat, 1)
            ]);

        Assert.Equal(2, research.Count);
        var bySkill = research.GetDescriptorsForSkillNode("Dig Sprint");
        Assert.Equal(2, bySkill.Count);
        Assert.All(bySkill, d => Assert.StartsWith("Trilobite.", d.StatKey, StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveEffectiveValue_AppliesSetThenAddThenPercentThenMultiply()
    {
        var research = new GlobalResearch();

        research.Intake(
            "Stack Demo",
            [
                new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.Set, 10),
                new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddFlat, 2),
                new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.AddPercent, 0.5),
                new ResearchEffectDescriptor("Trilobite.MoveSpeed", ResearchOperation.Multiply, 2)
            ]);

        var resolved = research.ResolveEffectiveValue(new ResearchQuery("Trilobite.MoveSpeed"), baseValue: 3);

        Assert.Equal(36, resolved);
    }

    [Fact]
    public void IntakeFromSkillNode_ProcessesDataDrivenDescriptors()
    {
        var research = new GlobalResearch();
        var template = new SkillNode(
            "Packed Haul",
            "Carry more resources.",
            [new ResearchEffectDescriptor("Trilobite.InventoryCapacity", ResearchOperation.AddFlat, 3)]);

        research.Intake(template);

        Assert.Single(research.Descriptors);
        var descriptor = Assert.Single(research.GetMatchingDescriptors(
            new ResearchQuery("Trilobite.InventoryCapacity")));
        Assert.Equal(ResearchOperation.AddFlat, descriptor.Operation);
        Assert.Equal(3, descriptor.Value);
    }

    [Fact]
    public void GetMatchingDescriptors_FiltersByScopedTarget()
    {
        var research = new GlobalResearch();

        research.Intake(
            "Farm Boost",
            [
                new ResearchEffectDescriptor("Building.HarvestYield", ResearchOperation.AddFlat, 1, ResearchTargetKind.BuildingType, "Algae Farm"),
                new ResearchEffectDescriptor("Building.HarvestYield", ResearchOperation.AddFlat, 10, ResearchTargetKind.BuildingType, "Other")
            ]);

        var matching = research.GetMatchingDescriptors(
            new ResearchQuery("Building.HarvestYield", ResearchTargetKind.BuildingType, "Algae Farm"));

        var descriptor = Assert.Single(matching);
        Assert.Equal(1, descriptor.Value);
    }
}
