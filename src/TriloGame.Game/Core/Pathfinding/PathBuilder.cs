using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

public static class PathBuilder
{
    public static IReadOnlyList<GridPoint>? Build(BfsField field, GridPoint startLocation)
    {
        return field.BuildPathFrom(startLocation);
    }
}
