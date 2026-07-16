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
}
