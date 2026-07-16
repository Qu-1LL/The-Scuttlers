using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Barracks : StationBuilding
{
    public const int DefaultFighterAssignmentPriority = 0;

    private static readonly int[][] DefaultOpenMap =
    [
        [1, 1, 1],
        [1, 0, 1],
        [1, 1, 1]
    ];
    private static readonly InteractionZoneDefinition[] StationZones =
    [
        new(
            "Fighter stations",
            InteractionZonePurpose.Station,
            new GridPoint(0, 0),
            new GridPoint(3, 3),
            [
                new GridPoint(0, 0), new GridPoint(1, 0), new GridPoint(2, 0),
                new GridPoint(0, 1), new GridPoint(2, 1),
                new GridPoint(0, 2), new GridPoint(1, 2), new GridPoint(2, 2)
            ])
    ];

    public Barracks(GameSession session)
        : base(
            "Barracks",
            new GridPoint(3, 3),
            DefaultOpenMap,
            session,
            "Barracks",
            "Fighters will wait here until danger arises.",
            DefaultFighterAssignmentPriority,
            CreateTileStations(DefaultOpenMap))
    {
    }

    protected override bool TracksAssignments => true;

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => StationZones;
}
