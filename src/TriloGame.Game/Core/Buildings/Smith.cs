using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Smith : Building
{
    private static readonly InteractionZoneDefinition[] WorkZones =
    [
        new(
            "Smith work slot",
            InteractionZonePurpose.Work,
            new GridPoint(1, 1),
            new GridPoint(1, 1),
            [new GridPoint(1, 1)])
    ];

    public Smith(GameSession session)
        : base("Smith", new GridPoint(2, 2), [[0, 0], [0, 1]], session, true)
    {
        TextureKey = "Smith";
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Rock, 20)];
        Description = "A building that allows you to craft new items for your species.";
    }

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => WorkZones;
}
