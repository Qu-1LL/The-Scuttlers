using TriloGame.Game.Core.Economy;

namespace TriloGame.Game.Core.Buildings;

public readonly record struct ScaffoldMaterialReservation(
    int RequirementIndex,
    ResourceRequirement Requirement,
    ResourceName ResourceType,
    int Amount);
