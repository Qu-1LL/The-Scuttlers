using TriloGame.Game.Core.Economy;

namespace TriloGame.Game.Core.Buildings;

public readonly record struct ScaffoldRequirementNeed(
    int RequirementIndex,
    ResourceRequirement Requirement,
    int Amount);
